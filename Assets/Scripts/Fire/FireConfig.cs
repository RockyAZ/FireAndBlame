using UnityEngine;

namespace FlameAndBlame.Fire
{
	[CreateAssetMenu(menuName = "FlameAndBlame/FireConfig")]
	public class FireConfig : ScriptableObject
	{
		[Tooltip("Seconds the fire needs per floor [s].")] public float spreadSecondsPerFloor = 12f;
		[Tooltip("Seconds a side stays out after extinguishing [s].")] public float maxStopFireTime = 20f;
		[Tooltip("Minimum water fraction that extinguishes [0..1].")] public float extinguishMinWater01 = 0.9f;
		[Tooltip("Ground fire cannot be extinguished.")] public bool groundFirePermanent = true;
		[Tooltip("Seconds per floor in lost mode [s].")] public float lostSpreadSecondsPerFloor = 3f;
		[Tooltip("Delay before the fire starts [s].")] public float startDelaySeconds = 5f;
		[Tooltip("Smoothing time of the roof fire visual [s]. Visual only.")] public float roofIgniteTime = 1.5f;
		[Tooltip("Height of the roof fire box above the slab top [m].")] public float roofFireHeight = 1.5f;
	}
}
