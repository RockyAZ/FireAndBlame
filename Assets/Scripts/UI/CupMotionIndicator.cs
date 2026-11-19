using FlameAndBlame.Rescue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlameAndBlame.UI
{
	/// <summary>World-space shake meter next to the cup: shows why hanging humans lose strength.</summary>
	public class CupMotionIndicator : MonoBehaviour
	{
		[Tooltip("Filled Image (Image Type = Filled, horizontal).")]
		[SerializeField] private Image fill;
		[SerializeField] private TMP_Text label;
		[Tooltip("Optional tick at the threshold (placed at 50% of the bar width by the scene).")]
		[SerializeField] private RectTransform thresholdMarker;
		[SerializeField] private Transform followTarget;
		[SerializeField] private Vector3 worldOffset = new Vector3(1.2f, 0f, 0f);
		[SerializeField] private Color okColor = Color.green;
		[SerializeField] private Color warnColor = Color.red;
		[Tooltip("Alpha of the whole indicator while no human is attached.")]
		[SerializeField, Range(0f, 1f)] private float idleAlpha = 0.35f;
		[SerializeField] private CanvasGroup group;

		private Cup _cup;
		private float _threshold;
		private Camera _cam;

		public void Bind(Cup cup, float threshold)
		{
			if (fill == null || label == null) { Debug.LogWarning("CupMotionIndicator: fill or label not assigned.", this); return; }
			_cup = cup;
			_threshold = Mathf.Max(threshold, 0.01f);
		}

		private void LateUpdate()
		{
			if (followTarget != null) transform.position = followTarget.position + worldOffset;
			if (_cam == null) _cam = Camera.main;
			if (_cam != null) transform.rotation = _cam.transform.rotation;
			if (_cup == null) return;

			// Polling is intended here: acceleration is a continuous value with no event.
			float a = _cup.ShakeMagnitude;
			fill.fillAmount = Mathf.Clamp01(a / (_threshold * 2f));
			fill.color = a > _threshold ? warnColor : okColor;
			label.text = "Shake " + a.ToString("0.0") + " m/s²";
			if (group != null) group.alpha = _cup.AttachedCount > 0 ? 1f : idleAlpha;
		}
	}
}
