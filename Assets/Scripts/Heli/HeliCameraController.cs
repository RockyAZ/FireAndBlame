using FlameAndBlame.Core;
using Injection;
using Unity.Cinemachine;
using UnityEngine;

namespace FlameAndBlame.Heli
{
	/// <summary>
	/// Orbit camera around the heli. Drives CinemachineOrbitalFollow axes from mouse look and scroll zoom.
	/// The CinemachineCamera needs CinemachineOrbitalFollow and CinemachineRotationComposer, and no InputAxisController.
	/// </summary>
	public class HeliCameraController : MonoBehaviour
	{
		[Inject] private HeliConfig _config;
		[Inject] private ICameraInputSource _input;
		[Inject] private HeliController _heli;

		[SerializeField] private CinemachineCamera cinemachineCamera;

		private CinemachineOrbitalFollow _orbit;
		private float _yaw, _pitch, _targetDistance, _distance, _distanceVelocity;
		private bool _ready;

		public float Distance => _distance;
		public float Pitch => _pitch;
		public float Yaw => _yaw;

		public void Initialize()
		{
			if (cinemachineCamera == null || !cinemachineCamera.TryGetComponent(out _orbit))
			{
				Debug.LogError("HeliCameraController: needs a CinemachineCamera with CinemachineOrbitalFollow.", this);
				return;
			}
			cinemachineCamera.Follow = _heli.transform;
			cinemachineCamera.LookAt = _heli.transform;

			_pitch = Mathf.Clamp(_config.cameraDefaultPitch, _config.cameraMinPitch, _config.cameraMaxPitch);
			_targetDistance = _distance = Mathf.Clamp(_config.cameraDefaultDistance, _config.cameraMinDistance, _config.cameraMaxDistance);

			_orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
			var tracker = _orbit.TrackerSettings;
			tracker.BindingMode = BindingMode.WorldSpace;
			tracker.PositionDamping = Vector3.one * _config.cameraFollowDamping;
			_orbit.TrackerSettings = tracker;
			var radial = _orbit.RadialAxis;
			radial.Value = 1f;
			_orbit.RadialAxis = radial;
			var v = _orbit.VerticalAxis;
			v.Range = new Vector2(_config.cameraMinPitch, _config.cameraMaxPitch);
			v.Wrap = false;
			_orbit.VerticalAxis = v;
			Apply();
			_ready = true;
		}

		private void LateUpdate()
		{
			if (!_ready) return;
			if (!_heli.IsTestMode)
			{
				Vector2 look = _input.Look;
				_yaw = Mathf.DeltaAngle(0f, _yaw + look.x * _config.cameraYawSensitivity);
				// Mouse up looks up, which lowers the camera.
				_pitch = Mathf.Clamp(_pitch - look.y * _config.cameraPitchSensitivity, _config.cameraMinPitch, _config.cameraMaxPitch);
				_targetDistance = Mathf.Clamp(_targetDistance - _input.Zoom * _config.cameraZoomSpeed,
					_config.cameraMinDistance, _config.cameraMaxDistance);
			}
			_distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distanceVelocity, _config.cameraZoomSmoothTime);
			Apply();
		}

		private void Apply()
		{
			var h = _orbit.HorizontalAxis;
			h.Value = _yaw;
			_orbit.HorizontalAxis = h;
			var v = _orbit.VerticalAxis;
			v.Value = _pitch;
			_orbit.VerticalAxis = v;
			_orbit.Radius = _distance;
		}
	}
}
