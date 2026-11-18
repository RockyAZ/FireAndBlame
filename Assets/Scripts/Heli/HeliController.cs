using System;
using FlameAndBlame.Core;
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

		private Rigidbody _body;
		private bool _ready, _testMode, _savedKinematic;
		private IControlSource _savedSource;
		private Vector3 _move;
		private float _vertical, _yawInput, _yawRate, _noiseSeed;
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
			_noiseSeed = UnityEngine.Random.value * 1000f;
			_ready = true;
		}

		public void SetControlSource(IControlSource s) { _source = s; }

		public void Crash()
		{
			if (IsCrashed) return;
			IsCrashed = true;
			_move = Vector3.zero; _vertical = 0f; _yawInput = 0f;
			if (!Body.isKinematic)
			{
				Body.useGravity = true;
				Body.linearDamping = 0f;
			}
			Crashed?.Invoke();
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
			float yawAccel = (_yawInput * _config.maxYawRate * Mathf.Deg2Rad - w) * _config.velocityResponse
				+ noise * _config.yawDriftTorque;

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
			if (!_ready || visual == null) return;
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
