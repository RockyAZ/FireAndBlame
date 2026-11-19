#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using FlameAndBlame.Heli;
using FlameAndBlame.Rescue;
using FlameAndBlame.Testing;
using FlameAndBlame.UI;
using FlameAndBlame.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Phase 3 visuals tests. Test mode OFF: real physics, dynamic bodies. Shader values are read via GetPropertyBlock.</summary>
    public class VisualsTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private FireSystem _fire;
        private FireConfig _fc;
        private HeliController _heli;
        private Cup _cup;

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
            _cup = Object.FindFirstObjectByType<Cup>(FindObjectsInactive.Include);
            _fc = AssetDatabase.LoadAssetAtPath<FireConfig>("Assets/Settings/FireConfig.asset");
            Assert.IsNotNull(_fire); Assert.IsNotNull(_heli); Assert.IsNotNull(_cup); Assert.IsNotNull(_fc);
            Assert.IsFalse(_heli.IsTestMode, "heli is in test mode");
            Assert.IsFalse(_cup.Body.isKinematic, "cup body kinematic");
        }

        private static float ShaderFill(Renderer r)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            return mpb.GetFloat("_Fill");
        }

        private static FillEffectView FindView(string objName)
        {
            foreach (var v in Object.FindObjectsByType<FillEffectView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v.gameObject.name == objName) return v;
            return null;
        }

        private static IEnumerator WaitReal(float s)
        {
            float t = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < t) yield return null;
        }

        [UnityTest]
        public IEnumerator wall_overlay_follows_height()
        {
            const string n = "wall_overlay_follows_height";
            yield return Load();
            string fail = null;
            foreach (var s in _fire.Sides)
            {
                var view = s.GetComponentInChildren<FillEffectView>(true);
                if (view == null) { fail = $"{s.Side}: no FillEffectView under side"; break; }
                var rend = view.GetComponent<Renderer>();
                s.Advance(_fc.startDelaySeconds + 0.01f);
                if (!s.IsBurning) { fail = $"{s.Side}: not burning after delay"; break; }
                float wait = view.SmoothTime * 6f + 0.3f;
                float prev = -1f;
                foreach (float target in new[] { 0.15f, 0.4f, 0.65f, 0.9f })
                {
                    int guard = 0;
                    while (s.Height01 < target && guard++ < 100000) s.Advance(0.1f);
                    yield return WaitReal(wait);
                    float h = s.Height01, f = ShaderFill(rend);
                    Debug.Log($"[TEST-INFO] {n} {s.Side} height={h:F3} shaderFill={f:F3} viewCur={view.CurrentFill:F3}");
                    if (Mathf.Abs(f - h) > 0.05f) { fail = $"{s.Side}: _Fill={f:F3} vs Height01={h:F3} (smooth {view.SmoothTime})"; break; }
                    if (f < prev) { fail = $"{s.Side}: _Fill fell {prev:F3} -> {f:F3} while height rose"; break; }
                    prev = f;
                }
                if (fail != null) break;
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator roof_sector_only_own_side()
        {
            const string n = "roof_sector_only_own_side";
            yield return Load();
            FireSide north = null;
            foreach (var s in _fire.Sides) if (s.Side == Side.North) north = s;
            foreach (var s in _fire.Sides) s.Advance(_fc.startDelaySeconds + 0.01f);
            north.Advance(100000f);
            string fail = north.RoofOnFire ? null : "North RoofOnFire false after reaching top";
            yield return WaitReal(_fc.roofIgniteTime * 3f + 0.5f);
            if (_fire.RoofSlabs == null || _fire.RoofSlabs.Count == 0) fail = fail ?? "FireSystem.RoofSlabs empty";
            else foreach (var slab in _fire.RoofSlabs)
            {
                var v = slab.RoofFireView;
                string nm = $"RoofSlab_{(slab.FireSide != null ? slab.FireSide.Side.ToString() : "?")}";
                if (v == null) { fail = fail ?? $"{nm}: roofFireView missing"; continue; }
                float f = ShaderFill(v.GetComponent<Renderer>());
                Debug.Log($"[TEST-INFO] {n} {nm} _Fill={f:F3}");
                if (slab.FireSide == north) { if (f <= 0.9f) fail = fail ?? $"{nm} _Fill={f:F3}, expected >0.9"; }
                else if (f >= 0.05f) fail = fail ?? $"{nm} _Fill={f:F3}, expected <0.05";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator tank_fills_in_pool_scripted()
        {
            const string n = "tank_fills_in_pool_scripted";
            yield return Load();
            var pool = Object.FindFirstObjectByType<PoolZone>(FindObjectsInactive.Include);
            var bowlView = _cup.GetComponentInChildren<FillEffectView>(true);
            if (bowlView == null) bowlView = FindView("Bowl");
            Assert.IsNotNull(bowlView, "Bowl FillEffectView not found");
            var bowlRend = bowlView.GetComponent<Renderer>();

            var src = new GameObject("ScriptedSrc").AddComponent<ScriptedControlSource>();
            _heli.SetControlSource(src);
            src.Set(default);

            // teleport heli and the whole cup chain above the pool, keeping relative offsets
            Vector3 target = new Vector3(pool.transform.position.x, 10f, pool.transform.position.z);
            Vector3 delta = target - _heli.Body.position;
            var bodies = new HashSet<Rigidbody> { _heli.Body, _cup.Body };
            foreach (var rb in _cup.GetComponentsInChildren<Rigidbody>(true)) bodies.Add(rb);
            foreach (var rb in bodies) { rb.position += delta; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
            Debug.Log($"[TEST-INFO] {n} teleported heli to {_heli.Body.position}, bowl at {_cup.Body.position}");
            yield return new WaitForSeconds(1f);

            string fail = null;
            float t = 0f;
            src.Set(new HeliCommand { Vertical = -1f });
            while (!_cup.InPool && t < 20f && !_heli.IsCrashed) { t += Time.deltaTime; yield return null; }
            src.Set(default);
            Debug.Log($"[TEST-INFO] {n} InPool={_cup.InPool} after {t:F1}s heli={_heli.Body.position} bowl={_cup.Body.position} crashed={_heli.IsCrashed}");
            if (_heli.IsCrashed) fail = "heli crashed while descending to the pool";
            else if (!_cup.InPool) fail = $"Cup.InPool never true within 20s (heli {_heli.Body.position}, bowl {_cup.Body.position})";
            if (fail == null)
            {
                float w0 = _cup.Water01;
                float v0 = ShaderFill(bowlRend);
                yield return new WaitForSeconds(1.0f);
                float wMid = _cup.Water01, vMid = ShaderFill(bowlRend);
                yield return new WaitForSeconds(1.0f);
                float w1 = _cup.Water01, v1 = ShaderFill(bowlRend);
                Debug.Log($"[TEST-INFO] {n} water {w0:F3}->{wMid:F3}->{w1:F3} shader {v0:F3}->{vMid:F3}->{v1:F3} inPool={_cup.InPool}");
                if (!(w1 > w0)) fail = $"Water01 did not rise ({w0:F3} -> {w1:F3}); InPool={_cup.InPool}";
                else if (!(v1 > v0)) fail = $"Bowl _Fill did not rise ({v0:F3} -> {v1:F3})";
                else if (Mathf.Abs(v1 - w1) > 0.2f) fail = $"Bowl _Fill {v1:F3} far from Water01 {w1:F3}";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator controls_hint_matches_config()
        {
            const string n = "controls_hint_matches_config";
            yield return Load();
            var cfg = AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Settings/UIConfig.asset");
            var hint = Object.FindFirstObjectByType<ControlsHintView>(FindObjectsInactive.Include);
            string fail = null;
            if (cfg == null) fail = "UIConfig asset missing";
            else if (hint == null) fail = "ControlsHintView missing";
            else
            {
                if (hint.ShownText != cfg.controlsHintText) fail = $"ShownText '{hint.ShownText}' != config '{cfg.controlsHintText}'";
                var tmp = hint.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                RectTransform rt = null; string shown = null;
                if (tmp != null) { rt = tmp.rectTransform; shown = tmp.text; }
                if (fail == null && rt == null) fail = "no text component under ControlsHintView";
                if (fail == null && shown != cfg.controlsHintText) fail = $"text component shows '{shown}' != config";
                if (fail == null && (rt.anchorMin != Vector2.one || rt.anchorMax != Vector2.one)) fail = $"anchors {rt.anchorMin}/{rt.anchorMax}, expected (1,1) top-right";
                if (fail == null && rt.pivot != Vector2.one) fail = $"pivot {rt.pivot}, expected (1,1)";
            }
            Report(n, fail == null, fail);
        }
    }
}
#endif
