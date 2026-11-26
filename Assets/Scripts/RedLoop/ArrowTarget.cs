using System.Collections.Generic;
using UnityEngine;

namespace RedLoop
{
    // Okla vurulabilen her şeyin ortak tabanı (muhafızlar, gölgeler).
    // Robotlarda collider olmadığı için oklar hedefleri bu listeden, basit bir kapsül yaklaşımıyla bulur.
    public abstract class ArrowTarget : MonoBehaviour
    {
        public static readonly List<ArrowTarget> All = new List<ArrowTarget>();

        public float radius = 0.6f;
        public float height = 1.9f;
        public float yOffset = 0f;

        public bool Dead { get; protected set; }

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        public Vector3 Bottom { get { return transform.position + Vector3.up * yOffset; } }
        public Vector3 Center { get { return Bottom + Vector3.up * (height * 0.5f); } }

        // Ok segmenti (a→b) bu hedefin kapsülüne değiyor mu?
        public bool Intersects(Vector3 a, Vector3 b, out Vector3 hitPoint)
        {
            Vector3 p0 = Bottom + Vector3.up * radius;
            Vector3 p1 = Bottom + Vector3.up * Mathf.Max(radius, height - radius);
            Vector3 c1, c2;
            ClosestPoints(a, b, p0, p1, out c1, out c2);
            hitPoint = c1;
            return (c1 - c2).sqrMagnitude <= radius * radius;
        }

        private static void ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-6f && e <= 1e-6f) { c1 = p1; c2 = p2; return; }
            if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }

        public abstract void Hit(Vector3 point, Vector3 direction);
    }
}
