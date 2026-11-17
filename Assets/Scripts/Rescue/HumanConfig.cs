using UnityEngine;

namespace FlameAndBlame.Rescue
{
	[CreateAssetMenu(menuName = "FlameAndBlame/HumanConfig")]
	public class HumanConfig : ScriptableObject
	{
		[Tooltip("Maximum strength.")] public float maxStrength = 100f;
		[Tooltip("Cup acceleration above which strength drains [m/s^2].")] public float accelerationThreshold = 4f;
		[Tooltip("Strength lost per m/s^2 over threshold per second.")] public float strengthLossFactor = 6f;
		[Tooltip("Strength regained per second when calm.")] public float strengthRecoveryPerSecond = 3f;
		[Tooltip("Stun duration [s].")] public float stunDuration = 2.5f;
		[Tooltip("Number of humans expected in the scene.")] public int humanCount = 4;
		[Tooltip("Animator trigger for WaitingOnRoof.")] public string triggerWaitingOnRoof = "WaitingOnRoof";
		[Tooltip("Animator trigger for RescuedHanging.")] public string triggerRescuedHanging = "RescuedHanging";
		[Tooltip("Animator trigger for ChillingInPool.")] public string triggerChillingInPool = "ChillingInPool";
		[Tooltip("Animator trigger for SwimmingInTank.")] public string triggerSwimmingInTank = "SwimmingInTank";
		[Tooltip("Animator trigger for Dead.")] public string triggerDead = "Dead";
		[Tooltip("Linear damping while chilling in the pool.")] public float poolDrag = 4f;
		[Tooltip("Downward speed given on detach [m/s].")] public float detachDownSpeed = 1f;
	}
}
