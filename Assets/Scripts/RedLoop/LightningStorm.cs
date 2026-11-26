using System.Collections;
using UnityEngine;

namespace RedLoop
{
    // Eski Liman'ın fırtınası: ara ara kızıl-beyaz şimşek çakar, birkaç saniye sonra gök gürler.
    public class LightningStorm : MonoBehaviour
    {
        private Light _flash;
        private float _next;

        private void Start()
        {
            var go = new GameObject("Simsek");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(55f, Random.Range(0f, 360f), 0f);
            _flash = go.AddComponent<Light>();
            _flash.type = LightType.Directional;
            _flash.color = new Color(1f, 0.82f, 0.85f);
            _flash.intensity = 0f;
            _flash.shadows = LightShadows.None;
            _next = Time.time + Random.Range(6f, 10f);
        }

        private void Update()
        {
            if (!GameManager.IsGameplayActive) return;
            if (Time.time < _next) return;
            _next = Time.time + Random.Range(9f, 18f);
            StartCoroutine(Strike());
        }

        private IEnumerator Strike()
        {
            _flash.transform.rotation = Quaternion.Euler(Random.Range(40f, 70f), Random.Range(0f, 360f), 0f);
            float[] pattern = { 1.8f, 0.2f, 1.2f, 0f, 2.4f };
            foreach (var p in pattern)
            {
                _flash.intensity = p;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.09f));
            }
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                _flash.intensity = Mathf.Lerp(2.4f, 0f, t / 0.5f);
                yield return null;
            }
            _flash.intensity = 0f;

            yield return new WaitForSeconds(Random.Range(0.4f, 1.6f));
            if (SoundFX.Instance != null) SoundFX.Instance.Play(Sfx.Thunder, Random.Range(0.6f, 0.9f), Random.Range(0.85f, 1.1f));
        }
    }
}
