#if UNITY_EDITOR
using System.Collections;
using FlameAndBlame.Fire;
using FlameAndBlame.Rescue;
using FlameAndBlame.Testing;
using NUnit.Framework;
using FlameAndBlame.Heli;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>
    /// Phase 2 fire and water checks. FireSide.Update also calls Advance(Time.deltaTime), so every FireSide
    /// component is disabled after load; the tests drive time only through FireSide.Advance(dt).
    /// </summary>
    public class FireWaterTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        private Cup _cup;
        private TestModeController _tm;
        private FireSystem _fire;
        private PoolZone _pool;
        private FireConfig _fc;
        private CupConfig _cc;

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
            _cup = Object.FindFirstObjectByType<Cup>(FindObjectsInactive.Include);
            _tm = Object.FindFirstObjectByType<TestModeController>(FindObjectsInactive.Include);
            _fire = Object.FindFirstObjectByType<FireSystem>(FindObjectsInactive.Include);
            _pool = Object.FindFirstObjectByType<PoolZone>(FindObjectsInactive.Include);
            _fc = AssetDatabase.LoadAssetAtPath<FireConfig>("Assets/Settings/FireConfig.asset");
            _cc = AssetDatabase.LoadAssetAtPath<CupConfig>("Assets/Settings/CupConfig.asset");
            Assert.IsNotNull(_cup, "Cup not found");
            Assert.IsNotNull(_tm, "TestModeController not found");
            Assert.IsNotNull(_fire, "FireSystem not found");
            Assert.IsNotNull(_pool, "PoolZone not found");
            Assert.IsNotNull(_fc, "FireConfig asset not found");
            Assert.IsNotNull(_cc, "CupConfig asset not found");
            foreach (var s in _fire.Sides) s.enabled = false; // stop the real-time Update clock
            _tm.EnterAll();
            yield return new WaitForFixedUpdate();
        }

        // Pass the start delay so the side is burning, then grow to the given height (fraction of full).
        private void IgniteTo(FireSide s, float height01)
        {
            s.Advance(_fc.startDelaySeconds + 0.01f);
            Assert.IsTrue(s.IsBurning, "side not burning after start delay");
            // total time to full = spreadSecondsPerFloor * floors; read floors via height growth probe
            float dt = 0.1f;
            int guard = 0;
            while (s.Height01 < height01 && guard++ < 100000) s.Advance(dt);
        }

        private Collider CupCollider()
        {
            foreach (var c in _cup.Body.GetComponentsInChildren<Collider>(true))
                if (c.CompareTag("Cup")) return c;
            foreach (var c in _cup.Body.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger) return c;
            return null;
        }

        private static Collider CatchCollider(FireSide s)
        {
            foreach (var c in s.GetComponentsInChildren<Collider>(true))
                if (c is BoxCollider && c.isTrigger) return c;
            return null;
        }

        private void FillCup()
        {
            for (int i = 0; i < 10000 && _cup.Water01 < 1f; i++) _cup.FillTick(0.1f);
        }

        [UnityTest]
        public IEnumerator fill_in_pool()
        {
            const string n = "fill_in_pool";
            yield return Load();
            var col = CupCollider();
            Assert.IsNotNull(col, "no cup collider");
            int events = 0; float last = -1f;
            _cup.WaterChanged += w => { events++; last = w; };
            float start = _cup.Water01;
            _pool.ReportEnter(col);
            string fail = null;
            if (!_cup.InPool) fail = "InPool false after PoolZone.ReportEnter";
            float expected = _cc.capacity / _cc.fillRatePerSecond; // seconds from 0 to 1
            float dt = Time.fixedDeltaTime;
            float t = 0f;
            while (_cup.Water01 < 1f && t < expected * 3f) { _pool.ReportStay(col); t += dt; }
            Debug.Log($"[TEST-INFO] {n} start={start} expected={expected:F2}s simulated={t:F2}s water={_cup.Water01}");
            if (fail == null && _cup.Water01 < 1f) fail = $"Water01={_cup.Water01} after {t:F1}s, expected 1 within {expected:F1}s";
            else if (fail == null && t > expected * 1.05f + dt) fail = $"took {t:F2}s, expected about {expected:F2}s";
            else if (fail == null && t < expected * 0.95f - dt) fail = $"filled too fast: {t:F2}s, expected about {expected:F2}s";
            else if (fail == null && events == 0) fail = "WaterChanged never fired";
            else if (fail == null && !Mathf.Approximately(last, 1f)) fail = $"last WaterChanged value {last}, expected 1";
            else if (fail == null && !_cup.IsFull) fail = "IsFull false";
            if (fail == null && !_cup.InPool) fail = "InPool false while inside";
            _pool.ReportExit(col);
            if (fail == null && _cup.InPool) fail = "InPool true after ReportExit";
            _tm.ExitAll();
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator dump_extinguishes_then_regrows()
        {
            const string n = "dump_extinguishes_then_regrows";
            yield return Load();
            var side = _fire.Sides[0];
            IgniteTo(side, 0.3f);
            float before = side.Height01;
            string fail = before <= 0f ? "side height not > 0 before dump" : null;

            FillCup();
            var existing = new System.Collections.Generic.HashSet<WaterDrop>(Object.FindObjectsByType<WaterDrop>(FindObjectsSortMode.None));
            _cup.Tug();
            WaterDrop drop = null;
            foreach (var d in Object.FindObjectsByType<WaterDrop>(FindObjectsSortMode.None))
                if (!existing.Contains(d)) drop = d;
            if (fail == null && drop == null) fail = "Tug spawned no WaterDrop";
            if (fail == null)
            {
                var catchCol = CatchCollider(side);
                if (catchCol == null) fail = "no water catch trigger under FireSide";
                else drop.ReportHit(catchCol);
            }
            if (fail == null && side.Height01 != 0f) fail = $"Height01={side.Height01} after hit, expected 0";
            if (fail == null && side.IsBurning) fail = "IsBurning true during pause";
            if (fail == null && Mathf.Abs(side.PauseTimeLeft - _fc.maxStopFireTime) > 0.01f) fail = $"PauseTimeLeft={side.PauseTimeLeft}, expected {_fc.maxStopFireTime}";

            // stays at 0 for (almost) the whole pause
            if (fail == null)
            {
                float dt = 0.1f, t = 0f;
                while (t < _fc.maxStopFireTime - 0.5f)
                {
                    side.Advance(dt); t += dt;
                    if (side.Height01 != 0f || side.IsBurning) { fail = $"fire back at t={t:F1}s, pause is {_fc.maxStopFireTime}s"; break; }
                }
            }
            // then restarts and grows
            if (fail == null)
            {
                for (int i = 0; i < 100 && !side.IsBurning; i++) side.Advance(0.1f);
                if (!side.IsBurning) fail = "fire never restarted after pause";
                else
                {
                    side.Advance(1f);
                    float h1 = side.Height01;
                    side.Advance(1f);
                    if (!(h1 > 0f) || !(side.Height01 > h1)) fail = $"Height01 did not grow after restart ({h1} -> {side.Height01})";
                }
            }
            _tm.ExitAll();
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator partial_water_no_effect()
        {
            const string n = "partial_water_no_effect";
            yield return Load();
            var side = _fire.Sides[1];
            IgniteTo(side, 0.3f);
            float before = side.Height01;
            side.Extinguish(0f);
            string fail = null;
            if (!Mathf.Approximately(side.Height01, before) || !side.IsBurning || side.PauseTimeLeft > 0f) fail = "zero water changed the fire";
            side.Extinguish(0.2f);
            if (fail == null && !Mathf.Approximately(side.Height01, before * 0.8f)) fail = $"Height01 {before} -> {side.Height01}, expected {before * 0.8f}";
            else if (fail == null && !Mathf.Approximately(side.PauseTimeLeft, _fc.maxStopFireTime * 0.2f)) fail = $"PauseTimeLeft={side.PauseTimeLeft}, expected {_fc.maxStopFireTime * 0.2f}";
            _tm.ExitAll();
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator top_sets_roof_fire()
        {
            const string n = "top_sets_roof_fire";
            yield return Load();
            var side = _fire.Sides[2];
            int raised = 0;
            side.ReachedTop += _ => raised++;
            string fail = null;
            side.Advance(_fc.startDelaySeconds + 0.01f);
            if (side.RoofOnFire) fail = "RoofOnFire true before reaching top";
            for (int i = 0; i < 100000 && side.Height01 < 1f; i++) side.Advance(0.5f);
            if (fail == null && side.Height01 < 1f) fail = $"never reached top, Height01={side.Height01}";
            else if (fail == null && !side.RoofOnFire) fail = "RoofOnFire false at top";
            else if (fail == null && raised != 1) fail = $"ReachedTop raised {raised} times, expected 1";
            _tm.ExitAll();
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator lost_mode_ignores_water()
        {
            const string n = "lost_mode_ignores_water";
            yield return Load();
            var side = _fire.Sides[3];
            IgniteTo(side, 0.3f);
            side.SetLost();
            float before = side.Height01;
            side.Extinguish(1f);
            string fail = null;
            if (!side.IsLost) fail = "IsLost false after SetLost";
            else if (!Mathf.Approximately(side.Height01, before)) fail = $"Height01 {before} -> {side.Height01} after Extinguish in lost mode";
            else if (!side.IsBurning) fail = "IsBurning false after Extinguish in lost mode";
            else if (side.PauseTimeLeft > 0f) fail = "pause started in lost mode";
            _tm.ExitAll();
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator tug_empties_cup()
        {
            const string n = "tug_empties_cup";
            yield return Load();
            FillCup();
            string fail = null;
            if (!_cup.IsFull) fail = $"cup not full before Tug ({_cup.Water01})";
            var existing = new System.Collections.Generic.HashSet<WaterDrop>(Object.FindObjectsByType<WaterDrop>(FindObjectsSortMode.None));
            _cup.Tug();
            int spawned = 0;
            foreach (var d in Object.FindObjectsByType<WaterDrop>(FindObjectsSortMode.None)) if (!existing.Contains(d)) spawned++;
            if (fail == null && _cup.Water01 != 0f) fail = $"Water01={_cup.Water01} after Tug, expected 0";
            else if (fail == null && !_cup.IsEmpty) fail = "IsEmpty false after Tug";
            else if (fail == null && spawned != 1) fail = $"{spawned} WaterDrops spawned, expected 1";
            _tm.ExitAll();
            Report(n, fail == null, fail);
        }
        [UnityTest]
        public IEnumerator camera_follows_heli()
        {
            const string n = "camera_follows_heli";
            yield return Load();
            var heli = Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            var cam = Object.FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
            string fail = null;
            if (heli == null) fail = "HeliController not found";
            else if (cam == null) fail = "CinemachineCamera not found";
            else if (cam.Follow != heli.transform) fail = $"Follow={(cam.Follow ? cam.Follow.name : "null")}, expected heli transform";
            else if (cam.LookAt != heli.transform) fail = $"LookAt={(cam.LookAt ? cam.LookAt.name : "null")}, expected heli transform";
            _tm.ExitAll();
            Report(n, fail == null, fail);
        }
    }
}
#endif
