using UnityEngine;

namespace RedLoop
{
    // Time.timeScale = 0 iken de silinebilsin diye gerçek zamanlı otomatik yok etme.
    public class AutoDestroyRealtime : MonoBehaviour
    {
        public float life = 1f;
        private float _start;
        private void Start() { _start = Time.unscaledTime; }
        private void Update() { if (Time.unscaledTime - _start > life) Destroy(gameObject); }
    }
}
