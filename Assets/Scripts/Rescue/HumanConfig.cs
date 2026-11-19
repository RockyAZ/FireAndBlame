using UnityEngine;

namespace FlameAndBlame.Rescue
{
	[CreateAssetMenu(menuName = "FlameAndBlame/HumanConfig")]
	public class HumanConfig : ScriptableObject
	{
		[Header("Spawning")]
		[Tooltip("Humans spawned on the roof slabs.")] public int humanCount = 4;
		[Tooltip("Rigidbody mass [kg].")] public float mass = 60f;

		[Header("Strength")]
		[Tooltip("Maximum strength.")] public float maxStrength = 100f;
		[Tooltip("Cup ShakeMagnitude above which strength drains [m/s^2].")] public float accelerationThreshold = 3.5f;
		[Tooltip("Strength lost per m/s^2 over threshold per second.")] public float strengthLossFactor = 4f;
		[Tooltip("Strength regained per second when calm.")] public float strengthRecoveryPerSecond = 5f;
		[Tooltip("After attaching, no strength drain for this long [s].")] public float strengthGraceTime = 1.5f;
		[Tooltip("Stun duration [s].")] public float stunDuration = 2.5f;

		[Header("Hang joint (tank attach point to hang slot)")]
		[Tooltip("Angular limit of the hang joint [deg].")] public float hangJointAngularLimitDeg = 60f;
		[Tooltip("Unused while the hang joint linear motion is Locked (hands fixed on the slot) [m].")] public float hangJointLinearLimit = 0.3f;
		[Tooltip("Soft limit spring of the hang joint.")] public float hangJointSpring = 200f;
		[Tooltip("Soft limit damper of the hang joint.")] public float hangJointDamper = 20f;

		[Header("Pool floating")]
		[Tooltip("FloatPoint height above the water surface [m].")] public float floatHeight = 0f;
		[Tooltip("Bobbing amplitude [m].")] public float bobAmplitude = 0.1f;
		[Tooltip("Bobbing frequency [Hz].")] public float bobFrequency = 0.5f;
		[Tooltip("Drift speed [m/s].")] public float driftSpeed = 0.8f;
		[Tooltip("Seconds before a new drift target is picked.")] public float driftRetargetTime = 4f;
		[Tooltip("Distance at which a drift target counts as reached [m].")] public float driftArriveDistance = 0.3f;
		[Tooltip("Distance kept from other pool humans [m].")] public float driftSeparation = 1.2f;
		[Tooltip("Turn rate toward the drift direction [deg/s].")] public float driftTurnRate = 90f;

		[Header("Dead")]
		[Tooltip("Seconds until a dead body turns kinematic.")] public float deadSettleTime = 2.5f;

		[Header("Animator trigger names")]
		public string waitRescueTrigger = "WaitRescue";
		public string hangingTrigger = "Hanging";
		public string chillPoolTrigger = "ChillPool";
		[Tooltip("No separate clip yet.")] public string swimInTankTrigger = "ChillPool";
		public string deadTrigger = "Dead";
		public string stunnedTrigger = "Stunned";
		public string fallingTrigger = "Hanging";
	}
}
