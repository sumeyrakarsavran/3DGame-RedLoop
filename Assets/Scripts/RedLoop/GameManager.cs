using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RedLoop
{
    public enum GameState { Boot, Title, Dialogue, Playing, Paused, GameOver, Victory, Transition }

    // Oyunun beyni. Oyun açılırken kendiliğinden oluşur (sahneye eklemeye gerek yok) ve sahneler arasında yaşar.
    // Akış: Başlık ekranı -> Bölüm I (Kızıl Avlu) -> Bölüm II (Yankılar Ormanı) -> Bölüm III (Eski Liman) -> Final.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public const int MaxLives = 20;

        // Düşmanlar vb. sadece oyun gerçekten oynanırken çalışsın diye.
        public static bool IsGameplayActive
        {
            get { return Instance == null || Instance.State == GameState.Playing; }
        }

        public GameState State { get; private set; }
        public LevelKind Level { get; private set; }
        public int Lives { get; private set; }
        public int ShardsCollected { get; private set; }
        public int MemoriesCollected { get; private set; }
        public int Deaths { get; private set; }
        public float PlayTime { get; private set; }

        public int LevelShardsTotal { get; private set; }
        public int LevelShardsTaken { get; private set; }
        public int LevelMemoriesTotal { get; private set; }
        public int LevelMemoriesTaken { get; private set; }
        public int LevelShardsRequired { get; private set; }
        public float LevelPlayTime { get; private set; }
        public int GuardiansDefeated { get; private set; }
        public int ShadowsDefeated { get; private set; }
        public int ArrowsShot { get; private set; }

        public bool ObjectivesComplete
        {
            get { return LevelShardsTaken >= LevelShardsRequired && LevelMemoriesTaken >= LevelMemoriesTotal; }
        }

        public Transform PlayerTransform { get { return _player; } }

        private SoundFX _sfx;
        private GameUI _ui;
        private LevelDirector _director;
        private LoadScene _portal;

        private Transform _player;
        private CharacterController _cc;
        private ThirdPersonController _tpc;
        private StarterAssetsInputs _inputs;
        private Animator _playerAnim;
        private Renderer[] _playerRenderers;
        private Vector3 _spawnPos;
        private Quaternion _spawnRot;

        private readonly List<Transform> _collectibles = new List<Transform>();
        private readonly HashSet<LevelKind> _seenIntro = new HashSet<LevelKind>();

        private int _setupHandle = -1;
        private int _buildIndex;
        private bool _showTitle = true;
        private bool _skipIntro;
        private bool _objectivesAnnounced;
        private int _shardsAtLevelStart, _memoriesAtLevelStart;
        private float _invulnUntil;
        private float _nextLockMsg;
        private float _nextBeat;
        private bool _wasGrounded = true;
        private Coroutine _blink;

        // ================================================================ Açılış

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[RedLoop]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            State = GameState.Boot;
            Lives = MaxLives;
            _sfx = gameObject.AddComponent<SoundFX>();
            _ui = gameObject.AddComponent<GameUI>();
            _sfx.Init();
            _ui.Build();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void Start()
        {
            var s = SceneManager.GetActiveScene();
            if (_setupHandle != s.handle) SetupScene(s);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (_setupHandle == scene.handle) return;
            SetupScene(scene);
        }

        // ================================================================ Sahne kurulumu

        private void SetupScene(Scene scene)
        {
            _setupHandle = scene.handle;
            StopAllCoroutines();
            _blink = null;
            Time.timeScale = 1f;

            _buildIndex = scene.buildIndex;
            Level = DetectLevel(scene);

            var pgo = GameObject.FindGameObjectWithTag("Player");
            _player = pgo != null ? pgo.transform : null;
            _cc = pgo != null ? pgo.GetComponent<CharacterController>() : null;
            _tpc = pgo != null ? pgo.GetComponent<ThirdPersonController>() : null;
            _inputs = pgo != null ? pgo.GetComponent<StarterAssetsInputs>() : null;
            _playerAnim = pgo != null ? pgo.GetComponent<Animator>() : null;
            _playerRenderers = pgo != null ? pgo.GetComponentsInChildren<SkinnedMeshRenderer>(true) : new Renderer[0];
            if (pgo != null)
            {
                _spawnPos = _player.position;
                _spawnRot = _player.rotation;
                var collide = pgo.GetComponent<Collide>();
                if (collide != null) collide.HideLegacyUI();
                var coins = pgo.GetComponent<CoinCollection>();
                if (coins != null) coins.HideLegacyUI();
            }

            EnsureEventSystem();

            _portal = FindObjectOfType<LoadScene>();

            _collectibles.Clear();
            foreach (var c in GameObject.FindGameObjectsWithTag("coin")) _collectibles.Add(c.transform);

            var dgo = new GameObject("[LevelDirector]");
            _director = dgo.AddComponent<LevelDirector>();
            _director.Init(this, Level, _player, _portal, _collectibles);

            CountCollectibles();
            // Kızıl Avlu: kapı için parçaların %60'ı yeterli (geri kalanı bonus puan)
            LevelShardsRequired = Level == LevelKind.Courtyard ? Mathf.CeilToInt(LevelShardsTotal * 0.6f) : 0;
            LevelPlayTime = 0f;

            // Yay, bloom, atmosfer, ortam sesi
            if (pgo != null && pgo.GetComponent<PlayerBow>() == null) pgo.AddComponent<PlayerBow>();
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<RedLoopBloom>() == null) cam.gameObject.AddComponent<RedLoopBloom>();
            Atmosphere.Setup(Level, _player, dgo.transform);
            _sfx.PlayAmbience(Level);
            Lives = MaxLives;
            LevelShardsTaken = 0;
            LevelMemoriesTaken = 0;
            _shardsAtLevelStart = ShardsCollected;
            _memoriesAtLevelStart = MemoriesCollected;
            _invulnUntil = 0f;
            _wasGrounded = true;
            _objectivesAnnounced = ObjectivesComplete;
            if (_objectivesAnnounced) _director.OpenPortal(false);

            _ui.HideAllScreens();
            _ui.ClearSubtitles();
            _ui.SetLives(Lives);
            RefreshCounter();
            _ui.SetObjective(Story.Objective(Level, ObjectivesComplete));
            _ui.SetLoopCount(Deaths + 1);
            _ui.SetFade(1f);

            SetState(GameState.Transition);
            _sfx.Preload(MusicFor(Level));
            StartCoroutine(BeginLevel());
        }

        private void CountCollectibles()
        {
            int shards = 0, memories = 0;
            foreach (var t in _collectibles)
            {
                if (t == null) continue;
                var c = t.GetComponent<Collectible>();
                if (c != null && c.kind == CollectibleKind.Memory) memories++;
                else if (c != null && c.kind == CollectibleKind.Heart) continue;
                else shards++;
            }
            LevelShardsTotal = shards;
            LevelMemoriesTotal = memories;
        }

        private static LevelKind DetectLevel(Scene scene)
        {
            string n = scene.name.ToLowerInvariant();
            if (n.Contains("robot")) return LevelKind.Courtyard;
            if (n == "kk" || n.Contains("coconut") || n.Contains("palm")) return LevelKind.Forest;
            if (n.Contains("dock") || n.Contains("port")) return LevelKind.Harbor;
            switch (scene.buildIndex)
            {
                case 0: return LevelKind.Courtyard;
                case 1: return LevelKind.Forest;
                case 2: return LevelKind.Harbor;
            }
            return LevelKind.Unknown;
        }

        private static MusicTrack MusicFor(LevelKind k)
        {
            switch (k)
            {
                case LevelKind.Courtyard: return MusicTrack.Courtyard;
                case LevelKind.Forest: return MusicTrack.Forest;
                case LevelKind.Harbor: return MusicTrack.Harbor;
                default: return MusicTrack.Title;
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private IEnumerator BeginLevel()
        {
            yield return null; // sahnedeki Start() metotları çalışsın

            bool title = _showTitle && _buildIndex == 0;
            _showTitle = false;

            if (title)
            {
                SetState(GameState.Title);
                _sfx.PlayMusic(MusicTrack.Title, 2f);
                _ui.ShowTitle(OnTitleStart);
                yield return _ui.FadeTo(0f, 1.5f);
                yield break;
            }

            _sfx.PlayMusic(MusicFor(Level));
            yield return _ui.FadeTo(0f, 1.2f);
            BeginIntroOrPlay();
        }

        private void OnTitleStart()
        {
            StartCoroutine(TitleToGame());
        }

        private IEnumerator TitleToGame()
        {
            _sfx.Play(Sfx.Whoosh, 0.6f);
            yield return _ui.HideTitle();
            _sfx.PlayMusic(MusicFor(Level));
            BeginIntroOrPlay();
        }

        private void BeginIntroOrPlay()
        {
            if (!_skipIntro && !_seenIntro.Contains(Level))
            {
                _seenIntro.Add(Level);
                SetState(GameState.Dialogue);
                _ui.ShowDialogue(Story.Intro(Level), () => StartPlaying(true));
            }
            else
            {
                StartPlaying(!_skipIntro);
                if (_skipIntro) _ui.ShowSubtitle(new Line(Story.Narrator, Story.RetryCaption(Deaths + 1)));
            }
            _skipIntro = false;
        }

        private void StartPlaying(bool chapterCard)
        {
            SetState(GameState.Playing);
            if (chapterCard) _ui.ShowChapter(Story.ChapterNumber(Level), Story.ChapterName(Level));
            if (_director != null) _director.OnPlayStarted();
        }

        // ================================================================ Durum

        private void SetState(GameState s)
        {
            State = s;
            bool playing = s == GameState.Playing;
            switch (s)
            {
                case GameState.Playing:
                case GameState.Transition:
                case GameState.Boot:
                    Time.timeScale = 1f;
                    break;
                case GameState.GameOver:
                    break; // yavaş çekimi GameOverRoutine yönetir
                default:
                    Time.timeScale = 0f;
                    break;
            }
            SetPlayerControl(playing);
            SetCursorLocked(playing);
            _ui.SetHudVisible(s == GameState.Playing || s == GameState.Paused || s == GameState.Dialogue);
        }

        private void SetPlayerControl(bool on)
        {
            if (_tpc != null) _tpc.enabled = on;
            if (_inputs != null)
            {
                _inputs.move = Vector2.zero;
                _inputs.look = Vector2.zero;
                _inputs.jump = false;
                _inputs.sprint = false;
                _inputs.cursorInputForLook = on;
                _inputs.cursorLocked = on;
            }
            if (!on && _playerAnim != null && _playerAnim.isActiveAndEnabled && _tpc != null)
            {
                _playerAnim.SetFloat("Speed", 0f);
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Resume();
            }

            if (State != GameState.Playing) return;

            PlayTime += Time.unscaledDeltaTime;
            LevelPlayTime += Time.deltaTime;

            if (_player != null)
            {
                // Haritadan düşme
                if (_player.position.y < _spawnPos.y - 30f) Fell();

                // Zıplama sesi
                bool grounded = _tpc == null || _tpc.Grounded;
                if (_wasGrounded && !grounded && _cc != null && _cc.velocity.y > 0.5f) _sfx.Play(Sfx.Jump, 0.3f, Random.Range(0.95f, 1.08f));
                _wasGrounded = grounded;
            }

            // Son canda kalp atışı
            if (Lives <= 3 && Lives > 0 && Time.unscaledTime >= _nextBeat)
            {
                _nextBeat = Time.unscaledTime + 1.15f;
                _sfx.Play(Sfx.Heartbeat, 0.5f);
            }
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Paused);
            _sfx.SetDuck(true);
            _ui.ShowPause(true);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            _ui.ShowPause(false);
            _sfx.SetDuck(false);
            SetState(GameState.Playing);
        }

        // ================================================================ Oynanış

        public void Say(Line line)
        {
            _ui.ShowSubtitle(line);
        }

        public void RegisterCollectible(Transform t)
        {
            if (t != null && !_collectibles.Contains(t)) _collectibles.Add(t);
        }

        public void Collect(GameObject go)
        {
            if (go == null || State != GameState.Playing) return;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (!col.enabled) return; // aynı parça iki kez sayılmasın
                col.enabled = false;
            }

            var c = go.GetComponent<Collectible>();
            bool memory = c != null && c.kind == CollectibleKind.Memory;
            if (c != null && c.kind == CollectibleKind.Heart)
            {
                _collectibles.Remove(go.transform);
                Vector3 hp = go.transform.position;
                Destroy(go);
                FX.Burst(hp, new Color(1f, 0.3f, 0.45f), 24, 3f, 0.15f, 0.7f);
                _sfx.Play(Sfx.Heal, 0.85f);
                if (Lives < MaxLives)
                {
                    Lives++;
                    _ui.SetLives(Lives);
                    _ui.ShowSubtitle(new Line(Story.Ayla, "Bir nefes daha... Devam edebilirim."));
                }
                return;
            }
            Vector3 pos = go.transform.position;
            _collectibles.Remove(go.transform);

            if (memory)
            {
                MemoriesCollected++;
                LevelMemoriesTaken++;
                _sfx.Play(Sfx.Memory, 0.9f);
                FX.PopLight(pos, new Color(0.7f, 0.88f, 1f), 9f, 6f);
                FX.Burst(pos, new Color(0.7f, 0.88f, 1f), 40, 3f, 0.15f, 1.1f);
                int idx = Mathf.Clamp(LevelMemoriesTaken - 1, 0, Story.Memories.Length - 1);
                _ui.ShowSubtitle(Story.Memories[idx]);
            }
            else
            {
                ShardsCollected++;
                LevelShardsTaken++;
                _sfx.Play(Sfx.Coin, 0.8f, 1f + 0.04f * LevelShardsTaken);
                FX.PopLight(pos, new Color(1f, 0.82f, 0.4f), 6f, 5f);
                FX.Burst(pos, new Color(1f, 0.8f, 0.35f), 22, 3.5f, 0.12f, 0.6f);
                Line l;
                if (LevelShardsRequired > 0 && Story.ShardLine(LevelShardsTaken, LevelShardsRequired, out l)) _ui.ShowSubtitle(l);
            }

            RefreshCounter();
            _ui.BumpShards();
            Destroy(go);

            if (ObjectivesComplete && !_objectivesAnnounced)
            {
                _objectivesAnnounced = true;
                _sfx.Play(Sfx.PortalOpen, 0.85f);
                if (_director != null) _director.OpenPortal(true);
                _ui.ShowSubtitle(Story.PortalOpened(Level));
                _ui.SetObjective(Story.Objective(Level, true));
            }
        }

        private void RefreshCounter()
        {
            if (LevelMemoriesTotal > 0) _ui.SetShards(LevelMemoriesTaken, LevelMemoriesTotal, true);
            else _ui.SetShards(LevelShardsTaken, LevelShardsRequired > 0 ? LevelShardsRequired : LevelShardsTotal, false);
        }

        public void TryUsePortal(LoadScene portal)
        {
            if (State != GameState.Playing) return;
            if (ObjectivesComplete)
            {
                StartCoroutine(PortalTransition());
                return;
            }
            if (Time.unscaledTime >= _nextLockMsg)
            {
                _nextLockMsg = Time.unscaledTime + 4f;
                _sfx.Play(Sfx.Locked, 0.7f);
                int remaining = LevelMemoriesTotal > 0
                    ? LevelMemoriesTotal - LevelMemoriesTaken
                    : LevelShardsRequired - LevelShardsTaken;
                _ui.ShowSubtitle(new Line(Story.Narrator, Story.PortalLocked(Level, remaining)));
            }
        }

        private IEnumerator PortalTransition()
        {
            SetState(GameState.Transition);
            _sfx.Play(Sfx.Whoosh, 0.9f);
            _ui.WhiteFlash(0.9f);
            int next = _buildIndex + 1;
            if (_buildIndex < 0 || next >= SceneManager.sceneCountInBuildSettings)
            {
                // Build listesinde başka bölüm yoksa yine de güzel bir sonla bitsin.
                yield return VictoryRoutine();
                yield break;
            }
            yield return LoadLevelRoutine(next);
        }

        // Hasar aldıysa true döner.
        public bool Damage(Vector3 from)
        {
            if (State != GameState.Playing) return false;
            if (Time.time < _invulnUntil) return false;
            _invulnUntil = Time.time + 2f;

            Lives = Mathf.Max(0, Lives - 1);
            _sfx.Play(Sfx.Hurt, 0.9f);
            _ui.DamageFlash();
            _ui.SetLives(Lives);

            if (Lives <= 0)
            {
                StartCoroutine(GameOverRoutine());
            }
            else
            {
                if (Lives == 1) _ui.ShowSubtitle(Story.LowHealth);
                if (_blink != null) StopCoroutine(_blink);
                _blink = StartCoroutine(Blink(1.4f));
            }
            return true;
        }

        private void Fell()
        {
            Lives = Mathf.Max(0, Lives - 1);
            _sfx.Play(Sfx.Hurt, 0.9f);
            _ui.DamageFlash();
            _ui.SetLives(Lives);
            if (Lives <= 0)
            {
                StartCoroutine(GameOverRoutine());
                return;
            }
            Respawn();
            _ui.ShowSubtitle(Story.Fell);
            _invulnUntil = Time.time + 2f;
            if (_blink != null) StopCoroutine(_blink);
            _blink = StartCoroutine(Blink(2f));
        }

        private void Respawn()
        {
            if (_player == null) return;
            bool ccOn = _cc != null && _cc.enabled;
            if (_cc != null) _cc.enabled = false;
            _player.SetPositionAndRotation(_spawnPos, _spawnRot);
            if (_cc != null) _cc.enabled = ccOn;
        }

        private IEnumerator Blink(float duration)
        {
            float end = Time.unscaledTime + duration;
            while (Time.unscaledTime < end && (State == GameState.Playing || State == GameState.Paused))
            {
                bool vis = Mathf.FloorToInt(Time.unscaledTime * 14f) % 2 == 0;
                SetRenderers(vis);
                yield return null;
            }
            SetRenderers(true);
            _blink = null;
        }

        private void SetRenderers(bool visible)
        {
            if (_playerRenderers == null) return;
            foreach (var r in _playerRenderers) if (r != null) r.enabled = visible;
        }

        private IEnumerator GameOverRoutine()
        {
            State = GameState.GameOver;
            Deaths++;
            if (_blink != null) { StopCoroutine(_blink); _blink = null; }
            SetRenderers(true);
            SetPlayerControl(false);
            _ui.ClearSubtitles();
            _sfx.PlayMusic(MusicTrack.None, 1f);
            _sfx.Play(Sfx.GameOver, 0.9f);
            if (_director != null) _director.StopHazards();

            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(1.4f);
            Time.timeScale = 0f;

            SetCursorLocked(false);
            _ui.SetHudVisible(false);
            _ui.ShowGameOver();
        }

        // Oyun sonu ekranındaki "Tekrar Uyan": bölüm baştan, toplananlar geri alınır.
        public void Retry()
        {
            if (State == GameState.Transition) return;
            ShardsCollected = _shardsAtLevelStart;
            MemoriesCollected = _memoriesAtLevelStart;
            _skipIntro = true;
            StartCoroutine(LoadLevelRoutine(_buildIndex));
        }

        public void RestartLevel()
        {
            if (State == GameState.Transition) return;
            _ui.ShowPause(false);
            _sfx.SetDuck(false);
            Retry();
        }

        public void MainMenu()
        {
            if (State == GameState.Transition) return;
            _ui.ShowPause(false);
            _sfx.SetDuck(false);
            ShardsCollected = 0;
            MemoriesCollected = 0;
            Deaths = 0;
            PlayTime = 0f;
            _seenIntro.Clear();
            _showTitle = true;
            _skipIntro = false;
            StartCoroutine(LoadLevelRoutine(0));
        }

        private IEnumerator LoadLevelRoutine(int index)
        {
            SetState(GameState.Transition);
            _ui.ClearSubtitles();
            yield return _ui.FadeTo(1f, 0.9f);
            _ui.HideAllScreens();
            if (index < 0 || index >= SceneManager.sceneCountInBuildSettings) index = 0;
            _setupHandle = -1;
            SceneManager.LoadScene(index);
        }

        // ================================================================ Final

        public void ReachFinalGoal()
        {
            if (State != GameState.Playing) return;
            if (_director != null) _director.StopHazards();
            _ui.ClearSubtitles();
            SetState(GameState.Dialogue);
            _sfx.PlayMusic(MusicTrack.Title, 2f);
            _ui.ShowDialogue(Story.Finale, () => StartCoroutine(VictoryRoutine()));
        }

        private IEnumerator VictoryRoutine()
        {
            SetState(GameState.Victory);
            _sfx.PlayMusic(MusicTrack.None, 0.4f);
            _sfx.Play(Sfx.Shatter, 1f);
            if (_director != null) _director.ShatterHeart();
            _ui.WhiteFlash(2.2f);
            yield return new WaitForSecondsRealtime(1.3f);

            float best = PlayerPrefs.GetFloat("redloop_best", 0f);
            bool newBest = best <= 0f || PlayTime < best;
            if (newBest) { PlayerPrefs.SetFloat("redloop_best", PlayTime); PlayerPrefs.Save(); best = PlayTime; }

            string stats =
                "Toplanan zaman parçası: <b>" + ShardsCollected + "</b>\n" +
                "Bulunan hatıra: <b>" + MemoriesCollected + " / " + Story.Memories.Length + "</b>\n" +
                "Devrilen muhafız: <b>" + GuardiansDefeated + "</b>   Dağıtılan gölge: <b>" + ShadowsDefeated + "</b>\n" +
                "Yaşanan döngü: <b>" + (Deaths + 1) + "</b>\n" +
                "Süre: <b>" + FormatTime(PlayTime) + "</b>" + (newBest ? "  <color=#ffe08a>(yeni rekor!)</color>" : "  (rekor " + FormatTime(best) + ")");

            _sfx.PlayMusic(MusicTrack.Ending, 3f);
            _ui.ShowVictory(stats);
        }

        private static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        // ================================================================ Savaş

        public void OnArrowShot() { ArrowsShot++; }

        public void OnShadowDefeated() { ShadowsDefeated++; }

        public void OnGuardianDefeated(Vector3 position)
        {
            GuardiansDefeated++;
            if (GuardiansDefeated == 1) _ui.ShowSubtitle(new Line(Story.Echo, "Bir muhafız daha özgür. Döngü onları da bırakıyor."));
            // %45 ihtimalle can kalbi bırakır
            if (Random.value < 0.45f)
            {
                var heart = FX.HeartPickup(position + Vector3.up * 1f);
                RegisterCollectible(heart.transform);
            }
        }

        // ================================================================ Hedef işaretçisi

        public bool TryGetObjective(out Vector3 position)
        {
            position = Vector3.zero;
            if (_director != null && _director.GoalTransform != null)
            {
                position = _director.GoalTransform.position;
                return true;
            }

            if (!ObjectivesComplete)
            {
                _collectibles.RemoveAll(t => t == null);
                Vector3 from = _player != null ? _player.position : Vector3.zero;
                float best = float.MaxValue;
                bool found = false;
                foreach (var t in _collectibles)
                {
                    var col = t.GetComponent<Collider>();
                    if (col != null && !col.enabled) continue;
                    var kind = t.GetComponent<Collectible>();
                    if (kind != null && kind.kind == CollectibleKind.Heart) continue;
                    float d = (t.position - from).sqrMagnitude;
                    if (d < best) { best = d; position = t.position; found = true; }
                }
                if (found) return true;
            }

            if (_portal != null)
            {
                position = _portal.transform.position;
                return true;
            }
            return false;
        }

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
