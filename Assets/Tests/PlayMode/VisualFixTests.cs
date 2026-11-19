#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using FlameAndBlame.Core;
using FlameAndBlame.Heli;
using FlameAndBlame.Rescue;
using FlameAndBlame.Testing;
using FlameAndBlame.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Visual fixes: cup chain view, pool surface material and draw order. Test mode OFF.</summary>
    public class VisualFixTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private HeliController _heli;
        private Cup _cup;
        private TestModeController _tm;
        private CupChainView _view;

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
            _view = Object.FindFirstObjectByType<CupChainView>(FindObjectsInactive.Include);
            Assert.IsNotNull(_heli); Assert.IsNotNull(_cup); Assert.IsNotNull(_tm); Assert.IsNotNull(_view, "CupChainView missing");
            Assert.IsFalse(_heli.IsTestMode);
            _heli.SetControlSource(_tm.Scripted);
            _tm.Scripted.Set(default);
            for (int i = 0; i < 5; i++) yield return null;
        }

        // The joint point on the hanging body's side (CupChainView draws pivots there, ChainAttachPoint sits there for the bowl).
        private static Vector3 Anchor(ConfigurableJoint j) => j.transform.TransformPoint(j.anchor);

        [UnityTest]
        public IEnumerator cup_chain_view_built()
        {
            const string n = "cup_chain_view_built";
            yield return Load();
            string fail = null;
            int jc = _cup.Joints.Count;
            if (jc <= 0) fail = "Cup.Joints empty";
            else if (_view.LinkCount != jc - 1 || _view.PivotCount != jc) fail = $"links {_view.LinkCount} (want {jc - 1}, no bowl link) pivots {_view.PivotCount} joints {jc}";
            else
            {
                var root = _view.Links[0].parent;
                if (root == null || root.name != "CupChainView") fail = $"root {(root != null ? root.name : "null")}";
                else if (root.parent != null) fail = "CupChainView root has a parent";
                else
                {
                    foreach (var t in _view.Links.Concat(_view.Pivots))
                        if (t.GetComponent<Collider>() != null || t.GetComponent<Rigidbody>() != null) { fail = $"{t.name} has Collider/Rigidbody"; break; }
                    if (fail == null)
                        foreach (var t in _view.Links)
                        {
                            var m = t.GetComponent<MeshRenderer>().sharedMaterial;
                            if (m == null || m.name != "M_Cup") { fail = $"{t.name} material {(m != null ? m.name : "null")}"; break; }
                        }
                }
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator cup_chain_view_follows()
        {
            const string n = "cup_chain_view_follows";
            yield return Load();
            _tm.Scripted.Set(new HeliCommand { MoveWorld = Vector3.right });
            yield return new WaitForSeconds(2f);
            yield return new WaitForEndOfFrame();
            string fail = null;
            var joints = _cup.Joints;
            if (_heli.Body.linearVelocity.magnitude < 0.1f) fail = "heli did not move";
            for (int i = 0; fail == null && i < _view.Links.Count; i++)
            {
                var link = _view.Links[i];
                if (!link.gameObject.activeSelf) { fail = $"{link.name} inactive"; break; }
                float len = link.localScale.y * 2f;
                Vector3 a = link.position + link.up * link.localScale.y;
                Vector3 b = link.position - link.up * link.localScale.y;
                float d = Mathf.Min(Vector3.Distance(a, Anchor(joints[i])), Vector3.Distance(b, Anchor(joints[i])));
                if (len <= 1e-3f) fail = $"{link.name} zero length";
                else if (d > 0.05f) fail = $"{link.name} top end {d:F3} m from joint anchor";
            }
            _tm.Scripted.Set(default);
            if (fail == null)
            {
                var root = _view.Links[0].parent;
                _heli.Crash();
                yield return new WaitForSeconds(0.5f);
                if (root == null) fail = "chain view destroyed after crash";
                else foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.GetComponent<Rigidbody>() != null) { fail = $"{t.name} got a Rigidbody after crash"; break; }
            }
            Report(n, fail == null, fail);
        }

        private string CheckAttach()
        {
            var joints = _cup.Joints;
            int last = joints.Count - 1;
            Transform ap = null;
            foreach (var t in _cup.Body.GetComponentsInChildren<Transform>(true))
                if (t.name == "ChainAttachPoint") { ap = t; break; }
            if (ap == null) return "ChainAttachPoint not found under Cup.Body";
            if (_view.Pivots.Count != joints.Count) return $"pivots {_view.Pivots.Count} joints {joints.Count}";
            float dp = Vector3.Distance(ap.position, _view.Pivots[last].position);
            float da = Vector3.Distance(ap.position, Anchor(joints[last]));
            if (dp > 0.05f) return $"attach point {dp:F3} m from last pivot";
            if (da > 0.05f) return $"attach point {da:F3} m from last joint anchor";
            foreach (var l in _view.Links)
                if (l.name.EndsWith("_" + last) || l.name.EndsWith(last.ToString())) return $"link {l.name} ends in last joint index {last}";
            return null;
        }

        [UnityTest]
        public IEnumerator cup_chain_attach_point_matches_last_joint()
        {
            const string n = "cup_chain_attach_point_matches_last_joint";
            yield return Load();
            yield return new WaitForEndOfFrame();
            string fail = CheckAttach();
            if (fail == null)
            {
                _tm.Scripted.Set(new HeliCommand { MoveWorld = Vector3.right });
                yield return new WaitForSeconds(2f);
                yield return new WaitForEndOfFrame();
                fail = CheckAttach();
                _tm.Scripted.Set(default);
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator pool_surface_underwater_material()
        {
            const string n = "pool_surface_underwater_material";
            yield return Load();
            string fail = null;
            var go = GameObject.Find("PoolSurface");
            if (go == null) { Report(n, false, "PoolSurface not found"); yield break; }
            var r = go.GetComponent<Renderer>();
            var m = r != null ? r.sharedMaterial : null;
            Material bowl = null;
            var jl = _cup.Joints;
            var br = jl[jl.Count - 1].GetComponentInChildren<MeshRenderer>();
            if (br != null) bowl = br.sharedMaterial;
            if (m == null || m.name != "M_PoolWater") fail = $"material {(m != null ? m.name : "null")}";
            else if (m.shader == null || m.shader.name != "FlameAndBlame/PoolWater") fail = $"shader {m.shader.name}";
            else if (bowl == null) fail = "bowl material not found";
            else if (!(m.renderQueue < bowl.renderQueue && m.renderQueue < 3000)) fail = $"queue pool {m.renderQueue}, bowl {bowl.renderQueue}";
            else if (r.shadowCastingMode != ShadowCastingMode.Off) fail = $"shadowCastingMode {r.shadowCastingMode}";
            Debug.Log($"[TEST-INFO] {n} poolQueue={(m != null ? m.renderQueue : -1)} bowlQueue={(bowl != null ? bowl.renderQueue : -1)}");

            if (fail == null)
            {
                var gauge = Object.FindFirstObjectByType<WaterGauge>(FindObjectsInactive.Include);
                var ind = Object.FindFirstObjectByType<CupMotionIndicator>(FindObjectsInactive.Include);
                foreach (var root in new Component[] { gauge, ind })
                {
                    if (root == null) { fail = "WaterGauge or CupMotionIndicator missing"; break; }
                    foreach (var g in root.GetComponentsInChildren<Graphic>(true))
                    {
                        var mr = g.materialForRendering;
                        if (mr != null && mr.renderQueue < 3000) { fail = $"{g.name} ({root.GetType().Name}) queue {mr.renderQueue} < 3000"; break; }
                    }
                    if (fail != null) break;
                }
            }
            if (fail == null)
            {
                var rp = GraphicsSettings.currentRenderPipeline;
                if (rp == null) fail = "no render pipeline asset active";
                else
                {
                    var so = new SerializedObject(rp);
                    var op = so.FindProperty("m_RequireOpaqueTexture");
                    var dp = so.FindProperty("m_RequireDepthTexture");
                    if (op == null || dp == null) fail = "URP texture properties not found on " + rp.name;
                    else if (!op.boolValue) fail = "supportsCameraOpaqueTexture false (" + rp.name + ")";
                    else if (!dp.boolValue) fail = "supportsCameraDepthTexture false (" + rp.name + ")";
                }
            }
            Report(n, fail == null, fail);
        }
    }
}
#endif
