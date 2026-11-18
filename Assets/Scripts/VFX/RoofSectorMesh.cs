using UnityEngine;
using FlameAndBlame.Core;

namespace FlameAndBlame.VFX
{
	/// <summary>
	/// Triangle sector from the roof center to the two roof corners of its Side.
	/// Local origin = roof center, XZ plane. North = +Z, East = +X, South = -Z, West = -X.
	/// Corners have Y 0, center has Y = centerRise, so the Y-based fill spreads from the wall edge inward.
	/// </summary>
	[RequireComponent(typeof(MeshFilter))]
	public class RoofSectorMesh : MonoBehaviour
	{
		[SerializeField] Side side;
		[SerializeField] float sizeX = 10f;
		[SerializeField] float sizeZ = 10f;
		[SerializeField] float centerRise = 0.3f;

		Mesh _mesh;

		public Side Side => side;

		public void Initialize(float x, float z)
		{
			sizeX = x;
			sizeZ = z;
			Build();
		}

		void Awake() => Build();
		void OnValidate() { if (isActiveAndEnabled) Build(); }

		void Build()
		{
			float hx = Mathf.Max(0.01f, sizeX) * 0.5f;
			float hz = Mathf.Max(0.01f, sizeZ) * 0.5f;
			Vector3 a, b;
			switch (side)
			{
				case Side.North: a = new Vector3(-hx, 0, hz); b = new Vector3(hx, 0, hz); break;
				case Side.East: a = new Vector3(hx, 0, hz); b = new Vector3(hx, 0, -hz); break;
				case Side.South: a = new Vector3(hx, 0, -hz); b = new Vector3(-hx, 0, -hz); break;
				default: a = new Vector3(-hx, 0, -hz); b = new Vector3(-hx, 0, hz); break;
			}
			if (_mesh == null)
			{
				_mesh = new Mesh { name = "RoofSector" };
				_mesh.hideFlags = HideFlags.HideAndDontSave;
			}
			_mesh.Clear();
			_mesh.vertices = new[] { new Vector3(0, centerRise, 0), a, b };
			_mesh.triangles = new[] { 0, 2, 1 };
			_mesh.RecalculateBounds();
			_mesh.RecalculateNormals();
			GetComponent<MeshFilter>().sharedMesh = _mesh;
			var view = GetComponent<FillEffectView>();
			if (view != null) view.RefreshBounds();
		}

		void OnDestroy()
		{
			if (_mesh == null) return;
			if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
		}
	}
}
