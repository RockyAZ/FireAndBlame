using System;
using FlameAndBlame.Core;
using FlameAndBlame.VFX;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Heli
{
	public class HeliController : MonoBehaviour, ITestModeAware
	{
		[Inject] private HeliConfig _config;
		[Inject] private IControlSource _source;

		[Tooltip("Optional visual child (Body) that leans toward the acceleration. No physical effect.")]
		[SerializeField] private Transform visual;

		[Header("Rotor blur")]
		[Tooltip("Blur disc on the main rotor (RotorBlurView, optional).")]
		[SerializeField] private RotorBlurView mainRotorBlur;
		[Tooltip("Blur disc on the tail rotor (RotorBlurView, optional).")]
		[SerializeField] private RotorBlurView tailRotorBlur;
		[Tooltip("The real blade renderers: hidden while the blur discs show, shown again on crash.")]
		[SerializeField] private Renderer[] bladeRenderers;

		public RotorBlurView MainRotorBlur => mainRotorBlur;
		public RotorBlurView TailRotorBlur => tailRotorBlur;
		public Renderer[] BladeRenderers => bladeRenderers;

		private Rigidbody _body;
		private bool _ready, _testMode, _savedKinematic;
		private IControlSource _savedSource;
		private Vector3 _move;
		private float _vertical, _yawInput, _yawRate, _noiseSeed;
		// Auto-align heading error, tracked continuously (not re-wrapped each step) so the turn never reverses at 180 deg.
		private float _alignError, _alignPrevSigned;
		private bool _alignTracking;
		private Vector3 _velocity, _acceleration;

		public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());
		public bool IsCrashed { get; private set; }
		public bool IsTestMode => _testMode;
		/// <summary>Velocity actually used by the model (own integration in test mode).</summary>
		public Vector3 Velocity => _testMode ? _velocity : Body.linearVelocity;
		/// <summary>Acceleration applied in the last physics step (clamped by maxAcceleration).</summary>
		public Vector3 LastAcceleration => _acceleration;
		public IControlSource ControlSource => _source;
		public event Action Crashed;

		public void Initialize()
		{
			var b = Body;
			b.useGravity = false;
			b.mass = _config.rigidbodyMass;
			b.linearDamping = _config.linearDrag;
			b.angularDamping = _config.angularDrag;
			b.interpolation = RigidbodyInterpolation.Interpolate;
			b.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
			_noiseSeed = UnityEngine.Random.value * 1000f;
			SetBlur(true);
			_ready = true;
		}

		// Blur discs shown means real blades hidden, and the other way round.
		private void SetBlur(bool blur)
		{
			if (mainRotorBlur != null) mainRotorBlur.SetVisible(blur);
			if (tailRotorBlur != null) tailRotorBlur.SetVisible(blur);
			if (bladeRenderers != null)
				foreach (var r in bladeRenderers) if (r != null) r.enabled = !blur;
		}

		private void UpdateRotors()
		{
			float a = Mathf.Clamp01(Mathf.Max(new Vector2(_move.x, _move.z).magnitude, Mathf.Abs(_vertical)));
			float factor = Mathf.Lerp(_config.rotorIdleFactor, 1f, a);
			factor = Mathf.Lerp(factor, _config.rotorLoadFactor, Mathf.Clamp01(_vertical));
			if (mainRotorBlur != null) mainRotorBlur.SetSpeed(_config.mainRotorTurnsPerSecond * factor);
			if (tailRotorBlur != null) tailRotorBlur.SetSpeed(_config.tailRotorTurnsPerSecond * factor);
		}

		public void SetControlSource(IControlSource s) { _source = s; }

		public void Crash()
		{
			if (IsCrashed) return;
			IsCrashed = true;
			_move = Vector3.zero; _vertical = 0f; _yawInput = 0f; _alignTracking = false;
			foreach (var r in GetComponentsInChildren<RotorHazard>(true)) r.enabled = false;
			SetBlur(false);   // real blades show (and become debris), also in test mode
			if (!Body.isKinematic)
			{
				Body.useGravity = true;
				Body.linearDamping = 0f;
				BreakApart();
			}
			Crashed?.Invoke();
		}

		// Collision callbacks of child colliders are delivered to the Rigidbody object, so forward them here.
		private void OnCollisionEnter(Collision c)
		{
			foreach (var contact in c.contacts)
			{
				// Any collider on or under a rotor (e.g. MainRotor/SecondRotorCollider) counts as that rotor.
				var hazard = contact.thisCollider.GetComponentInParent<RotorHazard>();
				if (hazard != null) { hazard.ReportHit(contact.otherCollider); if (IsCrashed) return; }
			}
		}

		private void BreakApart()
		{
			int layer = LayerMask.NameToLayer("Obstacle");
			Vector3 v = Body.linearVelocity, w = Body.angularVelocity;
			var parts = new System.Collections.Generic.List<Transform>();
			foreach (var r in GetComponentsInChildren<MeshRenderer>())
				// The cup (own Rigidbody) and anything under it stays out of the debris.
					// Blur discs are visual only and never become debris.
					if (r.GetComponentInParent<Rigidbody>() == Body && r.GetComponent<RotorBlurView>() == null) parts.Add(r.transform);
			foreach (var t in parts)
			{
				t.SetParent(null, true);
				foreach (var old in t.GetComponents<Collider>()) Destroy(old);
				var box = t.gameObject.AddComponent<BoxCollider>();
				var mf = t.GetComponent<MeshFilter>();
				if (mf != null && mf.sharedMesh != null) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
				if (layer >= 0) t.gameObject.layer = layer;
				var rb = t.gameObject.AddComponent<Rigidbody>();
				rb.mass = _config.breakPartMass;
				rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
				rb.linearVelocity = v + UnityEngine.Random.onUnitSphere * _config.breakImpulse;
				rb.angularVelocity = w + UnityEngine.Random.onUnitSphere * (_config.breakSpin * Mathf.Deg2Rad);
				if (_config.debrisLifetime > 0f) Destroy(t.gameObject, _config.debrisLifetime);
			}
		}

		public void EnterTestMode()
		{
			if (_testMode) return;
			_savedKinematic = Body.isKinematic;
			_savedSource = _source;
			_velocity = _savedKinematic ? Vector3.zero : Body.linearVelocity;
			_yawRate = _savedKinematic ? 0f : Body.angularVelocity.y;
			Body.isKinematic = true;
			_testMode = true;
		}

		public void ExitTestMode()
		{
			if (!_testMode) return;
			_testMode = false;
			Body.isKinematic = _savedKinematic;
			if (!_savedKinematic)
			{
				Body.linearVelocity = _velocity;
				Body.angularVelocity = new Vector3(0f, _yawRate, 0f);
			}
			_source = _savedSource;
		}

		/// <summary>
		/// Heading error toward the camera, unwrapped. The wrapped signed angle flips from +180 to -180 when the camera
		/// gets half a turn ahead of a lagging heli, which made the heli reverse and rock. Accumulating the frame-to-frame
		/// change keeps the turn going the way the camera went; whole extra circles are dropped (|error| stays within 360).
		/// </summary>
		private float AlignError(float signed)
		{
			if (!_alignTracking) { _alignError = signed; _alignTracking = true; }
			else _alignError += Mathf.DeltaAngle(_alignPrevSigned, signed);
			_alignPrevSigned = signed;
			if (_alignError > 360f) _alignError -= 360f;
			else if (_alignError < -360f) _alignError += 360f;
			return _alignError;
		}

		private void FixedUpdate()
		{
			if (!_ready || IsCrashed) return;
			float dt = Time.fixedDeltaTime;
			HeliCommand cmd = _source != null ? _source.Read() : default;
			Smooth(cmd, dt);

			Vector3 v = Velocity;
			Vector3 target = new Vector3(_move.x * _config.maxHorizontalSpeed,
				_vertical * _config.maxVerticalSpeed, _move.z * _config.maxHorizontalSpeed);
			// Clamped chase: the velocity can never change by more than maxAcceleration * dt per step.
			Vector3 accel = Vector3.ClampMagnitude((target - v) * _config.velocityResponse, _config.maxAcceleration);
			_acceleration = accel;

			float w = _testMode ? _yawRate : Body.angularVelocity.y;
			float noise = Mathf.PerlinNoise(Time.time * _config.driftNoiseFrequency, _noiseSeed) * 2f - 1f;
			float yawAccel;
			Vector3 face = new Vector3(cmd.FaceWorld.x, 0f, cmd.FaceWorld.z);
			if (_config.autoAlignToCamera && face.sqrMagnitude > 1e-4f)
			{
				// Turn rate toward the camera heading, closed with alignSmoothTime (drift stays a disturbance).
				Vector3 fwd = Vector3.ProjectOnPlane(Body.rotation * Vector3.forward, Vector3.up);
				float delta = AlignError(fwd.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(fwd, face, Vector3.up) : 0f);
				float rate = Mathf.Clamp(delta * _config.alignGain, -_config.alignMaxYawRate, _config.alignMaxYawRate) * Mathf.Deg2Rad;
				yawAccel = (rate - w) / Mathf.Max(_config.alignSmoothTime, 0.01f) + noise * _config.yawDriftTorque;
			}
			else
			{
				_alignTracking = false;
				yawAccel = (_yawInput * _config.maxYawRate * Mathf.Deg2Rad - w) * _config.velocityResponse
					+ noise * _config.yawDriftTorque;
			}

			if (_testMode)
			{
				_velocity += accel * dt;
				_yawRate += yawAccel * dt;
				Body.MovePosition(Body.position + _velocity * dt);
				Body.MoveRotation(Quaternion.AngleAxis(_yawRate * Mathf.Rad2Deg * dt, Vector3.up) * Body.rotation);
			}
			else
			{
				// Rigidbody damping is cancelled so the total change stays within the clamp; the chase does the braking.
				Body.AddForce(accel + v * _config.linearDrag, ForceMode.Acceleration);
				Vector3 level = Vector3.Cross(transform.up, Vector3.up) * _config.levelingStrength;
				Body.AddTorque(Vector3.up * (yawAccel + w * _config.angularDrag) + level, ForceMode.Acceleration);
			}
		}

		private void Update()
		{
			if (!_ready || IsCrashed) return;
			UpdateRotors();
			if (visual == null) return;
			Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, Body.rotation.eulerAngles.y, 0f)) * _acceleration;
			float k = _config.tiltVisualMaxDeg / Mathf.Max(_config.maxAcceleration, Mathf.Epsilon);
			Quaternion tilt = Quaternion.Euler(local.z * k, 0f, -local.x * k);
			visual.localRotation = Quaternion.Slerp(visual.localRotation, tilt, 1f - Mathf.Exp(-_config.velocityResponse * Time.deltaTime));
		}

		private void Smooth(HeliCommand cmd, float dt)
		{
			Vector3 m = Vector3.ClampMagnitude(new Vector3(cmd.MoveWorld.x, 0f, cmd.MoveWorld.z), 1f);
			_move.x = Axis(_move.x, m.x, dt);
			_move.z = Axis(_move.z, m.z, dt);
			_vertical = Axis(_vertical, Mathf.Clamp(cmd.Vertical, -1f, 1f), dt);
			_yawInput = Axis(_yawInput, Mathf.Clamp(cmd.Yaw, -1f, 1f), dt);
		}

		private float Axis(float current, float target, float dt)
		{
			float time = Mathf.Abs(target) > Mathf.Abs(current) ? _config.inputRiseTime : _config.inputFallTime;
			return Mathf.MoveTowards(current, target, dt / Mathf.Max(time, Mathf.Epsilon));
		}
	}
}
