using Injection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlameAndBlame.UI
{
	/// <summary>Shows the controls hint at the top-right corner. Uses TMP if assigned, else the legacy Text.</summary>
	public class ControlsHintView : MonoBehaviour
	{
		[Inject] private UIConfig _config;

		[SerializeField] private TextMeshProUGUI tmpText;
		[Tooltip("Fallback when TMP Essential Resources are missing.")]
		[SerializeField] private Text legacyText;

		public string ShownText { get; private set; } = "";

		public void Initialize()
		{
			ShownText = _config.controlsHintText;
			if (tmpText != null)
			{
				tmpText.text = ShownText;
				tmpText.fontSize = _config.hintFontSize;
				tmpText.alignment = TextAlignmentOptions.TopRight;
				Anchor(tmpText.rectTransform);
			}
			else if (legacyText != null)
			{
				legacyText.text = ShownText;
				legacyText.fontSize = Mathf.RoundToInt(_config.hintFontSize);
				legacyText.alignment = TextAnchor.UpperRight;
				Anchor(legacyText.rectTransform);
			}
			else Debug.LogWarning("ControlsHintView: assign tmpText or legacyText.", this);
		}

		private void Anchor(RectTransform rt)
		{
			rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
			rt.anchoredPosition = -_config.hintMargin;
		}
	}
}
