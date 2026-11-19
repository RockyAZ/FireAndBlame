namespace FlameAndBlame.VFX
{
	/// <summary>One-shot burst; every Play() re-emits.</summary>
	public class SplashEffect : EffectController
	{
		protected override bool RestartOnPlay => true;
	}
}
