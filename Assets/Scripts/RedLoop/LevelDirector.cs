using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RedLoop
{
    // Her bölüme özel kurulum (sahne dosyalarına dokunmadan, çalışma anında):
    //  Bölüm I  — Kızıl Avlu: muhafızlara can, ulaşılabilir yerlere fazladan zaman parçası, kapıya ışık sütunu.
    //  Bölüm II — Yankılar Ormanı: 4 hatıra ışığı, süzülen gölgeler, bonus parçalar.
    //  Bölüm III— Eski Liman: Döngünün Kalbi (hedef), etrafında dönen halkalar, gökten yağan kızıl küreler, bonus parçalar.
    public class LevelDirector : MonoBehaviour
    {
        private static readonly Color LockedColor = new Color(0.9f, 0.1f, 0.12f);
        private static readonly Color OpenColor = new Color(1f, 0.8f, 0.35f);
        private static readonly Color MemoryColor = new Color(0.65f, 0.85f, 1f);

        public Transform GoalTransform { get; private set; }

        // Son bölümde kazanma noktaları: kalbin konduğu yer ve orijinal sahnedeki hedef (oda).
        public readonly List<Vector3> WinPoints = new List<Vector3>();
        public const float WinHorizontal = 5f;
        public const float WinVertical = 6f;

        public bool IsInWinZone(Vector3 p)
        {
            if (GoalTransform != null && Check(GoalTransform.position, p)) return true;
            foreach (var w in WinPoints) if (Check(w, p)) return true;
            return false;
        }

        private static bool Check(Vector3 w, Vector3 p)
        {
            Vector3 d = p - w;
            float dy = Mathf.Abs(d.y);
            d.y = 0f;
            return d.magnitude <= WinHorizontal && dy <= WinVertical;
        }

        private GameManager _gm;
        private LevelKind _level;
        private Transform _player;
        private LoadScene _portal;

        private GameObject _portalBeam;
        private Light _portalLight;
        private GameObject _heartVisual;
        private GameObject _heartBeam;
        private LoopHeart _heart;
        private readonly List<Vector3> _used = new List<Vector3>();

        public void Init(GameManager gm, LevelKind level, Transform player, LoadScene portal, List<Transform> collectibles)
        {
            _gm = gm;
            _level = level;
            _player = player;
            _portal = portal;

            if (_portal != null)
            {
                Vector3 p = _portal.transform.position;
                _portalBeam = FX.Beam(p + Vector3.down * 1.2f, LockedColor, 14f, 0.5f);
                _portalBeam.transform.SetParent(transform, true);
                var lgo = new GameObject("PortalLight");
                lgo.transform.SetParent(transform, false);
                lgo.transform.position = p + Vector3.up * 0.5f;
                _portalLight = lgo.AddComponent<Light>();
                _portalLight.type = LightType.Point;
                _portalLight.color = LockedColor;
                _portalLight.range = 7f;
                _portalLight.intensity = 1.5f;
            }

            foreach (var c in collectibles) if (c != null) _used.Add(c.position);

            switch (level)
            {
                case LevelKind.Courtyard:
                    foreach (var c in collectibles)
                    {
                        if (c == null) continue;
                        FX.AddLight(c.gameObject, new Color(1f, 0.8f, 0.4f), 2.5f, 1.2f);
                    }
                    SetupGuardians();
                    SpawnBonusShardsOnNavMesh(collectibles, 12);
                    break;

                case LevelKind.Forest:
                    SpawnMemories(collectibles, 4);
                    SpawnWisps(4);
                    SpawnBonusShardsOnGround(collectibles, 8, 6f, 22f);
                    break;

                case LevelKind.Harbor:
                    BuildRuntimeNavMesh();
                    SetupHarbor();
                    SpawnBonusShardsOnNavMesh(collectibles, 8);
                    break;
            }
        }

        // ---------------------------------------------------------------- Çalışma anı NavMesh (Eski Liman)

        private NavMeshDataInstance _navInstance;
        private bool _hasNav;

        // Limanda hazır NavMesh yok. Fizik collider'larından oyun anında yürünebilir alan haritası çıkarıyoruz;
        // böylece hedefin ve parçaların oyuncunun gerçekten yürüyerek ulaşabileceği yerde olduğunu doğrulayabiliyoruz.
        private void BuildRuntimeNavMesh()
        {
            if (_player == null) return;
            NavMeshHit existing;
            if (NavMesh.SamplePosition(_player.position, out existing, 3f, NavMesh.AllAreas)) return; // zaten var

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.45f;
            settings.agentSlope = 45f;

            var center = _player.position;
            var killer = GameObject.Find("killer");
            if (killer != null) center = (center + killer.transform.position) * 0.5f;
            var bounds = new Bounds(center, new Vector3(220f, 80f, 220f));

            var sources = new List<NavMeshBuildSource>();
            int mask = ~(1 << PlayerLayer);
            NavMeshBuilder.CollectSources(bounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            // tetikleyicileri (görünmez alanlar) dışarıda bırak
            sources.RemoveAll(s => s.component is Collider && ((Collider)s.component).isTrigger);
            if (sources.Count == 0) return;

            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return;
            _navInstance = NavMesh.AddNavMeshData(data);
            _hasNav = _navInstance.valid;
        }

        private void OnDestroy()
        {
            if (_hasNav) _navInstance.Remove();
        }

        // İstenen noktaya en yakın, oyuncunun yürüyerek varabildiği nokta.
        private bool FindReachablePoint(Vector3 desired, out Vector3 result)
        {
            result = desired;
            if (_player == null) return false;
            NavMeshHit start;
            if (!NavMesh.SamplePosition(_player.position, out start, 4f, NavMesh.AllAreas)) return false;

            var path = new NavMeshPath();
            bool found = false;
            float best = float.MaxValue;
            const float step = 3f;
            const int half = 10; // ±30 m
            for (int x = -half; x <= half; x++)
            {
                for (int z = -half; z <= half; z++)
                {
                    Vector3 probe = desired + new Vector3(x * step, 0f, z * step);
                    NavMeshHit h;
                    if (!NavMesh.SamplePosition(probe, out h, 14f, NavMesh.AllAreas)) continue;
                    float score = (h.position - desired).sqrMagnitude;
                    if (score >= best) continue;
                    if (!NavMesh.CalculatePath(start.position, h.position, NavMesh.AllAreas, path)) continue;
                    if (path.status != NavMeshPathStatus.PathComplete) continue;
                    best = score;
                    result = h.position;
                    found = true;
                }
            }
            return found;
        }

        public void OnPlayStarted()
        {
            if (_heart != null) _heart.Activate();
        }

        public void OpenPortal(bool announce)
        {
            if (_portalBeam != null)
            {
                FX.Ghost(_portalBeam.GetComponent<Renderer>(), new Color(OpenColor.r, OpenColor.g, OpenColor.b, 0.5f), 2f);
                var core = _portalBeam.transform.Find("BeamCore");
                if (core != null) FX.Glow(core.GetComponent<Renderer>(), OpenColor, 7f);
            }
            if (_portalLight != null)
            {
                _portalLight.color = OpenColor;
                _portalLight.intensity = 4f;
                _portalLight.range = 10f;
            }
            if (announce && _portal != null)
            {
                FX.PopLight(_portal.transform.position + Vector3.up, OpenColor, 18f, 8f, 1.5f);
                FX.Burst(_portal.transform.position + Vector3.up, OpenColor, 60, 7f, 0.2f, 1.4f);
            }
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
                Vector3 p = _heartVisual.transform.position;
                FX.PopLight(p, new Color(1f, 0.9f, 0.8f), 40f, 12f, 2.5f);
                FX.Burst(p, new Color(1f, 0.3f, 0.2f), 120, 12f, 0.35f, 2f);
                FX.Burst(p, new Color(1f, 0.9f, 0.7f), 60, 6f, 0.25f, 2.5f);
                Destroy(_heartVisual);
            }
            if (_heartBeam != null) Destroy(_heartBeam);
        }

        // ---------------------------------------------------------------- Ortak yerleştirme

        private bool FarFromUsed(Vector3 p, float minDist)
        {
            foreach (var u in _used) if ((u - p).sqrMagnitude < minDist * minDist) return false;
            return true;
        }

        private static bool FindGround(Vector3 probe, float refY, float maxDy, bool terrainOnly, int ignoreLayer, out Vector3 point)
        {
            point = probe;
            RaycastHit hit;
            int mask = ignoreLayer >= 0 ? ~(1 << ignoreLayer) : ~0;
            if (!Physics.Raycast(new Vector3(probe.x, refY + 25f, probe.z), Vector3.down, out hit, 60f, mask, QueryTriggerInteraction.Ignore))
                return false;
            if (terrainOnly && !(hit.collider is TerrainCollider)) return false;  // ağacın/binanın üstüne düşmesin
            if (hit.normal.y < 0.8f) return false;                               // dik yamaç olmasın
            if (Mathf.Abs(hit.point.y - refY) > maxDy) return false;             // ulaşılabilir yükseklikte olsun
            if (Physics.CheckSphere(hit.point + Vector3.up * 1.1f, 0.6f, mask, QueryTriggerInteraction.Ignore)) return false;
            point = hit.point;
            return true;
        }

        private int PlayerLayer { get { return _player != null ? _player.gameObject.layer : -1; } }

        // ---------------------------------------------------------------- Bölüm I

        private void SetupGuardians()
        {
            foreach (var ai in FindObjectsOfType<NPCAI>())
            {
                if (ai.GetComponent<Guardian>() == null) ai.gameObject.AddComponent<Guardian>();
            }
        }

        // Robotların kullandığı NavMesh üzerinde, oyuncunun yürüyerek ulaşabildiği yerlere parça koyar.
        private void SpawnBonusShardsOnNavMesh(List<Transform> collectibles, int count)
        {
            if (_player == null) return;
            GameObject template = null;
            float templateHeight = 1f;
            foreach (var c in collectibles)
            {
                if (c == null || c.GetComponent<Collectible>() != null) continue;
                template = c.gameObject;
                RaycastHit h;
                if (Physics.Raycast(c.position, Vector3.down, out h, 5f, ~(1 << PlayerLayer), QueryTriggerInteraction.Ignore))
                    templateHeight = Mathf.Clamp(c.position.y - h.point.y, 0.5f, 1.5f);
                break;
            }

            NavMeshHit startHit;
            if (!NavMesh.SamplePosition(_player.position, out startHit, 4f, NavMesh.AllAreas))
            {
                SpawnBonusShardsOnGround(collectibles, count, 5f, 20f);
                return;
            }

            var robots = FindObjectsOfType<NPCAI>();
            var path = new NavMeshPath();
            int placed = 0;
            for (int attempt = 0; attempt < count * 25 && placed < count; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float r = Random.Range(5f, 32f);
                Vector3 probe = startHit.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                NavMeshHit nh;
                if (!NavMesh.SamplePosition(probe, out nh, 2f, NavMesh.AllAreas)) continue;
                if (!FarFromUsed(nh.position, 3.5f)) continue;

                bool nearRobot = false;
                foreach (var rb in robots)
                    if (rb != null && (rb.transform.position - nh.position).sqrMagnitude < 4f * 4f) { nearRobot = true; break; }
                if (nearRobot) continue;

                if (!NavMesh.CalculatePath(startHit.position, nh.position, NavMesh.AllAreas, path)) continue;
                if (path.status != NavMeshPathStatus.PathComplete) continue;

                Vector3 pos = nh.position + Vector3.up * templateHeight;
                GameObject shard;
                if (template != null)
                {
                    shard = Instantiate(template, pos, template.transform.rotation);
                    shard.name = "Coin (bonus " + placed + ")";
                    shard.tag = "coin";
                    var col = shard.GetComponent<Collider>();
                    if (col != null) col.enabled = true;
                    if (shard.GetComponentInChildren<Light>() == null) FX.AddLight(shard, new Color(1f, 0.8f, 0.4f), 2.5f, 1.2f);
                }
                else
                {
                    shard = FX.Shard(pos);
                }
                collectibles.Add(shard.transform);
                _used.Add(pos);
                placed++;
            }
        }

        // Zemine ışın atarak (NavMesh yoksa) bonus parça yerleştirir.
        private void SpawnBonusShardsOnGround(List<Transform> collectibles, int count, float minR, float maxR)
        {
            if (_player == null) return;
            Vector3 origin = _player.position;
            bool terrainOnly = _level == LevelKind.Forest;
            int placed = 0;
            for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                Vector3 probe = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(minR, maxR);
                Vector3 g;
                if (!FindGround(probe, origin.y, 2.5f, terrainOnly, PlayerLayer, out g)) continue;
                if (!FarFromUsed(g, 3.5f)) continue;
                var s = FX.Shard(g + Vector3.up * 1f);
                collectibles.Add(s.transform);
                _used.Add(g);
                placed++;
            }
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
                    if (!FindGround(probe, origin.y, 4f, true, PlayerLayer, out point)) continue;

                    var mem = CreateMemory(point + Vector3.up * 1.1f, placed);
                    collectibles.Add(mem.transform);
                    _used.Add(point);
                    placed++;
                    break;
                }
            }
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
            FX.Glow(go.GetComponent<Renderer>(), MemoryColor, 4f);
            FX.AddLight(go, MemoryColor, 7f, 2.5f);

            // etrafında süzülen ışık zerreleri
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = MemoryColor;
            main.gravityModifier = -0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var em = ps.emission;
            em.rateOverTime = 30f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            var colo = ps.colorOverLifetime;
            colo.enabled = true;
            colo.color = FX.FadeGradient(Color.white);
            go.GetComponent<ParticleSystemRenderer>().material = FX.AdditiveMaterial(Color.white, 2f);
            ps.Play();

            var col = go.AddComponent<Collectible>();
            col.kind = CollectibleKind.Memory;
            col.index = index;
            var bob = go.AddComponent<Bob>();
            bob.height = 0.25f;
            bob.speed = 1.6f;
            bob.spin = 30f;
            return go;
        }

        private void SpawnWisps(int count)
        {
            if (_player == null) return;
            Vector3 origin = _player.position;
            int placed = 0;
            for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                Vector3 probe = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(16f, 26f);
                Vector3 g;
                if (!FindGround(probe, origin.y, 4f, true, PlayerLayer, out g)) continue;
                ShadowWisp.Create(g + Vector3.up * 1.3f, _player).transform.SetParent(transform, true);
                placed++;
            }
        }

        // ---------------------------------------------------------------- Bölüm III

        private void SetupHarbor()
        {
            var killer = GameObject.Find("killer");
            Vector3 desired;
            if (killer != null)
            {
                // Eski "You Win" yazısı yerine artık gerçek final var
                for (int i = 0; i < killer.transform.childCount; i++)
                    killer.transform.GetChild(i).gameObject.SetActive(false);
                // Orijinal nokta havada asılı ve ulaşılamıyordu: artık sadece "istenen yer" olarak kullanılıyor
                var oldCol = killer.GetComponent<Collider>();
                if (oldCol != null) oldCol.enabled = false;
                desired = killer.transform.position;
                WinPoints.Add(desired);
            }
            else
            {
                Vector3 origin = _player != null ? _player.position : Vector3.zero;
                Vector3 fwd = _player != null ? _player.forward : Vector3.forward;
                desired = origin + fwd * 25f;
            }

            Vector3 goalPos;
            if (!FindReachablePoint(desired, out goalPos))
            {
                // NavMesh kurulamadıysa: istenen yerin altındaki zemin
                goalPos = desired;
                RaycastHit hit;
                if (Physics.Raycast(desired + Vector3.up * 2f, Vector3.down, out hit, 60f, ~(1 << PlayerLayer), QueryTriggerInteraction.Ignore))
                    goalPos = hit.point;
            }
            goalPos += Vector3.up * 0.2f;

            var g = new GameObject("DongununKalbi_Hedef");
            g.transform.SetParent(transform, false);
            g.transform.position = goalPos;
            var sc = g.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.2f;
            g.AddComponent<FinalGoal>();
            GoalTransform = g.transform;

            _heartVisual = FX.Primitive(PrimitiveType.Sphere, "DongununKalbi", goalPos + Vector3.up * 1.8f, Vector3.one * 1.1f);
            FX.Glow(_heartVisual.GetComponent<Renderer>(), new Color(1f, 0.05f, 0.08f), 6f);
            var light = FX.AddLight(_heartVisual, new Color(1f, 0.1f, 0.1f), 16f, 4f);

            // Kalbin etrafında farklı eksenlerde dönen üç ince halka
            for (int i = 0; i < 3; i++)
            {
                var pivot = new GameObject("HalkaPivot" + i);
                pivot.transform.SetParent(_heartVisual.transform, false);
                pivot.transform.localRotation = Quaternion.Euler(i * 60f, i * 45f, 0f);
                var spin = pivot.AddComponent<Bob>();
                spin.height = 0f;
                spin.spin = 40f + i * 25f;
                var ring = FX.Primitive(PrimitiveType.Cylinder, "Halka" + i, Vector3.zero, Vector3.one);
                ring.transform.SetParent(pivot.transform, false);
                ring.transform.localScale = new Vector3(2.2f, 0.01f, 2.2f);
                FX.Ghost(ring.GetComponent<Renderer>(), new Color(1f, 0.2f, 0.15f, 0.35f), 1.5f);
            }

            // Kalpten yükselen kızıl kıvılcımlar
            var ps = _heartVisual.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new Color(1f, 0.25f, 0.15f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 60f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;
            var colo = ps.colorOverLifetime;
            colo.enabled = true;
            colo.color = FX.FadeGradient(Color.white);
            _heartVisual.GetComponent<ParticleSystemRenderer>().material = FX.AdditiveMaterial(Color.white, 2.5f);
            ps.Play();

            _heartBeam = FX.Beam(goalPos, new Color(1f, 0.1f, 0.12f), 70f, 0.8f);

            _heart = gameObject.AddComponent<LoopHeart>();
            _heart.Init(_gm, _player, _heartVisual.transform, light);

            Debug.Log("[RedLoop] Liman hedefi: istenen=" + desired + " konulan=" + goalPos +
                      " oyuncu=" + (_player != null ? _player.position.ToString() : "-") + " navmesh=" + _hasNav);
        }

        // Güvence: oyuncu belli bir süre kalbe ulaşamazsa kalp gökten oyuncunun yakınına iner.
        private bool _descending;

        public void DescendToPlayer()
        {
            if (_descending || _player == null || GoalTransform == null || _heartVisual == null) return;
            _descending = true;
            StartCoroutine(DescendRoutine());
        }

        private System.Collections.IEnumerator DescendRoutine()
        {
            // oyuncunun baktığı yönde, oyuncuyla aynı yükseklikte bir zemin bul
            Vector3 target = _player.position;
            int mask = ~(1 << PlayerLayer);
            Vector3 fwd = _player.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            float[] dists = { 7f, 5f, 9f, 4f, 3f };
            foreach (var d in dists)
            {
                for (int a = 0; a < 8; a++)
                {
                    Vector3 dir = Quaternion.Euler(0f, a * 45f, 0f) * fwd;
                    Vector3 probe = _player.position + dir * d + Vector3.up * 1.5f;
                    RaycastHit hit;
                    if (Physics.Raycast(_player.position + Vector3.up * 1.2f, dir, d, mask, QueryTriggerInteraction.Ignore)) continue; // arada duvar var
                    if (Physics.Raycast(probe, Vector3.down, out hit, 4f, mask, QueryTriggerInteraction.Ignore) &&
                        hit.normal.y > 0.7f && Mathf.Abs(hit.point.y - _player.position.y) < 1.2f)
                    {
                        target = hit.point;
                        goto Found;
                    }
                }
            }
            Found:

            if (SoundFX.Instance != null) SoundFX.Instance.Play(Sfx.PortalOpen, 0.8f, 0.7f);
            if (_gm != null) _gm.Say(new Line(Story.Echo, "Kalp seni hissetti... Sana geliyor! Ona dokun, Ayla!"));

            Vector3 from = _heartVisual.transform.position;
            Vector3 to = target + Vector3.up * 1.8f;
            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 3f);
                Vector3 p = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 6f;
                if (_heartVisual != null) _heartVisual.transform.position = p;
                yield return null;
            }

            GoalTransform.position = target + Vector3.up * 0.2f;
            if (_heartBeam != null) _heartBeam.transform.position = target + Vector3.up * 35f;
            FX.Burst(to, new Color(1f, 0.2f, 0.15f), 60, 8f, 0.25f, 1.2f);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.Impact, target, 1f, 0.6f);
            Debug.Log("[RedLoop] Kalp oyuncunun yanına indi: " + target);
        }
    }
}
