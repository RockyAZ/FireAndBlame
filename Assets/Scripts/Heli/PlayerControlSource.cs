using FlameAndBlame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlameAndBlame.Heli
{
	public class PlayerControlSource : MonoBehaviour, IControlSource, ICameraInputSource
	{
		private InputAction _move, _vertical, _yaw, _tug, _look, _zoom;

		public Vector2 Look => _look.ReadValue<Vector2>();
		public float Zoom => _zoom.ReadValue<float>();

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
			_look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
			_zoom = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
		}

		private void OnEnable() { _move.Enable(); _vertical.Enable(); _yaw.Enable(); _tug.Enable(); _look.Enable(); _zoom.Enable(); }
		private void OnDisable() { _move.Disable(); _vertical.Disable(); _yaw.Disable(); _tug.Disable(); _look.Disable(); _zoom.Disable(); }

		private void OnDestroy()
		{
			_move.Dispose(); _vertical.Dispose(); _yaw.Dispose(); _tug.Dispose(); _look.Dispose(); _zoom.Dispose();
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
