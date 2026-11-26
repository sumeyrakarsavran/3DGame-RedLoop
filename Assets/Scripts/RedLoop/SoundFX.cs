using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RedLoop
{
    public enum Sfx
    {
        Coin, Memory, Hurt, Throw, Impact, Jump, Click, Hover, Type, Locked,
        PortalOpen, Heartbeat, GameOver, Victory, Shatter, OrbFall, Whoosh, ClockTick
    }

    public enum MusicTrack { None, Title, Courtyard, Forest, Harbor, Ending }

    // Bütün ses efektleri ve müzikler çalışma anında sentezlenir (harici ses dosyası gerekmez,
    // dosya kaybolması / import ayarı sorunu yaşanmaz). Müzikler kusursuz döngü yapacak şekilde üretilir.
    public class SoundFX : MonoBehaviour
    {
        public static SoundFX Instance { get; private set; }

        private const int SR = 22050;
        private const float LoopLen = 16f;
        private const string MutePref = "redloop_muted";

        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private readonly Dictionary<MusicTrack, AudioClip> _music = new Dictionary<MusicTrack, AudioClip>();
        private AudioSource[] _pool;
        private int _poolIndex;
        private AudioSource _musicA, _musicB;
        private bool _aIsActive = true;
        private MusicTrack _currentTrack = MusicTrack.None;
        private Coroutine _musicRoutine;
        private float _musicVolume = 0.5f;
        private float _duck = 1f;
        private System.Random _rng = new System.Random(3333);

        public bool Muted { get; private set; }

        private void Awake()
        {
            Instance = this;
            _pool = new AudioSource[12];
            for (int i = 0; i < _pool.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _pool[i] = s;
            }
            _musicA = CreateMusicSource();
            _musicB = CreateMusicSource();

            Muted = PlayerPrefs.GetInt(MutePref, 0) == 1;
            AudioListener.volume = Muted ? 0f : 1f;
        }

        private AudioSource CreateMusicSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.priority = 0;
            return s;
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            AudioListener.volume = muted ? 0f : 1f;
            PlayerPrefs.SetInt(MutePref, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        // Diyalog sırasında müziği biraz kısmak için.
        public void SetDuck(bool duck)
        {
            _duck = duck ? 0.55f : 1f;
            var active = _aIsActive ? _musicA : _musicB;
            if (_musicRoutine == null && active.isPlaying) active.volume = _musicVolume * _duck;
        }

        // ---------------------------------------------------------------- Oynatma

        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            var clip = Get(sfx);
            if (clip == null) return;
            var src = _pool[_poolIndex];
            _poolIndex = (_poolIndex + 1) % _pool.Length;
            src.Stop();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
        }

        public void PlayAt(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 40f)
        {
            var clip = Get(sfx);
            if (clip == null) return;
            var go = new GameObject("SFX_" + sfx);
            go.transform.position = position;
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 2f;
            a.maxDistance = maxDistance;
            a.dopplerLevel = 0f;
            a.volume = volume;
            a.pitch = pitch;
            a.Play();
            go.AddComponent<AutoDestroyRealtime>().life = clip.length / Mathf.Max(0.1f, pitch) + 0.2f;
        }

        public void PlayMusic(MusicTrack track, float fadeTime = 1.5f)
        {
            if (track == _currentTrack) return;
            _currentTrack = track;
            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            _musicRoutine = StartCoroutine(Crossfade(track, fadeTime));
        }

        private IEnumerator Crossfade(MusicTrack track, float fadeTime)
        {
            var from = _aIsActive ? _musicA : _musicB;
            var to = _aIsActive ? _musicB : _musicA;
            _aIsActive = !_aIsActive;

            float fromStart = from.volume;
            if (track != MusicTrack.None)
            {
                to.clip = GetMusic(track);
                to.volume = 0f;
                to.Play();
            }

            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeTime);
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (track != MusicTrack.None) to.volume = Mathf.Lerp(0f, _musicVolume * _duck, k);
                yield return null;
            }
            from.Stop();
            from.volume = 0f;
            if (track != MusicTrack.None) to.volume = _musicVolume * _duck;
            _musicRoutine = null;
        }

        // Sahne yüklemesinde takılma olmasın diye müzikleri önceden üretmek için.
        public void Preload(MusicTrack track)
        {
            if (track != MusicTrack.None) GetMusic(track);
        }

        // ---------------------------------------------------------------- Sentez yardımcıları

        private static float Sin(float f, float t) { return Mathf.Sin(2f * Mathf.PI * f * t); }
        private float Noise() { return (float)(_rng.NextDouble() * 2.0 - 1.0); }

        private AudioClip Build(string name, float duration, Func<float, float> fn, float gain = 0.85f)
        {
            int n = Mathf.CeilToInt(duration * SR);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float v = fn(i / (float)SR);
                if (float.IsNaN(v)) v = 0f;
                data[i] = v;
                float a = Mathf.Abs(v);
                if (a > peak) peak = a;
            }
            float k = gain / peak;
            int fade = Mathf.Min(n, SR / 100); // son 10 ms yumuşak bitiş (çıt sesi olmasın)
            for (int i = 0; i < n; i++)
            {
                data[i] *= k;
                int fromEnd = n - 1 - i;
                if (fromEnd < fade) data[i] *= fromEnd / (float)fade;
            }
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Bell(float x, float f, float decay)
        {
            if (x < 0f) return 0f;
            float atk = Mathf.Min(1f, x * 300f);
            return atk * (Sin(f, x) * Mathf.Exp(-x * decay)
                          + 0.35f * Sin(f * 2.01f, x) * Mathf.Exp(-x * decay * 1.8f)
                          + 0.12f * Sin(f * 3.03f, x) * Mathf.Exp(-x * decay * 2.6f));
        }

        private AudioClip Get(Sfx sfx)
        {
            AudioClip c;
            if (_clips.TryGetValue(sfx, out c)) return c;
            c = Create(sfx);
            _clips[sfx] = c;
            return c;
        }

        private AudioClip Create(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Coin:
                    return Build("coin", 0.5f, t =>
                        (t < 0.075f ? Sin(1318.5f, t) : Sin(1975.5f, t) + 0.3f * Sin(3951f, t))
                        * Mathf.Exp(-t * 7f) * Mathf.Min(1f, t * 400f), 0.7f);

                case Sfx.Memory:
                {
                    float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
                    return Build("memory", 2.6f, t =>
                    {
                        float v = 0f;
                        for (int i = 0; i < notes.Length; i++) v += Bell(t - i * 0.11f, notes[i], 2.0f);
                        return v * (0.85f + 0.15f * Sin(6f, t));
                    }, 0.7f);
                }

                case Sfx.Hurt:
                {
                    float ph = 0f;
                    return Build("hurt", 0.5f, t =>
                    {
                        float f = 80f + 200f * Mathf.Exp(-t * 8f);
                        ph += 2f * Mathf.PI * f / SR;
                        float sq = Mathf.Sin(ph) >= 0f ? 1f : -1f;
                        return sq * 0.45f * Mathf.Exp(-t * 6f) + Noise() * 0.7f * Mathf.Exp(-t * 22f);
                    }, 0.8f);
                }

                case Sfx.Throw:
                case Sfx.Whoosh:
                {
                    float y = 0f;
                    float dur = sfx == Sfx.Throw ? 0.4f : 1.1f;
                    return Build(sfx == Sfx.Throw ? "throw" : "whoosh", dur, t =>
                    {
                        float p = t / dur;
                        float a = 0.03f + 0.35f * p;
                        y += a * (Noise() - y);
                        float env = Mathf.Sin(Mathf.PI * p);
                        return y * env * env;
                    }, 0.75f);
                }

                case Sfx.Impact:
                {
                    float ph = 0f;
                    return Build("impact", 0.45f, t =>
                    {
                        float f = 45f + 110f * Mathf.Exp(-t * 25f);
                        ph += 2f * Mathf.PI * f / SR;
                        return Mathf.Sin(ph) * Mathf.Exp(-t * 9f) + Noise() * 0.45f * Mathf.Exp(-t * 40f);
                    }, 0.8f);
                }

                case Sfx.Jump:
                {
                    float ph = 0f;
                    return Build("jump", 0.2f, t =>
                    {
                        float f = 260f + 420f * (t / 0.2f);
                        ph += 2f * Mathf.PI * f / SR;
                        return Mathf.Sin(ph) * Mathf.Exp(-t * 12f) * Mathf.Min(1f, t * 300f);
                    }, 0.5f);
                }

                case Sfx.Click:
                    return Build("click", 0.08f, t =>
                        Sin(1046.5f, t) * Mathf.Exp(-t * 50f) + 0.4f * Sin(2093f, t) * Mathf.Exp(-t * 70f), 0.6f);

                case Sfx.Hover:
                    return Build("hover", 0.05f, t => Sin(1567.98f, t) * Mathf.Exp(-t * 80f), 0.35f);

                case Sfx.Type:
                    return Build("type", 0.03f, t =>
                        (Sin(1400f, t) >= 0f ? 1f : -1f) * Mathf.Exp(-t * 140f), 0.4f);

                case Sfx.Locked:
                    return Build("locked", 0.36f, t =>
                    {
                        bool on = t < 0.13f || (t > 0.18f && t < 0.32f);
                        if (!on) return 0f;
                        float sq1 = Sin(110f, t) >= 0f ? 1f : -1f;
                        float sq2 = Sin(116.5f, t) >= 0f ? 1f : -1f;
                        return (sq1 + sq2) * 0.5f;
                    }, 0.45f);

                case Sfx.PortalOpen:
                {
                    float p1 = 0f, p2 = 0f, p3 = 0f;
                    return Build("portal", 2.4f, t =>
                    {
                        float baseF = 220f * Mathf.Pow(2f, Mathf.Min(t, 1.2f) / 1.2f);
                        p1 += 2f * Mathf.PI * baseF / SR;
                        p2 += 2f * Mathf.PI * baseF * 1.5f / SR;
                        p3 += 2f * Mathf.PI * baseF * 2f / SR;
                        float env = Mathf.Min(1f, t * 3f) * Mathf.Exp(-Mathf.Max(0f, t - 1.0f) * 2.2f);
                        float shimmer = 0.25f * Sin(1760f + 40f * Sin(5f, t), t) * Mathf.Exp(-t * 1.5f);
                        return (Mathf.Sin(p1) + 0.6f * Mathf.Sin(p2) + 0.4f * Mathf.Sin(p3)) * env + shimmer;
                    }, 0.7f);
                }

                case Sfx.Heartbeat:
                    return Build("heartbeat", 0.75f, t => Thump(t) + 0.7f * Thump(t - 0.22f), 0.9f);

                case Sfx.GameOver:
                {
                    float[] notes = { 440f, 349.23f, 293.66f, 220f };
                    return Build("gameover", 3.2f, t =>
                    {
                        float v = 0f;
                        for (int i = 0; i < notes.Length; i++)
                        {
                            float x = t - i * 0.45f;
                            if (x < 0f) continue;
                            v += (Sin(notes[i], x) + 0.25f * Sin(notes[i] * 3f, x)) * Mathf.Exp(-x * 1.6f) * Mathf.Min(1f, x * 200f);
                        }
                        v += 0.5f * Sin(55f, t) * Mathf.Min(1f, t) * Mathf.Exp(-t * 0.8f);
                        return v;
                    }, 0.8f);
                }

                case Sfx.Victory:
                {
                    float[] arp = { 523.25f, 659.25f, 783.99f, 1046.5f };
                    float[] chord = { 261.63f, 329.63f, 392f, 523.25f };
                    return Build("victory", 4.2f, t =>
                    {
                        float v = 0f;
                        for (int i = 0; i < arp.Length; i++) v += Bell(t - i * 0.15f, arp[i], 2.2f);
                        float x = t - 0.65f;
                        if (x > 0f)
                        {
                            float env = Mathf.Min(1f, x * 2f) * Mathf.Exp(-x * 0.7f);
                            for (int i = 0; i < chord.Length; i++) v += 0.35f * Sin(chord[i], x) * env;
                        }
                        return v;
                    }, 0.8f);
                }

                case Sfx.Shatter:
                {
                    float[] f = new float[8];
                    float[] on = new float[8];
                    for (int i = 0; i < f.Length; i++)
                    {
                        f[i] = 1800f + (float)_rng.NextDouble() * 3600f;
                        on[i] = (float)_rng.NextDouble() * 0.35f;
                    }
                    return Build("shatter", 2.2f, t =>
                    {
                        float v = Noise() * Mathf.Exp(-t * 7f) * 0.8f;
                        for (int i = 0; i < f.Length; i++)
                        {
                            float x = t - on[i];
                            if (x > 0f) v += 0.3f * Sin(f[i], x) * Mathf.Exp(-x * 5f);
                        }
                        v += 0.9f * Sin(38f, t) * Mathf.Exp(-t * 2f);
                        return v;
                    }, 0.9f);
                }

                case Sfx.OrbFall:
                {
                    float ph = 0f;
                    return Build("orbfall", 1.2f, t =>
                    {
                        float f = 1500f - 1100f * (t / 1.2f);
                        ph += 2f * Mathf.PI * f / SR;
                        return Mathf.Sin(ph) * Mathf.Min(1f, t * 8f) * (1f - t / 1.2f);
                    }, 0.45f);
                }

                case Sfx.ClockTick:
                    return Build("tick", 0.12f, t =>
                        (Sin(2400f, t) + 0.5f * Sin(3600f, t)) * Mathf.Exp(-t * 60f) + Noise() * 0.2f * Mathf.Exp(-t * 120f), 0.7f);
            }
            return null;
        }

        private static float Thump(float x)
        {
            if (x < 0f) return 0f;
            float f = 48f + 35f * Mathf.Exp(-x * 30f);
            return Sin(f, x) * Mathf.Exp(-x * 13f);
        }

        // ---------------------------------------------------------------- Müzik

        private AudioClip GetMusic(MusicTrack track)
        {
            AudioClip c;
            if (_music.TryGetValue(track, out c)) return c;
            c = BuildMusic(track);
            _music[track] = c;
            return c;
        }

        // Döngü uzunluğuna tam oturan frekans (her sinüs 16 saniyede tam tur atar → kesintisiz loop).
        private static float Q(float f) { return Mathf.Max(1f / LoopLen, Mathf.Round(f * LoopLen) / LoopLen); }

        private static void AddDrone(float[] buf, float f, float amp, float lfoHz, float lfoDepth)
        {
            f = Q(f);
            float fd = Q(f + 0.25f); // hafif detune → canlı, nefes alan ses
            float lfo = Q(lfoHz);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)SR;
                float m = 1f - lfoDepth + lfoDepth * (0.5f + 0.5f * Sin(lfo, t));
                buf[i] += amp * m * (0.6f * Sin(f, t) + 0.4f * Sin(fd, t));
            }
        }

        private static void AddBell(float[] buf, float start, float f, float amp, float decay, bool echo = true)
        {
            AddBellRaw(buf, start, f, amp, decay);
            if (echo)
            {
                AddBellRaw(buf, start + 0.375f, f, amp * 0.35f, decay);
                AddBellRaw(buf, start + 0.75f, f, amp * 0.14f, decay);
            }
        }

        private static void AddBellRaw(float[] buf, float start, float f, float amp, float decay)
        {
            int n = buf.Length;
            int s = Mathf.RoundToInt(start * SR);
            int len = Mathf.Min(n, Mathf.RoundToInt(Mathf.Min(6f, 7f / decay) * SR));
            for (int j = 0; j < len; j++)
            {
                int idx = (s + j) % n;
                buf[idx] += amp * Bell(j / (float)SR, f, decay);
            }
        }

        private static void AddKick(float[] buf, float start, float amp)
        {
            int n = buf.Length;
            int s = Mathf.RoundToInt(start * SR);
            int len = SR / 2;
            float ph = 0f;
            for (int j = 0; j < len; j++)
            {
                float x = j / (float)SR;
                float f = 42f + 70f * Mathf.Exp(-x * 22f);
                ph += 2f * Mathf.PI * f / SR;
                buf[(s + j) % n] += amp * Mathf.Sin(ph) * Mathf.Exp(-x * 9f);
            }
        }

        private void AddWind(float[] buf, float amp, float swellHz)
        {
            float y = 0f, y2 = 0f;
            float sw = Q(swellHz);
            // iki tur: filtre durumu döngü başında da oturmuş olsun
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < buf.Length; i++)
                {
                    y += 0.02f * (Noise() - y);
                    y2 += 0.02f * (y - y2);
                    if (pass == 1)
                    {
                        float t = i / (float)SR;
                        buf[i] += amp * 6f * y2 * (0.55f + 0.45f * Sin(sw, t));
                    }
                }
            }
        }

        private AudioClip BuildMusic(MusicTrack track)
        {
            int n = Mathf.RoundToInt(LoopLen * SR);
            var buf = new float[n];

            switch (track)
            {
                case MusicTrack.Title:
                {
                    AddDrone(buf, 73.42f, 0.45f, 0.125f, 0.5f);
                    AddDrone(buf, 110f, 0.22f, 0.0625f, 0.6f);
                    AddWind(buf, 0.12f, 0.125f);
                    float[] bells = { 587.33f, 698.46f, 880f, 783.99f, 587.33f, 523.25f };
                    float[] times = { 1f, 3.5f, 6f, 9f, 11.5f, 14f };
                    for (int i = 0; i < bells.Length; i++) AddBell(buf, times[i], bells[i], 0.16f, 1.2f);
                    break;
                }
                case MusicTrack.Courtyard:
                {
                    AddDrone(buf, 73.42f, 0.5f, 0.125f, 0.5f);
                    AddDrone(buf, 110f, 0.28f, 0.0625f, 0.6f);
                    AddDrone(buf, 146.83f, 0.1f, 0.25f, 0.8f);
                    for (int b = 0; b < 8; b++) // kalp atışı gibi nabız
                    {
                        AddKick(buf, b * 2f, 0.55f);
                        AddKick(buf, b * 2f + 0.28f, 0.35f);
                    }
                    float[] bells = { 587.33f, 698.46f, 783.99f, 880f, 698.46f, 1046.5f, 587.33f };
                    float[] times = { 0.5f, 3f, 4.5f, 7f, 9.5f, 12f, 13.5f };
                    for (int i = 0; i < bells.Length; i++) AddBell(buf, times[i], bells[i], 0.14f, 1.6f);
                    break;
                }
                case MusicTrack.Forest:
                {
                    AddDrone(buf, 110f, 0.4f, 0.0625f, 0.5f);
                    AddDrone(buf, 164.81f, 0.18f, 0.125f, 0.7f);
                    AddWind(buf, 0.3f, 0.125f);
                    float[] bells = { 440f, 523.25f, 659.25f, 587.33f, 783.99f, 659.25f, 440f, 523.25f };
                    float[] times = { 0.8f, 2.6f, 4.9f, 6.4f, 8.7f, 10.5f, 12.8f, 14.6f };
                    for (int i = 0; i < bells.Length; i++) AddBell(buf, times[i], bells[i], 0.15f, 1.0f);
                    break;
                }
                case MusicTrack.Harbor:
                {
                    AddDrone(buf, 65.41f, 0.45f, 0.25f, 0.4f);
                    AddDrone(buf, 69.3f, 0.22f, 0.125f, 0.6f);   // gerilim yaratan uyumsuzluk
                    AddDrone(buf, 98f, 0.18f, 0.0625f, 0.5f);
                    for (int b = 0; b < 32; b++) AddKick(buf, b * 0.5f, (b % 4 == 0) ? 0.55f : 0.3f);
                    float[] ost = { 130.81f, 155.56f, 196f, 155.56f };
                    for (int b = 0; b < 32; b++) AddBell(buf, b * 0.5f + 0.25f, ost[b % 4], 0.1f, 5f, false);
                    float[] hi = { 1046.5f, 932.33f, 783.99f, 622.25f };
                    for (int i = 0; i < hi.Length; i++) AddBell(buf, 1f + i * 4f, hi[i], 0.1f, 1.4f);
                    break;
                }
                case MusicTrack.Ending:
                {
                    AddDrone(buf, 130.81f, 0.3f, 0.125f, 0.5f);
                    AddDrone(buf, 164.81f, 0.22f, 0.0625f, 0.5f);
                    AddDrone(buf, 196f, 0.2f, 0.125f, 0.6f);
                    AddDrone(buf, 246.94f, 0.1f, 0.25f, 0.7f);
                    AddDrone(buf, 65.41f, 0.25f, 0.0625f, 0.3f);
                    float[] bells = { 523.25f, 659.25f, 783.99f, 880f, 1174.66f, 1046.5f, 783.99f, 659.25f };
                    float[] times = { 0.5f, 2.5f, 4.5f, 6f, 8.5f, 10.5f, 12.5f, 14f };
                    for (int i = 0; i < bells.Length; i++) AddBell(buf, times[i], bells[i], 0.16f, 1.1f);
                    break;
                }
            }

            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            float k = 0.85f / peak;
            for (int i = 0; i < n; i++) buf[i] *= k;

            var clip = AudioClip.Create("music_" + track, n, 1, SR, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }

}
