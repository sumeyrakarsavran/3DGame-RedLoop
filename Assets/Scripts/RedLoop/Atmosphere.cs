using UnityEngine;

namespace RedLoop
{
    // Bölümlere hava katan efektler. Kızıl gökyüzüne (skybox) dokunmaz; sis skybox'ı etkilemez.
    public static class Atmosphere
    {
        public static void Setup(LevelKind level, Transform player, Transform parent)
        {
            switch (level)
            {
                case LevelKind.Courtyard:
                    Fog(new Color(0.22f, 0.03f, 0.04f), 0.012f);
                    Embers(player, parent);
                    break;
                case LevelKind.Forest:
                    Fog(new Color(0.03f, 0.05f, 0.08f), 0.028f);
                    Fireflies(player, parent);
                    Dust(player, parent, new Color(0.6f, 0.8f, 1f, 0.25f));
                    break;
                case LevelKind.Harbor:
                    Fog(new Color(0.12f, 0.05f, 0.07f), 0.01f);
                    Rain(player, parent);
                    var storm = new GameObject("Firtina");
                    storm.transform.SetParent(parent, false);
                    storm.AddComponent<LightningStorm>();
                    break;
            }
        }

        private static void Fog(Color color, float density)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;
        }

        private static ParticleSystem Make(string name, Transform player, Transform parent, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (player != null) go.transform.position = player.position + offset;
            var f = go.AddComponent<FollowTarget>();
            f.target = player;
            f.offset = offset;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            return ps;
        }

        // Kızıl Avlu: havada yavaşça süzülen seyrek közler (yerden fışkırmaz)
        private static void Embers(Transform player, Transform parent)
        {
            var ps = Make("Kozler", player, parent, new Vector3(0f, 3f, 0f));
            var main = ps.main;
            main.duration = 6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.15f, 0.8f), new Color(1f, 0.6f, 0.25f, 0.8f));
            main.gravityModifier = -0.01f;
            main.maxParticles = 90;
            var em = ps.emission;
            em.rateOverTime = 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 4f, 30f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.3f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FX.FadeGradient(Color.white);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = FX.AdditiveMaterial(Color.white, 1.4f);
            ps.Play();
        }

        // Yankılar Ormanı: yanıp sönen ateş böcekleri
        private static void Fireflies(Transform player, Transform parent)
        {
            var ps = Make("AtesBocekleri", player, parent, new Vector3(0f, 1.2f, 0f));
            var main = ps.main;
            main.duration = 6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 1f, 0.4f), new Color(1f, 0.9f, 0.5f));
            main.maxParticles = 160;
            var em = ps.emission;
            em.rateOverTime = 22f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 3f, 30f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.2f, 0.3f),
                    new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.3f, 0.65f), new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = FX.AdditiveMaterial(Color.white, 3f);
            ps.Play();
        }

        // Havada asılı ince toz
        private static void Dust(Transform player, Transform parent, Color c)
        {
            var ps = Make("Toz", player, parent, new Vector3(0f, 1.5f, 0f));
            var main = ps.main;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = c;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 25f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 4f, 20f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FX.FadeGradient(Color.white);
            ps.GetComponent<ParticleSystemRenderer>().material = FX.AdditiveMaterial(Color.white, 1f);
            ps.Play();
        }

        // Eski Liman: kızıl ışıkta parlayan yağmur
        private static void Rain(Transform player, Transform parent)
        {
            var ps = Make("Yagmur", player, parent, new Vector3(0f, 14f, 0f));
            var main = ps.main;
            main.duration = 2f;
            main.startLifetime = 1.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(16f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startColor = new Color(1f, 0.75f, 0.75f, 0.35f);
            main.maxParticles = 1500;
            var em = ps.emission;
            em.rateOverTime = 600f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 40f);
            shape.rotation = new Vector3(90f, 0f, 0f); // aşağı doğru
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1f;
            r.material = FX.AdditiveMaterial(Color.white, 1.2f);
            ps.Play();
        }
    }
}
