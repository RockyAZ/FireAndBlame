using UnityEngine;

namespace FlameAndBlame.UI
{
	[CreateAssetMenu(menuName = "FlameAndBlame/UIConfig")]
	public class UIConfig : ScriptableObject
	{
		[Tooltip("Controls hint shown in the top-right corner.")]
		[TextArea(4, 10)]
		public string controlsHintText = "WASD move (camera) | Space/Ctrl up/down | Z/X turn | F drop water | Mouse look | Wheel zoom";
		[Tooltip("Hint font size [px].")] public float hintFontSize = 22f;
		[Tooltip("Hint offset from the top-right corner [px].")] public Vector2 hintMargin = new Vector2(20f, 20f);
	}
}
