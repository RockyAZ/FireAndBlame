using UnityEngine;

namespace FlameAndBlame.VFX
{
	/// <summary>Drives FillEffect shader properties through a MaterialPropertyBlock with smoothing.</summary>
	[RequireComponent(typeof(Renderer))]
	public class FillEffectView : MonoBehaviour
	{
		static readonly int FillId = Shader.PropertyToID("_Fill");
		static readonly int IntensityId = Shader.PropertyToID("_Intensity");
		static readonly int MinYId = Shader.PropertyToID("_MinY");
		static readonly int MaxYId = Shader.PropertyToID("_MaxY");

		[SerializeField] float smoothTime = 0.25f;
		[SerializeField, Range(0f, 1f)] float initialFill;
		[SerializeField, Range(0f, 1f)] float initialIntensity = 1f;

		Renderer _renderer;
		MaterialPropertyBlock _block;
		float _targetFill, _fill, _fillVel;
		float _targetIntensity, _intensity, _intVel;
		float _minY, _maxY = 1f;

		public float SmoothTime { get => smoothTime; set => smoothTime = Mathf.Max(0f, value); }
		public float CurrentFill => _fill;
		public float CurrentIntensity => _intensity;
		public float TargetFill => _targetFill;

		void Awake()
		{
			_renderer = GetComponent<Renderer>();
			_block = new MaterialPropertyBlock();
			_targetFill = _fill = initialFill;
			_targetIntensity = _intensity = initialIntensity;
			RefreshBounds();
		}

		/// <summary>Re-reads the mesh bounds (object-space Y range). Call after the mesh changes.</summary>
		public void RefreshBounds()
		{
			if (_renderer == null) _renderer = GetComponent<Renderer>();
			if (_block == null) _block = new MaterialPropertyBlock();
			var mf = GetComponent<MeshFilter>();
			if (mf != null && mf.sharedMesh != null)
			{
				var b = mf.sharedMesh.bounds;
				_minY = b.min.y;
				_maxY = b.max.y;
			}
			Apply();
		}

		public void SetFill(float fill01) => _targetFill = Mathf.Clamp01(fill01);
		public void SetIntensity(float intensity01) => _targetIntensity = Mathf.Clamp01(intensity01);

		void Update()
		{
			float dt = Time.deltaTime;
			_fill = Mathf.SmoothDamp(_fill, _targetFill, ref _fillVel, smoothTime, Mathf.Infinity, dt);
			_intensity = Mathf.SmoothDamp(_intensity, _targetIntensity, ref _intVel, smoothTime, Mathf.Infinity, dt);
			if (Mathf.Abs(_fill - _targetFill) < 0.0005f) { _fill = _targetFill; _fillVel = 0f; }
			if (Mathf.Abs(_intensity - _targetIntensity) < 0.0005f) { _intensity = _targetIntensity; _intVel = 0f; }
			Apply();
		}

		void Apply()
		{
			if (_renderer == null) return;
			_renderer.GetPropertyBlock(_block);
			_block.SetFloat(FillId, _fill);
			_block.SetFloat(IntensityId, _intensity);
			_block.SetFloat(MinYId, _minY);
			_block.SetFloat(MaxYId, _maxY);
			_renderer.SetPropertyBlock(_block);
		}
	}
}
