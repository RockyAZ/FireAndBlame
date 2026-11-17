using System;
using UnityEngine;

namespace FlameAndBlame.GameRules
{
	[Serializable]
	public class PoolConfig
	{
		[Tooltip("Pool size [m].")] public Vector3 poolSize = new Vector3(10f, 2.5f, 6f);
		[Tooltip("Wall to nearest pool edge [m].")] public float distanceFromBuilding = 14f;
	}

	[CreateAssetMenu(menuName = "FlameAndBlame/GameRulesConfig")]
	public class GameRulesConfig : ScriptableObject
	{
		[Tooltip("Score per rescued human.")] public int scorePerRescued = 100;
		[Tooltip("Penalty per dead human.")] public int penaltyPerDeath = 25;
		[Tooltip("Bonus per side not on fire when winning.")] public int bonusPerSideNotOnFire = 25;
		[Tooltip("Lose when all four sides burn to the top.")] public bool endOnAllSidesBurning = true;
		public PoolConfig pool = new PoolConfig();
	}
}
