using System.Collections.Generic;
using UnityEngine;

namespace RedLoop
{
    // Her bölüme özel kurulum (sahne dosyalarına dokunmadan, çalışma anında):
    //  Bölüm I  — Kızıl Avlu: zaman parçalarına hafif altın parıltı, merdiven tepesindeki kapıya ışık sütunu.
    //  Bölüm II — Yankılar Ormanı: 4 hatıra ışığı yerleştirilir, geçit onlar toplanınca açılır.
    //  Bölüm III— Eski Liman: Döngünün Kalbi (hedef), ışık sütunu ve gökten yağan kızıl küreler.
    public class LevelDirector : MonoBehaviour
    {
        private static readonly Color LockedColor = new Color(0.9f, 0.1f, 0.12f);
        private static readonly Color OpenColor = new Color(1f, 0.8f, 0.35f);
        private static readonly Color MemoryColor = new Color(0.65f, 0.85f, 1f);

        public Transform GoalTransform { get; private set; }

        private GameManager _gm;
        private LevelKind _level;
        private Transform _player;
        private LoadScene _portal;

        private GameObject _portalBeam;
        private Light _portalLight;
        private GameObject _heartVisual;
        private GameObject _heartBeam;
        private LoopHeart _heart;

        public void Init(GameManager gm, LevelKind level, Transform player, LoadScene portal, List<Transform> collectibles)
        {
            _gm = gm;
            _level = level;
            _player = player;
            _portal = portal;

            // Kapı işareti
            if (_portal != null)
            {
                Vector3 p = _portal.transform.position;
                _portalBeam = FX.Beam(p + Vector3.down * 1.2f, LockedColor, 14f, 0.18f);
                var lgo = new GameObject("PortalLight");
                lgo.transform.position = p + Vector3.up * 0.5f;
                _portalLight = lgo.AddComponent<Light>();
                _portalLight.type = LightType.Point;
                _portalLight.color = LockedColor;
                _portalLight.range = 7f;
                _portalLight.intensity = 1.5f;
            }

            switch (level)
            {
                case LevelKind.Courtyard:
                    foreach (var c in collectibles)
                    {
                        if (c == null) continue;
                        FX.AddLight(c.gameObject, new Color(1f, 0.8f, 0.4f), 2.5f, 1.2f);
                    }
                    break;

                case LevelKind.Forest:
                    SpawnMemories(collectibles, 4);
                    break;

                case LevelKind.Harbor:
                    SetupHarbor();
                    break;
            }
        }

        public void OnPlayStarted()
        {
            if (_heart != null) _heart.Activate();
        }

        public void OpenPortal(bool announce)
        {
            if (_portalBeam != null) FX.Glow(_portalBeam.GetComponent<Renderer>(), OpenColor, 6f);
            if (_portalLight != null)
            {
                _portalLight.color = OpenColor;
                _portalLight.intensity = 4f;
                _portalLight.range = 10f;
            }
            if (announce && _portal != null) FX.PopLight(_portal.transform.position + Vector3.up, OpenColor, 18f, 8f, 1.5f);
        }

        public void StopHazards()
        {
            if (_heart != null) _heart.StopAll();
        }

        public void ShatterHeart()
        {
            StopHazards();
            if (_heartVisual != null)
            {
                FX.PopLight(_heartVisual.transform.position, new Color(1f, 0.9f, 0.8f), 40f, 12f, 2.5f);
                Destroy(_heartVisual);
            }
            if (_heartBeam != null) Destroy(_heartBeam);
        }

        // ---------------------------------------------------------------- Bölüm II

        private void SpawnMemories(List<Transform> collectibles, int count)
        {
            if (_player == null) return;
            Vector3 origin = _player.position;
            Vector3 toward = _portal != null ? _portal.transform.position - origin : Vector3.forward;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f) toward = Vector3.forward;
            float baseAngle = Mathf.Atan2(toward.z, toward.x) * Mathf.Rad2Deg + 180f; // geçidin tersine doğru keşfe çıksın

            float[] radii = { 9f, 12f, 10f, 14f };
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 18; attempt++)
                {
                    float spread = (i - (count - 1) * 0.5f) * 60f;
                    float jitter = (attempt % 2 == 0 ? 1f : -1f) * (attempt / 2) * 17f;
                    float ang = (baseAngle + spread + jitter) * Mathf.Deg2Rad;
                    float r = radii[i % radii.Length] - (attempt / 6) * 2f;
                    Vector3 probe = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;

                    Vector3 point;
                    if (!FindGround(probe, origin.y, out point)) continue;

                    var mem = CreateMemory(point + Vector3.up * 1.1f, placed);
                    collectibles.Add(mem.transform);
                    placed++;
                    break;
                }
            }
        }

        private static bool FindGround(Vector3 probe, float refY, out Vector3 point)
        {
            point = probe;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(probe.x, refY + 40f, probe.z), Vector3.down, out hit, 90f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            if (!(hit.collider is TerrainCollider)) return false;      // ağacın / binanın üstüne düşmesin
            if (hit.normal.y < 0.8f) return false;                     // dik yamaç olmasın
            if (Mathf.Abs(hit.point.y - refY) > 4f) return false;      // ulaşılabilir yükseklikte olsun
            if (Physics.CheckSphere(hit.point + Vector3.up * 1.1f, 0.7f, ~0, QueryTriggerInteraction.Ignore)) return false;
            point = hit.point;
            return true;
        }

        private GameObject CreateMemory(Vector3 position, int index)
        {
            var go = FX.Primitive(PrimitiveType.Sphere, "Hatira_" + (index + 1), position, Vector3.one * 0.45f, true);
            go.tag = "coin";
            var sc = go.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                sc.radius = 2.2f; // ölçek 0.45 → dünyada ~1 m
            }
            FX.Glow(go.GetComponent<Renderer>(), MemoryColor, 3f);
            FX.AddLight(go, MemoryColor, 7f, 2.5f);
            var col = go.AddComponent<Collectible>();
            col.kind = CollectibleKind.Memory;
            col.index = index;
            var bob = go.AddComponent<Bob>();
            bob.height = 0.25f;
            bob.speed = 1.6f;
            bob.spin = 30f;
            return go;
        }

        // ---------------------------------------------------------------- Bölüm III

        private void SetupHarbor()
        {
            var killer = GameObject.Find("killer");
            Vector3 goalPos;
            if (killer != null)
            {
                // Eski "You Win" yazısı yerine artık gerçek final var
                for (int i = 0; i < killer.transform.childCount; i++)
                    killer.transform.GetChild(i).gameObject.SetActive(false);

                var sc = killer.GetComponent<SphereCollider>();
                if (sc == null) sc = killer.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = Mathf.Max(sc.radius, 1.5f);
                if (killer.GetComponent<FinalGoal>() == null) killer.AddComponent<FinalGoal>();
                GoalTransform = killer.transform;
                goalPos = killer.transform.position;
            }
            else
            {
                // Yedek: hedef bulunamazsa oyuncunun önüne yerleştir
                Vector3 origin = _player != null ? _player.position : Vector3.zero;
                Vector3 fwd = _player != null ? _player.forward : Vector3.forward;
                goalPos = origin + fwd * 25f;
                RaycastHit hit;
                if (Physics.Raycast(goalPos + Vector3.up * 40f, Vector3.down, out hit, 90f, ~0, QueryTriggerInteraction.Ignore))
                    goalPos = hit.point + Vector3.up;
                var g = new GameObject("DongununKalbi_Hedef");
                g.transform.position = goalPos;
                var sc = g.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 2f;
                g.AddComponent<FinalGoal>();
                GoalTransform = g.transform;
            }

            _heartVisual = FX.Primitive(PrimitiveType.Sphere, "DongununKalbi", goalPos + Vector3.up * 1.8f, Vector3.one * 1.1f);
            FX.Glow(_heartVisual.GetComponent<Renderer>(), new Color(1f, 0.05f, 0.08f), 6f);
            var light = FX.AddLight(_heartVisual, new Color(1f, 0.1f, 0.1f), 16f, 4f);
            _heartBeam = FX.Beam(goalPos, new Color(1f, 0.1f, 0.12f), 70f, 0.35f);

            _heart = gameObject.AddComponent<LoopHeart>();
            _heart.Init(_gm, _player, _heartVisual.transform, light);
        }
    }
}
