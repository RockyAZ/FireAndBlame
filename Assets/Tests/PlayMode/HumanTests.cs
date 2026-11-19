#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
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
    /// <summary>Phase 5 human tests. Test mode OFF (real physics). FireSide components are disabled so time only moves through Advance(dt).</summary>
    public class HumanTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private HumansSystem _humans;
        private FireSystem _fire;
        private Cup _cup;
        private PoolZone _pool;
        private HeliController _heli;
        private TestModeController _tm;
        private HumanConfig _hc;
        private CupConfig _cc;
        private FireConfig _fc;

        private static void Report(string name, bool ok, string reason)
        {
            Debug.Log(ok ? $"[TEST] {name} PASS" : $"[TEST] {name} FAIL {reason}");
            Assert.IsTrue(ok, reason);
        }

        private IEnumerator Load(float settle = 1.5f)
        {
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return op;
            yield return null;
            _humans = UnityEngine.Object.FindFirstObjectByType<HumansSystem>(FindObjectsInactive.Include);
            _fire = UnityEngine.Object.FindFirstObjectByType<FireSystem>(FindObjectsInactive.Include);
            _cup = UnityEngine.Object.FindFirstObjectByType<Cup>(FindObjectsInactive.Include);
            _pool = UnityEngine.Object.FindFirstObjectByType<PoolZone>(FindObjectsInactive.Include);
            _heli = UnityEngine.Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            _tm = UnityEngine.Object.FindFirstObjectByType<TestModeController>(FindObjectsInactive.Include);
            _hc = AssetDatabase.LoadAssetAtPath<HumanConfig>("Assets/Settings/HumanConfig.asset");
            _cc = AssetDatabase.LoadAssetAtPath<CupConfig>("Assets/Settings/CupConfig.asset");
            _fc = AssetDatabase.LoadAssetAtPath<FireConfig>("Assets/Settings/FireConfig.asset");
            Assert.IsNotNull(_humans); Assert.IsNotNull(_fire); Assert.IsNotNull(_cup); Assert.IsNotNull(_pool);
            Assert.IsNotNull(_heli); Assert.IsNotNull(_tm); Assert.IsNotNull(_hc); Assert.IsNotNull(_cc); Assert.IsNotNull(_fc);
            foreach (var s in _fire.Sides) s.enabled = false;
            Assert.IsFalse(_heli.IsTestMode, "must start in real physics");
            if (settle > 0f) yield return new WaitForSeconds(settle);   // chain settles
        }

        private static Transform[] HangSlots(Cup c)
            => (Transform[])typeof(Cup).GetField("hangSlots", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c);
        private static Transform SwimSlot(Cup c)
            => (Transform)typeof(Cup).GetField("swimSlot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c);

        private static void Place(Human h, Vector3 p)
        {
            h.transform.position = p;
            h.Body.position = p;
            if (!h.Body.isKinematic) { h.Body.linearVelocity = Vector3.zero; h.Body.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
        }

        private static IEnumerator Until(Func<bool> cond, float timeout)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        private Vector3 PoolCenter()
        {
            Vector3 c = _pool.DriftBounds.center;
            return new Vector3(c.x, _pool.SurfaceY, c.z);
        }

        private void IgniteSide(FireSide s)
        {
            s.Advance(_fc.startDelaySeconds + 0.01f);
            s.Advance(100000f);
        }

        // ---------------------------------------------------------------- 1
        [UnityTest]
        public IEnumerator humans_spawn_on_slabs()
        {
            const string n = "humans_spawn_on_slabs";
            yield return Load(0f);
            string fail = null;
            var list = _humans.Humans;
            if (list.Count != _hc.humanCount) fail = $"Humans.Count={list.Count}, humanCount={_hc.humanCount}";
            var slabs = _fire.RoofSlabs;
            for (int i = 0; i < list.Count && fail == null; i++)
            {
                var h = list[i];
                Vector3 p = h.transform.position;
                var own = slabs[i % slabs.Count].SpawnBounds; own.Expand(0.1f);
                bool inOwn = p.x >= own.min.x && p.x <= own.max.x && p.z >= own.min.z && p.z <= own.max.z;
                bool inAny = false;
                foreach (var s in slabs)
                {
                    var b = s.SpawnBounds; b.Expand(0.1f);
                    if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z) inAny = true;
                }
                if (!inAny) fail = $"{h.name} at {p} not inside any SpawnBounds";
                else if (!inOwn) fail = $"{h.name} at {p} not inside round-robin slab {slabs[i % slabs.Count].FireSide.Side}";
                else if (h.State != HumanState.WaitingOnRoof) fail = $"{h.name} state {h.State}, expected WaitingOnRoof";
            }
            var prev = (GameObject)typeof(HumansSystem).GetField("scenePreview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_humans);
            if (fail == null && prev != null && prev.activeSelf) fail = "scene preview still active";
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- 2
        [UnityTest]
        public IEnumerator empty_tank_grabs_to_hang_slot()
        {
            const string n = "empty_tank_grabs_to_hang_slot";
            yield return Load();
            string fail = null;
            var slots = HangSlots(_cup);
            var a = _humans.Humans[0]; var b = _humans.Humans[1]; var c = _humans.Humans[2];
            if (!_cup.IsEmpty) fail = "cup not empty at start";
            var ra = _cup.TryGrab(a); var rb = _cup.TryGrab(b);
            if (fail == null && (ra != GrabResult.Grabbed || rb != GrabResult.Grabbed)) fail = $"TryGrab results {ra}, {rb}, expected Grabbed";
            if (fail == null && (a.State != HumanState.RescuedHanging || b.State != HumanState.RescuedHanging)) fail = $"states {a.State}, {b.State}";
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            if (fail == null)
            {
                int ia = NearestSlot(slots, a.TankAttachPoint.position, out float da);
                int ib = NearestSlot(slots, b.TankAttachPoint.position, out float db);
                Debug.Log($"[TEST-INFO] {n} dist a={da:F3} (slot {ia}) b={db:F3} (slot {ib}) attached={_cup.AttachedCount}");
                if (da > 0.15f || db > 0.15f) fail = $"TankAttachPoint not at slot after 10 steps (a {da:F3} m, b {db:F3} m)";
                else if (ia == ib) fail = "both humans use the same slot";
            }
            if (fail == null)
            {
                var rc = _cup.TryGrab(c);
                if (rc != GrabResult.Rejected) fail = $"third TryGrab returned {rc}, expected Rejected";
                else if (c.State != HumanState.WaitingOnRoof) fail = $"third human state {c.State}";
                else if (_cup.AttachedCount != 2) fail = $"AttachedCount {_cup.AttachedCount}";
            }
            Report(n, fail == null, fail);
        }

        private static int NearestSlot(Transform[] slots, Vector3 p, out float dist)
        {
            int best = -1; dist = float.MaxValue;
            for (int i = 0; i < slots.Length; i++)
            {
                float d = Vector3.Distance(slots[i].position, p);
                if (d < dist) { dist = d; best = i; }
            }
            return best;
        }

        // ---------------------------------------------------------------- 3
        [UnityTest]
        public IEnumerator full_tank_bounces_and_stuns()
        {
            const string n = "full_tank_bounces_and_stuns";
            yield return Load();
            string fail = null;
            int guard = 0;
            while (_cup.IsEmpty && guard++ < 100) _cup.FillTick(1f);
            var h = _humans.Humans[0];
            var anim = h.GetComponent<HumanAnimator>();
            var fired = new List<string>();
            anim.TriggerFired += t => fired.Add(t);
            Vector3 before = h.transform.position;
            Vector3 away = h.Body.worldCenterOfMass - _cup.Body.worldCenterOfMass; away.y = 0f; away.Normalize();
            var r = _cup.TryGrab(h);
            if (r != GrabResult.BouncedFull) fail = $"TryGrab {r}, expected BouncedFull (Water01={_cup.Water01:F2})";
            else if (!h.IsStunned) fail = "IsStunned false after bounce";
            else if (!fired.Contains(_hc.stunnedTrigger)) fail = $"no '{_hc.stunnedTrigger}' trigger, saw [{string.Join(",", fired)}]";
            float maxAlong = 0f;
            float t = 0f;
            yield return new WaitForFixedUpdate();
            Debug.Log($"[TEST-INFO] {n} velocity one step after bump={h.Body.linearVelocity} (bumpImpulse={_cc.bumpImpulse}, mass={h.Body.mass}, expected speed {_cc.bumpImpulse / h.Body.mass:F3} m/s)");
            while (fail == null && t < 0.4f)
            {
                t += Time.deltaTime;
                maxAlong = Mathf.Max(maxAlong, Vector3.Dot(h.transform.position - before, away));
                yield return null;
            }
            if (fail == null && maxAlong < 0.1f) fail = $"human not pushed away from the cup (max {maxAlong:F3} m along away dir in 0.4 s)";
            if (fail == null)
            {
                int stunnedIdx = fired.IndexOf(_hc.stunnedTrigger);
                yield return new WaitForSeconds(_hc.stunDuration + 0.2f - t);
                Debug.Log($"[TEST-INFO] {n} pushed {maxAlong:F2} m, fired=[{string.Join(",", fired)}] stunned={h.IsStunned} state={h.State}");
                if (h.IsStunned) fail = "still stunned after stunDuration";
                else if (fired.LastIndexOf(_hc.waitRescueTrigger) <= stunnedIdx) fail = $"'{_hc.waitRescueTrigger}' did not fire again after the stun, saw [{string.Join(",", fired)}]";
                else if (h.State != HumanState.WaitingOnRoof) fail = $"state changed to {h.State}";
            }
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- 4
        [UnityTest]
        public IEnumerator pool_tank_scoops_swimmer()
        {
            const string n = "pool_tank_scoops_swimmer";
            yield return Load();
            string fail = null;
            var h = _humans.Humans[0];
            Place(h, PoolCenter());
            h.EnterPool(_pool);
            yield return new WaitForFixedUpdate();
            if (h.State != HumanState.ChillingInPool) fail = $"state {h.State}, expected ChillingInPool";
            _cup.SetInPool(true);
            var r = fail == null ? _cup.TryGrab(h) : GrabResult.Rejected;
            if (fail == null && r != GrabResult.Grabbed) fail = $"TryGrab {r}, expected Grabbed (Water01={_cup.Water01:F2})";
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            if (fail == null)
            {
                var slot = SwimSlot(_cup);
                float d = Vector3.Distance(h.FloatPoint.position, slot.position);
                Debug.Log($"[TEST-INFO] {n} state={h.State} dist={d:F3} parent={(h.transform.parent != null ? h.transform.parent.name : "null")} attached={_cup.AttachedCount}");
                if (h.State != HumanState.SwimmingInTank) fail = $"state {h.State}";
                else if (d > 0.05f) fail = $"FloatPoint {d:F3} m from SwimSlot";
                else if (h.transform.parent != slot) fail = "human not parented under the swim slot";
                else if (_cup.AttachedCount != 1) fail = $"AttachedCount {_cup.AttachedCount}";
            }
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- 5
        [UnityTest]
        public IEnumerator tug_releases_velocity_and_lands()
        {
            const string n = "tug_releases_velocity_and_lands";
            yield return Load();
            string fail = null;
            var a = _humans.Humans[0]; var b = _humans.Humans[1]; var c = _humans.Humans[2];
            if (_cup.TryGrab(a) != GrabResult.Grabbed || _cup.TryGrab(b) != GrabResult.Grabbed) fail = "could not attach two humans";
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            if (fail == null)
            {
                _cup.Body.linearVelocity = new Vector3(3f, 0f, 0f);
                _cup.Tug();   // water is 0: ReleaseAll only
                Vector3 vb = _cup.Body.linearVelocity;
                Debug.Log($"[TEST-INFO] {n} bowl v={vb} a v={a.Body.linearVelocity} b v={b.Body.linearVelocity}");
                if (_cup.AttachedCount != 0) fail = $"AttachedCount {_cup.AttachedCount} after Tug";
                else if (a.IsAttached || b.IsAttached) fail = "human still attached";
                else if (!a.IsFalling || !b.IsFalling) fail = $"IsFalling a={a.IsFalling} b={b.IsFalling}";
                else if ((a.Body.linearVelocity - vb).magnitude > 0.1f || (b.Body.linearVelocity - vb).magnitude > 0.1f)
                    fail = $"velocity not inherited: bowl {vb}, a {a.Body.linearVelocity}, b {b.Body.linearVelocity}";
            }
            if (fail == null)
            {
                // landings: a above the pool, b above a roof slab (real falls)
                Place(a, PoolCenter() + Vector3.up * 5f);
                var slab = _fire.RoofSlabs[0];
                Place(b, slab.GetSpawnPoint() + Vector3.up * 3f);
                yield return Until(() => a.State == HumanState.ChillingInPool && !a.IsFalling, 6f);
                yield return Until(() => b.State == HumanState.WaitingOnRoof && !b.IsFalling, 6f);
                Debug.Log($"[TEST-INFO] {n} a={a.State} falling={a.IsFalling}, b={b.State} falling={b.IsFalling}");
                if (a.State != HumanState.ChillingInPool) fail = $"pool landing: state {a.State}, IsFalling {a.IsFalling}";
                else if (b.State != HumanState.WaitingOnRoof) fail = $"roof landing: state {b.State}, IsFalling {b.IsFalling}";
                else
                {
                    yield return new WaitForSeconds(0.5f);
                    float tilt = Vector3.Angle(b.transform.up, Vector3.up);
                    if (tilt > 5f) fail = $"roof human not upright (tilt {tilt:F1} deg)";
                    else if (b.Body.isKinematic) fail = "roof human is kinematic, expected dynamic";
                }
            }
            if (fail == null)
            {
                yield return new WaitForSeconds(_cc.regrabCooldown + 0.3f);
                var r = _cup.TryGrab(c);
                if (r != GrabResult.Grabbed) fail = $"third attach {r}";
                else
                {
                    yield return new WaitForFixedUpdate();
                    _cup.ReleaseAll();
                    Place(c, new Vector3(-25f, 3f, 25f));
                    if (!c.IsFalling) fail = "c not falling after ReleaseAll";
                    else
                    {
                        yield return Until(() => c.State == HumanState.Dead, 6f);
                        Debug.Log($"[TEST-INFO] {n} c={c.State}");
                        if (c.State != HumanState.Dead) fail = $"ground fire landing: state {c.State}";
                    }
                }
            }
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- 6
        [UnityTest]
        public IEnumerator strength_drains_and_recovers()
        {
            // Cup.Acceleration is read-only, so this drives Human.TickStrength directly (the formula and the detach); strength_drains_from_real_shake covers the wiring.
            const string n = "strength_drains_and_recovers";
            yield return Load();
            string fail = null;
            var h = _humans.Humans[0]; var s = _humans.Humans[1];
            if (_cup.TryGrab(h) != GrabResult.Grabbed) fail = "attach failed";
            yield return new WaitForFixedUpdate();
            if (fail == null)
            {
                h.enabled = false;   // stop its own FixedUpdate tick so only the explicit ticks count
                float thr = _hc.accelerationThreshold;
                if (Mathf.Abs(h.Strength01 - 1f) > 0.001f) fail = $"Strength01 {h.Strength01} after attach, expected 1";
                float exp1 = 1f - 5f * _hc.strengthLossFactor / _hc.maxStrength;
                h.TickStrength(1f, thr + 5f);
                if (fail == null && Mathf.Abs(h.Strength01 - exp1) > 0.005f) fail = $"drain: Strength01 {h.Strength01:F3}, expected {exp1:F3}";
                float exp2 = exp1 + _hc.strengthRecoveryPerSecond / _hc.maxStrength;
                h.TickStrength(1f, 0f);
                if (fail == null && Mathf.Abs(h.Strength01 - exp2) > 0.005f) fail = $"recover: Strength01 {h.Strength01:F3}, expected {exp2:F3}";
                if (fail == null)
                {
                    h.TickStrength(100f, thr + 10f);
                    if (h.Strength01 > 0f) fail = $"Strength01 {h.Strength01} after huge drain";
                    else if (h.IsAttached || _cup.AttachedCount != 0) fail = "human still attached at strength 0";
                    else if (!h.IsFalling) fail = "human not falling at strength 0";
                }
            }
            if (fail == null)
            {
                // swimming humans do not drain
                Place(s, PoolCenter());
                s.EnterPool(_pool);
                _cup.SetInPool(true);
                yield return new WaitForSeconds(_cc.regrabCooldown + 0.3f);
                if (_cup.TryGrab(s) != GrabResult.Grabbed) fail = "swimmer attach failed";
                else
                {
                    s.TickStrength(1f, 100f);
                    if (s.Strength01 < 0.999f) fail = $"swimming human drained to {s.Strength01}";
                }
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator strength_drains_from_real_shake()
        {
            const string n = "strength_drains_from_real_shake";
            yield return Load();
            string fail = null;
            var h = _humans.Humans[0];
            if (_cup.TryGrab(h) != GrabResult.Grabbed) fail = "attach failed";
            if (fail == null)
            {
                _heli.SetControlSource(_tm.Scripted);
                float t = 0f, minStr = 1f, maxAcc = 0f;
                while (t < 12f && h.IsAttached)
                {
                    float phase = Mathf.Floor(t / 0.4f);
                    _tm.Scripted.Set(new HeliCommand { MoveWorld = new Vector3((phase % 2f == 0f) ? 1f : -1f, 0f, 0f) });
                    t += Time.deltaTime;
                    minStr = Mathf.Min(minStr, h.Strength01);
                    maxAcc = Mathf.Max(maxAcc, _cup.Acceleration.magnitude);
                    yield return null;
                }
                _tm.Scripted.Set(default);
                Debug.Log($"[TEST-INFO] {n} t={t:F1}s minStrength={minStr:F3} maxCupAcc={maxAcc:F2} threshold={_hc.accelerationThreshold} attached={h.IsAttached} falling={h.IsFalling}");
                if (minStr >= 0.999f && h.IsAttached) fail = $"strength never dropped (max Cup.Acceleration {maxAcc:F2} vs threshold {_hc.accelerationThreshold})";
            }
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- 7
        [UnityTest]
        public IEnumerator pool_human_floats_and_drifts()
        {
            const string n = "pool_human_floats_and_drifts";
            yield return Load(0f);
            string fail = null;
            var h = _humans.Humans[0];
            Place(h, PoolCenter());
            h.EnterPool(_pool);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Vector3 start = h.transform.position;
            float maxDy = 0f, maxDist = 0f;
            Bounds b = _pool.DriftBounds;
            float target = _pool.SurfaceY + _hc.floatHeight;
            float tol = _hc.bobAmplitude + 0.02f;
            float t = 0f;
            int outside = 0, frames = 0;
            while (t < 6f)
            {
                t += Time.deltaTime; frames++;
                float dy = Mathf.Abs(h.FloatPoint.position.y - target);
                maxDy = Mathf.Max(maxDy, dy);
                Vector3 p = h.transform.position;
                maxDist = Mathf.Max(maxDist, new Vector2(p.x - start.x, p.z - start.z).magnitude);
                if (p.x < b.min.x - 0.05f || p.x > b.max.x + 0.05f || p.z < b.min.z - 0.05f || p.z > b.max.z + 0.05f) outside++;
                yield return null;
            }
            Debug.Log($"[TEST-INFO] {n} frames={frames} maxDy={maxDy:F3} (tol {tol:F3}) maxDist={maxDist:F2} outsideFrames={outside} state={h.State}");
            if (h.State != HumanState.ChillingInPool) fail = $"state {h.State}";
            else if (maxDy > tol) fail = $"FloatPoint y off the waterline by {maxDy:F3} m (allowed {tol:F3})";
            else if (maxDist < 0.3f) fail = $"human barely moved ({maxDist:F2} m in 6 s)";
            else if (outside > 0) fail = $"{outside} frames outside DriftBounds";
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- 8
        [UnityTest]
        public IEnumerator burning_slab_kills_human()
        {
            const string n = "burning_slab_kills_human";
            yield return Load(0f);
            string fail = null;
            var h = _humans.Humans[0];          // on slab 0
            var other = _humans.Humans[1];      // on slab 1
            var slab = _fire.RoofSlabs[0];
            yield return new WaitForSeconds(0.7f);
            if (h.State != HumanState.WaitingOnRoof) fail = $"dead/changed before ignition: {h.State}";
            if (fail == null)
            {
                IgniteSide(slab.FireSide);
                yield return Until(() => h.State == HumanState.Dead, 4f);
                Debug.Log($"[TEST-INFO] {n} h={h.State} other={other.State} slabOnFire={slab.OnFire} humanPos={h.transform.position}");
                if (!slab.OnFire) fail = "slab not OnFire after Advance";
                else if (h.State != HumanState.Dead) fail = $"human on burning slab still {h.State} after 4 s";
                else if (other.State != HumanState.WaitingOnRoof) fail = $"human on another slab is {other.State}";
            }
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- 9
        [UnityTest]
        public IEnumerator animator_triggers_match_state()
        {
            const string n = "animator_triggers_match_state";
            yield return Load(0f);
            string fail = null;
            var h = _humans.Humans[0];
            var anim = h.GetComponent<HumanAnimator>();
            var fired = new List<string>();
            anim.TriggerFired += t => fired.Add(t);
            var steps = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>(_hc.chillPoolTrigger, () => h.SetState(HumanState.ChillingInPool)),
                new KeyValuePair<string, Action>(_hc.waitRescueTrigger, () => h.SetState(HumanState.WaitingOnRoof)),
                new KeyValuePair<string, Action>(_hc.hangingTrigger, () => h.SetState(HumanState.RescuedHanging)),
                new KeyValuePair<string, Action>(_hc.swimInTankTrigger, () => h.SetState(HumanState.SwimmingInTank)),
                new KeyValuePair<string, Action>(_hc.stunnedTrigger, () => h.Stun()),
                new KeyValuePair<string, Action>(_hc.deadTrigger, () => h.SetState(HumanState.Dead)),
            };
            foreach (var s in steps)
            {
                int before = fired.Count;
                s.Value();
                if (anim.LastTrigger != s.Key || fired.Count != before + 1 || fired[fired.Count - 1] != s.Key)
                {
                    fail = $"expected '{s.Key}', LastTrigger '{anim.LastTrigger}', fired {fired.Count - before} new [{string.Join(",", fired)}]";
                    break;
                }
            }
            yield return null;
            Report(n, fail == null, fail);
        }
    }
}
#endif
