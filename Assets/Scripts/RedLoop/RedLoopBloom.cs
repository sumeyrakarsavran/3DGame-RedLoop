using UnityEngine;

namespace RedLoop
{
    // Kameraya eklenen hafif bloom (parlak şeylerin etrafa ışık saçması) ve vinyet.
    // Built-in render pipeline'da OnRenderImage ile çalışır; shader yoksa veya desteklenmiyorsa kendini kapatır.
    [RequireComponent(typeof(Camera))]
    public class RedLoopBloom : MonoBehaviour
    {
        public float threshold = 1.0f;
        public float intensity = 0.5f;
        public float vignette = 0.12f;

        private Material _mat;
        private readonly RenderTexture[] _rts = new RenderTexture[6];

        private void OnEnable()
        {
            var cam = GetComponent<Camera>();
            if (cam != null) cam.allowHDR = true;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null)
            {
                var sh = Shader.Find("Hidden/RedLoop/Bloom");
                if (sh == null || !sh.isSupported)
                {
                    Graphics.Blit(src, dst);
                    enabled = false;
                    return;
                }
                _mat = new Material(sh);
                _mat.hideFlags = HideFlags.HideAndDontSave;
            }

            _mat.SetFloat("_Threshold", threshold);
            _mat.SetFloat("_Intensity", intensity);
            _mat.SetFloat("_Vignette", vignette);

            var fmt = src.format;
            int w = Mathf.Max(4, src.width / 2);
            int h = Mathf.Max(4, src.height / 2);
            int count = 0;

            // Önceki karenin _BloomTex bağlantısını temizle: geçici dokular tekrar kullanıldığı için
            // aynı doku hem okunup hem yazılıyordu (ekranda kare kare siyah bozulmalar).
            _mat.SetTexture("_BloomTex", Texture2D.blackTexture);

            var cur = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, _mat, 0);
            _rts[count++] = cur;

            for (int i = 1; i < _rts.Length; i++)
            {
                w /= 2;
                h /= 2;
                if (w < 4 || h < 4) break;
                var next = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(cur, next, _mat, 1);
                cur = next;
                _rts[count++] = cur;
            }

            for (int i = count - 2; i >= 0; i--)
            {
                Graphics.Blit(cur, _rts[i], _mat, 2); // eklemeli büyütme
                cur = _rts[i];
            }

            _mat.SetTexture("_BloomTex", cur);
            Graphics.Blit(src, dst, _mat, 3);
            _mat.SetTexture("_BloomTex", Texture2D.blackTexture);

            for (int i = 0; i < count; i++)
            {
                RenderTexture.ReleaseTemporary(_rts[i]);
                _rts[i] = null;
            }
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
