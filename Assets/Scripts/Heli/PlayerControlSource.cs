using FlameAndBlame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlameAndBlame.Heli
{
	public class PlayerControlSource : MonoBehaviour, IControlSource
	{
		private InputAction _move, _vertical, _yaw, _tug;

		private void Awake()
		{
			_move = new InputAction("Move", InputActionType.Value);
			_move.AddCompositeBinding("2DVector")
				.With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
				.With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
			_vertical = new InputAction("Vertical", InputActionType.Value);
			_vertical.AddCompositeBinding("1DAxis")
				.With("Positive", "<Keyboard>/space").With("Negative", "<Keyboard>/leftCtrl");
			_yaw = new InputAction("Yaw", InputActionType.Value);
			_yaw.AddCompositeBinding("1DAxis")
				.With("Positive", "<Keyboard>/e").With("Negative", "<Keyboard>/q");
			_tug = new InputAction("Tug", InputActionType.Button, "<Keyboard>/f");
		}

		private void OnEnable() { _move.Enable(); _vertical.Enable(); _yaw.Enable(); _tug.Enable(); }
		private void OnDisable() { _move.Disable(); _vertical.Disable(); _yaw.Disable(); _tug.Disable(); }

		private void OnDestroy()
		{
			_move.Dispose(); _vertical.Dispose(); _yaw.Dispose(); _tug.Dispose();
		}

		public HeliCommand Read()
		{
			return new HeliCommand
			{
				Move = _move.ReadValue<Vector2>(),
				Vertical = _vertical.ReadValue<float>(),
				Yaw = _yaw.ReadValue<float>(),
				TugPressed = _tug.WasPressedThisFrame()
			};
		}
	}
}
