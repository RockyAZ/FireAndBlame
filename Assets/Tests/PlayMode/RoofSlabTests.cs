#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using FlameAndBlame.Rescue;
using FlameAndBlame.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Phase 4 roof slab tests. Test mode OFF. FireSide components are disabled so time only moves through Advance(dt).</summary>
    public class RoofSlabTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private FireSystem _fire;
        private FireConfig _fc;
        private BuildingConfig _bc;

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
            _fc = AssetDatabase.LoadAssetAtPath<FireConfig>("Assets/Settings/FireConfig.asset");
            _bc = AssetDatabase.LoadAssetAtPath<BuildingConfig>("Assets/Settings/BuildingConfig.asset");
            Assert.IsNotNull(_fire); Assert.IsNotNull(_fc); Assert.IsNotNull(_bc);
            Assert.IsNotNull(_fire.RoofSlabs); Assert.AreEqual(4, _fire.RoofSlabs.Count, "RoofSlabs count");
            foreach (var s in _fire.Sides) s.enabled = false;   // time moves only through Advance
        }

        private static string Nm(RoofSlab s) => $"{s.name}({s.FireSide.Side})";

        private static float ShaderFill(Renderer r)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            return mpb.GetFloat("_Fill");
        }

        private static IEnumerator WaitReal(float s)
        {
            float t = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < t) yield return null;
        }

        private void Ignite(FireSide s)
        {
            s.Advance(_fc.startDelaySeconds + 0.01f);
            s.Advance(100000f);
        }

        private static Vector3 TopCenter(RoofSlab slab)
        {
            var b = slab.GetComponent<BoxCollider>();
            return slab.transform.TransformPoint(b.center + new Vector3(0f, b.size.y * 0.5f, 0f));
        }

        private static Transform SpawnArea(RoofSlab slab)
            => (Transform)typeof(RoofSlab).GetField("humanSpawnArea", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(slab);

        [UnityTest]
        public IEnumerator roof_children_not_moved_and_fire_collider()
        {
            // RoofFire and HumanSpawnArea are placed by hand in the prefab; Initialize must not move them.
            const string n = "roof_children_not_moved_and_fire_collider";
            yield return Load();
            string fail = null;
            int fireZone = LayerMask.NameToLayer("FireZone");
            foreach (var slab in _fire.RoofSlabs)
            {
                var fire = slab.RoofFireView != null ? slab.RoofFireView.transform : null;
                var area = SpawnArea(slab);
                if (fire == null || area == null) { fail = $"{Nm(slab)}: roofFireView or humanSpawnArea missing"; break; }
                Matrix4x4 f0 = fire.localToWorldMatrix, a0 = area.localToWorldMatrix;
                slab.Initialize(_fc, _bc);
                if (f0 != fire.localToWorldMatrix) fail = fail ?? $"{Nm(slab)}: Initialize moved RoofFire";
                if (a0 != area.localToWorldMatrix) fail = fail ?? $"{Nm(slab)}: Initialize moved HumanSpawnArea";
                var col = fire.GetComponent<BoxCollider>();
                if (col == null || !col.isTrigger) fail = fail ?? $"{Nm(slab)}: RoofFire has no trigger BoxCollider";
                if (fire.gameObject.layer != fireZone) fail = fail ?? $"{Nm(slab)}: RoofFire layer {LayerMask.LayerToName(fire.gameObject.layer)}, expected FireZone";
                if (fire.GetComponent<RoofSlabTrigger>() == null) fail = fail ?? $"{Nm(slab)}: RoofFire has no RoofSlabTrigger";
                if (slab.transform.Find("TopTrigger") != null) fail = fail ?? $"{Nm(slab)}: TopTrigger still exists";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator roof_only_own_slab_ignites()
        {
            const string n = "roof_only_own_slab_ignites";
            yield return Load();
            string fail = null;
            foreach (var target in _fire.RoofSlabs)
            {
                foreach (var s in _fire.Sides) s.Extinguish(1f);
                yield return WaitReal(_fc.roofIgniteTime * 3f + 0.3f);
                Ignite(target.FireSide);
                yield return WaitReal(_fc.roofIgniteTime * 3f + 0.5f);
                foreach (var slab in _fire.RoofSlabs)
                {
                    float f = ShaderFill(slab.RoofFireView.GetComponent<Renderer>());
                    Debug.Log($"[TEST-INFO] {n} ignited={Nm(target)} slab={Nm(slab)} OnFire={slab.OnFire} _Fill={f:F3}");
                    if (slab == target)
                    {
                        if (!slab.OnFire) fail = fail ?? $"{Nm(slab)} OnFire false after reaching top";
                        if (f < 0.9f) fail = fail ?? $"{Nm(slab)} _Fill={f:F3}, expected >0.9";
                    }
                    else
                    {
                        if (slab.OnFire) fail = fail ?? $"{Nm(slab)} OnFire true while only {Nm(target)} burns";
                        if (f > 0.02f) fail = fail ?? $"{Nm(slab)} _Fill={f:F3}, expected ~0 while {Nm(target)} burns";
                    }
                }
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator roof_extinguish_clears_slab()
        {
            const string n = "roof_extinguish_clears_slab";
            yield return Load();
            string fail = null;
            foreach (var slab in _fire.RoofSlabs)
            {
                Ignite(slab.FireSide);
                yield return WaitReal(_fc.roofIgniteTime * 3f + 0.3f);
                float before = ShaderFill(slab.RoofFireView.GetComponent<Renderer>());
                slab.FireSide.Extinguish(1f);
                yield return WaitReal(_fc.roofIgniteTime * 3f + 0.5f);
                float after = ShaderFill(slab.RoofFireView.GetComponent<Renderer>());
                Debug.Log($"[TEST-INFO] {n} {Nm(slab)} fill {before:F3} -> {after:F3} OnFire={slab.OnFire}");
                if (before < 0.9f) fail = fail ?? $"{Nm(slab)} did not light first (_Fill {before:F3})";
                if (slab.OnFire) fail = fail ?? $"{Nm(slab)} OnFire still true after Extinguish(1)";
                if (after > 0.05f) fail = fail ?? $"{Nm(slab)} _Fill={after:F3} after Extinguish, expected ~0";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator ball_on_slab_extinguishes_side()
        {
            const string n = "ball_on_slab_extinguishes_side";
            yield return Load();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WaterDrop.prefab");
            Assert.IsNotNull(prefab, "WaterDrop prefab");
            Assert.IsNotNull(prefab.GetComponent<WaterDrop>());
            foreach (var s in _fire.Sides) Ignite(s);
            string fail = null;
            foreach (var slab in _fire.RoofSlabs)
            {
                if (!slab.FireSide.RoofOnFire) { Ignite(slab.FireSide); }
                Vector3 from = TopCenter(slab) + Vector3.up * 6f;
                var go = Object.Instantiate(prefab, from, Quaternion.identity);
                var drop = go.GetComponent<WaterDrop>();
                drop.Launch(1f, Vector3.zero);
                float t = 0f;
                while (go != null && t < 6f) { t += Time.deltaTime; yield return null; }
                bool destroyed = go == null;
                if (go != null) Object.Destroy(go);
                yield return null;
                Debug.Log($"[TEST-INFO] {n} {Nm(slab)} destroyed={destroyed} after {t:F2}s RoofOnFire={slab.FireSide.RoofOnFire} Height01={slab.FireSide.Height01:F2}");
                if (slab.FireSide.RoofOnFire) fail = fail ?? $"{Nm(slab)}: RoofOnFire still true after ball (ball destroyed={destroyed}, {t:F2}s)";
                if (slab.FireSide.Height01 > 0.001f) fail = fail ?? $"{Nm(slab)}: Height01={slab.FireSide.Height01:F2}, expected 0";
                foreach (var other in _fire.RoofSlabs)
                {
                    if (other == slab) continue;
                    // untouched sides must still be burning, unless they were the target of an earlier drop
                    if (!_done.Contains(other.FireSide) && !other.FireSide.RoofOnFire)
                        fail = fail ?? $"{Nm(other)} lost its fire when ball hit {Nm(slab)}";
                }
                _done.Add(slab.FireSide);
            }
            Report(n, fail == null, fail);
        }

        private readonly System.Collections.Generic.HashSet<FireSide> _done = new System.Collections.Generic.HashSet<FireSide>();

        [UnityTest]
        public IEnumerator spawn_points_in_area()
        {
            // The spawn area is placed by hand; only check GetSpawnPoint stays on the bottom face of the area.
            const string n = "spawn_points_in_area";
            yield return Load();
            string fail = null;
            foreach (var slab in _fire.RoofSlabs)
            {
                var area = SpawnArea(slab);
                if (area == null) { fail = $"{Nm(slab)}: humanSpawnArea not assigned"; break; }
                Bounds sb = slab.SpawnBounds; sb.Expand(0.002f);
                int bad = 0;
                for (int i = 0; i < 200; i++)
                {
                    Vector3 w = slab.GetSpawnPoint();
                    Vector3 l = area.InverseTransformPoint(w);
                    if (!sb.Contains(w) || Mathf.Abs(l.x) > 0.5001f || Mathf.Abs(l.z) > 0.5001f || Mathf.Abs(l.y + 0.5f) > 0.001f) bad++;
                }
                if (bad > 0) fail = fail ?? $"{Nm(slab)}: {bad}/200 spawn points outside the spawn area";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator burning_slab_kills_killable()
        {
            // Death only by touching the RoofFire collider while the roof burns; nothing above the fire box.
            const string n = "burning_slab_kills_killable";
            yield return Load();
            string fail = null;
            foreach (var slab in _fire.RoofSlabs)
            {
                Bounds fb = slab.RoofFireView.GetComponent<BoxCollider>().bounds;
                var inside = MakeKillable(fb.center);
                var above = MakeKillable(new Vector3(fb.center.x, fb.max.y + 1.5f, fb.center.z));
                Physics.SyncTransforms();
                yield return new WaitForSeconds(0.3f);
                int calm = inside.Kills;
                Ignite(slab.FireSide);
                yield return new WaitForSeconds(0.3f + _fc.roofIgniteTime);   // Fire01 must grow past roofFireKillThreshold
                Debug.Log($"[TEST-INFO] {n} {Nm(slab)} kills notBurning={calm} burningInside={inside.Kills} burningAbove={above.Kills} OnFire={slab.OnFire}");
                if (calm != 0) fail = fail ?? $"{Nm(slab)}: killed {calm}x while not on fire";
                if (inside.Kills == 0) fail = fail ?? $"{Nm(slab)}: not killed inside the fire collider while OnFire";
                if (above.Kills != 0) fail = fail ?? $"{Nm(slab)}: killed {above.Kills}x 1.5 m above the fire box";
                Object.Destroy(inside.gameObject); Object.Destroy(above.gameObject);
            }
            Report(n, fail == null, fail);
        }

        private static TestKillable MakeKillable(Vector3 pos)
        {
            var go = new GameObject("TestKillable");
            go.layer = LayerMask.NameToLayer("Human");
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider>(); col.size = Vector3.one * 0.5f;
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            return go.AddComponent<TestKillable>();
        }

        [UnityTest]
        public IEnumerator hint_shows_z_x()
        {
            const string n = "hint_shows_z_x";
            yield return Load();
            var hint = Object.FindFirstObjectByType<FlameAndBlame.UI.ControlsHintView>(FindObjectsInactive.Include);
            string fail = null;
            if (hint == null) fail = "ControlsHintView missing";
            else
            {
                string t = hint.ShownText ?? "";
                Debug.Log($"[TEST-INFO] {n} ShownText='{t}'");
                // Phase 6: Z/X turning replaced by auto-align, so the hint says "Heli faces camera" instead of Z/X.
                if (!t.Contains("faces camera")) fail = $"ShownText lacks 'faces camera': '{t}'";
                else if (t.Contains("Q/E")) fail = $"ShownText still mentions 'Q/E': '{t}'";
                else if (System.Text.RegularExpressions.Regex.IsMatch(t, @"(^|[^A-Za-z])[QE]([^A-Za-z]|$)")) fail = $"ShownText mentions Q or E key: '{t}'";
            }
            Report(n, fail == null, fail);
        }
    }
}
#endif
