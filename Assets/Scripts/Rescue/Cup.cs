using System;
using System.Collections.Generic;
using FlameAndBlame.Core;
using FlameAndBlame.VFX;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	public class Cup : MonoBehaviour, ITestModeAware
	{
		[Inject] private CupConfig _config;
		[Inject] private IControlSource _control;

		[Tooltip("Heli Rigidbody the chain hangs from.")]
		[SerializeField] private Rigidbody anchorBody;
		[SerializeField] private WaterDrop dropPrefab;
		[Tooltip("Must be a child of the bowl (it is moved with the bowl).")]
		[SerializeField] private Transform humanAnchor;
		[Tooltip("Bowl Rigidbody, the last chain segment. Reparented to a runtime chain root in Initialize.")]
		[SerializeField] private Rigidbody bowlBody;

		[Tooltip("FillEffectView on the bowl renderer (M_Water), optional.")]
		[SerializeField] private FillEffectView waterView;

		[Tooltip("Splash prefab asset spawned at the bowl when water is dumped (optional).")]
		[SerializeField] private SplashEffect tugSplashPrefab;
		private const float SplashFallbackLifetime = 5f;   // cleanup if the prefab does not destroy itself

		[Tooltip("HangSlot_1,HangSlot_2: children of the bowl on the rim. One hanging human each.")]
		[SerializeField] private Transform[] hangSlots;
		[Tooltip("SwimSlot: child of the bowl inside near the top. One swimming human.")]
		[SerializeField] private Transform swimSlot;

		private readonly Dictionary<Human, Transform> _slots = new Dictionary<Human, Transform>();
		private bool _grabLocked;
		private readonly List<Rigidbody> _segments = new List<Rigidbody>();
		private readonly List<ConfigurableJoint> _joints = new List<ConfigurableJoint>();
		private readonly List<bool> _savedKinematic = new List<bool>();
		private GameObject _chainRoot;
		private Vector3 _startLocal;
		private bool _ready, _testMode;
		private int _samples;
		private float _grace;
		private Vector3 _lastPos, _lastVelocity, _smoothVel;

		public Rigidbody Body => bowlBody;
		public Transform HumanAnchor => humanAnchor;
		public float Water01 { get; private set; }
		public bool IsEmpty => _config != null && Water01 <= _config.emptyThreshold;
		public bool IsFull => _config != null && Water01 >= _config.fullThreshold;
		public int AttachedCount => _slots.Count;
		/// <summary>Smoothed, spike-clamped bowl acceleration (see UpdateAcceleration).</summary>
		public Vector3 Acceleration { get; private set; }
		/// <summary>Calm shake value [m/s^2]: smoothed |Acceleration| with slow rise, faster fall. Drives strength loss and the UI meter.</summary>
		public float ShakeMagnitude { get; private set; }
		public bool InPool { get; private set; }
		public bool IsTestMode => _testMode;
		/// <summary>Chain joints built by Initialize (count == CupConfig.segmentCount).</summary>
		public IReadOnlyList<ConfigurableJoint> Joints => _joints;
		public event Action<float> WaterChanged;

		public void Initialize()
		{
			if (anchorBody == null || bowlBody == null)
			{
				Debug.LogError("Cup: anchorBody and bowlBody must be assigned.", this);
				return;
			}
			InitWaterView();
			_chainRoot = new GameObject("CupChain");
			_startLocal = anchorBody.transform.InverseTransformPoint(transform.position);
			Vector3 start = transform.position;
			int n = Mathf.Max(1, _config.segmentCount);
			float len = _config.segmentLength;
			Rigidbody prev = anchorBody;
			Vector3 prevAnchor = _startLocal;
			for (int i = 0; i < n; i++)
			{
				bool isBowl = i == n - 1;
				Rigidbody rb = isBowl ? bowlBody : new GameObject("CupSegment_" + i).AddComponent<Rigidbody>();
				rb.transform.SetParent(_chainRoot.transform);
				// Same rotation as the heli: the joint spring targets the relative rotation at build time, so nothing snaps.
				rb.transform.SetPositionAndRotation(start + Vector3.down * (len * (i + 1)), anchorBody.rotation);
				rb.position = rb.transform.position;
				rb.rotation = rb.transform.rotation;
				rb.mass = isBowl ? _config.bowlMass : _config.segmentMass;
				rb.useGravity = true;
				rb.isKinematic = false;
				// All links interpolate so the chain visual (CupChainView) moves smoothly with the bowl.
				rb.interpolation = RigidbodyInterpolation.Interpolate;
				var joint = AddJoint(rb, prev, prevAnchor, len);
				// The heli is effectively immovable for the first joint, so the chain cannot kick it.
				// connectedMassScale scales the connected body's INVERSE mass, so a heavier heli means a smaller value.
				if (i == 0) joint.connectedMassScale = 1f / Mathf.Max(1f, _config.heliIsolationMassScale);
				_joints.Add(joint);
				_segments.Add(rb);
				prev = rb;
				prevAnchor = Vector3.zero;
			}
			_ready = true;
		}

		private void InitWaterView()
		{
			if (waterView == null) return;
			waterView.SmoothTime = _config.fillVisualSmoothTime;
			waterView.SetFill(Water01);
			WaterChanged += waterView.SetFill;
		}

		public void SetInPool(bool value) { InPool = value; }
		/// <summary>Called by PoolZone each physics step while the bowl is inside.</summary>
		public void FillTick(float dt)
		{
			if (_config == null || Water01 >= 1f) return;
			SetWater(Water01 + _config.fillRatePerSecond / _config.capacity * dt);
		}

		/// <summary>Called by CupGrabTrigger for anything tagged Human in the grab zone or touching the bowl.</summary>
		public void ReportGrabContact(Collider other)
		{
			if (other == null || !other.CompareTag("Human")) return;
			var h = other.GetComponentInParent<Human>();
			if (h != null) TryGrab(h);
		}

		/// <summary>Rules in GAME_DESIGN.md section 1 (Cup). First match wins.</summary>
		public GrabResult TryGrab(Human h)
		{
			if (h == null || _config == null) return GrabResult.Rejected;
			if (h.State == HumanState.Dead || h.IsAttached || h.IsFalling || h.IsStunned
				|| AttachedCount >= _config.maxHumans || _grabLocked) return GrabResult.Rejected;
			bool onRoof = h.State == HumanState.WaitingOnRoof, inPoolHuman = h.State == HumanState.ChillingInPool;
			if (InPool && inPoolHuman && FreeSlot(true) != null) return Attach(h);
			if (!IsEmpty && (onRoof || inPoolHuman))
			{
				Bump(h);
				return GrabResult.BouncedFull;
			}
			if (IsEmpty && onRoof && FreeSlot(false) != null) return Attach(h);
			return GrabResult.Rejected;
		}

		private GrabResult Attach(Human h)
		{
			h.AttachTo(this);
			return h.AttachedCup == this ? GrabResult.Grabbed : GrabResult.Rejected;
		}

		private void Bump(Human h)
		{
			Vector3 away = h.Body.worldCenterOfMass - bowlBody.worldCenterOfMass;
			away.y = 0f;
			Vector3 impulse = (away.normalized + Vector3.up * _config.bumpUpBias) * _config.bumpImpulse;
			// VelocityChange: the bump speed does not depend on the human's mass (60 kg would swallow a plain 4 N s impulse).
			if (!h.Body.isKinematic) h.Body.AddForce(impulse, ForceMode.VelocityChange);
			h.Stun();
		}

		// ---------- slots (one human per slot, hanging and swimming together count against maxHumans) ----------

		private bool IsUsed(Transform slot)
		{
			foreach (var kv in _slots) if (kv.Value == slot) return true;
			return false;
		}

		private Transform FreeSlot(bool swim)
		{
			if (swim)
			{
				if (swimSlot != null && !IsUsed(swimSlot)) return swimSlot;
			}
			else if (hangSlots != null)
				foreach (var s in hangSlots) if (s != null && !IsUsed(s)) return s;
			// Test mode fallback when the slot children are missing.
			return _testMode && humanAnchor != null && !IsUsed(humanAnchor) ? humanAnchor : null;
		}

		internal Transform ClaimSlot(Human h, bool swim)
		{
			if (_config == null || AttachedCount >= _config.maxHumans) return null;
			Transform slot = FreeSlot(swim);
			if (slot != null) { _slots[h] = slot; StartGrace(); }
			return slot;
		}

		internal void NotifyDetached(Human h) { if (_slots.Remove(h)) StartGrace(); }

		private void Unlock() => _grabLocked = false;

		/// <summary>Launches a WaterDrop with the current water and empties the cup, then releases humans.</summary>
		public void Tug()
		{
			if (_config != null && !IsEmpty)
			{
				if (tugSplashPrefab != null)
				{
					var splash = Instantiate(tugSplashPrefab, bowlBody.position, Quaternion.identity);
					splash.Play();
					Destroy(splash.gameObject, SplashFallbackLifetime);
				}
				if (dropPrefab != null)
				{
					var drop = Instantiate(dropPrefab, bowlBody.position, Quaternion.identity);
					drop.transform.localScale = Vector3.one * (2f * _config.dumpDropRadius);
					drop.Launch(Water01, bowlBody.linearVelocity);
					Destroy(drop.gameObject, _config.dropLifetime);
				}
				else Debug.LogWarning("Cup: dropPrefab is not assigned, water is lost.", this);
				SetWater(0f);
			}
			ReleaseAll();
		}
		/// <summary>Every attached human detaches, falls with the bowl's velocity, and the grab lock starts.</summary>
		public void ReleaseAll()
		{
			Vector3 v = bowlBody.isKinematic ? _lastVelocity : bowlBody.linearVelocity;
			var list = new List<Human>(_slots.Keys);
			foreach (var h in list)
			{
				h.Detach();
				if (h.Body.isKinematic) continue;
				h.Body.linearVelocity = v;
				h.Body.angularVelocity = bowlBody.angularVelocity;
			}
			if (list.Count == 0 || _config == null) return;
			_grabLocked = true;
			CancelInvoke(nameof(Unlock));
			Invoke(nameof(Unlock), _config.regrabCooldown);
		}

		public void EnterTestMode()
		{
			if (_testMode) return;
			_testMode = true;
			ResetShake();
			_savedKinematic.Clear();
			foreach (var rb in _segments)
			{
				_savedKinematic.Add(rb.isKinematic);
				rb.isKinematic = true;
			}
		}

		public void ExitTestMode()
		{
			if (!_testMode) return;
			_testMode = false;
			for (int i = 0; i < _segments.Count; i++)
			{
				_segments[i].isKinematic = _savedKinematic[i];
				if (!_savedKinematic[i])
				{
					_segments[i].linearVelocity = Vector3.zero;
					_segments[i].angularVelocity = Vector3.zero;
				}
			}
			ResetShake();
		}

		private void Update()
		{
			if (!_ready || _testMode || _control == null) return;
			if (_control.Read().TugPressed) Tug();
		}

		private void FixedUpdate()
		{
			if (!_ready) return;
			UpdateAcceleration(Time.fixedDeltaTime);
			if (_testMode) FollowAnchor();
		}

		// Test mode: the chain hangs straight below the heli and follows it rigidly.
		private void FollowAnchor()
		{
			for (int i = 0; i < _segments.Count; i++)
			{
				Vector3 local = _startLocal + Vector3.down * (_config.segmentLength * (i + 1));
				_segments[i].MovePosition(anchorBody.position + anchorBody.rotation * local);
				_segments[i].MoveRotation(anchorBody.rotation);
			}
		}

		// Velocity from the position delta (works kinematic and dynamic), then a low-passed derivative.
		// Calm shake measure: velocity (Rigidbody when dynamic, position delta when kinematic) is low-passed, differentiated,
		// each raw sample is clamped, the result is low-passed again and ShakeMagnitude follows it with a slow rise and a faster fall.
		private void UpdateAcceleration(float dt)
		{
			Vector3 pos = bowlBody.position;
			Vector3 delta = (pos - _lastPos) / dt;
			Vector3 vel = bowlBody.isKinematic ? delta : bowlBody.linearVelocity;
			bool teleport = _samples > 0 && delta.magnitude > _config.shakeTeleportSpeed;
			_lastPos = pos;
			_lastVelocity = vel;
			_grace -= dt;
			if (_samples == 0 || teleport || _grace > 0f)
			{
				// No valid measurement: re-baseline, no acceleration, shake decays.
				_samples = 1;
				_smoothVel = vel;
				Acceleration = Vector3.zero;
			}
			else
			{
				Vector3 prev = _smoothVel;
				_smoothVel = Vector3.Lerp(_smoothVel, vel, dt / (_config.shakeVelocitySmoothing + dt));
				Vector3 raw = Vector3.ClampMagnitude((_smoothVel - prev) / dt, _config.shakeMaxRawAcceleration);
				Acceleration = Vector3.Lerp(Acceleration, raw, dt / (_config.accelerationSmoothing + dt));
			}
			float target = Acceleration.magnitude;
			float tau = target > ShakeMagnitude ? _config.shakeRiseTime : _config.shakeFallTime;
			ShakeMagnitude = Mathf.Lerp(ShakeMagnitude, target, dt / (tau + dt));
		}

		/// <summary>Forgets the shake history (teleport, test mode switch, attach or detach start a grace period).</summary>
		public void ResetShake()
		{
			_samples = 0;
			_smoothVel = Vector3.zero;
			Acceleration = Vector3.zero;
			ShakeMagnitude = 0f;
		}

		private void StartGrace() { if (_config != null) _grace = _config.grabGraceTime; }

		private ConfigurableJoint AddJoint(Rigidbody rb, Rigidbody connected, Vector3 connectedAnchor, float len)
		{
			var j = rb.gameObject.AddComponent<ConfigurableJoint>();
			j.connectedBody = connected;
			j.autoConfigureConnectedAnchor = false;
			j.anchor = Vector3.up * len;
			j.connectedAnchor = connectedAnchor;
			j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
			j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
			var limit = new SoftJointLimit { limit = _config.jointAngularLimitDeg };
			j.highAngularXLimit = limit;
			j.angularYLimit = limit;
			j.angularZLimit = limit;
			limit.limit = -_config.jointAngularLimitDeg;
			j.lowAngularXLimit = limit;
			var drive = new JointDrive
			{
				positionSpring = _config.jointSpring,
				positionDamper = _config.jointDamper,
				maximumForce = float.MaxValue
			};
			j.angularXDrive = drive;
			j.angularYZDrive = drive;
			return j;
		}

		private void OnDestroy()
		{
			if (_chainRoot != null) Destroy(_chainRoot);
		}

		private void SetWater(float value)
		{
			value = Mathf.Clamp01(value);
			if (value == Water01) return;
			Water01 = value;
			WaterChanged?.Invoke(Water01);
		}
	}
}
