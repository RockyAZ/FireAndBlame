using UnityEngine;

namespace FlameAndBlame.UI
{
	[CreateAssetMenu(menuName = "FlameAndBlame/UIConfig")]
	public class UIConfig : ScriptableObject
	{
		[Tooltip("Controls hint shown in the top-right corner.")]
		[TextArea(4, 10)]
		public string controlsHintText = "WASD  move\nSpace / Left Ctrl  up / down\nQ / E  turn\nF  drop water / release\nMouse  orbit camera\nScroll  zoom";
		[Tooltip("Hint font size [px].")] public float hintFontSize = 22f;
		[Tooltip("Hint offset from the top-right corner [px].")] public Vector2 hintMargin = new Vector2(20f, 20f);
	}
}
