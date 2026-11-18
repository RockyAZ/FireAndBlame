using UnityEngine;

namespace FlameAndBlame.Core
{
	/// <summary>Look = mouse delta this frame [pixels]; Zoom = scroll wheel delta this frame.</summary>
	public interface ICameraInputSource { Vector2 Look { get; } float Zoom { get; } }
	public interface IControlSource { HeliCommand Read(); }
	public interface ITestModeAware { void EnterTestMode(); void ExitTestMode(); }
	public interface IKillable { void Kill(); }
}
