using FlameAndBlame.Core;
using UnityEngine;

namespace FlameAndBlame.Testing
{
	public class ScriptedControlSource : MonoBehaviour, IControlSource
	{
		private HeliCommand _command;

		/// <summary>If only Move is set (MoveWorld zero), Move maps x to world X and y to world Z.</summary>
		public void Set(HeliCommand command)
		{
			if (command.MoveWorld == Vector3.zero && command.Move != Vector2.zero)
				command.MoveWorld = new Vector3(command.Move.x, 0f, command.Move.y);
			_command = command;
		}
		public HeliCommand Read() => _command;
	}
}
