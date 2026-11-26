using UnityEngine;
using UnityEngine.Rendering;

namespace RedLoop
{
    public enum CollectibleKind { Shard, Memory }

    // Çalışma anında görsel efekt üretmek için yardımcılar (prefab gerektirmez).
    public static class FX
    {
        public static void Glow(Renderer r, Color color, float emission)
        {
            if (r == null) return;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", color * emission);
            }
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

        // Işık sütunu (kapı / hedef işareti)
        public static GameObject Beam(Vector3 basePos, Color color, float height, float width)
        {
            var go = Primitive(PrimitiveType.Cylinder, "Beam", basePos + Vector3.up * (height * 0.5f), new Vector3(width, height * 0.5f, width));
            Glow(go.GetComponent<Renderer>(), color, 4f);
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
            return go;
        }

        // Kürenin düşeceği yeri gösteren yer halkası
        public static GameObject Marker(Vector3 position, Vector3 normal)
        {
            var go = Primitive(PrimitiveType.Cylinder, "Isaret", position, new Vector3(1.6f, 0.01f, 1.6f));
            go.transform.up = normal;
            Glow(go.GetComponent<Renderer>(), new Color(1f, 0.05f, 0.08f), 2.5f);
            return go;
        }
    }
}
