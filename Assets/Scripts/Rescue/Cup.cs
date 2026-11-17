using System;
using FlameAndBlame.Core;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	public class Cup : MonoBehaviour, ITestModeAware
	{
		[Inject] private CupConfig _config;
		[Inject] private IControlSource _control;

		[SerializeField] private Rigidbody anchorBody;
		[SerializeField] private WaterDrop dropPrefab;
		[SerializeField] private Transform humanAnchor;
		[SerializeField] private Rigidbody bowlBody;

		public Rigidbody Body => bowlBody;
		public Transform HumanAnchor => humanAnchor;
		public float Water01 { get; private set; }
		public bool IsEmpty => _config != null && Water01 <= _config.emptyThreshold;
		public bool IsFull => _config != null && Water01 >= _config.fullThreshold;
		public int AttachedCount { get; private set; }
		public Vector3 Acceleration { get; private set; }
		public bool InPool { get; private set; }
		public event Action<float> WaterChanged;

		public void Initialize() { }
		public void SetInPool(bool value) { InPool = value; }
		public void FillTick(float dt) { }
		public GrabResult TryGrab(Human h) => GrabResult.Rejected;
		public void Tug() { }
		public void ReleaseAll() { }

		public void EnterTestMode() { }
		public void ExitTestMode() { }

		private void RaiseWaterChanged() => WaterChanged?.Invoke(Water01);
	}
}
