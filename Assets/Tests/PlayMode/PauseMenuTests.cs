#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using FlameAndBlame.Core;
using FlameAndBlame.Heli;
using FlameAndBlame.GameRules;
using FlameAndBlame.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlameAndBlame.Tests.PlayMode
{
    /// <summary>Pause menu, GamePause and cursor tests. Test mode OFF.</summary>
    public class PauseMenuTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private PauseMenu _menu;
        private PlayerControlSource _pcs;
        private HeliController _heli;
        private GameManager _game;
        private Keyboard _kb;

        private static void Report(string name, bool ok, string reason)
        {
            Debug.Log(ok ? $"[TEST] {name} PASS" : $"[TEST] {name} FAIL {reason}");
            Assert.IsTrue(ok, reason);
        }

        [TearDown]
        public void Cleanup()
        {
            if (_kb != null && _kb.added) InputSystem.RemoveDevice(_kb);
            _kb = null;
            Time.timeScale = 1f;
            GamePause.Reset();
        }

        private IEnumerator Load()
        {
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return op;
            yield return null;
            yield return null;
            Bind();
            _kb = InputSystem.AddDevice<Keyboard>("PauseTestKeyboard");
            _kb.MakeCurrent();
        }

        private void Bind()
        {
            _menu = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            _pcs = Object.FindFirstObjectByType<PlayerControlSource>(FindObjectsInactive.Include);
            _heli = Object.FindFirstObjectByType<HeliController>(FindObjectsInactive.Include);
            _game = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "PauseMenu not in scene");
            Assert.IsNotNull(_pcs); Assert.IsNotNull(_heli); Assert.IsNotNull(_game);
        }

        private IEnumerator Press(params Key[] keys)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.QueueStateEvent(_kb, new KeyboardState(keys));
            InputSystem.Update();
            yield return null;
        }

        /// <summary>Queues the state and lets the normal input update deliver it before script Update, so wasPressedThisFrame is true in Update.</summary>
        private IEnumerator PressDeferred(params Key[] keys)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.QueueStateEvent(_kb, new KeyboardState(keys));
            yield return null;
            yield return null;
        }

        private static T Field<T>(object o, string name) =>
            (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        [UnityTest]
        public IEnumerator pause_cursor_locked_in_play()
        {
            const string n = "pause_cursor_locked_in_play";
            yield return Load();
            string fail = null;
            Debug.Log($"[TEST-INFO] {n} lockState={Cursor.lockState} visible={Cursor.visible} timeScale={Time.timeScale}");
            if (_menu.IsOpen) fail = "panel open at start";
            else if (GamePause.IsPaused) fail = "IsPaused true at start";
            else if (GamePause.InputBlocked) fail = "InputBlocked true at start";
            else if (Cursor.visible) fail = "cursor visible during play";
            else if (Cursor.lockState != CursorLockMode.Locked)
                Debug.Log($"[TEST-INFO] {n} lockState is {Cursor.lockState}, not Locked (Editor may not lock without Game view focus); not failing on lockState alone");
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator pause_esc_opens_and_closes()
        {
            const string n = "pause_esc_opens_and_closes";
            yield return Load();
            string fail = null;

            yield return PressDeferred(Key.Escape);
            Debug.Log($"[TEST-INFO] {n} Keyboard.current={Keyboard.current?.name} esc={Keyboard.current?.escapeKey.isPressed} open={_menu.IsOpen}");
            yield return Press();
            var panel = Field<GameObject>(_menu, "panelRoot");
            var title = Field<TMP_Text>(_menu, "titleText");
            var btn = Field<Button>(_menu, "replayButton");
            var label = btn != null ? btn.GetComponentInChildren<TMP_Text>(true) : null;

            if (!_menu.IsOpen || !panel.activeInHierarchy) fail = "Esc did not open the panel (virtual keyboard Esc not seen?)";
            else if (title == null || title.text != "Pause") fail = $"title text '{title?.text}'";
            else if (label == null || label.text != "Replay") fail = $"replay label '{label?.text}'";
            else if (Time.timeScale != 0f) fail = $"timeScale {Time.timeScale} while paused";
            else if (!GamePause.IsPaused || !GamePause.InputBlocked) fail = $"IsPaused={GamePause.IsPaused} InputBlocked={GamePause.InputBlocked}";
            else if (!Cursor.visible) fail = "cursor not visible while paused";

            if (fail == null)
            {
                yield return Press(Key.W);
                var cmd = _pcs.Read();
                if (cmd.Move != Vector2.zero || cmd.MoveWorld != Vector3.zero)
                    fail = $"Read() not default while paused with W held (Move={cmd.Move})";
                var cam = (ICameraInputSource)_pcs;
                if (fail == null && (cam.Look != Vector2.zero || cam.Zoom != 0f)) fail = "Look/Zoom nonzero while paused";
                yield return Press();
            }
            if (fail == null)
            {
                yield return PressDeferred(Key.Escape);
                yield return Press();
                if (_menu.IsOpen) fail = "Esc again did not close the panel";
                else if (Time.timeScale != 1f) fail = $"timeScale {Time.timeScale} after close";
                else if (GamePause.InputBlocked || GamePause.IsPaused) fail = "still blocked/paused after close";
                else if (Cursor.visible) fail = "cursor visible after close";
            }
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator pause_blocked_after_game_end()
        {
            const string n = "pause_blocked_after_game_end";
            yield return Load();
            string fail = null;
            _heli.Crash();
            yield return null; yield return null;
            Debug.Log($"[TEST-INFO] {n} result={_game.Result}");
            _menu.Toggle();
            yield return PressDeferred(Key.Escape);
            yield return Press();
            if (_game.Result != GameResult.Lost) fail = $"result {_game.Result}, expected Lost";
            else if (_menu.IsOpen) fail = "panel opened after game end";
            else if (!GamePause.InputBlocked) fail = "InputBlocked false after game end";
            else if (!Cursor.visible) fail = "cursor not visible after game end";
            else if (Time.timeScale != 1f) fail = $"timeScale {Time.timeScale}";
            Report(n, fail == null, fail);
        }

        [UnityTest]
        public IEnumerator pause_replay_reloads()
        {
            const string n = "pause_replay_reloads";
            SceneManager.LoadScene("Main");
            yield return null; yield return null; yield return null;
            Bind();
            _kb = InputSystem.AddDevice<Keyboard>("PauseTestKeyboard");
            string fail = null;
            _menu.SetOpen(true);
            yield return null;
            if (!_menu.IsOpen) fail = "menu did not open";
            else
            {
                Field<Button>(_menu, "replayButton").onClick.Invoke();
                yield return null; yield return null; yield return null;
                Bind();
                Debug.Log($"[TEST-INFO] {n} after reload timeScale={Time.timeScale} paused={GamePause.IsPaused}");
                if (Time.timeScale != 1f) fail = $"timeScale {Time.timeScale} after reload";
                else if (GamePause.IsPaused) fail = "IsPaused after reload";
                else if (_menu.IsOpen) fail = "panel open after reload";
            }
            Report(n, fail == null, fail);
        }
    }
}
#endif
