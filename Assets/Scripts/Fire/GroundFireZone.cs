using FlameAndBlame.Core;
using UnityEngine;

namespace FlameAndBlame.Fire
{
	/// <summary>Trigger on layer FireZone, tag GroundFire. Kills anything killable that touches it.</summary>
	public class GroundFireZone : MonoBehaviour
	{
		private FireConfig _config;
		private Collider _collider;

		public void Initialize(FireConfig config)
		{
			_config = config;
			_collider = GetComponent<Collider>();
		}

		/// <summary>Only when groundFirePermanent is false: the zone is switched off for maxStopFireTime.</summary>
		public void Extinguish(float amount01)
		{
			if (_config == null || _config.groundFirePermanent || amount01 < _config.extinguishMinWater01) return;
			if (_collider != null) _collider.enabled = false;
			CancelInvoke(nameof(Restore));
			Invoke(nameof(Restore), _config.maxStopFireTime);
		}

		public void ReportEnter(Collider other) => Burn(other);
		public void ReportStay(Collider other) => Burn(other);

		private void Burn(Collider other)
		{
			// TODO Phase 3: only humans are IKillable; a hanging human low over the fire dies too.
			var killable = other.GetComponentInParent<IKillable>();
			if (killable != null) killable.Kill();
		}

		private void Restore() { if (_collider != null) _collider.enabled = true; }

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerStay(Collider other) => ReportStay(other);
	}
}
