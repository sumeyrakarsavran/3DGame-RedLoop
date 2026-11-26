using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace RedLoop
{
    // Kızıl Muhafız'ın canı. İki okla devrilir: geriye düşer, yere gömülür, bazen can kalbi bırakır.
    public class Guardian : ArrowTarget
    {
        public int hitPoints = 2;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Coroutine _flash;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            radius = 0.55f;
            height = 1.9f;
        }

        // Saldırı öncesi uyarı: kızıl yanıp sönme
        public void Telegraph(float duration)
        {
            if (Dead) return;
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(Flash(new Color(1f, 0.15f, 0.1f), duration, 12f));
        }

        public override void Hit(Vector3 point, Vector3 direction)
        {
            if (Dead) return;
            hitPoints--;
            FX.Burst(point, new Color(1f, 0.7f, 0.3f), 18, 5f, 0.12f, 0.5f);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.ArrowHit, point, 1f, Random.Range(0.9f, 1.1f));
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(Flash(Color.white, 0.25f, 30f));

            // geri itilme
            var agent = GetComponent<NavMeshAgent>();
            Vector3 push = direction;
            push.y = 0f;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && push.sqrMagnitude > 0.001f)
                agent.Move(push.normalized * 0.4f);

            if (hitPoints <= 0) StartCoroutine(Die(direction));
        }

        private IEnumerator Flash(Color color, float duration, float speed)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 0.5f + 0.5f * Mathf.Sin(t * speed);
                SetTint(Color.Lerp(Color.white, color, k), k);
                yield return null;
            }
            SetTint(Color.white, 0f);
            _flash = null;
        }

        private void SetTint(Color c, float amount)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (amount <= 0.001f)
                {
                    _mpb.Clear();
                }
                else
                {
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_Color", c);
                    _mpb.SetColor("_EmissionColor", c * amount * 0.6f);
                }
                r.SetPropertyBlock(_mpb);
            }
        }

        private IEnumerator Die(Vector3 direction)
        {
            Dead = true;
            All.Remove(this);
            var ai = GetComponent<NPCAI>();
            if (ai != null)
            {
                ai.StopAllCoroutines();
                ai.CancelInvoke();
                ai.enabled = false;
            }
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            var anim = GetComponent<Animator>();
            if (anim != null) anim.enabled = false;
            SetTint(Color.white, 0f);

            Vector3 center = Center;
            if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.RobotDie, center, 1f);
            FX.Burst(center, new Color(1f, 0.25f, 0.1f), 40, 6f, 0.2f, 0.9f);
            FX.PopLight(center, new Color(1f, 0.4f, 0.2f), 8f, 5f, 0.5f);
            if (GameManager.Instance != null) GameManager.Instance.OnGuardianDefeated(Bottom);

            // geriye devrilme
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            Vector3 axis = flat.sqrMagnitude > 0.001f ? Vector3.Cross(Vector3.up, flat.normalized) : transform.right;
            Quaternion start = transform.rotation;
            Quaternion end = Quaternion.AngleAxis(85f, axis) * start;
            float t = 0f;
            while (t < 0.55f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
                transform.rotation = Quaternion.Slerp(start, end, k * k);
                yield return null;
            }
            yield return new WaitForSeconds(1.2f);

            // yere gömülme
            Vector3 p = transform.position;
            t = 0f;
            while (t < 1.5f)
            {
                t += Time.deltaTime;
                transform.position = p + Vector3.down * (t * 1.2f);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
