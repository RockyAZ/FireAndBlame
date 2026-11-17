using UnityEngine;

namespace FlameAndBlame.Fire
{
	[CreateAssetMenu(menuName = "FlameAndBlame/BuildingConfig")]
	public class BuildingConfig : ScriptableObject
	{
		[Tooltip("Floor count.")] public int floors = 5;
		[Tooltip("Height of one floor [m].")] public float floorHeight = 3.5f;
		[Tooltip("Footprint X [m].")] public float footprintX = 12f;
		[Tooltip("Footprint Z [m].")] public float footprintZ = 12f;
		[Tooltip("Roof edge margin for human spawn [m].")] public float roofHumanSpawnMargin = 1.5f;
		[Tooltip("Depth of the burn trigger band [m].")] public float burnTriggerDepth = 1.5f;
	}
}
