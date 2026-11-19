using FlameAndBlame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlameAndBlame.UI
{
	public class ResultPanel : MonoBehaviour
	{
		[SerializeField] private GameObject panelRoot;
		[SerializeField] private TMP_Text titleText;
		[SerializeField] private TMP_Text scoreText;
		[SerializeField] private TMP_Text rescuedText;
		[SerializeField] private TMP_Text deathsText;
		[SerializeField] private Button restartButton;

		private void Awake()
		{
			if (restartButton != null) restartButton.onClick.AddListener(Restart);
			else Debug.LogWarning("ResultPanel: restartButton not assigned.", this);
			if (panelRoot != null) panelRoot.SetActive(false);
			else Debug.LogWarning("ResultPanel: panelRoot not assigned.", this);
		}

		public void Show(GameResult result, int score, int rescued, int total, int deaths)
		{
			if (panelRoot == null) return;
			panelRoot.SetActive(true);
			if (titleText != null) titleText.text = result == GameResult.Won ? "Rescued!" : "Lost";
			if (scoreText != null) scoreText.text = "Score: " + score;
			if (rescuedText != null) rescuedText.text = "Rescued: " + rescued + " / " + total;
			if (deathsText != null) deathsText.text = "Deaths: " + deaths;
		}

		public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}
}
