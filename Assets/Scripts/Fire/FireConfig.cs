using UnityEngine;

namespace FlameAndBlame.Fire
{
	[CreateAssetMenu(menuName = "FlameAndBlame/FireConfig")]
	public class FireConfig : ScriptableObject
	{
		[Tooltip("Seconds the fire needs per floor [s].")] public float spreadSecondsPerFloor = 12f;
		[Tooltip("Seconds a side stays out after extinguishing [s].")] public float maxStopFireTime = 20f;
		[Tooltip("OBSOLETE, unused since Phase 8 (extinguishing is proportional to the water amount). Kept so old assets load.")] public float extinguishMinWater01 = 0.9f;
		[Tooltip("Ground fire cannot be extinguished.")] public bool groundFirePermanent = true;
		[Tooltip("Seconds per floor in lost mode [s].")] public float lostSpreadSecondsPerFloor = 3f;
		[Tooltip("Delay before the fire starts [s].")] public float startDelaySeconds = 5f;
		[Tooltip("Seconds for the roof fire to grow from 0 to full once its side has reached the top [s].")] public float roofIgniteTime = 1.5f;
		[Tooltip("Roof fire level above which the roof kills [0..1].")] public float roofFireKillThreshold = 0.1f;
		[Tooltip("Height of the roof fire box above the slab top [m].")] public float roofFireHeight = 1.5f;
	}
}
