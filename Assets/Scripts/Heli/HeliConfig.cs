using UnityEngine;

namespace FlameAndBlame.Heli
{
	[CreateAssetMenu(menuName = "FlameAndBlame/HeliConfig")]
	public class HeliConfig : ScriptableObject
	{
		[Tooltip("Top horizontal speed [m/s].")] public float maxHorizontalSpeed = 12f;
		[Tooltip("Top climb and descent speed [m/s].")] public float maxVerticalSpeed = 6f;
		[Tooltip("Seconds for input to reach full from zero.")] public float inputRiseTime = 0.6f;
		[Tooltip("Seconds for input to return to zero.")] public float inputFallTime = 0.9f;
		[Tooltip("How aggressively velocity chases the target [1/s].")] public float velocityResponse = 1.5f;
		[Tooltip("Acceleration cap [m/s^2].")] public float maxAcceleration = 6f;
		[Tooltip("Top Q/E turn rate [deg/s].")] public float maxYawRate = 60f;
		[Tooltip("Strength of random yaw push [rad/s^2].")] public float yawDriftTorque = 0.6f;
		[Tooltip("How fast the drift changes [Hz].")] public float driftNoiseFrequency = 0.25f;
		[Tooltip("Pull of pitch and roll back to level [1/s^2].")] public float levelingStrength = 2f;
		[Tooltip("Cosmetic lean cap [deg].")] public float tiltVisualMaxDeg = 12f;
		[Tooltip("Rigidbody linear damping.")] public float linearDrag = 0.3f;
		[Tooltip("Rigidbody angular damping.")] public float angularDrag = 1.5f;
		[Tooltip("Rigidbody mass [kg].")] public float rigidbodyMass = 10f;

		[Header("Camera")]
		[Tooltip("Orbit yaw per mouse pixel [deg/px].")] public float cameraYawSensitivity = 0.15f;
		[Tooltip("Orbit pitch per mouse pixel [deg/px].")] public float cameraPitchSensitivity = 0.15f;
		[Tooltip("Lowest camera pitch, negative = below the heli [deg].")] public float cameraMinPitch = -30f;
		[Tooltip("Highest camera pitch [deg].")] public float cameraMaxPitch = 75f;
		[Tooltip("Start pitch [deg].")] public float cameraDefaultPitch = 20f;
		[Tooltip("Closest zoom distance [m].")] public float cameraMinDistance = 6f;
		[Tooltip("Farthest zoom distance [m].")] public float cameraMaxDistance = 30f;
		[Tooltip("Start distance [m].")] public float cameraDefaultDistance = 14f;
		[Tooltip("Meters per scroll unit (a wheel notch is often 120 units).")] public float cameraZoomSpeed = 0.01f;
		[Tooltip("Zoom smoothing time [s].")] public float cameraZoomSmoothTime = 0.15f;
		[Tooltip("Camera follow position damping [s].")] public float cameraFollowDamping = 0.3f;
	}
}
