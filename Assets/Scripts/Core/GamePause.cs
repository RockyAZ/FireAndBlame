using System;
using UnityEngine;

namespace FlameAndBlame.Core
{
	/// <summary>
	/// Shared pause and cursor state. Set by the UI (PauseMenu), read by input sources (PlayerControlSource),
	/// so lower modules can react to the pause menu and end screen without referencing UI.
	/// </summary>
	public static class GamePause
	{
		/// <summary>The pause menu is open (time is frozen).</summary>
		public static bool IsPaused { get; private set; }
		/// <summary>A menu (pause or end screen) owns the mouse: player input is ignored and the cursor is free.</summary>
		public static bool InputBlocked { get; private set; }
		public static event Action<bool> PausedChanged;

		public static void SetPaused(bool paused)
		{
			if (IsPaused == paused) return;
			IsPaused = paused;
			Time.timeScale = paused ? 0f : 1f;
			PausedChanged?.Invoke(paused);
		}

		public static void SetInputBlocked(bool blocked) => InputBlocked = blocked;

		/// <summary>Back to normal play: unpaused, time 1, input on. Call on scene start and before reloading.</summary>
		public static void Reset()
		{
			IsPaused = false;
			InputBlocked = false;
			Time.timeScale = 1f;
		}

		/// <summary>Cursor hidden and locked during play, visible and free while a menu is open.</summary>
		public static void ApplyCursor(bool menuOpen)
		{
			Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
			Cursor.visible = menuOpen;
		}
	}
}
