namespace FlameAndBlame.Core
{
	public interface IControlSource { HeliCommand Read(); }
	public interface ITestModeAware { void EnterTestMode(); void ExitTestMode(); }
	public interface IKillable { void Kill(); }
}
