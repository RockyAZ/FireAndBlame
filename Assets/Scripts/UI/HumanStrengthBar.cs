using FlameAndBlame.Core;
using FlameAndBlame.Rescue;
using UnityEngine;
using UnityEngine.UI;

namespace FlameAndBlame.UI
{
	/// <summary>World-space bar on each human prefab. Shown only while the human hangs from the tank.</summary>
	public class HumanStrengthBar : MonoBehaviour
	{
		[Tooltip("Filled Image (Image Type = Filled).")]
		[SerializeField] private Image fill;
		[Tooltip("Object shown and hidden (the small canvas).")]
		[SerializeField] private GameObject root;
		[Tooltip("World offset above the human, applied to the bar in LateUpdate [m].")]
		[SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
		[SerializeField] private Color lowColor = Color.red;
		[SerializeField] private Color highColor = Color.green;

		private Human _human;
		private Camera _cam;

		public void Bind(Human human)
		{
			if (fill == null || root == null) { Debug.LogWarning("HumanStrengthBar: fill or root not assigned.", this); return; }
			if (_human != null) Unbind();
			_human = human;
			_human.StrengthChanged += OnStrength;
			_human.StateChanged += OnState;
			OnStrength(_human.Strength01);
			OnState(_human, _human.State);
		}

		private void Unbind()
		{
			_human.StrengthChanged -= OnStrength;
			_human.StateChanged -= OnState;
		}

		private void OnDestroy() { if (_human != null) Unbind(); }

		private void OnStrength(float s)
		{
			fill.fillAmount = s;
			fill.color = Color.Lerp(lowColor, highColor, s);
		}

		private void OnState(Human h, HumanState s) => root.SetActive(s == HumanState.RescuedHanging);

		private void LateUpdate()
		{
			if (_human == null || root == null || !root.activeSelf) return;
			// Transform follow only: stay above the human, face the camera.
			root.transform.position = _human.transform.position + worldOffset;
			if (_cam == null) _cam = Camera.main;
			if (_cam != null) root.transform.rotation = _cam.transform.rotation;
		}
	}
}
