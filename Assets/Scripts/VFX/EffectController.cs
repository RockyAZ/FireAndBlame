using UnityEngine;

namespace FlameAndBlame.VFX
{
	/// <summary>Base for particle effect controllers. Public API: Play() and Stop() only.</summary>
	public class EffectController : MonoBehaviour
	{
		[SerializeField] private ParticleSystem[] systems;
		[Tooltip("Destroy this object after the effect finished (use when instantiated per hit).")]
		[SerializeField] private bool destroyWhenDone;
		[SerializeField] private float destroyDelay = 2f;

		protected ParticleSystem[] Systems
		{
			get
			{
				if (systems == null || systems.Length == 0) systems = GetComponentsInChildren<ParticleSystem>(true);
				return systems;
			}
		}

		protected virtual bool RestartOnPlay => false;

		public void Play()
		{
			var s = Systems;
			if (s == null) return;
			for (int i = 0; i < s.Length; i++)
			{
				var ps = s[i];
				if (ps == null) continue;
				if (RestartOnPlay) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(true); }
				else if (!ps.isEmitting) ps.Play(true);
			}
			if (destroyWhenDone) Destroy(gameObject, destroyDelay);
		}

		public void Stop()
		{
			var s = Systems;
			if (s == null) return;
			for (int i = 0; i < s.Length; i++)
				if (s[i] != null) s[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}
	}
}
