using UnityEngine;

namespace RedLoop
{
    // Ayla'nın altın oku. Hızla uçar, hafifçe düşer, hedefe ya da duvara saplanır.
    public class Arrow : MonoBehaviour
    {
        public float speed = 42f;
        public float gravity = 4f;
        public int ignoreLayer = -1;

        private Vector3 _velocity;
        private float _life = 2.5f;
        private bool _stuck;

        public void Launch(Vector3 direction)
        {
            _velocity = direction.normalized * speed;
            transform.rotation = Quaternion.LookRotation(_velocity);
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }
            if (_stuck) return;

            Vector3 a = transform.position;
            _velocity += Vector3.down * gravity * Time.deltaTime;
            Vector3 b = a + _velocity * Time.deltaTime;

            // 1) Okla vurulabilen hedefler (muhafız, gölge)
            ArrowTarget best = null;
            Vector3 bestPoint = b;
            float bestDist = float.MaxValue;
            for (int i = ArrowTarget.All.Count - 1; i >= 0; i--)
            {
                var t = ArrowTarget.All[i];
                if (t == null || t.Dead) continue;
                Vector3 p;
                if (t.Intersects(a, b, out p))
                {
                    float d = (p - a).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = t; bestPoint = p; }
                }
            }

            // 2) Duvar / zemin
            int mask = ignoreLayer >= 0 ? ~(1 << ignoreLayer) : ~0;
            RaycastHit hit;
            Vector3 seg = b - a;
            bool wall = Physics.Raycast(a, seg.normalized, out hit, seg.magnitude, mask, QueryTriggerInteraction.Ignore);

            if (best != null && (!wall || bestDist <= (hit.point - a).sqrMagnitude))
            {
                best.Hit(bestPoint, _velocity.normalized);
                Destroy(gameObject);
                return;
            }

            if (wall)
            {
                transform.position = hit.point - seg.normalized * 0.25f;
                _stuck = true;
                _life = 3f;
                var tr = GetComponent<TrailRenderer>();
                if (tr != null) tr.emitting = false;
                var l = GetComponentInChildren<Light>();
                if (l != null) l.intensity = 0.4f;
                if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.ArrowThunk, hit.point, 0.8f, Random.Range(0.9f, 1.15f));
                FX.Burst(hit.point, new Color(1f, 0.85f, 0.5f), 10, 2.5f, 0.08f, 0.35f);
                return;
            }

            transform.position = b;
            if (_velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(_velocity);
        }

        public static Arrow Create(Vector3 position, Vector3 direction, int ignoreLayer)
        {
            var go = new GameObject("Ok");
            go.transform.position = position;
            var shaft = FX.Primitive(PrimitiveType.Cube, "Govde", position, new Vector3(0.035f, 0.035f, 0.75f));
            shaft.transform.SetParent(go.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            FX.Glow(shaft.GetComponent<Renderer>(), new Color(1f, 0.8f, 0.35f), 4f);
            var tip = FX.Primitive(PrimitiveType.Cube, "Uc", position, new Vector3(0.09f, 0.09f, 0.09f));
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.08f);
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            FX.Glow(tip.GetComponent<Renderer>(), new Color(1f, 0.95f, 0.75f), 6f);
            FX.AddLight(go, new Color(1f, 0.8f, 0.4f), 3f, 1.5f);
            FX.Trail(go, new Color(1f, 0.75f, 0.3f), 0.22f, 0.07f);

            var arrow = go.AddComponent<Arrow>();
            arrow.ignoreLayer = ignoreLayer;
            arrow.Launch(direction);
            return arrow;
        }
    }
}
