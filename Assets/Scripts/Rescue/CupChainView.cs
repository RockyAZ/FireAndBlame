using System.Collections.Generic;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>
	/// Visual only: draws the cup's hanging joint chain with primitives (a capsule per link, a small cube per joint pivot).
	/// Reads Cup.Joints every frame and places the primitives in world space. No colliders, no physics.
	/// The primitives live under their own scene-root object so a heli crash never turns them into debris.
	/// </summary>
	public class CupChainView : MonoBehaviour
	{
		[SerializeField] private Cup cup;
		[Tooltip("Material for the links and joints (optional; built-in default if empty).")]
		[SerializeField] private Material material;
		[SerializeField, Min(0.005f)] private float linkRadius = 0.06f;
		[Tooltip("Edge length of the cube drawn at each joint pivot. 0 hides the joint cubes.")]
		[SerializeField, Min(0f)] private float jointSize = 0.16f;
		[SerializeField] private bool castShadows = true;
		[Tooltip("Draw the last link (from the last joint into the bowl). Off: the chain visual ends at the last joint cube, " +
			"and the bowl's ChainAttachPoint child marks that point for hand-made static attachments.")]
		[SerializeField] private bool drawBowlLink = false;

		private readonly List<Transform> _links = new List<Transform>();
		private readonly List<Transform> _pivots = new List<Transform>();
		private GameObject _root;
		private Renderer _bowlRenderer;
		private int _linkTotal;
		private bool _built;

		/// <summary>Links drawn: one per joint, minus the bowl link unless drawBowlLink.</summary>
		public int LinkCount => _links.Count;
		public int PivotCount => _pivots.Count;
		public IReadOnlyList<Transform> Links => _links;
		public IReadOnlyList<Transform> Pivots => _pivots;

		private void Awake()
		{
			if (cup == null) cup = GetComponent<Cup>();
		}

		private void LateUpdate()
		{
			if (cup == null) return;
			var joints = cup.Joints;
			if (!_built)
			{
				if (joints == null || joints.Count == 0) return;
				Build(joints);
			}
			for (int i = 0; i < joints.Count && i < _pivots.Count; i++)
			{
				var j = joints[i];
				bool hasLink = i < _links.Count;
				if (j == null || j.connectedBody == null)
				{
					if (hasLink) _links[i].gameObject.SetActive(false);
					_pivots[i].gameObject.SetActive(false);
					continue;
				}
				// Each pivot sits on the hanging body's side of its joint (its own anchor), so the last pivot always
				// coincides with the bowl's ChainAttachPoint even when the physics joint stretches a little.
				Vector3 top = PivotPoint(j);
				bool last = i == joints.Count - 1;
				Vector3 bottom;
				if (!last && joints[i + 1] != null) bottom = PivotPoint(joints[i + 1]);
				else
				{
					bottom = j.transform.position;
					// The bowl link (drawBowlLink) ends at the bowl's surface, not at its centre.
					if (_bowlRenderer != null && !_bowlRenderer.bounds.Contains(top)) bottom = _bowlRenderer.bounds.ClosestPoint(top);
				}
				if (hasLink) PlaceLink(_links[i], top, bottom);
				PlacePivot(_pivots[i], top, j.transform.rotation);
			}
		}

		private static Vector3 PivotPoint(ConfigurableJoint j) => j.transform.TransformPoint(j.anchor);

		private void Build(IReadOnlyList<ConfigurableJoint> joints)
		{
			_root = new GameObject("CupChainView");
			var bowl = joints[joints.Count - 1];
			if (bowl != null) _bowlRenderer = bowl.GetComponentInChildren<MeshRenderer>();
			_linkTotal = drawBowlLink ? joints.Count : joints.Count - 1;
			for (int i = 0; i < joints.Count; i++)
			{
				if (i < _linkTotal) _links.Add(MakePrimitive(PrimitiveType.Capsule, "Link_" + i));
				_pivots.Add(MakePrimitive(PrimitiveType.Cube, "Joint_" + i));
				_pivots[i].localScale = Vector3.one * jointSize;
				_pivots[i].gameObject.SetActive(jointSize > 0f);
			}
			_built = true;
		}

		private Transform MakePrimitive(PrimitiveType type, string name)
		{
			var go = GameObject.CreatePrimitive(type);
			go.name = name;
			// Removed at once so the visual never takes part in physics, not even for one frame.
			var col = go.GetComponent<Collider>();
			if (col != null) DestroyImmediate(col);
			go.transform.SetParent(_root.transform, false);
			var r = go.GetComponent<MeshRenderer>();
			if (material != null) r.sharedMaterial = material;
			r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
			return go.transform;
		}

		private void PlaceLink(Transform link, Vector3 a, Vector3 b)
		{
			Vector3 d = b - a;
			float len = d.magnitude;
			bool show = len > 1e-4f;
			if (link.gameObject.activeSelf != show) link.gameObject.SetActive(show);
			if (!show) return;
			link.position = (a + b) * 0.5f;
			link.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
			// Unity capsule: height 2, radius 0.5.
			float diameter = linkRadius * 2f;
			link.localScale = new Vector3(diameter, len * 0.5f, diameter);
		}

		private void PlacePivot(Transform pivot, Vector3 at, Quaternion rot)
		{
			if (jointSize <= 0f) return;
			if (!pivot.gameObject.activeSelf) pivot.gameObject.SetActive(true);
			pivot.SetPositionAndRotation(at, rot);
		}

		private void OnDestroy()
		{
			if (_root != null) Destroy(_root);
		}
	}
}
