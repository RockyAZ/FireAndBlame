using System;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using FlameAndBlame.VFX;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>
	/// One human. SetState (and Detach for Falling) is the only place that changes state and the Rigidbody mode (ApplyMode).
	/// Strength is ticked here in FixedUpdate from Cup.Acceleration (the Cup does not tick humans).
	/// </summary>
	public class Human : MonoBehaviour, ITestModeAware, IKillable
	{
		[Tooltip("Point that hangs from the tank (hands above the head). Falls back to the root.")]
		[SerializeField] private Transform tankAttachPoint;
		[Tooltip("Point that sits on the water line (chest). Falls back to the root.")]
		[SerializeField] private Transform floatPoint;

		[Tooltip("StunStarsEffect child of the HumanPrefab (optional).")]
		[SerializeField] private StunStarsEffect stunStars;

		private const int HitBuffer = 8;

		private Rigidbody _body;
		private HumanConfig _config;
		private HumanAnimator _anim;
		private Collider[] _colliders;
		private Collider[] _ignored;
		private Transform _home;
		private Cup _cup;
		private Transform _slot;
		private ConfigurableJoint _joint;
		private PoolZone _pool;
		private readonly Collider[] _hits = new Collider[HitBuffer];
		private Vector3 _target;
		private float _retarget, _bobPhase, _floatOffsetY, _strengthGrace;
		private bool _hasTarget, _started, _testMode;

		public HumanState State { get; private set; }
		public float Strength01 { get; private set; } = 1f;
		public bool IsStunned { get; private set; }
		public bool IsFalling { get; private set; }
		public bool IsAttached => _cup != null;
		public Cup AttachedCup => _cup;
		public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());
		public Transform TankAttachPoint => tankAttachPoint != null ? tankAttachPoint : transform;
		public Transform FloatPoint => floatPoint != null ? floatPoint : transform;
		public event Action<Human, HumanState> StateChanged;
		public event Action<float> StrengthChanged;

		public void Initialize(HumanConfig config)
		{
			_config = config;
			Strength01 = 1f;
			IsFalling = false;
			_home = transform.parent;
			_colliders = GetComponentsInChildren<Collider>();
			_anim = GetComponent<HumanAnimator>();
			if (_anim != null) _anim.Initialize(config);
			if (tankAttachPoint == null || floatPoint == null)
				Debug.LogWarning("Human: tankAttachPoint or floatPoint not assigned, using the root transform.", this);
			_floatOffsetY = transform.InverseTransformPoint(FloatPoint.position).y * transform.lossyScale.y;
			_bobPhase = UnityEngine.Random.value * Mathf.PI * 2f;
			Body.mass = config.mass;
		}

		/// <summary>The only place state changes. Also picks the Rigidbody mode and fires the animator trigger.</summary>
		public void SetState(HumanState s)
		{
			if (State == HumanState.Dead) return;
			bool same = _started && s == State;
			if (same && !IsFalling) return;
			_started = true;
			if (_cup != null && s != HumanState.RescuedHanging && s != HumanState.SwimmingInTank) Unlink();
			State = s;
			IsFalling = false;
			if (s == HumanState.Dead) { IsStunned = false; CancelInvoke(nameof(EndStun)); }
			if (!same && stunStars != null) stunStars.Stop();   // any real state change leaves the stun visuals
			ApplyMode();
			if (_anim != null) _anim.Apply(s);
			if (!same) StateChanged?.Invoke(this, s);
		}

		/// <summary>From WaitingOnRoof to RescuedHanging (hang slot) or from ChillingInPool to SwimmingInTank (swim slot).</summary>
		public void AttachTo(Cup cup)
		{
			if (cup == null || _cup != null || State == HumanState.Dead) return;
			bool swim = State == HumanState.ChillingInPool;
			if (!swim && State != HumanState.WaitingOnRoof) return;
			Transform slot = cup.ClaimSlot(this, swim);
			if (slot == null) return;
			_cup = cup;
			_slot = slot;
			SetStrength(1f);
			_strengthGrace = _config != null ? _config.strengthGraceTime : 0f;
			SetState(swim ? HumanState.SwimmingInTank : HumanState.RescuedHanging);
		}

		/// <summary>Frees the cup slot and starts falling. The state is unchanged; the landing decides.</summary>
		public void Detach()
		{
			if (_cup == null) return;
			Unlink();
			IsFalling = true;
			ApplyMode();
			if (_anim != null) _anim.PlayFalling();
		}

		public void Kill() { SetState(HumanState.Dead); }

		public void Stun()
		{
			if (State == HumanState.Dead || _config == null) return;
			IsStunned = true;
			if (stunStars != null) stunStars.Play();
			if (_anim != null) _anim.PlayStun();
			CancelInvoke(nameof(EndStun));
			Invoke(nameof(EndStun), _config.stunDuration);
		}

		/// <summary>Only RescuedHanging humans lose or regain strength. At zero the human lets go and falls.</summary>
		public void TickStrength(float dt, float acceleration)
		{
			if (_config == null || State != HumanState.RescuedHanging || _cup == null) return;
			float delta = acceleration > _config.accelerationThreshold
				? -(acceleration - _config.accelerationThreshold) * _config.strengthLossFactor * dt / _config.maxStrength
				: _config.strengthRecoveryPerSecond / _config.maxStrength * dt;
			SetStrength(Strength01 + delta);
			if (Strength01 <= 0f) Detach();
		}

		/// <summary>Landing (and the harness entry point): a falling human touching a RoofSlab stands up on it.</summary>
		public void ReportCollision(Collider other)
		{
			if (!IsFalling || State == HumanState.Dead || other == null) return;
			if (other.GetComponentInParent<RoofSlab>() != null) SetState(HumanState.WaitingOnRoof);
		}

		/// <summary>Called by PoolZone for an unattached human: floats and drifts in that pool.</summary>
		public void EnterPool(PoolZone pool)
		{
			if (State == HumanState.Dead || IsAttached || pool == null) return;
			_pool = pool;
			SetState(HumanState.ChillingInPool);
		}

		public void EnterTestMode() { _testMode = true; ApplyMode(); }
		public void ExitTestMode() { _testMode = false; ApplyMode(); }

		private void OnCollisionEnter(Collision c) => ReportCollision(c.collider);

		private void FixedUpdate()
		{
			if (_config == null) return;
			if (State == HumanState.RescuedHanging && _cup != null)
			{
				if (_strengthGrace > 0f) _strengthGrace -= Time.fixedDeltaTime;   // newly attached: no drain
				else TickStrength(Time.fixedDeltaTime, _cup.ShakeMagnitude);
			}
			else if (State == HumanState.ChillingInPool && !IsFalling && _pool != null) Drift(Time.fixedDeltaTime);
		}

		// ---------- physics mode (called only from SetState, Detach and the test mode toggles) ----------

		private void ApplyMode()
		{
			if (_config == null) return;
			CancelInvoke(nameof(SettleDead));
			bool hang = _cup != null && State == HumanState.RescuedHanging && !_testMode;
			if (!hang) DestroyJoint();
			if (_cup == null && !IsFalling) RestoreIgnored();

			if (_testMode)
			{
				Body.isKinematic = true;
				if (_cup != null) ParentToSlot(); else Unparent();
				return;
			}
			if (State == HumanState.Dead)
			{
				Unparent();
				SetDynamic(false);
				Invoke(nameof(SettleDead), _config.deadSettleTime);
			}
			else if (hang)
			{
				IgnoreCup();
				Unparent();
				SetDynamic(false);
				BuildJoint();
			}
			else if (_cup != null)
			{
				IgnoreCup();
				Body.isKinematic = true;
				ParentToSlot();
			}
			else if (IsFalling) { Unparent(); SetDynamic(false); }
			else if (State == HumanState.WaitingOnRoof) { Unparent(); SetDynamic(true); Upright(); }
			else if (State == HumanState.ChillingInPool) { Unparent(); Body.isKinematic = true; Upright(); }
			else { Unparent(); SetDynamic(false); }
			Body.interpolation = transform.parent != _home ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
		}

		private void SetDynamic(bool freezeRotation)
		{
			Body.isKinematic = false;
			Body.useGravity = true;
			Body.constraints = freezeRotation ? RigidbodyConstraints.FreezeRotation : RigidbodyConstraints.None;
		}

		private void Upright() => transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

		private void Unparent() { if (transform.parent != _home) transform.SetParent(_home, true); }

		// Moves the human so that the attach point sits exactly on the slot.
		private void PlaceOn(Transform point)
		{
			transform.rotation = Quaternion.Euler(0f, _slot.eulerAngles.y, 0f);
			transform.position += _slot.position - point.position;
		}

		private void ParentToSlot()
		{
			PlaceOn(State == HumanState.RescuedHanging ? TankAttachPoint : FloatPoint);
			transform.SetParent(_slot, true);
		}

		private void BuildJoint()
		{
			PlaceOn(TankAttachPoint);
			Body.position = transform.position;
			Body.rotation = transform.rotation;
			_joint = gameObject.AddComponent<ConfigurableJoint>();
			_joint.connectedBody = _cup.Body;
			_joint.autoConfigureConnectedAnchor = false;
			_joint.anchor = transform.InverseTransformPoint(TankAttachPoint.position);
			_joint.connectedAnchor = _cup.Body.transform.InverseTransformPoint(_slot.position);
			// Hands stay fixed on the slot: linear motion locked so the human never stretches away from the cup; only rotation swings.
			_joint.xMotion = _joint.yMotion = _joint.zMotion = ConfigurableJointMotion.Locked;
			_joint.angularXMotion = _joint.angularYMotion = _joint.angularZMotion = ConfigurableJointMotion.Limited;
			var limit = new SoftJointLimit { limit = _config.hangJointAngularLimitDeg };
			_joint.highAngularXLimit = limit;
			_joint.angularYLimit = limit;
			_joint.angularZLimit = limit;
			limit.limit = -_config.hangJointAngularLimitDeg;
			_joint.lowAngularXLimit = limit;
			var spring = new SoftJointLimitSpring { spring = _config.hangJointSpring, damper = _config.hangJointDamper };
			_joint.angularXLimitSpring = spring;
			_joint.angularYZLimitSpring = spring;
		}

		private void DestroyJoint()
		{
			if (_joint == null) return;
			// Joint has no 'enabled' and Destroy is deferred, so free every axis now (a null connectedBody would pin to world space).
			_joint.xMotion = _joint.yMotion = _joint.zMotion = ConfigurableJointMotion.Free;
			_joint.angularXMotion = _joint.angularYMotion = _joint.angularZMotion = ConfigurableJointMotion.Free;
			Destroy(_joint);
			_joint = null;
		}

		// Collisions with the own cup stay ignored until the human has landed (so a falling body is not kicked by the bowl).
		private void IgnoreCup()
		{
			if (_ignored != null) return;
			_ignored = _cup.Body.GetComponentsInChildren<Collider>(true);
			SetIgnored(true);
		}

		private void RestoreIgnored()
		{
			if (_ignored == null) return;
			SetIgnored(false);
			_ignored = null;
		}

		private void SetIgnored(bool ignore)
		{
			foreach (var a in _colliders)
				foreach (var b in _ignored)
					if (a != null && b != null) Physics.IgnoreCollision(a, b, ignore);
		}

		// Removes the link to the cup without changing state or falling flag.
		private void Unlink()
		{
			var cup = _cup;
			_cup = null;
			_slot = null;
			DestroyJoint();
			cup.NotifyDetached(this);
		}

		private void SetStrength(float value)
		{
			value = Mathf.Clamp01(value);
			if (Mathf.Approximately(value, Strength01)) return;
			Strength01 = value;
			StrengthChanged?.Invoke(Strength01);
		}

		private void EndStun()
		{
			IsStunned = false;
			if (stunStars != null) stunStars.Stop();
			if (State == HumanState.Dead || _anim == null) return;
			if (IsFalling) _anim.PlayFalling(); else _anim.Apply(State);
		}

		private void SettleDead()
		{
			if (State == HumanState.Dead) Body.isKinematic = true;
		}

		// ---------- pool floating ----------

		private void Drift(float dt)
		{
			Bounds b = _pool.DriftBounds;
			Vector3 pos = Body.position;
			_retarget -= dt;
			Vector3 toTarget = _target - pos;
			toTarget.y = 0f;
			if (!_hasTarget || toTarget.magnitude < _config.driftArriveDistance || _retarget <= 0f)
			{
				_target = new Vector3(UnityEngine.Random.Range(b.min.x, b.max.x), 0f, UnityEngine.Random.Range(b.min.z, b.max.z));
				_retarget = _config.driftRetargetTime;
				_hasTarget = true;
				toTarget = _target - pos;
				toTarget.y = 0f;
			}
			Vector3 vel = Vector3.ClampMagnitude((toTarget.normalized + Separation(pos)) * _config.driftSpeed, _config.driftSpeed);
			bool inside = pos.x >= b.min.x && pos.x <= b.max.x && pos.z >= b.min.z && pos.z <= b.max.z;
			Vector3 next = pos + vel * dt;
			if (inside)
			{
				next.x = Mathf.Clamp(next.x, b.min.x, b.max.x);
				next.z = Mathf.Clamp(next.z, b.min.z, b.max.z);
			}
			float bob = _config.bobAmplitude * Mathf.Sin(Mathf.PI * 2f * _config.bobFrequency * Time.time + _bobPhase);
			next.y = _pool.SurfaceY + _config.floatHeight + bob - _floatOffsetY;
			Body.MovePosition(next);
			if (vel != Vector3.zero)
				Body.MoveRotation(Quaternion.RotateTowards(Body.rotation, Quaternion.LookRotation(vel), _config.driftTurnRate * dt));
		}

		private Vector3 Separation(Vector3 pos)
		{
			Vector3 push = Vector3.zero;
			int n = Physics.OverlapSphereNonAlloc(pos, _config.driftSeparation, _hits, LayerMask.GetMask("Human"), QueryTriggerInteraction.Ignore);
			for (int i = 0; i < n; i++)
			{
				var other = _hits[i].GetComponentInParent<Human>();
				if (other == null || other == this || other.State != HumanState.ChillingInPool) continue;
				Vector3 away = pos - other.Body.position;
				away.y = 0f;
				float d = away.magnitude;
				if (d > 0f) push += away / d * (1f - d / _config.driftSeparation);
			}
			return push;
		}
	}
}
