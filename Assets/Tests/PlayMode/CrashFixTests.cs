#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using FlameAndBlame.Core;
using FlameAndBlame.Heli;
using FlameAndBlame.Testing;
using FlameAndBlame.VFX;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Crash fixes: debris lands on ground, SecondRotorCollider crashes the heli. Test mode OFF.</summary>
    public class CrashFixTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private HeliController _heli;
        private TestModeController _tm;

        private static void Report(string name, bool ok, string reason)
        {
            Debug.Log(ok ? $"[TEST] {name} PASS" : $"[TEST] {name} FAIL {reason}");
            Assert.IsTrue(ok, reason);
        }

        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            _heli = Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            _tm = Object.FindFirstObjectByType<TestModeController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_heli); Assert.IsNotNull(_tm);
            Assert.IsFalse(_heli.IsTestMode);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator debris_lands_on_ground()
        {
            const string n = "debris_lands_on_ground";
            yield return Load();
            int obstacle = LayerMask.NameToLayer("Obstacle"), groundLayer = LayerMask.NameToLayer("Ground");
            Debug.Log($"[TEST-INFO] {n} IgnoreLayerCollision(Obstacle,Ground)={Physics.GetIgnoreLayerCollision(obstacle, groundLayer)} (expected False)");
            string fail = null;
            if (Physics.GetIgnoreLayerCollision(obstacle, groundLayer)) fail = "Obstacle x Ground collision is ignored";

            var gameObj = GameObject.Find("Ground");
            var gc = gameObj != null ? gameObj.GetComponentInChildren<Collider>() : null;
            if (gc == null) { Report(n, false, "no Ground collider"); yield break; }
            float groundTop = gc.bounds.max.y;
            Bounds gb = gc.bounds;

            // open ground: no other solid collider within 8 m horizontally in a column above the ground
            Vector3? spot = null;
            for (int i = 1; i < 12 && spot == null; i++)
                for (int j = 1; j < 12 && spot == null; j++)
                {
                    var p = new Vector3(Mathf.Lerp(gb.min.x, gb.max.x, i / 12f), groundTop + 1f, Mathf.Lerp(gb.min.z, gb.max.z, j / 12f));
                    bool clear = true;
                    foreach (var c in Physics.OverlapCapsule(p, p + Vector3.up * 12f, 8f, ~0, QueryTriggerInteraction.Ignore))
                        if (c != gc && !c.transform.IsChildOf(_heli.transform)) { clear = false; break; }
                    if (clear && Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var h, 20f, ~0, QueryTriggerInteraction.Ignore) && h.collider == gc)
                        spot = new Vector3(p.x, groundTop + 4f, p.z);
                }
            if (spot == null) { Report(n, false, "no open ground spot found"); yield break; }

            _heli.SetControlSource(_tm.Scripted);
            _tm.Scripted.Set(default);
            _heli.Body.position = spot.Value;
            _heli.Body.linearVelocity = Vector3.zero;
            _heli.transform.position = spot.Value;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            var parts = new List<Renderer>();
            foreach (var r in _heli.GetComponentsInChildren<MeshRenderer>())
                if (r.GetComponentInParent<Rigidbody>() == _heli.Body && r.GetComponent<RotorBlurView>() == null) parts.Add(r);
            _heli.Crash();
            yield return new WaitForSeconds(3f);

            int checkedCount = 0, destroyed = 0;
            foreach (var r in parts)
            {
                if (r == null) { destroyed++; continue; }
                var rb = r.GetComponent<Rigidbody>();
                if (rb == null || r.gameObject.layer != obstacle) { fail = fail ?? $"{r.name} not debris (rb={(rb != null)}, layer={LayerMask.LayerToName(r.gameObject.layer)})"; continue; }
                checkedCount++;
                float minY = r.bounds.min.y;
                Debug.Log($"[TEST-INFO] {n} {r.name} minY={minY:F2} groundTop={groundTop:F2} sleeping={rb.IsSleeping()}");
                if (!(minY > groundTop - 0.3f)) fail = fail ?? $"{r.name} fell below ground: minY {minY:F2} vs groundTop {groundTop:F2}";
            }
            Debug.Log($"[TEST-INFO] {n} parts={parts.Count} checked={checkedCount} destroyedByLifetime={destroyed}");
            if (fail == null && checkedCount == 0) fail = "no debris parts left to check";
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator second_rotor_collider_crashes()
        {
            const string n = "second_rotor_collider_crashes";
            yield return Load();
            var mr = Find(_heli.transform, "MainRotor");
            var sc = mr != null ? Find(mr, "SecondRotorCollider") : null;
            string fail = null;
            if (sc == null || sc == mr) { Report(n, false, "SecondRotorCollider not found under MainRotor"); yield break; }
            var scol = sc.GetComponent<Collider>();
            var mcol = mr.GetComponent<Collider>();
            if (scol == null || scol.isTrigger) { Report(n, false, "SecondRotorCollider has no non-trigger collider"); yield break; }
            if (sc.gameObject.layer != LayerMask.NameToLayer("Rotors")) { Report(n, false, "SecondRotorCollider not on layer Rotors"); yield break; }
            if (mcol == null) { Report(n, false, "MainRotor has no own collider"); yield break; }

            _heli.SetControlSource(_tm.Scripted);
            _tm.Scripted.Set(default);
            int fired = 0; _heli.Crashed += () => fired++;
            yield return new WaitForSeconds(0.3f);

            Bounds sb = scol.bounds, mb = mcol.bounds;
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            Vector3 dir = Vector3.zero; float face = 0f;
            foreach (var d in dirs)
            {
                float se = Vector3.Dot(sb.center, d) + Mathf.Abs(d.x) * sb.extents.x + Mathf.Abs(d.z) * sb.extents.z;
                float me = Vector3.Dot(mb.center, d) + Mathf.Abs(d.x) * mb.extents.x + Mathf.Abs(d.z) * mb.extents.z;
                if (se - me > 0.1f) { dir = d; face = (se + me) * 0.5f; break; }
            }
            bool disabledMain = false;
            if (dir == Vector3.zero)
            {
                mcol.enabled = false; disabledMain = true;
                dir = Vector3.right;
                face = sb.center.x + sb.extents.x + 1.5f;
            }
            Debug.Log($"[TEST-INFO] {n} second={sb} main={mb} dir={dir} face={face:F2} mainColliderDisabledForTest={disabledMain}");

            const float thick = 0.5f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestSecondRotorWall";
            wall.layer = LayerMask.NameToLayer("Obstacle");
            float along = face + thick * 0.5f;   // face value is measured along dir
            Vector3 pos = new Vector3(sb.center.x, sb.center.y, sb.center.z);
            if (dir.x != 0) pos.x = dir.x * along; else pos.z = dir.z * along;
            wall.transform.position = pos;
            float h = Mathf.Max(0.2f, sb.size.y + 0.05f);
            wall.transform.localScale = new Vector3(dir.x != 0 ? thick : 6f, h, dir.x != 0 ? 6f : thick);
            Physics.SyncTransforms();
            bool mainTouch = mcol.enabled && mcol.bounds.Intersects(new Bounds(wall.transform.position, wall.transform.localScale));
            Debug.Log($"[TEST-INFO] {n} wall={wall.transform.position} scale={wall.transform.localScale} mainBoundsTouchWall={mainTouch}");

            _tm.Scripted.Set(new HeliCommand { MoveWorld = dir });
            float t = 0f;
            while (!_heli.IsCrashed && t < 8f) { t += Time.deltaTime; yield return null; }
            _tm.Scripted.Set(default);
            Debug.Log($"[TEST-INFO] {n} crashed={_heli.IsCrashed} after {t:F2}s");
            if (!_heli.IsCrashed) fail = $"SecondRotorCollider hit did not crash within {t:F1}s";
            else if (fired != 1) fail = $"Crashed fired {fired}x";
            if (wall != null) Object.Destroy(wall);
            if (disabledMain) mcol.enabled = true;
            Report(n, fail == null, fail);
        }
    }
}
#endif
