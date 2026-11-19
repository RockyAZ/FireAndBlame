using FlameAndBlame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlameAndBlame.Heli
{
	public class PlayerControlSource : MonoBehaviour, IControlSource, ICameraInputSource
	{
		[Tooltip("Main Camera driven by the CinemachineBrain. Move is relative to it.")]
		[SerializeField] private Transform cameraTransform;

		private InputAction _move, _vertical, _yaw, _tug, _look, _zoom;
		private bool _warned;

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
				.With("Positive", "<Keyboard>/x").With("Negative", "<Keyboard>/z");
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
			Vector2 move = _move.ReadValue<Vector2>();
			Vector3 forward = Vector3.forward, right = Vector3.right;
			if (cameraTransform != null)
			{
				forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
				if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
				forward.Normalize();
				right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
			}
			else if (!_warned) { _warned = true; Debug.LogWarning("PlayerControlSource: cameraTransform not assigned, using world axes.", this); }
			return new HeliCommand
			{
				Move = move,
				MoveWorld = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f),
				FaceWorld = forward,
				Vertical = _vertical.ReadValue<float>(),
				Yaw = _yaw.ReadValue<float>(),
				TugPressed = _tug.WasPressedThisFrame()
			};
		}
	}
}
