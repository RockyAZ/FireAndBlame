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
	}
}
