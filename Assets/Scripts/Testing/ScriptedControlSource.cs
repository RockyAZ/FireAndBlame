using FlameAndBlame.Core;
using UnityEngine;

namespace FlameAndBlame.Testing
{
	public class ScriptedControlSource : MonoBehaviour, IControlSource
	{
		private HeliCommand _command;

		public void Set(HeliCommand command) { _command = command; }
		public HeliCommand Read() => _command;
	}
}
