using UnityEngine;

namespace RedLoop
{
    // Kısa süreli ışık parlaması.
    public class FadeLight : MonoBehaviour
    {
        public float duration = 0.6f;
        private Light _light;
        private float _start, _intensity;

        private void Start()
        {
            _light = GetComponent<Light>();
            _start = Time.unscaledTime;
            _intensity = _light != null ? _light.intensity : 0f;
        }

        private void Update()
        {
            float k = (Time.unscaledTime - _start) / duration;
            if (_light != null) _light.intensity = _intensity * Mathf.Clamp01(1f - k);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
