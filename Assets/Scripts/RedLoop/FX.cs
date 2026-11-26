using UnityEngine;
using UnityEngine.Rendering;

namespace RedLoop
{
    public enum CollectibleKind { Shard, Memory, Heart }

    // Çalışma anında görsel efekt üretmek için yardımcılar (prefab gerektirmez).
    // Shader'lar Assets/Resources/RedLoop altında; Resources klasöründe oldukları için build'e otomatik girerler.
    public static class FX
    {
        private static Shader _glowShader, _additiveShader;
        private static Texture2D _softTex;
        private static bool _searched;

        private static void FindShaders()
        {
            if (_searched) return;
            _searched = true;
            _glowShader = Shader.Find("RedLoop/Glow");
            _additiveShader = Shader.Find("RedLoop/Additive");
        }

        // Yumuşak yuvarlak doku (parçacıklar için)
        public static Texture2D SoftTexture
        {
            get
            {
                if (_softTex != null) return _softTex;
                const int n = 64;
                _softTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                _softTex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        float a = Mathf.Clamp01(1f - d);
                        px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                    }
                _softTex.SetPixels(px);
                _softTex.Apply();
                return _softTex;
            }
        }

        public static Material AdditiveMaterial(Color tint, float intensity, bool soft = true)
        {
            FindShaders();
            Material m;
            if (_additiveShader != null)
            {
                m = new Material(_additiveShader);
                m.SetColor("_Color", tint);
                m.SetFloat("_Intensity", intensity);
                if (soft) m.SetTexture("_MainTex", SoftTexture);
            }
            else
            {
                var sh = Shader.Find("Sprites/Default");
                m = new Material(sh);
                m.color = tint;
            }
            return m;
        }

        // Parlayan, ışıksız malzeme. Bloom efektiyle birlikte gerçekten ışık saçıyormuş gibi görünür.
        public static void Glow(Renderer r, Color color, float emission)
        {
            if (r == null) return;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            FindShaders();
            if (_glowShader != null)
            {
                var m = new Material(_glowShader);
                m.SetColor("_Color", color);
                m.SetFloat("_Intensity", Mathf.Max(1.2f, emission * 0.5f));
                r.material = m;
                return;
            }
            var mat = r.material;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", color * emission);
            }
        }

        // Yarı saydam ışık (ışık sütunları, halkalar)
        public static void Ghost(Renderer r, Color color, float intensity)
        {
            if (r == null) return;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.material = AdditiveMaterial(color, intensity, false);
        }

        public static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            if (!keepCollider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null)
                {
                    c.enabled = false;
                    Object.Destroy(c);
                }
            }
            return go;
        }

        public static Light AddLight(GameObject parent, Color color, float range, float intensity, bool shadows = false)
        {
            var lgo = new GameObject("Light");
            lgo.transform.SetParent(parent.transform, false);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        public static void PopLight(Vector3 position, Color color, float range, float intensity, float duration = 0.6f)
        {
            var go = new GameObject("PopLight");
            go.transform.position = position;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            go.AddComponent<FadeLight>().duration = duration;
        }

        // Kıvılcım patlaması (toplama, vuruş, ölüm)
        public static void Burst(Vector3 position, Color color, int count = 24, float speed = 4f, float size = 0.18f, float life = 0.7f)
        {
            var go = new GameObject("Burst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(color);
            var sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = AdditiveMaterial(Color.white, 2f);
            ps.Play();
            go.AddComponent<AutoDestroyRealtime>().life = life + 0.5f;
        }

        public static Gradient FadeGradient(Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // Işık sütunu (kapı / hedef işareti)
        public static GameObject Beam(Vector3 basePos, Color color, float height, float width)
        {
            var go = Primitive(PrimitiveType.Cylinder, "Beam", basePos + Vector3.up * (height * 0.5f), new Vector3(width, height * 0.5f, width));
            Ghost(go.GetComponent<Renderer>(), new Color(color.r, color.g, color.b, 0.45f), 1.6f);
            // içteki parlak çekirdek
            var core = Primitive(PrimitiveType.Cylinder, "BeamCore", go.transform.position, new Vector3(width * 0.3f, height * 0.5f, width * 0.3f));
            core.transform.SetParent(go.transform, true);
            Glow(core.GetComponent<Renderer>(), color, 5f);
            return go;
        }

        // Kızıl küre (Döngünün Kalbi'nin yağdırdığı)
        public static GameObject Orb(Vector3 position, float size)
        {
            var go = Primitive(PrimitiveType.Sphere, "KizilKure", position, Vector3.one * size, true);
            go.tag = "sphere";
            Glow(go.GetComponent<Renderer>(), new Color(1f, 0.08f, 0.1f), 5f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            AddLight(go, new Color(1f, 0.15f, 0.1f), 4f, 2.5f);
            Trail(go, new Color(1f, 0.15f, 0.1f), 0.35f, size * 0.8f);
            return go;
        }

        public static TrailRenderer Trail(GameObject go, Color color, float time, float width)
        {
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = time;
            tr.widthMultiplier = width;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.minVertexDistance = 0.05f;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.material = AdditiveMaterial(Color.white, 2f, false);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            return tr;
        }

        // Kürenin düşeceği yeri gösteren yer halkası
        public static GameObject Marker(Vector3 position, Vector3 normal)
        {
            var go = Primitive(PrimitiveType.Cylinder, "Isaret", position, new Vector3(1.6f, 0.01f, 1.6f));
            go.transform.up = normal;
            Ghost(go.GetComponent<Renderer>(), new Color(1f, 0.05f, 0.08f, 0.6f), 2f);
            return go;
        }

        // Zaman parçası (altın elmas) — sahnede kopyalanacak hazır para yoksa
        public static GameObject Shard(Vector3 position)
        {
            var go = Primitive(PrimitiveType.Cube, "ZamanParcasi", position, new Vector3(0.32f, 0.32f, 0.32f), true);
            go.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
            go.tag = "coin";
            var bc = go.GetComponent<BoxCollider>();
            if (bc != null) { bc.isTrigger = true; bc.size = Vector3.one * 3f; }
            Glow(go.GetComponent<Renderer>(), new Color(1f, 0.78f, 0.3f), 4f);
            AddLight(go, new Color(1f, 0.8f, 0.4f), 3f, 1.4f);
            var bob = go.AddComponent<Bob>();
            bob.height = 0.15f;
            bob.spin = 90f;
            return go;
        }

        // Can (kalp) — iki küre ve döndürülmüş bir küpten klasik kalp şekli
        public static GameObject HeartPickup(Vector3 position)
        {
            var root = new GameObject("CanKalbi");
            root.transform.position = position;
            root.tag = "coin";
            var c = new Color(1f, 0.2f, 0.35f);
            var a = Primitive(PrimitiveType.Sphere, "L", Vector3.zero, Vector3.one * 0.3f);
            var b = Primitive(PrimitiveType.Sphere, "R", Vector3.zero, Vector3.one * 0.3f);
            var t = Primitive(PrimitiveType.Cube, "T", Vector3.zero, Vector3.one * 0.3f);
            foreach (var p in new[] { a, b, t })
            {
                p.transform.SetParent(root.transform, false);
                Glow(p.GetComponent<Renderer>(), c, 4f);
            }
            a.transform.localPosition = new Vector3(-0.1f, 0.08f, 0f);
            b.transform.localPosition = new Vector3(0.1f, 0.08f, 0f);
            t.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            t.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.9f;
            AddLight(root, c, 4f, 2f);
            var col = root.AddComponent<Collectible>();
            col.kind = CollectibleKind.Heart;
            var bob = root.AddComponent<Bob>();
            bob.height = 0.2f;
            bob.spin = 120f;
            return root;
        }
    }
}
