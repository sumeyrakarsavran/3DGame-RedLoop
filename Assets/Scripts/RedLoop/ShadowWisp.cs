using UnityEngine;

namespace RedLoop
{
    // Yankılar Ormanı'ndaki gölgeler: karanlıkta süzülür, oyuncuya yaklaşır, dokunursa can götürür.
    // Tek okla dağılır.
    public class ShadowWisp : ArrowTarget
    {
        private Transform _player;
        private Vector3 _home;
        private float _phase;
        private Light _light;
        private const float Hover = 1.3f;

        public void Init(Transform player)
        {
            _player = player;
            _home = transform.position;
            _phase = Random.value * 10f;
            _light = GetComponentInChildren<Light>();
            radius = 0.6f;
            height = 1.2f;
            yOffset = -0.6f;
        }

        private void Update()
        {
            if (Dead) return;
            float t = Time.time + _phase;
            if (_light != null) _light.intensity = 1.6f + 0.6f * Mathf.Sin(t * 3f);

            bool active = GameManager.IsGameplayActive && _player != null;
            Vector3 target = _home + new Vector3(Mathf.Sin(t * 0.5f) * 2f, 0f, Mathf.Cos(t * 0.4f) * 2f);
            float speed = 1.2f;
            if (active)
            {
                float d = Vector3.Distance(_player.position + Vector3.up, transform.position);
                if (d < 13f)
                {
                    target = _player.position + Vector3.up * 1.1f;
                    speed = 2.1f;
                }
                if (d < 1.1f && GameManager.Instance != null)
                {
                    GameManager.Instance.Damage(transform.position);
                    Dissolve();
                    return;
                }
            }
            else
            {
                return; // menü/diyalogda dur
            }

            Vector3 next = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            RaycastHit hit;
            if (Physics.Raycast(next + Vector3.up * 3f, Vector3.down, out hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                next.y = Mathf.Lerp(next.y, hit.point.y + Hover + Mathf.Sin(t * 2f) * 0.2f, Time.deltaTime * 3f);
            transform.position = next;
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);
        }

        public override void Hit(Vector3 point, Vector3 direction)
        {
            if (Dead) return;
            if (GameManager.Instance != null) GameManager.Instance.OnShadowDefeated();
            Dissolve();
        }

        private void Dissolve()
        {
            Dead = true;
            All.Remove(this);
            FX.Burst(transform.position, new Color(0.6f, 0.3f, 1f), 30, 4f, 0.25f, 0.9f);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.WispDie, transform.position, 1f);
            Destroy(gameObject);
        }

        public static ShadowWisp Create(Vector3 position, Transform player)
        {
            var go = FX.Primitive(PrimitiveType.Sphere, "Golge", position, Vector3.one * 0.55f);
            FX.Glow(go.GetComponent<Renderer>(), new Color(0.2f, 0.05f, 0.35f), 2.4f);
            FX.AddLight(go, new Color(0.55f, 0.2f, 1f), 5f, 1.6f);

            // etrafındaki mor duman
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = new Color(0.5f, 0.2f, 0.9f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = ps.emission;
            em.rateOverTime = 25f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FX.FadeGradient(new Color(0.5f, 0.2f, 0.9f));
            go.GetComponent<ParticleSystemRenderer>().material = FX.AdditiveMaterial(Color.white, 1.2f);
            ps.Play();

            var w = go.AddComponent<ShadowWisp>();
            w.Init(player);
            return w;
        }
    }
}
