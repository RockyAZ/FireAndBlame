using FlameAndBlame.Rescue;
using TMPro;
using UnityEngine;

namespace FlameAndBlame.UI
{
	/// <summary>World-space percentage label next to the bowl. Data comes from Cup.WaterChanged only.</summary>
	public class WaterGauge : MonoBehaviour
	{
		[SerializeField] private TMP_Text label;
		[Tooltip("The bowl to follow.")]
		[SerializeField] private Transform followTarget;
		[SerializeField] private Vector3 worldOffset = new Vector3(1.2f, 0.6f, 0f);
		[SerializeField] private Color normalColor = Color.white;
		[SerializeField] private Color fullColor = new Color(0.4f, 0.8f, 1f);

		private Cup _cup;
		private Camera _cam;

		public void Bind(Cup cup)
		{
			if (label == null) { Debug.LogWarning("WaterGauge: label not assigned.", this); return; }
			_cup = cup;
			_cup.WaterChanged += OnWater;
			OnWater(_cup.Water01);
		}

		private void OnDestroy() { if (_cup != null) _cup.WaterChanged -= OnWater; }

		private void OnWater(float water01)
		{
			label.text = Mathf.RoundToInt(water01 * 100f) + "%";
			label.color = _cup.IsFull ? fullColor : normalColor;
		}

		private void LateUpdate()
		{
			// Transform follow only, no data polling.
			if (followTarget != null) transform.position = followTarget.position + worldOffset;
			if (_cam == null) _cam = Camera.main;
			if (_cam != null) transform.rotation = _cam.transform.rotation;
		}
	}
}
