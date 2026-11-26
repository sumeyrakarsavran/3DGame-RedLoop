using UnityEngine;

namespace RedLoop
{
    // Düşman küresi: ömrü dolunca silinir, çarpınca ses çıkarır, oyuncuya değerse hasar verir.
    public class HostileOrb : MonoBehaviour
    {
        public float lifeTime = 6f;
        public bool destroyOnImpact = true;
        public GameObject marker;

        private bool _impacted;
        private Vector3 _markerScale;

        private void Start()
        {
            Destroy(gameObject, lifeTime);
            if (marker != null) _markerScale = marker.transform.localScale;
        }

        private void Update()
        {
            if (marker != null)
            {
                float k = 1f + 0.25f * Mathf.Sin(Time.time * 14f);
                marker.transform.localScale = new Vector3(_markerScale.x * k, _markerScale.y, _markerScale.z * k);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.CompareTag("Player"))
            {
                if (CompareTag("sphere") && GameManager.Instance != null)
                {
                    GameManager.Instance.Damage(transform.position);
                }
                Burst();
                return;
            }

            if (!_impacted)
            {
                _impacted = true;
                if (SoundFX.Instance != null)
                    SoundFX.Instance.PlayAt(Sfx.Impact, transform.position, destroyOnImpact ? 0.9f : 0.5f, Random.Range(0.85f, 1.15f));
                if (destroyOnImpact) Burst();
            }
        }

        private void Burst()
        {
            FX.PopLight(transform.position, new Color(1f, 0.2f, 0.1f), 6f, 4f, 0.4f);
            FX.Burst(transform.position, new Color(1f, 0.25f, 0.12f), 26, 5f, 0.16f, 0.6f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker);
        }
    }
}
