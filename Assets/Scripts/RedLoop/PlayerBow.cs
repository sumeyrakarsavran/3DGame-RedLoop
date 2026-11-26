using UnityEngine;

namespace RedLoop
{
    // Ayla'nın yayı: Sol tık veya F ile ekranın ortasına (nişangâha) ok atar.
    // Atarken karakter nişan yönüne döner ve yay hafifçe geri teper.
    public class PlayerBow : MonoBehaviour
    {
        public float cooldown = 0.5f;
        private float _next;
        private Transform _bowVisual;

        private void Start()
        {
            // Karakterin elindeki yay modelini bul (varsa geri tepme animasyonu için)
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLowerInvariant().Contains("bow_mesh")) { _bowVisual = t; break; }
            }
        }

        private void Update()
        {
            if (!GameManager.IsGameplayActive || GameManager.Instance == null) return;
            if (Time.time < _next) return;
            if (!(Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F))) return;

            var cam = Camera.main;
            if (cam == null) return;
            _next = Time.time + cooldown;

            int playerLayer = gameObject.layer;
            int mask = ~(1 << playerLayer);
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 target = ray.origin + ray.direction * 80f;
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 80f, mask, QueryTriggerInteraction.Ignore)) target = hit.point;

            // Hedef listesi: nişangâhın yakınındaki muhafız/gölgeye hafif yardım (yumuşak hedefleme)
            float bestScore = 0.985f;
            for (int i = 0; i < ArrowTarget.All.Count; i++)
            {
                var t = ArrowTarget.All[i];
                if (t == null || t.Dead) continue;
                Vector3 to = (t.Center - ray.origin);
                float dist = to.magnitude;
                if (dist > 45f) continue;
                float dot = Vector3.Dot(to / dist, ray.direction);
                if (dot > bestScore) { bestScore = dot; target = t.Center; }
            }

            Vector3 origin = transform.position + Vector3.up * 1.35f + transform.right * 0.15f;
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(flat.normalized);
                origin = transform.position + Vector3.up * 1.35f + flat.normalized * 0.6f;
            }

            Vector3 dir = target - origin;
            // okun düşüşünü biraz telafi et
            dir += Vector3.up * (dir.magnitude * 0.045f);
            Arrow.Create(origin, dir, playerLayer);
            if (SoundFX.Instance != null) SoundFX.Instance.Play(Sfx.Bow, 0.75f, Random.Range(0.95f, 1.06f));
            GameManager.Instance.OnArrowShot();

            if (_bowVisual != null) StartCoroutine(Recoil());
        }

        private System.Collections.IEnumerator Recoil()
        {
            Vector3 basePos = _bowVisual.localPosition;
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.2f));
                if (_bowVisual != null) _bowVisual.localPosition = basePos - Vector3.forward * 0.03f * k;
                yield return null;
            }
            if (_bowVisual != null) _bowVisual.localPosition = basePos;
        }
    }
}
