using FlameAndBlame.Core;
using FlameAndBlame.GameRules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlameAndBlame.UI
{
	/// <summary>
	/// Esc toggles the pause menu (title "Pause", one Replay button) while the game is playing.
	/// Owns the cursor: hidden and locked during play, visible on the pause menu and the end screen.
	/// Bound by HudController.
	/// </summary>
	public class PauseMenu : MonoBehaviour
	{
		[SerializeField] private GameObject panelRoot;
		[SerializeField] private TMP_Text titleText;
		[SerializeField] private Button replayButton;

		private GameManager _game;
		private bool _ended;

		public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

		private void Awake()
		{
			if (replayButton != null) replayButton.onClick.AddListener(Replay);
			else Debug.LogWarning("PauseMenu: replayButton not assigned.", this);
			if (panelRoot != null) panelRoot.SetActive(false);
			else Debug.LogWarning("PauseMenu: panelRoot not assigned.", this);
		}

		public void Bind(GameManager game)
		{
			GamePause.Reset();
			_game = game;
			_ended = game != null && game.Result != GameResult.Playing;
			if (_game != null) _game.ResultChanged += OnResult;
			if (titleText != null) titleText.text = "Pause";
			Refresh();
		}

		private void OnDestroy()
		{
			if (_game != null) _game.ResultChanged -= OnResult;
			GamePause.Reset();
			GamePause.ApplyCursor(true);
		}

		private void Update()
		{
			var kb = Keyboard.current;
			if (kb != null && kb.escapeKey.wasPressedThisFrame) Toggle();
		}

		/// <summary>Opens or closes the pause menu. Ignored after the game has ended.</summary>
		public void Toggle() => SetOpen(!IsOpen);

		public void SetOpen(bool open)
		{
			if (_ended) open = false;
			if (panelRoot != null) panelRoot.SetActive(open);
			GamePause.SetPaused(open);
			Refresh();
		}

		public void Replay()
		{
			GamePause.Reset();
			var scene = SceneManager.GetActiveScene();
			// buildIndex is -1 when the scene was loaded by path (e.g. from tests), so fall back to the path.
			if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
			else SceneManager.LoadScene(scene.path);
		}

		// The Editor frees the cursor on Esc after scripts ran (the same Esc that closes this menu), and on focus changes.
		// So while playing, keep re-applying hidden + locked every frame instead of only when the menu toggles.
		private void LateUpdate()
		{
			if (_game == null || IsOpen || _ended) return;
			if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible) GamePause.ApplyCursor(false);
		}

		private void OnResult(GameResult result)
		{
			_ended = result != GameResult.Playing;
			if (_ended && IsOpen) { panelRoot.SetActive(false); GamePause.SetPaused(false); }
			Refresh();
		}

		private void Refresh()
		{
			bool menuOpen = IsOpen || _ended;
			GamePause.SetInputBlocked(menuOpen);
			GamePause.ApplyCursor(menuOpen);
		}

		// The Editor frees the cursor when the Game view loses focus; lock it again on return while playing.
		private void OnApplicationFocus(bool focused)
		{
			if (focused && _game != null) Refresh();
		}
	}
}
