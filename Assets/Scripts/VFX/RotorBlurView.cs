using UnityEngine;

namespace FlameAndBlame.VFX
{
    /// <summary>Drives the RotorBlur shader on a flat disc: accumulates the angle so speed changes never jump.</summary>
    [RequireComponent(typeof(Renderer))]
    public class RotorBlurView : MonoBehaviour
    {
        static readonly int AngleId = Shader.PropertyToID("_Angle");
        static readonly int SpeedId = Shader.PropertyToID("_Speed");

        Renderer _renderer;
        MaterialPropertyBlock _block;
        float _speed;
        float _angle;

        public float Speed => _speed;
        public float Angle => _angle;
        public bool IsVisible => Rend != null && Rend.enabled;

        Renderer Rend
        {
            get { if (_renderer == null) _renderer = GetComponent<Renderer>(); return _renderer; }
        }

        public void SetSpeed(float turnsPerSecond) => _speed = turnsPerSecond;

        public void SetVisible(bool visible)
        {
            if (Rend != null) Rend.enabled = visible;
        }

        void Update()
        {
            if (Rend == null || !Rend.enabled) return;
            _angle = Mathf.Repeat(_angle + _speed * Time.deltaTime, 1f);
            _block ??= new MaterialPropertyBlock();
            Rend.GetPropertyBlock(_block);
            _block.SetFloat(AngleId, _angle);
            _block.SetFloat(SpeedId, _speed);
            Rend.SetPropertyBlock(_block);
        }
    }
}
