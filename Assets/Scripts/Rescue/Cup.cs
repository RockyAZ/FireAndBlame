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

		private readonly List<Rigidbody> _segments = new List<Rigidbody>();
		private readonly List<ConfigurableJoint> _joints = new List<ConfigurableJoint>();
		private readonly List<bool> _savedKinematic = new List<bool>();
		private GameObject _chainRoot;
		private Vector3 _startLocal;
		private bool _ready, _testMode;
		private int _samples;
		private Vector3 _lastPos, _lastVelocity;

		public Rigidbody Body => bowlBody;
		public Transform HumanAnchor => humanAnchor;
		public float Water01 { get; private set; }
		public bool IsEmpty => _config != null && Water01 <= _config.emptyThreshold;
		public bool IsFull => _config != null && Water01 >= _config.fullThreshold;
		public int AttachedCount { get; private set; }
		public Vector3 Acceleration { get; private set; }
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
				if (isBowl) rb.interpolation = RigidbodyInterpolation.Interpolate;
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

		public GrabResult TryGrab(Human h) => GrabResult.Rejected; // TODO Phase 3

		/// <summary>Launches a WaterDrop with the current water and empties the cup, then releases humans.</summary>
		public void Tug()
		{
			if (_config != null && !IsEmpty)
			{
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
		public void ReleaseAll() { }

		public void EnterTestMode()
		{
			if (_testMode) return;
			_testMode = true;
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
			_samples = 0;
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
		private void UpdateAcceleration(float dt)
		{
			Vector3 pos = bowlBody.position;
			if (_samples > 0)
			{
				Vector3 vel = (pos - _lastPos) / dt;
				if (_samples > 1)
				{
					Vector3 raw = (vel - _lastVelocity) / dt;
					float alpha = dt / (_config.accelerationSmoothing + dt);
					Acceleration = Vector3.Lerp(Acceleration, raw, alpha);
				}
				_lastVelocity = vel;
			}
			if (_samples < 2) _samples++;
			_lastPos = pos;
		}

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
