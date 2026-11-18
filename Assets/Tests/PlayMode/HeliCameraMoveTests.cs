#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FlameAndBlame.Core;
using FlameAndBlame.Heli;
using FlameAndBlame.Testing;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Phase 4 camera-relative movement and Z/X yaw tests. Test mode OFF (real physics). Keys come from a virtual keyboard.</summary>
    public class HeliCameraMoveTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private HeliController _heli;
        private PlayerControlSource _pcs;
        private Transform _cam;
        private Keyboard _kb;
        private ScriptedControlSource _src;

        private static void Report(string name, bool ok, string reason)
        {
            Debug.Log(ok ? $"[TEST] {name} PASS" : $"[TEST] {name} FAIL {reason}");
            Assert.IsTrue(ok, reason);
        }

        [TearDown]
        public void Cleanup()
        {
            if (_kb != null && _kb.added) InputSystem.RemoveDevice(_kb);
            _kb = null;
        }

        private IEnumerator Load()
        {
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return op;
            yield return null;
            _heli = Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            _pcs = Object.FindFirstObjectByType<PlayerControlSource>(FindObjectsInactive.Include);
            Assert.IsNotNull(_heli); Assert.IsNotNull(_pcs);
            Assert.IsFalse(_heli.IsTestMode, "heli in test mode");
            _kb = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            _src = new GameObject("ScriptedSrc").AddComponent<ScriptedControlSource>();
            _heli.SetControlSource(_src);
            _src.Set(default);
        }

        /// <summary>Replaces the camera the PlayerControlSource reads with a dummy transform we can aim freely.</summary>
        private void UseDummyCamera(float yawDeg, float pitchDeg = 25f)
        {
            var f = typeof(PlayerControlSource).GetField("cameraTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            var real = (Transform)f.GetValue(_pcs);
            Debug.Log($"[TEST-INFO] PlayerControlSource.cameraTransform = {(real != null ? real.name : "NULL")}");
            Assert.IsNotNull(real, "PlayerControlSource.cameraTransform is not assigned in the scene");
            if (_cam == null) _cam = new GameObject("DummyCamera").transform;
            _cam.position = _heli.Body.position + new Vector3(0, 5, 0);
            _cam.rotation = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            f.SetValue(_pcs, _cam);
        }

        private IEnumerator Press(params Key[] keys)
        {
            // Let the editor deliver input to the game view even when the window is unfocused
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.QueueStateEvent(_kb, new KeyboardState(keys));
            InputSystem.Update();
            yield return null;
        }

        private static float Angle(Vector3 a, Vector3 b) => Vector3.Angle(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));

        private void Teleport(Vector3 pos)
        {
            Vector3 delta = pos - _heli.Body.position;
            var bodies = new HashSet<Rigidbody> { _heli.Body };
            foreach (var rb in _heli.GetComponentsInChildren<Rigidbody>(true)) bodies.Add(rb);
            foreach (var rb in bodies) { rb.position += delta; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
        }

        /// <summary>Settles with zero input, then flies with the given command; returns horizontal displacement.</summary>
        private IEnumerator Fly(HeliCommand cmd, float seconds, System.Action<Vector3> result)
        {
            _src.Set(default);
            yield return new WaitForSeconds(3.0f);
            Vector3 start = _heli.Body.position;
            _src.Set(cmd);
            yield return new WaitForSeconds(seconds);
            Vector3 end = _heli.Body.position;
            _src.Set(default);
            result(end - start);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v.normalized; }

        [UnityTest]
        public IEnumerator move_camera_relative()
        {
            const string n = "move_camera_relative";
            yield return Load();
            Teleport(new Vector3(-80f, 15f, 0f));
            UseDummyCamera(90f);
            Vector3 camFwd = Flat(_cam.forward), camRight = Flat(_cam.right);
            string fail = null;

            yield return Press(Key.W);
            var w = _pcs.Read();
            yield return Press(Key.D);
            var d = _pcs.Read();
            yield return Press();
            Debug.Log($"[TEST-INFO] {n} camFwd={camFwd} camRight={camRight} W.MoveWorld={w.MoveWorld} W.Move={w.Move} D.MoveWorld={d.MoveWorld} D.Move={d.Move}");
            if (w.Move.y < 0.9f) fail = $"virtual keyboard W not seen by PlayerControlSource (Move={w.Move}); input did not reach the action";
            else if (d.Move.x < 0.9f) fail = $"virtual keyboard D not seen (Move={d.Move})";
            else if (Angle(w.MoveWorld, camFwd) > 1f) fail = $"W MoveWorld {w.MoveWorld} vs camera forward {camFwd} angle {Angle(w.MoveWorld, camFwd):F2}";
            else if (Angle(d.MoveWorld, camRight) > 1f) fail = $"D MoveWorld {d.MoveWorld} vs camera right {camRight} angle {Angle(d.MoveWorld, camRight):F2}";
            else if (Mathf.Abs(w.MoveWorld.y) > 1e-4f || w.MoveWorld.magnitude < 0.99f) fail = $"W MoveWorld not horizontal unit: {w.MoveWorld}";

            if (fail == null)
            {
                Vector3 dispF = default, dispR = default;
                yield return Fly(new HeliCommand { MoveWorld = w.MoveWorld }, 1.5f, v => dispF = v);
                Teleport(new Vector3(-80f, 15f, 0f));
                yield return Fly(new HeliCommand { MoveWorld = d.MoveWorld }, 1.5f, v => dispR = v);
                float aF = Angle(dispF, camFwd), aR = Angle(dispR, camRight);
                Debug.Log($"[TEST-INFO] {n} flew W disp={dispF} angle={aF:F2}; D disp={dispR} angle={aR:F2}");
                if (dispF.magnitude < 0.5f) fail = $"heli barely moved on W ({dispF.magnitude:F2} m)";
                else if (aF > 5f) fail = $"heli moved {aF:F1} deg off camera forward (disp {dispF})";
                else if (dispR.magnitude < 0.5f) fail = $"heli barely moved on D ({dispR.magnitude:F2} m)";
                else if (aR > 5f) fail = $"heli moved {aR:F1} deg off camera right (disp {dispR})";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator move_ignores_heli_yaw()
        {
            const string n = "move_ignores_heli_yaw";
            yield return Load();
            Teleport(new Vector3(-80f, 15f, 0f));
            UseDummyCamera(90f);
            Vector3 camFwd = Flat(_cam.forward);
            string fail = null;

            yield return Press(Key.W);
            var before = _pcs.Read().MoveWorld;
            float yaw0 = _heli.Body.rotation.eulerAngles.y;
            yield return Press();
            _src.Set(new HeliCommand { Yaw = 1f });
            yield return new WaitForSeconds(2f);
            _src.Set(default);
            float turned = Mathf.Abs(Mathf.DeltaAngle(yaw0, _heli.Body.rotation.eulerAngles.y));
            yield return Press(Key.W);
            var after = _pcs.Read().MoveWorld;
            yield return Press();
            Debug.Log($"[TEST-INFO] {n} heli turned {turned:F1} deg; MoveWorld before={before} after={after}");
            if (turned < 20f) fail = $"heli only turned {turned:F1} deg, test is meaningless";
            else if (Angle(before, after) > 5f) fail = $"MoveWorld changed by {Angle(before, after):F1} deg after heli yaw";

            if (fail == null)
            {
                Vector3 disp = default;
                yield return Fly(new HeliCommand { MoveWorld = after }, 1.5f, v => disp = v);
                float a = Angle(disp, camFwd);
                Debug.Log($"[TEST-INFO] {n} yawed heli flew disp={disp} angle to camera forward={a:F2}");
                if (disp.magnitude < 0.5f) fail = $"heli barely moved ({disp.magnitude:F2} m)";
                else if (a > 5f) fail = $"yawed heli moved {a:F1} deg off camera forward";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator yaw_keys_z_x()
        {
            const string n = "yaw_keys_z_x";
            yield return Load();
            Teleport(new Vector3(-80f, 15f, 0f));
            UseDummyCamera(0f);
            string fail = null;

            yield return Press(Key.Z);
            float zYaw = _pcs.Read().Yaw;
            yield return Press(Key.X);
            float xYaw = _pcs.Read().Yaw;
            yield return Press();
            Debug.Log($"[TEST-INFO] {n} Z -> Yaw {zYaw}, X -> Yaw {xYaw}");
            if (!Mathf.Approximately(zYaw, -1f)) fail = $"Z gave Yaw {zYaw}, expected -1 (if 0 the virtual keyboard was not seen)";
            else if (!Mathf.Approximately(xYaw, 1f)) fail = $"X gave Yaw {xYaw}, expected +1";

            if (fail == null)
            {
                // Yaw drift (random torque) is as large as the command over a short window, so alternate Z and X
                // windows several times and compare the averages: drift is uncorrelated with the command.
                float zSum = 0f, xSum = 0f; const int pairs = 4; const float win = 4f; const float skip = 2f;
                var log = new System.Text.StringBuilder();
                for (int i = 0; i < pairs; i++)
                {
                    float zd = 0f, xd = 0f, t;
                    _src.Set(new HeliCommand { Yaw = -1f });
                    for (t = 0f; t < win; t += Time.deltaTime) { yield return null; if (t >= skip) zd += _heli.Body.angularVelocity.y * Mathf.Rad2Deg * Time.deltaTime; }
                    _src.Set(new HeliCommand { Yaw = 1f });
                    for (t = 0f; t < win; t += Time.deltaTime) { yield return null; if (t >= skip) xd += _heli.Body.angularVelocity.y * Mathf.Rad2Deg * Time.deltaTime; }
                    zSum += zd; xSum += xd; log.Append($"[Z {zd:F0} X {xd:F0} tilt={Vector3.Angle(_heli.transform.up, Vector3.up):F0}] ");
                }
                _src.Set(default);
                float zMean = zSum / pairs, xMean = xSum / pairs;
                Debug.Log($"[TEST-INFO] {n} yaw change over the last {win - skip}s of each {win}s window (command lag ~{skip}s), deg: {log}mean Z={zMean:F1} mean X={xMean:F1}");
                if (zMean > -5f) fail = $"Yaw -1 (Z) mean yaw change {zMean:F1} deg per {win}s, expected clearly negative";
                else if (xMean < 5f) fail = $"Yaw +1 (X) mean yaw change {xMean:F1} deg per {win}s, expected clearly positive";
            }
            Report(n, fail == null, fail);
        }
    }
}
#endif
