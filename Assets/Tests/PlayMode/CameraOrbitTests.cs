using System.Collections;
using System.Reflection;
using FlameAndBlame.Heli;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Orbiting the camera through several full circles must never make the camera jump or the heli rock back and forth.</summary>
    public class CameraOrbitTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [UnityTest]
        public IEnumerator camera_orbit_360_seamless() => Run("camera_orbit_360_seamless", 4f, false);

        [UnityTest]
        public IEnumerator camera_orbit_360_seamless_fast() => Run("camera_orbit_360_seamless_fast", 15f, false);

        [UnityTest]
        public IEnumerator camera_orbit_360_seamless_mouse() => Run("camera_orbit_360_seamless_mouse", 4f, true);

        private IEnumerator Run(string n, float step, bool mouse)
        {
            Mouse vm = mouse ? InputSystem.AddDevice<Mouse>() : null;
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
            yield return null;
            var camCtl = Object.FindFirstObjectByType<HeliCameraController>(FindObjectsInactive.Include);
            var heli = Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            var cam = Camera.main.transform;
            var yawField = typeof(HeliCameraController).GetField("_yaw", BindingFlags.NonPublic | BindingFlags.Instance);
            // Settle.
            for (int i = 0; i < 90; i++) yield return null;

            string fail = null;
            float px = 0f;
            Vector3 prevOffset = cam.position - heli.transform.position;
            float prevCamYaw = cam.eulerAngles.y;
            float maxOrbitJump = 0f, maxLookJump = 0f;
            int worstFrame = -1;
            float prevW = 0f; int flips = 0;
            int frames = Mathf.CeilToInt(360f * 3f / step) + 60;
            for (int f = 0; f < frames; f++)
            {
                // Drive the yaw exactly as mouse look does (same wrap), bypassing the input device.
                if (mouse)
                {
                    var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<HeliConfig>("Assets/Settings/HeliConfig.asset");
                    px = step / Mathf.Max(cfg.cameraYawSensitivity, 1e-4f);
                    InputSystem.QueueDeltaStateEvent(vm.delta, new Vector2(px, 0f));
                }
                else
                {
                    float yaw = (float)yawField.GetValue(camCtl);
                    yawField.SetValue(camCtl, Mathf.DeltaAngle(0f, yaw + step));
                }
                yield return null;
                Vector3 offset = cam.position - heli.transform.position;
                float orbitJump = Vector3.Angle(Vector3.ProjectOnPlane(prevOffset, Vector3.up), Vector3.ProjectOnPlane(offset, Vector3.up));
                float lookJump = Mathf.Abs(Mathf.DeltaAngle(prevCamYaw, cam.eulerAngles.y));
                if (orbitJump > maxOrbitJump) { maxOrbitJump = orbitJump; worstFrame = f; }
                if (lookJump > maxLookJump) maxLookJump = lookJump;
                if (orbitJump > 3f * step || lookJump > 3f * step)
                    Debug.Log($"[TEST-INFO] {n} jump frame={f} ctlYaw={(float)yawField.GetValue(camCtl):F1} orbitJump={orbitJump:F1} lookJump={lookJump:F1} camYaw={cam.eulerAngles.y:F1}");
                float w = heli.Body.angularVelocity.y * Mathf.Rad2Deg;
                if (Mathf.Abs(w) > 20f && Mathf.Abs(prevW) > 20f && Mathf.Sign(w) != Mathf.Sign(prevW))
                {
                    flips++;
                    Debug.Log($"[TEST-INFO] {n} heli turn reversed frame={f} w={prevW:F0}->{w:F0} camYaw={cam.eulerAngles.y:F0} heliYaw={heli.transform.eulerAngles.y:F0}");
                }
                if (Mathf.Abs(w) > 20f) prevW = w;
                prevOffset = offset; prevCamYaw = cam.eulerAngles.y;
            }
            Debug.Log($"[TEST-INFO] {n} maxOrbitJump={maxOrbitJump:F2} maxLookJump={maxLookJump:F2} worstFrame={worstFrame} step={step} mouse={mouse} px={px:F1} finalYaw={camCtl.Yaw:F1} heliTurnReversals={flips}");
            if (maxOrbitJump > 3f * step) fail = $"camera orbit jumped {maxOrbitJump:F1} deg in one frame (step {step})";
            else if (maxLookJump > 3f * step) fail = $"camera look direction jumped {maxLookJump:F1} deg in one frame (step {step})";
            // The heli auto-aligns to the camera: while the camera keeps circling one way, the heli must never reverse its turn.
            else if (flips > 0) fail = $"heli reversed its turn {flips} times while the camera circled one way";
            if (vm != null) InputSystem.RemoveDevice(vm);
            Debug.Log(fail == null ? $"[TEST] {n} PASS" : $"[TEST] {n} FAIL {fail}");
            Assert.IsNull(fail, fail);
        }
    }
}
