using System.Collections.Generic;
using UnityEngine;

namespace RedLoop
{
    // Döngünün Kalbi: son bölümün "boss"u. Atar, yaklaştıkça hızlanır ve oyuncunun üstüne
    // kızıl küreler yağdırır. Kürenin düşeceği yer yerde kızıl bir halkayla önceden gösterilir.
    public class LoopHeart : MonoBehaviour
    {
        private GameManager _gm;
        private Transform _player;
        private CharacterController _cc;
        private Transform _visual;
        private Light _light;
        private Vector3 _baseScale;

        private float _timer = 4f;
        private float _beatPhase;
        private bool _stopped;
        private bool _toldNear;
        private bool _active;
        private readonly List<GameObject> _orbs = new List<GameObject>();

        public void Init(GameManager gm, Transform player, Transform visual, Light light)
        {
            _gm = gm;
            _player = player;
            _cc = player != null ? player.GetComponent<CharacterController>() : null;
            _visual = visual;
            _light = light;
            _baseScale = visual != null ? visual.localScale : Vector3.one;
        }

        // Oyun gerçekten başlayınca (giriş diyaloğundan sonra) birkaç saniye nefes payı verip başlar.
        public void Activate()
        {
            _active = true;
            _timer = 4f;
        }

        private void Update()
        {
            float dist = (_player != null && _visual != null) ? Vector3.Distance(_player.position, _visual.position) : 999f;

            // Kalp atışı
            if (_visual != null)
            {
                float closeness = Mathf.InverseLerp(70f, 8f, dist);
                float interval = Mathf.Lerp(1.4f, 0.5f, closeness);
                _beatPhase += Time.deltaTime / interval;
                if (_beatPhase >= 1f)
                {
                    _beatPhase -= 1f;
                    if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.Heartbeat, _visual.position, 1f, 0.75f, 70f);
                }
                float k = Mathf.Exp(-_beatPhase * 6f);
                _visual.localScale = _baseScale * (1f + 0.18f * k);
                if (_light != null) _light.intensity = 3f + 6f * k;
            }

            if (_stopped || !_active || _gm == null || _gm.State != GameState.Playing || _player == null) return;

            if (!_toldNear && dist < 25f)
            {
                _toldNear = true;
                _gm.Say(Story.NearHeart);
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                SpawnOrb();
                _timer = Mathf.Lerp(1.6f, 3.4f, Mathf.InverseLerp(10f, 60f, dist));
            }
        }

        private void SpawnOrb()
        {
            Vector3 vel = _cc != null ? _cc.velocity : Vector3.zero;
            vel.y = 0f;
            Vector2 rnd = Random.insideUnitCircle * 1.8f;
            Vector3 aim = _player.position + vel * 1.1f + new Vector3(rnd.x, 0f, rnd.y);
            Vector3 spawn = aim + Vector3.up * 16f;

            GameObject marker = null;
            RaycastHit hit;
            int mask = ~(1 << _player.gameObject.layer); // halka oyuncunun kafasına değil yere çizilsin
            if (Physics.Raycast(aim + Vector3.up * 1.2f, Vector3.down, out hit, 30f, mask, QueryTriggerInteraction.Ignore))
            {
                marker = FX.Marker(hit.point + hit.normal * 0.03f, hit.normal);
            }

            var orb = FX.Orb(spawn, 0.6f);
            var rb = orb.GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = Vector3.down * 6f;
            var ho = orb.AddComponent<HostileOrb>();
            ho.lifeTime = 5f;
            ho.destroyOnImpact = true;
            ho.marker = marker;

            if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.OrbFall, aim, 0.8f, Random.Range(0.9f, 1.1f), 35f);

            _orbs.RemoveAll(o => o == null);
            _orbs.Add(orb);
        }

        public void StopAll()
        {
            _stopped = true;
            foreach (var o in _orbs) if (o != null) Destroy(o);
            _orbs.Clear();
        }
    }
}
