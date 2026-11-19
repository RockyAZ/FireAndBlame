using FlameAndBlame.Core;
using FlameAndBlame.VFX;
using UnityEngine;

namespace FlameAndBlame.Fire
{
	/// <summary>
	/// A hand-made roof box (non-trigger BoxCollider, layer Building, tag Roof) that belongs to one FireSide.
	/// The RoofFire child and the HumanSpawnArea child are placed by hand (in the RoofSlab prefab); this script never moves them.
	/// The RoofFire child carries a trigger BoxCollider + RoofSlabTrigger: while the roof burns, any IKillable touching it dies.
	/// </summary>
	[RequireComponent(typeof(BoxCollider))]
	public class RoofSlab : MonoBehaviour
	{
		[SerializeField] private FireSide fireSide;
		[Tooltip("FillEffectView on the RoofFire child (box with a trigger collider and RoofSlabTrigger).")]
		[SerializeField] private FillEffectView roofFireView;
		[Tooltip("Unit-box transform on the top face where humans spawn (placed by hand).")]
		[SerializeField] private Transform humanSpawnArea;

		[Tooltip("FireEffect for the burning roof (optional). Plays while OnFire.")]
		[SerializeField] private FireEffect roofFireEffect;
		[Tooltip("SmokeEffect for the burning roof (optional). Plays while OnFire.")]
		[SerializeField] private SmokeEffect roofSmokeEffect;

		private BoxCollider _box;
		private bool _effectsOn;

		public FireSide FireSide => fireSide;
		public FillEffectView RoofFireView => roofFireView;
		public bool OnFire => fireSide != null && fireSide.RoofOnFire;

		/// <summary>World bounds of the spawn box (AABB of the rotated spawn box).</summary>
		public Bounds SpawnBounds
		{
			get
			{
				if (humanSpawnArea == null) return _slabBox() is BoxCollider slabBox ? slabBox.bounds : new Bounds(transform.position, Vector3.zero);
				Matrix4x4 m = humanSpawnArea.localToWorldMatrix;
				var b = new Bounds(m.MultiplyPoint3x4(new Vector3(-0.5f, -0.5f, -0.5f)), Vector3.zero);
				for (int i = 1; i < 8; i++)
					b.Encapsulate(m.MultiplyPoint3x4(new Vector3(
						(i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f)));
				return b;
			}
		}

		private BoxCollider _slabBox() => _box != null ? _box : (_box = GetComponent<BoxCollider>());

		/// <summary>Only sets the roof fire smoothing; positions and sizes stay as placed in the prefab.</summary>
		public void Initialize(FireConfig fire, BuildingConfig building)
		{
			if (roofFireView != null) roofFireView.SmoothTime = fire.roofIgniteTime;
		}

		/// <summary>Random point on the bottom face of the spawn box, in world space.</summary>
		public Vector3 GetSpawnPoint()
		{
			if (humanSpawnArea == null) return SpawnBounds.center;
			return humanSpawnArea.TransformPoint(new Vector3(Random.value - 0.5f, -0.5f, Random.value - 0.5f));
		}

		/// <summary>Called by RoofSlabTrigger on the RoofFire collider. While OnFire, an IKillable touching the fire dies.</summary>
		public void ReportStay(Collider other)
		{
			if (!OnFire || other == null) return;
			var k = other.GetComponentInParent<IKillable>();
			if (k != null) k.Kill();
		}

		private void Update()
		{
			if (roofFireView != null) roofFireView.SetFill(OnFire ? 1f : 0f);
			if (OnFire == _effectsOn) return;   // edge-triggered
			_effectsOn = OnFire;
			if (roofFireEffect != null) { if (_effectsOn) roofFireEffect.Play(); else roofFireEffect.Stop(); }
			if (roofSmokeEffect != null) { if (_effectsOn) roofSmokeEffect.Play(); else roofSmokeEffect.Stop(); }
		}

		private void OnDrawGizmos()
		{
			if (humanSpawnArea == null) return;
			Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
			Gizmos.matrix = humanSpawnArea.localToWorldMatrix;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
		}
	}
}
