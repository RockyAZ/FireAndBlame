using UnityEngine;

namespace FlameAndBlame.Rescue
{
	[CreateAssetMenu(menuName = "FlameAndBlame/CupConfig")]
	public class CupConfig : ScriptableObject
	{
		[Tooltip("Water capacity [units].")] public float capacity = 100f;
		[Tooltip("Fill speed in the pool [units/s].")] public float fillRatePerSecond = 25f;
		[Tooltip("Water01 at or below this counts as empty.")] public float emptyThreshold = 0.05f;
		[Tooltip("Water01 at or above this counts as full.")] public float fullThreshold = 0.95f;
		[Tooltip("Humans the cup can carry.")] public int maxHumans = 2;
		[Tooltip("Chain segment count.")] public int segmentCount = 3;
		[Tooltip("Chain segment length [m].")] public float segmentLength = 2f;
		[Tooltip("Joint angular limit [deg].")] public float jointAngularLimitDeg = 40f;
		[Tooltip("Joint spring.")] public float jointSpring = 50f;
		[Tooltip("Joint damper.")] public float jointDamper = 8f;
		[Tooltip("Grab zone radius [m].")] public float grabZoneRadius = 1.2f;
		[Tooltip("Dumped water drop radius [m].")] public float dumpDropRadius = 1.5f;
		[Tooltip("Bump speed given to a human hit by a full cup, mass independent (VelocityChange) [m/s].")] public float bumpImpulse = 4f;
		[Tooltip("Upward bias of the bump [0..1].")] public float bumpUpBias = 0.5f;
		[Tooltip("Grab lock after a Tug [s].")] public float regrabCooldown = 1f;
		[Tooltip("Distance between human slots [m].")] public float humanSlotSpacing = 0.8f;
		[Tooltip("Bowl mass [kg].")] public float bowlMass = 2f;
		[Tooltip("Chain segment mass [kg].")] public float segmentMass = 0.5f;
		[Header("Shake measurement")]
		[Tooltip("Low-pass time of the bowl velocity before differentiating [s].")] public float shakeVelocitySmoothing = 0.15f;
		[Tooltip("Smoothing of bowl acceleration [s].")] public float accelerationSmoothing = 0.4f;
		[Tooltip("Each raw acceleration sample is clamped to this [m/s^2]; one spike cannot dominate.")] public float shakeMaxRawAcceleration = 15f;
		[Tooltip("Time for ShakeMagnitude to rise toward a higher value [s].")] public float shakeRiseTime = 0.8f;
		[Tooltip("Time for ShakeMagnitude to fall toward a lower value [s].")] public float shakeFallTime = 0.4f;
		[Tooltip("Bowl speed above which a position jump counts as a teleport and the measurement resets [m/s].")] public float shakeTeleportSpeed = 40f;
		[Tooltip("No shake measured for this long after a human attaches or detaches [s].")] public float grabGraceTime = 0.7f;
		[Header("Other")]
		[Tooltip("Mass scale of the heli in the first chain joint; large values stop the chain from pushing the heli.")] public float heliIsolationMassScale = 100f;
		[Tooltip("Lifetime of a dumped water drop [s].")] public float dropLifetime = 6f;
		[Tooltip("Smoothing time of the cup water visual [s]. Visual only.")] public float fillVisualSmoothTime = 0.25f;
	}
}
