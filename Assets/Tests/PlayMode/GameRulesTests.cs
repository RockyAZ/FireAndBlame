#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using FlameAndBlame.GameRules;
using FlameAndBlame.Heli;
using FlameAndBlame.Rescue;
using FlameAndBlame.Testing;
using FlameAndBlame.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Phase 6 rules, score, auto-align and UI tests. FireSide components are disabled so time only moves through Advance(dt).</summary>
    public class GameRulesTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private GameManager _game;
        private HumansSystem _humans;
        private FireSystem _fire;
        private Cup _cup;
        private HeliController _heli;
        private TestModeController _tm;
        private GameRulesConfig _rc;
        private FireConfig _fc;
        private HeliConfig _hcfg;
        private bool _savedAlign;

        private static void Report(string name, bool ok, string reason)
        {
            Debug.Log(ok ? $"[TEST] {name} PASS" : $"[TEST] {name} FAIL {reason}");
            Assert.IsTrue(ok, reason);
        }

        [TearDown]
        public void Restore()
        {
            if (_hcfg != null) _hcfg.autoAlignToCamera = _savedAlign;
        }

        private IEnumerator Load(bool testMode)
        {
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return op;
            yield return null;
            _game = UnityEngine.Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            _humans = UnityEngine.Object.FindFirstObjectByType<HumansSystem>(FindObjectsInactive.Include);
            _fire = UnityEngine.Object.FindFirstObjectByType<FireSystem>(FindObjectsInactive.Include);
            _cup = UnityEngine.Object.FindFirstObjectByType<Cup>(FindObjectsInactive.Include);
            _heli = UnityEngine.Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            _tm = UnityEngine.Object.FindFirstObjectByType<TestModeController>(FindObjectsInactive.Include);
            _rc = AssetDatabase.LoadAssetAtPath<GameRulesConfig>("Assets/Settings/GameRulesConfig.asset");
            _fc = AssetDatabase.LoadAssetAtPath<FireConfig>("Assets/Settings/FireConfig.asset");
            _hcfg = AssetDatabase.LoadAssetAtPath<HeliConfig>("Assets/Settings/HeliConfig.asset");
            _savedAlign = _hcfg.autoAlignToCamera;
            Assert.IsNotNull(_game); Assert.IsNotNull(_humans); Assert.IsNotNull(_fire); Assert.IsNotNull(_cup);
            Assert.IsNotNull(_heli); Assert.IsNotNull(_tm); Assert.IsNotNull(_rc); Assert.IsNotNull(_fc); Assert.IsNotNull(_hcfg);
            foreach (var s in _fire.Sides) s.enabled = false;
            Assert.AreEqual(GameResult.Playing, _game.Result, "game must start Playing");
            if (testMode) _tm.EnterAll();
            yield return new WaitForFixedUpdate();
        }

        private void Ignite(FireSide s)
        {
            s.Advance(_fc.startDelaySeconds + 0.01f);
            s.Advance(100000f);
        }

        private int Count(HumanState st) => _humans.CountIn(st);

        // ---------------------------------------------------------------- crash_lost
        [UnityTest]
        public IEnumerator crash_lost()
        {
            const string n = "crash_lost";
            yield return Load(true);
            int fired = 0; GameResult last = GameResult.Playing;
            _game.ResultChanged += r => { fired++; last = r; };
            _heli.Crash();
            _heli.Crash();
            string fail = null;
            if (_game.Result != GameResult.Lost) fail = $"Result {_game.Result}, expected Lost";
            else if (fired != 1) fail = $"ResultChanged fired {fired} times, expected 1";
            else if (last != GameResult.Lost) fail = $"event carried {last}";
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- crash_kills_all_and_sides_lost
        [UnityTest]
        public IEnumerator crash_kills_all_and_sides_lost()
        {
            const string n = "crash_kills_all_and_sides_lost";
            yield return Load(true);
            // One human already in the pool before the crash. Design: KillAll kills everyone, rescued is a snapshot.
            _humans.Humans[0].SetState(HumanState.ChillingInPool);
            _heli.Crash();
            string fail = null;
            foreach (var h in _humans.Humans)
                if (h.State != HumanState.Dead) { fail = $"{h.name} state {h.State}, expected Dead after crash"; break; }
            if (fail == null)
                foreach (var s in _fire.Sides)
                    if (!s.IsLost) { fail = $"FireSide {s.Side} IsLost false after crash"; break; }
            if (fail == null && _game.Rescued != 1) fail = $"Rescued snapshot {_game.Rescued}, expected 1";
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- all_sides_burning_lost
        [UnityTest]
        public IEnumerator all_sides_burning_lost()
        {
            const string n = "all_sides_burning_lost";
            yield return Load(true);
            string fail = null;
            if (!_rc.endOnAllSidesBurning) fail = "endOnAllSidesBurning is false in the asset";
            int fired = 0;
            _game.ResultChanged += r => fired++;
            for (int i = 0; i < _fire.Sides.Count - 1; i++) Ignite(_fire.Sides[i]);
            if (fail == null && _game.Result != GameResult.Playing) fail = $"Result {_game.Result} with only 3 sides burning";
            Ignite(_fire.Sides[_fire.Sides.Count - 1]);
            if (fail == null && _game.Result != GameResult.Lost) fail = $"Result {_game.Result} with all sides at the top, expected Lost (SidesNotOnFire={_fire.SidesNotOnFire})";
            else if (fail == null && fired != 1) fail = $"ResultChanged fired {fired} times";
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- all_dead_lost
        [UnityTest]
        public IEnumerator all_dead_lost()
        {
            const string n = "all_dead_lost";
            yield return Load(true);
            string fail = null;
            int k = 0;
            foreach (var h in _humans.Humans)
            {
                if (_game.Result != GameResult.Playing) { fail = $"ended early after {k} kills ({_game.Result})"; break; }
                h.Kill(); k++;
            }
            if (fail == null && _game.Result != GameResult.Lost) fail = $"Result {_game.Result} with all humans dead, none in pool, expected Lost";
            if (fail == null && _game.Rescued != 0) fail = $"Rescued {_game.Rescued}, expected 0";
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- all_resolved_won
        [UnityTest]
        public IEnumerator all_resolved_won()
        {
            const string n = "all_resolved_won";
            yield return Load(true);
            string fail = null;
            var list = _humans.Humans;
            if (list.Count < 3) { Report(n, false, $"need at least 3 humans, have {list.Count}"); yield break; }
            list[0].SetState(HumanState.ChillingInPool);
            list[1].SetState(HumanState.ChillingInPool);
            for (int i = 2; i < list.Count; i++)
            {
                if (_game.Result != GameResult.Playing) { fail = $"ended early before human {i} resolved ({_game.Result})"; break; }
                list[i].Kill();
            }
            if (fail == null && _game.Result != GameResult.Won) fail = $"Result {_game.Result}, expected Won (pool={Count(HumanState.ChillingInPool)}, dead={Count(HumanState.Dead)}, waiting={Count(HumanState.WaitingOnRoof)})";
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- score_matches_config
        [UnityTest]
        public IEnumerator score_matches_config()
        {
            const string n = "score_matches_config";
            yield return Load(true);
            string fail = null;
            var list = _humans.Humans;
            if (list.Count < 3) { Report(n, false, $"need at least 3 humans, have {list.Count}"); yield break; }

            // Won case: 2 rescued, rest dead, one side burning.
            Ignite(_fire.Sides[0]);
            int sidesNotOnFire = _fire.SidesNotOnFire;
            int rescuedAtEnd = -1, deadAtEnd = -1, sidesAtEnd = -1;
            _game.ResultChanged += r => { rescuedAtEnd = Count(HumanState.ChillingInPool); deadAtEnd = Count(HumanState.Dead); sidesAtEnd = _fire.SidesNotOnFire; };
            list[0].SetState(HumanState.ChillingInPool);
            list[1].SetState(HumanState.ChillingInPool);
            for (int i = 2; i < list.Count; i++) list[i].Kill();
            if (_game.Result != GameResult.Won) fail = $"Result {_game.Result}, expected Won";
            else
            {
                int expected = Mathf.Max(0, rescuedAtEnd * _rc.scorePerRescued - deadAtEnd * _rc.penaltyPerDeath + sidesAtEnd * _rc.bonusPerSideNotOnFire);
                Debug.Log($"[TEST-INFO] {n} Won: rescued={_game.Rescued} deaths={_game.Deaths} total={_game.TotalHumans} sidesNotOnFire={sidesAtEnd} (before {sidesNotOnFire}) score={_game.Score} expected={expected}");
                if (sidesAtEnd != 3) fail = $"SidesNotOnFire {sidesAtEnd}, expected 3 after igniting one side";
                else if (_game.Rescued != rescuedAtEnd || _game.Rescued != 2) fail = $"Rescued {_game.Rescued}, expected 2 (counted {rescuedAtEnd})";
                else if (_game.Deaths != deadAtEnd || _game.Deaths != list.Count - 2) fail = $"Deaths {_game.Deaths}, expected {list.Count - 2}";
                else if (_game.TotalHumans != list.Count) fail = $"TotalHumans {_game.TotalHumans}, expected {list.Count}";
                else if (_game.Score != expected) fail = $"Won Score {_game.Score}, expected {expected}";
            }

            // Lost case on a fresh scene: crash with 2 in pool and 1 dead before. No side bonus.
            if (fail == null)
            {
                yield return Load(true);
                list = _humans.Humans;
                Ignite(_fire.Sides[0]);
                list[0].SetState(HumanState.ChillingInPool);
                list[1].SetState(HumanState.ChillingInPool);
                list[2].Kill();
                int r0 = Count(HumanState.ChillingInPool), d0 = Count(HumanState.Dead);
                _heli.Crash();
                int expected = Mathf.Max(0, r0 * _rc.scorePerRescued - d0 * _rc.penaltyPerDeath);
                Debug.Log($"[TEST-INFO] {n} Lost: rescued={_game.Rescued} deaths={_game.Deaths} total={_game.TotalHumans} score={_game.Score} expected={expected}");
                if (_game.Result != GameResult.Lost) fail = $"crash Result {_game.Result}";
                else if (_game.Rescued != r0) fail = $"Lost Rescued {_game.Rescued}, expected snapshot {r0}";
                else if (_game.Deaths != d0) fail = $"Lost Deaths {_game.Deaths}, expected snapshot {d0}";
                else if (_game.TotalHumans != list.Count) fail = $"Lost TotalHumans {_game.TotalHumans}";
                else if (_game.Score != expected) fail = $"Lost Score {_game.Score}, expected {expected} (no side bonus)";
            }
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- heli_aligns_to_camera
        [UnityTest]
        public IEnumerator heli_aligns_to_camera()
        {
            const string n = "heli_aligns_to_camera";
            yield return Load(false);
            _hcfg.autoAlignToCamera = true;
            _heli.SetControlSource(_tm.Scripted);
            _tm.Scripted.Set(default);
            yield return new WaitForSeconds(0.5f);

            Vector3 fwd0 = Vector3.ProjectOnPlane(_heli.transform.forward, Vector3.up).normalized;
            Vector3 face = Quaternion.AngleAxis(90f, Vector3.up) * fwd0;
            _tm.Scripted.Set(new HeliCommand { FaceWorld = face });

            float maxOvershoot = 0f, maxTilt = 0f;
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.CeilToInt(5f / dt);
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                Vector3 f = Vector3.ProjectOnPlane(_heli.transform.forward, Vector3.up);
                // signed angle from start; target is +90, overshoot is anything beyond it
                float a = Vector3.SignedAngle(fwd0, f, Vector3.up);
                maxOvershoot = Mathf.Max(maxOvershoot, a - 90f);
                maxTilt = Mathf.Max(maxTilt, Vector3.Angle(_heli.transform.up, Vector3.up));
            }
            Vector3 ff = Vector3.ProjectOnPlane(_heli.transform.forward, Vector3.up);
            float err = Vector3.Angle(ff, face);
            Debug.Log($"[TEST-INFO] {n} finalErr={err:F2} maxOvershoot={maxOvershoot:F2} maxTilt={maxTilt:F2}");
            string fail = null;
            if (_heli.IsCrashed) fail = "heli crashed";
            else if (err > 5f) fail = $"final heading error {err:F1} deg > 5";
            else if (maxTilt > 2f) fail = $"heli up tilted {maxTilt:F2} deg > 2";
            else if (maxOvershoot > 15f) fail = $"overshoot {maxOvershoot:F1} deg > 15";
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- ui_result_panel_shows
        [UnityTest]
        public IEnumerator ui_result_panel_shows()
        {
            const string n = "ui_result_panel_shows";
            yield return Load(true);
            var panel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "ResultPanel not found");
            var root = (GameObject)typeof(ResultPanel).GetField("panelRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            var score = (TMPro.TMP_Text)typeof(ResultPanel).GetField("scoreText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            string fail = null;
            if (root == null || score == null) fail = "panelRoot or scoreText not assigned";
            else if (root.activeSelf) fail = "panel visible before the game ended";
            if (fail == null)
            {
                _humans.Humans[0].SetState(HumanState.ChillingInPool);
                for (int i = 1; i < _humans.Humans.Count; i++) _humans.Humans[i].Kill();
                yield return null;
                if (_game.Result == GameResult.Playing) fail = "game did not end";
                else if (!root.activeInHierarchy) fail = "panelRoot not active after the game ended";
                else if (!score.text.Contains(_game.Score.ToString())) fail = $"score text '{score.text}' does not contain Score {_game.Score}";
            }
            Report(n, fail == null, fail);
        }

        // ---------------------------------------------------------------- ui_water_gauge_percent
        [UnityTest]
        public IEnumerator ui_water_gauge_percent()
        {
            const string n = "ui_water_gauge_percent";
            yield return Load(true);
            var gauge = UnityEngine.Object.FindFirstObjectByType<WaterGauge>(FindObjectsInactive.Include);
            Assert.IsNotNull(gauge, "WaterGauge not found");
            var label = (TMPro.TMP_Text)typeof(WaterGauge).GetField("label", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gauge);
            string fail = null;
            if (label == null) fail = "label not assigned";
            else
            {
                int guard = 0;
                while (_cup.Water01 < 0.5f && guard++ < 100000) _cup.FillTick(0.02f);
                yield return null;
                string t = label.text.Trim().TrimEnd('%');
                if (!int.TryParse(t, out int pct)) fail = $"label '{label.text}' not a percentage";
                else
                {
                    int expected = Mathf.RoundToInt(_cup.Water01 * 100f);
                    Debug.Log($"[TEST-INFO] {n} Water01={_cup.Water01:F3} label='{label.text}'");
                    if (Mathf.Abs(pct - 50) > 1) fail = $"label shows {pct}%, expected 50 (+-1); Water01={_cup.Water01:F3}";
                    else if (Mathf.Abs(pct - expected) > 1) fail = $"label {pct}% differs from Water01*100 = {expected}";
                }
            }
            Report(n, fail == null, fail);
        }
    }
}
#endif
