#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using FlameAndBlame.Heli;
using FlameAndBlame.Testing;
using FlameAndBlame.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Phase 8: water drop material, proportional extinguishing, roof Fire01, rotor blur, crash debris, rotor hit. Test mode OFF.</summary>
    public class Phase8Tests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private FireSystem _fire;
        private FireConfig _fc;
        private HeliController _heli;
        private TestModeController _tm;

        private class TestKillable : MonoBehaviour, IKillable
        {
            public int Kills;
            public void Kill() { Kills++; }
        }

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
            _fire = Object.FindFirstObjectByType<FireSystem>(FindObjectsInactive.Include);
            _heli = Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            _tm = Object.FindFirstObjectByType<TestModeController>(FindObjectsInactive.Include);
            _fc = AssetDatabase.LoadAssetAtPath<FireConfig>("Assets/Settings/FireConfig.asset");
            Assert.IsNotNull(_fire); Assert.IsNotNull(_heli); Assert.IsNotNull(_fc); Assert.IsNotNull(_tm);
            foreach (var s in _fire.Sides) s.enabled = false;
            foreach (var r in _fire.RoofSlabs) r.enabled = false;   // time moves only through Advance
        }

        private void Ignite(FireSide s)
        {
            s.Advance(_fc.startDelaySeconds + 0.01f);
            s.Advance(100000f);
        }

        [UnityTest]
        public IEnumerator p8_waterdrop_material_transparent()
        {
            const string n = "p8_waterdrop_material_transparent";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WaterDrop.prefab");
            string fail = null;
            var r = prefab != null ? prefab.GetComponentInChildren<Renderer>(true) : null;
            if (r == null) fail = "no renderer on WaterDrop prefab";
            else
            {
                var m = r.sharedMaterial;
                if (m == null || m.name != "M_WaterDrop") fail = $"material is '{(m != null ? m.name : "null")}', expected M_WaterDrop";
                else
                {
                    bool surface = m.HasProperty("_Surface") && m.GetFloat("_Surface") == 1f;
                    bool kw = m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
                    Debug.Log($"[TEST-INFO] {n} queue={m.renderQueue} _Surface={(m.HasProperty("_Surface") ? m.GetFloat("_Surface") : -1)} kw={kw} alpha={m.color.a}");
                    if (m.renderQueue < 3000) fail = $"renderQueue {m.renderQueue} < 3000";
                    else if (!surface && !kw) fail = "not Surface Type Transparent";
                    else if (m.color.a >= 0.99f) fail = $"base color alpha {m.color.a} is opaque";
                }
            }
            Report(n, fail == null, fail);
            yield return null;
        }

        [UnityTest]
        public IEnumerator p8_extinguish_proportional()
        {
            const string n = "p8_extinguish_proportional";
            yield return Load();
            string fail = null;
            var sides = _fire.Sides;
            // full
            Ignite(sides[0]);
            if (sides[0].Height01 < 0.99f) fail = $"setup: Height01 {sides[0].Height01}";
            sides[0].Extinguish(1f);
            if (fail == null && sides[0].Height01 != 0f) fail = $"Extinguish(1) Height01={sides[0].Height01}";
            // 20 percent
            Ignite(sides[1]);
            sides[1].Extinguish(0.5f);              // 0.5, pause running
            float h0 = sides[1].Height01;
            sides[1].Extinguish(0.2f);
            if (fail == null && Mathf.Abs(sides[1].Height01 - h0 * 0.8f) > 0.01f) fail = $"Extinguish(0.2): {h0:F3} -> {sides[1].Height01:F3}, expected {h0 * 0.8f:F3}";
            // tiny
            Ignite(sides[2]);
            float h1 = sides[2].Height01;
            sides[2].Extinguish(0.01f);
            if (fail == null && !(sides[2].Height01 < h1)) fail = $"Extinguish(0.01) did not lower Height01 ({h1} -> {sides[2].Height01})";
            if (fail == null && Mathf.Abs(sides[2].Height01 - h1 * 0.99f) > 0.001f) fail = $"Extinguish(0.01) gave {sides[2].Height01}, expected {h1 * 0.99f}";
            // zero amount does nothing
            Ignite(sides[3]);
            float h3 = sides[3].Height01;
            sides[3].Extinguish(0f);
            if (fail == null && sides[3].Height01 != h3) fail = "Extinguish(0) changed Height01";
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator p8_extinguish_pause()
        {
            const string n = "p8_extinguish_pause";
            yield return Load();
            string fail = null;
            float[] amounts = { 1f, 0.5f, 0.25f, 0.05f };
            for (int i = 0; i < 4; i++)
            {
                var s = _fire.Sides[i];
                Ignite(s);
                s.Extinguish(amounts[i]);
                float expect = _fc.maxStopFireTime * amounts[i];
                if (Mathf.Abs(s.PauseTimeLeft - expect) > 0.01f) fail = fail ?? $"amount {amounts[i]}: pause {s.PauseTimeLeft:F3}, expected {expect:F3}";
                // after the pause, fire grows again from the new height
                float h = s.Height01;
                s.Advance(expect + 0.01f);
                s.Advance(1f);
                if (!s.IsBurning) fail = fail ?? $"amount {amounts[i]}: not burning again after pause";
                else if (amounts[i] < 1f && !(s.Height01 > h)) fail = fail ?? $"amount {amounts[i]}: height did not grow after pause";
            }
            // longer pause wins
            var s0 = _fire.Sides[0];
            Ignite(s0); s0.Extinguish(0.5f); s0.Extinguish(0.1f);
            if (Mathf.Abs(s0.PauseTimeLeft - _fc.maxStopFireTime * 0.5f) > 0.01f) fail = fail ?? $"shorter water shortened pause: {s0.PauseTimeLeft}";
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator p8_roof_fire01_scales_and_kills()
        {
            const string n = "p8_roof_fire01_scales_and_kills";
            yield return Load();
            string fail = null;
            var slab = _fire.RoofSlabs[0];
            var side = slab.FireSide;
            Ignite(side);
            slab.Advance(_fc.roofIgniteTime * 2f);
            if (slab.Fire01 < 0.99f) fail = $"setup: Fire01={slab.Fire01:F3}";
            Bounds fb = slab.RoofFireView.GetComponent<BoxCollider>().bounds;
            var k = new GameObject("K");
            k.layer = LayerMask.NameToLayer("Human");
            k.transform.position = fb.center;
            k.AddComponent<BoxCollider>().size = Vector3.one * 0.3f;
            var rb = k.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var kill = k.AddComponent<TestKillable>();
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.3f);
            if (fail == null && kill.Kills == 0) fail = "not killed while Fire01=1";
            // water 0.5: Fire01 halves
            side.Extinguish(0.5f);
            if (fail == null && Mathf.Abs(slab.Fire01 - 0.5f) > 0.01f) fail = $"Fire01 after 0.5 water = {slab.Fire01:F3}, expected 0.5";
            if (fail == null && slab.Fire01 <= _fc.roofFireKillThreshold) fail = "setup: 0.5 should still be above threshold";
            kill.Kills = 0;
            yield return new WaitForSeconds(0.3f);
            if (fail == null && kill.Kills == 0) fail = "not killed at Fire01=0.5 (above threshold)";
            // push below the threshold: 0.5 * (1 - 0.9) = 0.05
            side.Extinguish(0.9f);
            if (fail == null && slab.Fire01 > _fc.roofFireKillThreshold) fail = $"Fire01={slab.Fire01:F3} still above threshold {_fc.roofFireKillThreshold}";
            if (fail == null && slab.OnFire) fail = "OnFire true below threshold";
            kill.Kills = 0;
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[TEST-INFO] {n} Fire01={slab.Fire01:F3} killsBelow={kill.Kills}");
            if (fail == null && kill.Kills != 0) fail = $"killed {kill.Kills}x below threshold";
            // direct slab water goes through the side once
            Ignite(_fire.RoofSlabs[1].FireSide);
            _fire.RoofSlabs[1].Advance(_fc.roofIgniteTime * 2f);
            _fire.RoofSlabs[1].Extinguish(0.5f);
            if (fail == null && Mathf.Abs(_fire.RoofSlabs[1].Fire01 - 0.5f) > 0.01f) fail = $"slab.Extinguish(0.5) gave Fire01 {_fire.RoofSlabs[1].Fire01:F3}";
            Object.Destroy(k);
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator p8_lost_side_ignores_water()
        {
            const string n = "p8_lost_side_ignores_water";
            yield return Load();
            var s = _fire.Sides[0];
            Ignite(s);
            s.SetLost();
            float h = s.Height01;
            s.Extinguish(1f);
            string fail = null;
            if (s.Height01 != h) fail = $"Height01 {h} -> {s.Height01} after water on lost side";
            else if (s.PauseTimeLeft > 0f) fail = $"pause {s.PauseTimeLeft} started on lost side";
            else if (!s.IsLost) fail = "side no longer lost";
            Report(n, fail == null, fail);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator p8_rotor_blur_while_flying()
        {
            const string n = "p8_rotor_blur_while_flying";
            yield return Load();
            Assert.IsFalse(_heli.IsTestMode);
            // The rotor transforms are hand-tuned in the scene, so compare against their values at load, not fixed numbers.
            var mr0 = Find(_heli.transform, "MainRotor"); var tr0 = Find(_heli.transform, "TailRotor");
            Vector3 mrPos0 = mr0 != null ? mr0.localPosition : Vector3.zero, mrScale0 = mr0 != null ? mr0.localScale : Vector3.zero;
            Vector3 trPos0 = tr0 != null ? tr0.localPosition : Vector3.zero, trScale0 = tr0 != null ? tr0.localScale : Vector3.zero;
            _heli.SetControlSource(_tm.Scripted);
            _tm.Scripted.Set(new HeliCommand { Vertical = 1f });
            yield return new WaitForSeconds(0.2f);
            float a1m = _heli.MainRotorBlur != null ? _heli.MainRotorBlur.Angle : -1f;
            float a1t = _heli.TailRotorBlur != null ? _heli.TailRotorBlur.Angle : -1f;
            yield return new WaitForSeconds(0.1f);
            string fail = null;
            if (_heli.MainRotorBlur == null || _heli.TailRotorBlur == null) fail = "blur views not assigned";
            else
            {
                float am = _heli.MainRotorBlur.Angle, at = _heli.TailRotorBlur.Angle;
                Debug.Log($"[TEST-INFO] {n} mainSpeed={_heli.MainRotorBlur.Speed:F2} tailSpeed={_heli.TailRotorBlur.Speed:F2} angle {a1m:F3}->{am:F3} tail {a1t:F3}->{at:F3}");
                if (!_heli.MainRotorBlur.IsVisible) fail = "main blur hidden";
                else if (!_heli.TailRotorBlur.IsVisible) fail = "tail blur hidden";
                else if (_heli.MainRotorBlur.Speed <= 0f) fail = "main speed 0";
                else if (_heli.TailRotorBlur.Speed <= 0f) fail = "tail speed 0";
                else if (Mathf.Approximately(am, a1m)) fail = "main angle not advancing";
                else if (Mathf.Approximately(at, a1t)) fail = "tail angle not advancing";
                else if (_heli.MainRotorBlur.Speed < _heli.MainRotorBlur.Speed * 0f + 0.01f) fail = "speed";
                if (fail == null)
                {
                    if (_heli.BladeRenderers == null || _heli.BladeRenderers.Length == 0) fail = "BladeRenderers empty";
                    else foreach (var r in _heli.BladeRenderers) if (r != null && r.enabled) fail = fail ?? $"blade renderer {r.name} enabled";
                }
                // idle vs climb: load factor speeds up main rotor
                var cfg = AssetDatabase.LoadAssetAtPath<HeliConfig>("Assets/Settings/HeliConfig.asset");
                float expect = cfg.mainRotorTurnsPerSecond * cfg.rotorLoadFactor;
                if (fail == null && _heli.MainRotorBlur.Speed < cfg.mainRotorTurnsPerSecond * 0.99f)
                    fail = $"main speed {_heli.MainRotorBlur.Speed:F2} below base {cfg.mainRotorTurnsPerSecond} at climb (expected toward {expect:F2})";
            }
            // transforms
            var mr = Find(_heli.transform, "MainRotor"); var tr = Find(_heli.transform, "TailRotor");
            if (fail == null)
            {
                if (mr == null || tr == null) fail = "MainRotor/TailRotor missing";
                else
                {
                    bool Near(Vector3 a, Vector3 b) => (a - b).magnitude < 0.001f;
                    if (!Near(mr.localPosition, mrPos0) || !Near(mr.localScale, mrScale0))
                        fail = $"MainRotor local pos {mr.localPosition} scale {mr.localScale}";
                    else if (!Near(tr.localPosition, trPos0) || !Near(tr.localScale, trScale0))
                        fail = $"TailRotor local pos {tr.localPosition} scale {tr.localScale}";
                    else
                    {
                        foreach (var t in new[] { mr, tr })
                        {
                            var bc = t.GetComponent<BoxCollider>();
                            var hz = t.GetComponent<RotorHazard>();
                            if (bc == null || hz == null) { fail = $"{t.name}: BoxCollider or RotorHazard missing"; break; }
                            Debug.Log($"[TEST-INFO] {n} {t.name} collider size={bc.size} center={bc.center} trigger={bc.isTrigger} layer={LayerMask.LayerToName(t.gameObject.layer)}");
                            if (t.gameObject.layer != LayerMask.NameToLayer("Rotors")) { fail = $"{t.name} layer not Rotors"; break; }
                            // own collider only: MainRotor may have child colliders (SecondRotorCollider)
                            if (_heli.MainRotorBlur != null && _heli.MainRotorBlur.GetComponent<Collider>() != null) { fail = "blur quad has a collider"; break; }
                        }
                    }
                }
            }
            _tm.Scripted.Set(default);
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator p8_crash_blades_become_debris()
        {
            const string n = "p8_crash_blades_become_debris";
            yield return Load();
            Assert.IsFalse(_heli.IsTestMode);
            var blur = new[] { _heli.MainRotorBlur, _heli.TailRotorBlur };
            var blades = _heli.BladeRenderers;
            yield return null;
            _heli.Crash();
            yield return new WaitForSeconds(0.2f);
            string fail = null;
            if (!_heli.IsCrashed) fail = "not crashed";
            foreach (var b in blur) if (fail == null && b != null && b.IsVisible) fail = $"blur {b.name} still visible";
            if (fail == null && (blades == null || blades.Length == 0)) fail = "no blade renderers";
            if (fail == null)
                foreach (var r in blades)
                {
                    if (r == null) { fail = "blade renderer destroyed"; break; }
                    if (!r.enabled) { fail = $"{r.name} blade renderer not re-enabled"; break; }
                    var rb = r.GetComponent<Rigidbody>();
                    if (rb == null) { fail = $"{r.name} has no own Rigidbody (not debris)"; break; }
                    if (r.transform.IsChildOf(_heli.transform)) { fail = $"{r.name} still under the heli"; break; }
                    if (r.GetComponentInParent<Rigidbody>() == _heli.Body) { fail = $"{r.name} still under heli body"; break; }
                }
            if (fail == null)
                foreach (var b in blur)
                {
                    if (b == null) continue;
                    if (b.GetComponent<Rigidbody>() != null) { fail = $"blur quad {b.name} got a Rigidbody"; break; }
                    if (b.GetComponent<Renderer>().enabled) { fail = $"blur quad {b.name} renderer enabled"; break; }
                }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator p8_rotor_hit_still_crashes()
        {
            const string n = "p8_rotor_hit_still_crashes";
            yield return Load();
            Assert.IsFalse(_heli.IsTestMode);
            _heli.SetControlSource(_tm.Scripted);
            _tm.Scripted.Set(default);
            int fired = 0; _heli.Crashed += () => fired++;
            yield return new WaitForSeconds(0.3f);
            var mr = Find(_heli.transform, "MainRotor");
            var bc = mr.GetComponent<BoxCollider>();
            // thin obstacle at rotor height, ahead of the tip (heli moves along world +X; rotor runs along its local X)
            Bounds rb = bc.bounds;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestRotorWall";
            wall.layer = LayerMask.NameToLayer("Obstacle");
            wall.transform.position = new Vector3(rb.max.x + 2.5f, rb.center.y, rb.center.z);
            wall.transform.localScale = new Vector3(0.5f, Mathf.Max(0.2f, rb.size.y * 0.8f), Mathf.Max(8f, rb.size.z + 4f));
            Physics.SyncTransforms();
            Debug.Log($"[TEST-INFO] {n} rotorBounds={rb} wall={wall.transform.position} heliPos={_heli.Body.position}");
            _tm.Scripted.Set(new HeliCommand { MoveWorld = Vector3.right });
            float t = 0f;
            while (!_heli.IsCrashed && t < 8f) { t += Time.deltaTime; yield return null; }
            _tm.Scripted.Set(default);
            Debug.Log($"[TEST-INFO] {n} crashed={_heli.IsCrashed} after {t:F2}s");
            string fail = null;
            if (!_heli.IsCrashed) fail = $"rotor hit did not crash within {t:F1}s";
            else if (fired != 1) fail = $"Crashed fired {fired}x";
            if (wall != null) Object.Destroy(wall);
            Report(n, fail == null, fail);
        }
    }
}
#endif
