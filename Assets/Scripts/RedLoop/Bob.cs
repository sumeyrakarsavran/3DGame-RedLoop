using UnityEngine;

namespace RedLoop
{
    // Havada süzülme ve dönme.
    public class Bob : MonoBehaviour
    {
        public float height = 0.2f;
        public float speed = 2f;
        public float spin = 60f;
        private Vector3 _base;
        private float _phase;

        private void Start()
        {
            _base = transform.position;
            _phase = Random.value * 10f;
        }

        private void Update()
        {
            transform.position = _base + Vector3.up * Mathf.Sin(Time.time * speed + _phase) * height;
            transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
        }
    }
}
