using UnityEngine;

namespace FlameAndBlame.Core
{
	public struct HeliCommand
	{
		/// <summary>Raw WASD (x = A/D, y = W/S), for debugging.</summary>
		public Vector2 Move;
		/// <summary>Horizontal move direction in world space (y = 0, magnitude 0..1).</summary>
		public Vector3 MoveWorld;
		/// <summary>Horizontal world direction the nose should face (y = 0, normalized). Zero = no alignment request.</summary>
		public Vector3 FaceWorld;
		public float Vertical;
		public float Yaw;
		public bool TugPressed;
	}
}
