#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using FlameAndBlame.Core;
using FlameAndBlame.Heli;
using FlameAndBlame.Rescue;
using FlameAndBlame.Testing;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlameAndBlame.Tests.PlayMode
{
    public class HeliTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ConfigPath = "Assets/Settings/HeliConfig.asset";

        private class Sampler : MonoBehaviour
        {
            public Rigidbody Body;
            public readonly List<Vector3> Vel = new List<Vector3>();
            public readonly List<float> YawDeg = new List<float>();
            public readonly List<float> YawRate = new List<float>();
            public readonly List<Vector3> Pos = new List<Vector3>();
            private void FixedUpdate()
            {
                Vel.Add(Body.linearVelocity);
                Pos.Add(Body.position);
                YawDeg.Add(Body.rotation.eulerAngles.y);
                YawRate.Add(Body.angularVelocity.y);
            }
        }

        private HeliController _heli;
        private Cup _cup;
        private TestModeController _tm;
        private HeliConfig _cfg;

        private static void Report(string name, bool ok, string reason)
        {
            Debug.Log(ok ? $"[TEST] {name} PASS" : $"[TEST] {name} FAIL {reason}");
            Assert.IsTrue(ok, reason);
        }

        private IEnumerator Load()
        {
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return op;
            yield return null;
            _heli = Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            _cup = Object.FindFirstObjectByType<Cup>(FindObjectsInactive.Include);
            _tm = Object.FindFirstObjectByType<TestModeController>(FindObjectsInactive.Include);
            _cfg = AssetDatabase.LoadAssetAtPath<HeliConfig>(ConfigPath);
            Assert.IsNotNull(_heli, "HeliController not found");
            Assert.IsNotNull(_cup, "Cup not found");
            Assert.IsNotNull(_tm, "TestModeController not found");
            Assert.IsNotNull(_cfg, "HeliConfig asset not found");
        }

        [UnityTest]
        public IEnumerator heli_smooth_accel()
        {
            const string n = "heli_smooth_accel";
            yield return Load();
            Assert.IsFalse(_heli.IsTestMode, "should start in real physics mode");
            var scripted = _tm.Scripted;
            _heli.SetControlSource(scripted);
            scripted.Set(new HeliCommand { Move = new Vector2(0f, 1f) });
            {
                var cc = AssetDatabase.LoadAssetAtPath<CupConfig>("Assets/Settings/CupConfig.asset");
                Debug.Log($"[TEST-INFO] {n} heliIsolationMassScale={cc.heliIsolationMassScale}");
                var bowl = _cup.Body;
                Debug.Log($"[TEST-INFO] {n} heliRot={_heli.transform.rotation.eulerAngles} bowlRot={bowl.transform.rotation.eulerAngles} bowlPos={bowl.position} heliPos={_heli.Body.position}");
                foreach (var bc in bowl.GetComponentsInChildren<Collider>())
                    foreach (var hc in _heli.GetComponentsInChildren<Collider>())
                    {
                        if (bc == hc || bc.isTrigger || hc.isTrigger) continue;
                        bool ov = Physics.ComputePenetration(bc, bc.transform.position, bc.transform.rotation, hc, hc.transform.position, hc.transform.rotation, out _, out float dist);
                        Debug.Log($"[TEST-INFO] {n} overlap {bc.name} vs {hc.name}: {ov} dist={dist:F3}");
                    }
            }
            var s = _heli.gameObject.AddComponent<Sampler>();
            s.Body = _heli.Body;

            float dt = Time.fixedDeltaTime;
            float duration = 3f;
            yield return new WaitForSeconds(duration);
            scripted.Set(default);

            string fail = null;
            float limit = _cfg.maxAcceleration * dt * 1.05f + 1e-3f;
            float maxDelta = 0f, maxSpeed = 0f;
            for (int i = 1; i < s.Vel.Count; i++)
            {
                float d = (s.Vel[i] - s.Vel[i - 1]).magnitude;
                maxDelta = Mathf.Max(maxDelta, d);
                if (d > limit && fail == null) fail = $"step {i}: |dv|={d:F4} > maxAcceleration*dt={_cfg.maxAcceleration * dt:F4}";
                float sp = s.Vel[i].magnitude;
                maxSpeed = Mathf.Max(maxSpeed, sp);
                if (i * dt < _cfg.inputRiseTime && sp >= _cfg.maxHorizontalSpeed * 0.999f && fail == null)
                    fail = $"speed {sp:F2} reached max within inputRiseTime at step {i}";
            }
            if (fail == null && maxSpeed < 2f) fail = $"never reached meaningful speed (max {maxSpeed:F2} m/s)";
            if (fail == null && _heli.IsCrashed) fail = "heli crashed";
            { float late = 0f; int lastBad = -1, worst = 0; float worstD = 0f; for (int i = 1; i < s.Vel.Count; i++) { float d = (s.Vel[i] - s.Vel[i - 1]).magnitude; if (d > limit) lastBad = i; if (d > worstD) { worstD = d; worst = i; } if (i > 40) late = Mathf.Max(late, d); }
              Debug.Log($"[TEST-INFO] {n} lastViolationStep={lastBad} worstStep={worst} worstDv={worstD:F4} maxDeltaAfterStep40={late:F4} startPos={_heli.transform.position}"); }
            for (int i = 0; i < Mathf.Min(8, s.Vel.Count); i++) Debug.Log($"[TEST-INFO] {n} v[{i}]={s.Vel[i]:F3}");
            Debug.Log($"[TEST-INFO] {n} steps={s.Vel.Count} maxDelta={maxDelta:F4} limit={limit:F4} maxSpeed={maxSpeed:F2}");
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator heli_smooth_accel_testmode()
        {
            const string n = "heli_smooth_accel_testmode";
            yield return Load();
            _tm.EnterAll();
            _tm.Scripted.Set(new HeliCommand { Move = new Vector2(0f, 1f) });
            var prev = _heli.Velocity;
            float dt = Time.fixedDeltaTime;
            float limit = _cfg.maxAcceleration * dt * 1.05f + 1e-3f;
            string fail = null;
            float maxSpeed = 0f;
            int steps = Mathf.CeilToInt(3f / dt);
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                var v = _heli.Velocity;
                float d = (v - prev).magnitude;
                if (d > limit && fail == null) fail = $"step {i}: |dv|={d:F4} > limit {limit:F4}";
                maxSpeed = Mathf.Max(maxSpeed, v.magnitude);
                prev = v;
            }
            if (fail == null && maxSpeed < 2f) fail = $"never reached meaningful speed (max {maxSpeed:F2})";
            _tm.ExitAll();
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator heli_yaw_drift()
        {
            const string n = "heli_yaw_drift";
            yield return Load();
            _heli.SetControlSource(_tm.Scripted);
            _tm.Scripted.Set(default);
            var s = _heli.gameObject.AddComponent<Sampler>();
            s.Body = _heli.Body;
            yield return new WaitForSeconds(10f);

            { int k = Mathf.Min(s.Pos.Count - 1, Mathf.RoundToInt(3f / Time.fixedDeltaTime)); Debug.Log($"[TEST-INFO] {n} noInput drift over 3s: dPos={s.Pos[k] - s.Pos[0]} v0={s.Vel[0]} v@3s={s.Vel[k]}"); }
            string fail = null;
            // unwrap yaw
            float total = 0f, minY = 0f, maxY = 0f, maxRate = 0f, maxRateStep = 0f;
            for (int i = 1; i < s.YawDeg.Count; i++)
            {
                total += Mathf.DeltaAngle(s.YawDeg[i - 1], s.YawDeg[i]);
                minY = Mathf.Min(minY, total); maxY = Mathf.Max(maxY, total);
                maxRate = Mathf.Max(maxRate, Mathf.Abs(s.YawRate[i]));
                maxRateStep = Mathf.Max(maxRateStep, Mathf.Abs(s.YawRate[i] - s.YawRate[i - 1]));
            }
            float range = maxY - minY;
            float rateBound = _cfg.yawDriftTorque / _cfg.velocityResponse * 1.25f; // rad/s steady-state bound
            float stepBound = (_cfg.yawDriftTorque + rateBound * _cfg.velocityResponse) * Time.fixedDeltaTime * 1.5f + 1e-3f;
            if (range < 0.5f) fail = $"yaw did not drift (range {range:F3} deg over 10 s)";
            else if (maxRate > rateBound) fail = $"yaw rate {maxRate:F3} rad/s exceeds bound {rateBound:F3}";
            else if (maxRateStep > stepBound) fail = $"yaw rate jumps {maxRateStep:F4} rad/s per step > {stepBound:F4}";
            Debug.Log($"[TEST-INFO] {n} range={range:F2}deg maxRate={maxRate:F3}rad/s maxStep={maxRateStep:F5}");
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator heli_rotor_crash()
        {
            const string n = "heli_rotor_crash";
            yield return Load();
            var hazard = _heli.GetComponentInChildren<RotorHazard>(true);
            Assert.IsNotNull(hazard, "RotorHazard not found under heli");

            // cup collider first
            Collider cupCol = null;
            // Cup.Initialize reparents the bowl under a runtime chain root, so search from the bowl body.
            foreach (var c in _cup.Body.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && c.CompareTag("Cup")) { cupCol = c; break; }
            if (cupCol == null)
                foreach (var c in _cup.Body.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) { cupCol = c; break; }
            Assert.IsNotNull(cupCol, "no non-trigger cup collider");

            int fired = 0;
            _heli.Crashed += () => fired++;
            hazard.ReportHit(cupCol);
            string fail = null;
            if (_heli.IsCrashed || fired != 0) fail = "ReportHit(cup collider) crashed the heli";

            Collider ground = null;
            var gt = GameObject.Find("Ground");
            if (gt != null) ground = gt.GetComponentInChildren<Collider>();
            if (ground == null) fail = fail ?? "Ground collider not found in scene";
            else if (fail == null)
            {
                hazard.ReportHit(ground);
                if (!_heli.IsCrashed) fail = "ReportHit(ground) did not set IsCrashed";
                else if (fired != 1) fail = $"Crashed event fired {fired} times, expected 1";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator testmode_toggle()
        {
            const string n = "testmode_toggle";
            yield return Load();
            bool heliKin = _heli.Body.isKinematic, cupKin = _cup.Body.isKinematic;
            var original = _heli.ControlSource;
            Assert.IsNotNull(original, "heli has no control source before test mode");

            _tm.EnterAll();
            yield return new WaitForFixedUpdate();
            string fail = null;
            if (!_heli.Body.isKinematic) fail = "heli Body not kinematic in test mode";
            else if (!_cup.Body.isKinematic) fail = "cup Body not kinematic in test mode";
            else if (!ReferenceEquals(_heli.ControlSource, _tm.Scripted)) fail = "heli source is not Scripted in test mode";
            else if (!_heli.IsTestMode || !_cup.IsTestMode) fail = "IsTestMode false after EnterAll";

            _tm.ExitAll();
            yield return new WaitForFixedUpdate();
            if (fail == null)
            {
                if (_heli.Body.isKinematic != heliKin) fail = "heli isKinematic not restored";
                else if (_cup.Body.isKinematic != cupKin) fail = "cup isKinematic not restored";
                else if (!ReferenceEquals(_heli.ControlSource, original)) fail = "heli control source not restored";
                else if (!(_heli.ControlSource is PlayerControlSource)) fail = "restored source is not PlayerControlSource";
                else if (_heli.IsTestMode || _cup.IsTestMode) fail = "IsTestMode still true after ExitAll";
            }
            Report(n, fail == null, fail);
        }
    }
}
#endif
