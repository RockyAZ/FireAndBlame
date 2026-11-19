using System;
using FlameAndBlame.Core;
using FlameAndBlame.VFX;
using UnityEngine;

namespace FlameAndBlame.Fire
{
	/// <summary>
	/// One building side. Local X runs along the wall, local +Z points away from the wall, origin at the wall base center.
	/// Child colliders: burn band (kills, forwards through FireSideTrigger) and water catch (full height).
	/// </summary>
	public class FireSide : MonoBehaviour
	{
		[SerializeField] private Side side;
		[Tooltip("Trigger box, layer FireZone, tag BuildingSide, with FireSideTrigger. Resized to the burning height.")]
		[SerializeField] private BoxCollider burnBand;
		[Tooltip("Trigger box, layer FireZone. Resized to the full building height.")]
		[SerializeField] private BoxCollider waterCatch;

		[Tooltip("FillEffectView on the FireOverlay child (optional).")]
		[SerializeField] private FillEffectView wallFireView;

		[Tooltip("FireEffect scene child instance (optional). Plays while IsBurning, also when the side is lost.")]
		[SerializeField] private FireEffect fireEffect;
		[Tooltip("SmokeEffect scene child instance (optional). Plays while IsBurning, also when the side is lost.")]
		[SerializeField] private SmokeEffect smokeEffect;

		private bool _effectsOn;
		private FireConfig _config;
		private BuildingConfig _building;
		private float _delayLeft;

		public FireConfig Config => _config;
		public BuildingConfig Building => _building;

		public Side Side => side;
		public float Height01 { get; private set; }
		public bool IsBurning { get; private set; }
		public bool RoofOnFire { get; private set; }
		public bool IsLost { get; private set; }
		public float PauseTimeLeft { get; private set; }
		public event Action<FireSide> ReachedTop;

		public void Initialize(FireConfig config)
		{
			_config = config;
			_delayLeft = config.startDelaySeconds;
			Height01 = 0f;
			IsBurning = RoofOnFire = IsLost = false;
			PauseTimeLeft = 0f;
			UpdateBand();
			SyncEffects();
		}

		public void ApplyBuilding(BuildingConfig building)
		{
			_building = building;
			float total = TotalHeight;
			float width = side == Side.North || side == Side.South ? building.footprintX : building.footprintZ;
			if (waterCatch != null)
			{
				waterCatch.isTrigger = true;
				waterCatch.size = new Vector3(width, total, building.burnTriggerDepth);
				waterCatch.center = new Vector3(0f, total * 0.5f, building.burnTriggerDepth * 0.5f);
			}
			UpdateBand();
		}

		private float TotalHeight => _building == null ? 0f : _building.floors * _building.floorHeight;

		private void Update() => Advance(Time.deltaTime);

		/// <summary>Fire clock: start delay, pause countdown and restart, growth, reaching the top.</summary>
		public void Advance(float dt)
		{
			Step(dt);
			UpdateView();
		}

		// Edge-triggered: Play/Stop only when IsBurning changes.
		private void SyncEffects()
		{
			if (IsBurning == _effectsOn) return;
			_effectsOn = IsBurning;
			if (fireEffect != null) { if (_effectsOn) fireEffect.Play(); else fireEffect.Stop(); }
			if (smokeEffect != null) { if (_effectsOn) smokeEffect.Play(); else smokeEffect.Stop(); }
		}

		private void UpdateView()
		{
			SyncEffects();
			if (wallFireView == null) return;
			wallFireView.SetFill(Height01);
			wallFireView.SetIntensity(IsBurning ? 1f : 0f);
		}

		private void Step(float dt)
		{
			if (_config == null || _building == null) return;
			if (_delayLeft > 0f)
			{
				_delayLeft -= dt;
				if (_delayLeft > 0f) return;
				_delayLeft = 0f;
				IsBurning = true;
			}
			if (!IsBurning)
			{
				if (PauseTimeLeft <= 0f) return;
				PauseTimeLeft -= dt;
				if (PauseTimeLeft > 0f) return;
				PauseTimeLeft = 0f;
				IsBurning = true;
				Height01 = 0f;
			}
			if (Height01 < 1f)
			{
				float secondsPerFloor = IsLost ? _config.lostSpreadSecondsPerFloor : _config.spreadSecondsPerFloor;
				Height01 = Mathf.Min(1f, Height01 + dt / (secondsPerFloor * _building.floors));
				if (Height01 >= 1f)
				{
					RoofOnFire = true;
					ReachedTop?.Invoke(this);
				}
			}
			UpdateBand();
		}

		/// <summary>Ignored if lost or below extinguishMinWater01. Otherwise the fire goes out for maxStopFireTime.</summary>
		public void Extinguish(float amount01)
		{
			if (_config == null || IsLost || amount01 < _config.extinguishMinWater01) return;
			_delayLeft = 0f;
			Height01 = 0f;
			IsBurning = false;
			RoofOnFire = false;
			PauseTimeLeft = _config.maxStopFireTime;
			UpdateBand();
			SyncEffects();
		}

		/// <summary>Fire resumes from its current height at the lost speed and ignores water forever.</summary>
		public void SetLost()
		{
			IsLost = true;
			_delayLeft = 0f;
			PauseTimeLeft = 0f;
			if (_config != null) IsBurning = true;
			UpdateBand();
			SyncEffects();
		}

		/// <summary>Burn band contact: anything killable dies while the side burns.</summary>
		public void ReportEnter(Collider other) => Burn(other);
		public void ReportStay(Collider other) => Burn(other);

		private void Burn(Collider other)
		{
			if (!IsBurning || Height01 <= 0f) return;
			// TODO Phase 3: humans (attached or not) are the only IKillable that matters here.
			var killable = other.GetComponentInParent<IKillable>();
			if (killable != null) killable.Kill();
		}

		private void UpdateBand()
		{
			if (burnBand == null || _building == null) return;
			float h = Height01 * TotalHeight;
			float width = waterCatch != null ? waterCatch.size.x : burnBand.size.x;
			burnBand.isTrigger = true;
			burnBand.size = new Vector3(width, h, _building.burnTriggerDepth);
			burnBand.center = new Vector3(0f, h * 0.5f, _building.burnTriggerDepth * 0.5f);
			burnBand.enabled = IsBurning && Height01 > 0f;
		}
	}
}
