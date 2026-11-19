using Injection;
using TMPro;
using UnityEngine;

namespace FlameAndBlame.UI
{
	/// <summary>Shows the controls hint at the top-right corner with TextMeshPro.</summary>
	public class ControlsHintView : MonoBehaviour
	{
		[Inject] private UIConfig _config;

		[SerializeField] private TextMeshProUGUI tmpText;

		public string ShownText { get; private set; } = "";

		public void Initialize()
		{
			ShownText = _config.controlsHintText;
			if (tmpText == null) { Debug.LogWarning("ControlsHintView: assign tmpText.", this); return; }
			tmpText.text = ShownText;
			tmpText.fontSize = _config.hintFontSize;
			tmpText.alignment = TextAlignmentOptions.TopRight;
			var rt = tmpText.rectTransform;
			rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
			rt.anchoredPosition = -_config.hintMargin;
		}
	}
}
