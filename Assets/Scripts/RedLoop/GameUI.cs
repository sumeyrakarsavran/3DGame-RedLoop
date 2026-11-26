using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RedLoop
{
    // Oyunun bütün arayüzü kodla kurulur (sahnelere dokunmadan her sahnede aynı görünür):
    // HUD (can, zaman parçası, görev, hedef işaretçisi), diyalog kutusu, altyazılar, bölüm kartı,
    // başlık ekranı, duraklatma menüsü, oyun sonu ve final ekranı.
    public class GameUI : MonoBehaviour
    {
        // Renk paleti — oyunun kızıl atmosferine uygun
        private static readonly Color Red = new Color(0.86f, 0.12f, 0.16f, 1f);
        private static readonly Color DeepRed = new Color(0.22f, 0.02f, 0.04f, 1f);
        private static readonly Color Gold = new Color(1f, 0.83f, 0.42f, 1f);
        private static readonly Color Moon = new Color(0.72f, 0.88f, 1f, 1f);
        private static readonly Color Ink = new Color(0.03f, 0.0f, 0.01f, 1f);

        private Font _font;
        private Canvas _canvas;
        private RectTransform _root;

        private Sprite _heartSprite, _diamondSprite, _vignetteSprite, _dawnSprite, _panelSprite, _glowSprite;

        // HUD
        private GameObject _hud;
        private Image[] _hearts;
        private Text _shardText;
        private Image _shardIcon;
        private Text _objectiveText;
        private Text _loopText;
        private RectTransform _marker;
        private Text _markerText;
        private Image _markerImage;
        private GameObject _crosshair;

        // Bölüm kartı / altyazı / efektler
        private CanvasGroup _chapterGroup;
        private Text _chapterNumber, _chapterName;
        private CanvasGroup _subtitleGroup;
        private Text _subtitleText;
        private Image _damageFlash;
        private Image _whiteFlash;
        private Image _fade;
        private Coroutine _subtitleRoutine, _chapterRoutine, _flashRoutine, _whiteRoutine, _pulseRoutine;
        private readonly Queue<Line> _subtitleQueue = new Queue<Line>();

        // Diyalog
        private GameObject _dialogue;
        private Text _dlgSpeaker, _dlgBody, _dlgHint;
        private Image _dlgAccent;
        public bool DialogueOpen { get; private set; }

        // Ekranlar
        private GameObject _titleScreen, _howToPanel, _pauseScreen, _gameOverScreen, _victoryScreen;
        private Text _titlePrologue;
        private CanvasGroup _titleGroup;
        private Text _muteLabel;
        private Button _titleFirst, _pauseFirst, _gameOverFirst, _victoryFirst;
        private CanvasGroup _victoryGroup, _victoryContent;
        private Image _victoryRed, _victoryDawn;
        private Text _victoryClock, _victoryStats;

        private bool _built;

        private void Awake()
        {
            Build();
        }

        // BeforeSceneLoad sırasında eklenen bileşenlerde Unity Awake'i geciktirebiliyor;
        // bu yüzden GameManager arayüzü kullanmadan önce bunu doğrudan çağırır.
        public void Build()
        {
            if (_built) return;
            _built = true;
            try
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                MakeSprites();
                BuildCanvas();
                BuildHud();
                BuildOverlays();
                BuildDialogue();
                BuildTitle();
                BuildPause();
                BuildGameOver();
                BuildVictory();
                HideAllScreens();
                SetHudVisible(false);
            }
            catch (Exception e)
            {
                Debug.LogError("[RedLoop] GameUI.Build FAILED");
                Debug.LogException(e);
            }
        }

        // ================================================================ Kurulum

        private void BuildCanvas()
        {
            var go = new GameObject("RedLoopUI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)go.transform;
        }

        private void BuildHud()
        {
            _hud = NewRect("HUD", _root).gameObject;
            Stretch((RectTransform)_hud.transform);

            // Sol üst: kalpler
            _hearts = new Image[GameManager.MaxLives];
            for (int i = 0; i < _hearts.Length; i++)
            {
                var h = NewImage("Heart" + i, _hud.transform, Red, _heartSprite);
                _hearts[i] = h;
                Place(h.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40 + i * 62, -36), new Vector2(54, 54));
                h.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -3);
            }

            // Sol üst: zaman parçaları
            _shardIcon = NewImage("ShardIcon", _hud.transform, Gold, _diamondSprite);
            Place(_shardIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -106), new Vector2(40, 40));
            _shardText = NewText("ShardText", _hud.transform, "0", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(_shardText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -100), new Vector2(400, 52));
            AddShadow(_shardText);

            // Sağ üst: görev
            var objTitle = NewText("ObjectiveTitle", _hud.transform, "GÖREV", 20, new Color(1f, 0.55f, 0.55f, 0.9f), TextAnchor.UpperRight, FontStyle.Bold);
            Place(objTitle.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -30), new Vector2(700, 30));
            AddShadow(objTitle);
            _objectiveText = NewText("Objective", _hud.transform, "", 28, Color.white, TextAnchor.UpperRight);
            Place(_objectiveText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -58), new Vector2(760, 80));
            AddShadow(_objectiveText);
            _loopText = NewText("Loop", _hud.transform, "", 20, new Color(1f, 1f, 1f, 0.55f), TextAnchor.UpperRight, FontStyle.Italic);
            Place(_loopText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -104), new Vector2(500, 30));
            AddShadow(_loopText);

            // Hedef işaretçisi
            _marker = NewRect("Marker", _hud.transform);
            _marker.anchorMin = _marker.anchorMax = Vector2.zero;
            _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(44, 44);
            var glow = NewImage("Glow", _marker, new Color(1f, 0.8f, 0.4f, 0.35f), _glowSprite);
            Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            _markerImage = NewImage("Diamond", _marker, Gold, _diamondSprite);
            Place(_markerImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            _markerText = NewText("Dist", _marker, "", 20, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            Place(_markerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(200, 30));
            AddShadow(_markerText);
            _marker.gameObject.SetActive(false);

            // Nişangâh (ok atmak için)
            var ch = NewRect("Crosshair", _hud.transform);
            Place(ch, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            var dot = NewImage("Dot", ch, new Color(1f, 0.9f, 0.7f, 0.9f), _glowSprite);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));
            for (int i = 0; i < 4; i++)
            {
                var tick = NewImage("Tick" + i, ch, new Color(1f, 0.85f, 0.6f, 0.75f));
                bool horiz = i < 2;
                float sgn = (i % 2 == 0) ? 1f : -1f;
                Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    horiz ? new Vector2(sgn * 14f, 0f) : new Vector2(0f, sgn * 14f),
                    horiz ? new Vector2(8, 2) : new Vector2(2, 8));
            }
            _crosshair = ch.gameObject;

            // Bölüm kartı
            var chapter = NewRect("Chapter", _root);
            Place(chapter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1400, 220));
            _chapterGroup = chapter.gameObject.AddComponent<CanvasGroup>();
            _chapterGroup.alpha = 0f;
            _chapterGroup.blocksRaycasts = false;
            _chapterNumber = NewText("Num", chapter, "", 30, new Color(1f, 0.6f, 0.6f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(_chapterNumber.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400, 50));
            AddShadow(_chapterNumber);
            _chapterName = NewText("Name", chapter, "", 86, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(_chapterName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(1400, 130));
            AddShadow(_chapterName, 4);
            var line = NewImage("Line", chapter, new Color(0.9f, 0.15f, 0.2f, 0.9f));
            Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 52), new Vector2(420, 3));

            // Altyazı
            var sub = NewRect("Subtitle", _root);
            Place(sub, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(1500, 120));
            _subtitleGroup = sub.gameObject.AddComponent<CanvasGroup>();
            _subtitleGroup.alpha = 0f;
            _subtitleGroup.blocksRaycasts = false;
            var subBg = NewImage("Bg", sub, new Color(0f, 0f, 0f, 0.55f), _panelSprite);
            Stretch(subBg.rectTransform);
            _subtitleText = NewText("Text", sub, "", 30, Color.white, TextAnchor.MiddleCenter);
            Stretch(_subtitleText.rectTransform, 30, 10);
            AddShadow(_subtitleText);
        }

        private void BuildOverlays()
        {
            _damageFlash = NewImage("DamageFlash", _root, new Color(1f, 0f, 0.05f, 0f), _vignetteSprite);
            Stretch(_damageFlash.rectTransform);
            _whiteFlash = NewImage("WhiteFlash", _root, new Color(1f, 0.95f, 0.9f, 0f));
            Stretch(_whiteFlash.rectTransform);
        }

        private void BuildDialogue()
        {
            var box = NewRect("Dialogue", _root);
            Place(box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(1500, 250));
            _dialogue = box.gameObject;

            var bg = NewImage("Bg", box, new Color(0.04f, 0.0f, 0.01f, 0.88f), _panelSprite);
            Stretch(bg.rectTransform);
            var ol = bg.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0.8f, 0.1f, 0.15f, 0.7f);
            ol.effectDistance = new Vector2(2, -2);

            _dlgAccent = NewImage("Accent", box, Red);
            Place(_dlgAccent.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 250));

            _dlgSpeaker = NewText("Speaker", box, "", 30, Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(_dlgSpeaker.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -22), new Vector2(800, 40));
            AddShadow(_dlgSpeaker);

            _dlgBody = NewText("Body", box, "", 32, Color.white, TextAnchor.UpperLeft);
            Place(_dlgBody.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -70), new Vector2(1410, 140));
            _dlgBody.lineSpacing = 1.1f;
            AddShadow(_dlgBody);

            _dlgHint = NewText("Hint", box, "Space / Tık  ›", 20, new Color(1f, 1f, 1f, 0.6f), TextAnchor.LowerRight, FontStyle.Italic);
            Place(_dlgHint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 16), new Vector2(400, 30));

            _dialogue.SetActive(false);
        }

        private void BuildTitle()
        {
            var screen = NewRect("Title", _root);
            Stretch(screen);
            _titleScreen = screen.gameObject;
            _titleGroup = _titleScreen.AddComponent<CanvasGroup>();

            // Arka plan yarı saydam: kızıl dünya arkada görünmeye devam eder.
            var bg = NewImage("Bg", screen, new Color(0.05f, 0f, 0.01f, 0.62f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var vig = NewImage("Vignette", screen, new Color(0f, 0f, 0f, 0.85f), _vignetteSprite);
            Stretch(vig.rectTransform);

            var title = NewText("TitleText", screen, Story.GameTitle, 150, new Color(1f, 0.92f, 0.9f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1600, 190));
            var tOut = title.gameObject.AddComponent<Outline>();
            tOut.effectColor = new Color(0.9f, 0.05f, 0.1f, 0.9f);
            tOut.effectDistance = new Vector2(4, -4);
            AddShadow(title, 8);

            var subtitle = NewText("Subtitle", screen, Story.GameSubtitle, 34, new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -280), new Vector2(1200, 50));
            AddShadow(subtitle);

            _titlePrologue = NewText("Prologue", screen, Story.Prologue, 29, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, FontStyle.Italic);
            Place(_titlePrologue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1300, 330));
            _titlePrologue.lineSpacing = 1.2f;
            AddShadow(_titlePrologue);

            _titleFirst = NewButton(screen, "UYAN", () => { if (_onTitleStart != null) { var cb = _onTitleStart; _onTitleStart = null; cb(); } },
                new Vector2(0.5f, 0f), new Vector2(0, 300));
            NewButton(screen, "NASIL OYNANIR", () => { _howToPanel.SetActive(!_howToPanel.activeSelf); }, new Vector2(0.5f, 0f), new Vector2(0, 220));
            NewButton(screen, "ÇIKIŞ", () => GameManager.QuitGame(), new Vector2(0.5f, 0f), new Vector2(0, 140));

            // Nasıl oynanır paneli
            var how = NewImage("HowTo", screen, new Color(0.05f, 0f, 0.01f, 0.95f), _panelSprite);
            Place(how.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1000, 640));
            how.raycastTarget = true;
            var hOut = how.gameObject.AddComponent<Outline>();
            hOut.effectColor = new Color(0.8f, 0.1f, 0.15f, 0.8f);
            hOut.effectDistance = new Vector2(2, -2);
            var howTitle = NewText("T", how.transform, "NASIL OYNANIR", 36, Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            Place(howTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(900, 50));
            var howText = NewText("Body", how.transform, Story.HowToPlay, 28, Color.white, TextAnchor.UpperCenter);
            Place(howText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(900, 470));
            howText.lineSpacing = 1.2f;
            NewButton(how.transform, "TAMAM", () => _howToPanel.SetActive(false), new Vector2(0.5f, 0f), new Vector2(0, 50), 260);
            _howToPanel = how.gameObject;
            _howToPanel.SetActive(false);
        }

        private void BuildPause()
        {
            var screen = NewRect("Pause", _root);
            Stretch(screen);
            _pauseScreen = screen.gameObject;
            var bg = NewImage("Bg", screen, new Color(0.02f, 0f, 0.01f, 0.75f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            var t = NewText("T", screen, "DURAKLATILDI", 80, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1200, 120));
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.9f, 0.05f, 0.1f, 0.8f);
            o.effectDistance = new Vector2(3, -3);

            _pauseFirst = NewButton(screen, "DEVAM ET", () => GameManager.Instance.Resume(), new Vector2(0.5f, 0.5f), new Vector2(0, 110));
            NewButton(screen, "BÖLÜMÜ BAŞTAN BAŞLAT", () => GameManager.Instance.RestartLevel(), new Vector2(0.5f, 0.5f), new Vector2(0, 30));
            var mute = NewButton(screen, "SES: AÇIK", () =>
            {
                if (SoundFX.Instance == null) return;
                SoundFX.Instance.SetMuted(!SoundFX.Instance.Muted);
                RefreshMuteLabel();
            }, new Vector2(0.5f, 0.5f), new Vector2(0, -50));
            _muteLabel = mute.GetComponentInChildren<Text>();
            NewButton(screen, "ANA MENÜ", () => GameManager.Instance.MainMenu(), new Vector2(0.5f, 0.5f), new Vector2(0, -130));
            NewButton(screen, "OYUNDAN ÇIK", () => GameManager.QuitGame(), new Vector2(0.5f, 0.5f), new Vector2(0, -210));
        }

        private void BuildGameOver()
        {
            var screen = NewRect("GameOver", _root);
            Stretch(screen);
            _gameOverScreen = screen.gameObject;
            var bg = NewImage("Bg", screen, new Color(0.25f, 0f, 0.02f, 0.72f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var vig = NewImage("Vig", screen, new Color(0f, 0f, 0f, 0.95f), _vignetteSprite);
            Stretch(vig.rectTransform);

            var t = NewText("T", screen, Story.GameOverTitle, 104, new Color(1f, 0.85f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1700, 140));
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.8f, 0f, 0.05f, 1f);
            o.effectDistance = new Vector2(4, -4);
            AddShadow(t, 6);
            var s = NewText("S", screen, Story.GameOverSubtitle, 36, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Italic);
            Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(1200, 60));
            AddShadow(s);

            _gameOverFirst = NewButton(screen, "TEKRAR UYAN", () => GameManager.Instance.Retry(), new Vector2(0.5f, 0.5f), new Vector2(0, -40));
            NewButton(screen, "ANA MENÜ", () => GameManager.Instance.MainMenu(), new Vector2(0.5f, 0.5f), new Vector2(0, -120));
        }

        private void BuildVictory()
        {
            var screen = NewRect("Victory", _root);
            Stretch(screen);
            _victoryScreen = screen.gameObject;
            _victoryGroup = _victoryScreen.AddComponent<CanvasGroup>();

            _victoryRed = NewImage("Red", screen, new Color(0.35f, 0.02f, 0.04f, 1f));
            Stretch(_victoryRed.rectTransform);
            _victoryRed.raycastTarget = true;
            _victoryDawn = NewImage("Dawn", screen, new Color(1f, 1f, 1f, 0f), _dawnSprite);
            Stretch(_victoryDawn.rectTransform);
            var vig = NewImage("Vig", screen, new Color(0f, 0f, 0f, 0.45f), _vignetteSprite);
            Stretch(vig.rectTransform);

            _victoryClock = NewText("Clock", screen, "03:33", 170, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(_victoryClock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1000, 220));
            AddShadow(_victoryClock, 6);

            var content = NewRect("Content", screen);
            Stretch(content);
            _victoryContent = content.gameObject.AddComponent<CanvasGroup>();

            var t = NewText("T", content, Story.VictoryTitle, 84, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1600, 110));
            AddShadow(t, 5);
            var body = NewText("Body", content, Story.VictoryBody, 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Italic);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(1500, 150));
            body.lineSpacing = 1.2f;
            AddShadow(body, 3);
            _victoryStats = NewText("Stats", content, "", 28, new Color(1f, 0.95f, 0.85f), TextAnchor.MiddleCenter);
            Place(_victoryStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(1200, 150));
            _victoryStats.lineSpacing = 1.2f;
            AddShadow(_victoryStats, 3);
            var credits = NewText("Credits", content, Story.Credits, 22, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter, FontStyle.Italic);
            Place(credits.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1200, 70));
            AddShadow(credits);

            _victoryFirst = NewButton(content, "YENİDEN OYNA", () => GameManager.Instance.MainMenu(), new Vector2(0.5f, 0f), new Vector2(-230, 140));
            NewButton(content, "ÇIKIŞ", () => GameManager.QuitGame(), new Vector2(0.5f, 0f), new Vector2(230, 140));
        }

        // ================================================================ HUD API

        public void SetHudVisible(bool visible)
        {
            if (_hud != null) _hud.SetActive(visible);
            if (!visible && _marker != null) _marker.gameObject.SetActive(false);
        }

        public void SetLives(int lives)
        {
            if (_hearts == null)
            {
                Debug.LogError("[RedLoop] SetLives: hearts null, built=" + _built + " id=" + GetInstanceID());
                return;
            }
            for (int i = 0; i < _hearts.Length; i++)
            {
                if (_hearts[i] == null)
                {
                    Debug.LogError("[RedLoop] SetLives: heart " + i + " missing (destroyed=" + ReferenceEquals(_hearts[i], null) + ") canvas=" + (_canvas == null ? "gone" : "ok"));
                    return;
                }
            }
            for (int i = 0; i < _hearts.Length; i++)
            {
                bool full = i < lives;
                _hearts[i].color = full ? Red : new Color(0.15f, 0.05f, 0.06f, 0.6f);
                _hearts[i].rectTransform.localScale = Vector3.one;
            }
            if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
            if (lives == 1) _pulseRoutine = StartCoroutine(PulseHeart(_hearts[0].rectTransform));
        }

        public void SetShards(int levelTaken, int levelTotal, bool memories)
        {
            _shardIcon.color = memories ? Moon : Gold;
            if (levelTotal <= 0)
            {
                _shardText.text = "";
                _shardIcon.enabled = false;
                return;
            }
            _shardIcon.enabled = true;
            string label = memories ? "  hatıra" : "  zaman parçası";
            _shardText.text = levelTaken + " / " + levelTotal + "<size=22><color=#ffffffaa>" + label + "</color></size>";
        }

        public void BumpShards()
        {
            StartCoroutine(Bump(_shardIcon.rectTransform, 1.6f));
        }

        public void SetObjective(string text)
        {
            _objectiveText.text = text;
        }

        public void SetLoopCount(int loop)
        {
            _loopText.text = loop > 1 ? "Döngü #" + loop : "";
        }

        private void LateUpdate()
        {
            UpdateMarker();
            if (_crosshair != null)
            {
                var gmx = GameManager.Instance;
                bool on = gmx != null && gmx.State == GameState.Playing;
                if (_crosshair.activeSelf != on) _crosshair.SetActive(on);
            }
            if (DialogueOpen && _dlgHint != null)
            {
                var c = _dlgHint.color;
                c.a = 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                _dlgHint.color = c;
            }
        }

        private void UpdateMarker()
        {
            var gm = GameManager.Instance;
            if (gm == null || _marker == null || !_hud.activeSelf) return;

            Vector3 target;
            bool show = gm.State == GameState.Playing && gm.TryGetObjective(out target);
            Camera cam = Camera.main;
            if (!show || cam == null)
            {
                if (_marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
                return;
            }
            gm.TryGetObjective(out target);
            if (!_marker.gameObject.activeSelf) _marker.gameObject.SetActive(true);

            Vector3 sp = cam.WorldToScreenPoint(target + Vector3.up * 0.6f);
            bool behind = sp.z < 0f;
            if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            float margin = 60f;
            bool offscreen = behind || sp.x < margin || sp.x > Screen.width - margin || sp.y < margin || sp.y > Screen.height - margin;
            if (behind)
            {
                // arkadaysa en yakın kenara yapıştır
                Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 d = new Vector2(sp.x, sp.y) - c;
                if (d.sqrMagnitude < 1f) d = Vector2.down;
                float sx = (Screen.width * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(d.x));
                float sy = (Screen.height * 0.5f - margin) / Mathf.Max(0.001f, Mathf.Abs(d.y));
                d *= Mathf.Min(sx, sy);
                sp.x = c.x + d.x;
                sp.y = c.y + d.y;
            }
            sp.x = Mathf.Clamp(sp.x, margin, Screen.width - margin);
            sp.y = Mathf.Clamp(sp.y, margin, Screen.height - margin);
            _marker.position = new Vector3(sp.x, sp.y, 0f);

            float dist = Vector3.Distance(cam.transform.position, target);
            if (gm.PlayerTransform != null) dist = Vector3.Distance(gm.PlayerTransform.position, target);
            _markerText.text = Mathf.RoundToInt(dist) + " m";
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
            _markerImage.rectTransform.localScale = Vector3.one * (offscreen ? 0.8f : pulse);
            var col = gm.ObjectivesComplete ? Gold : (gm.Level == LevelKind.Forest ? Moon : Gold);
            if (gm.Level == LevelKind.Harbor) col = new Color(1f, 0.35f, 0.35f);
            _markerImage.color = col;
        }

        // ================================================================ Efektler

        public void DamageFlash()
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(_damageFlash, 0.85f, 0.7f));
        }

        public void WhiteFlash(float duration = 1.2f)
        {
            if (_whiteRoutine != null) StopCoroutine(_whiteRoutine);
            _whiteRoutine = StartCoroutine(FlashRoutine(_whiteFlash, 1f, duration));
        }

        private IEnumerator FlashRoutine(Image img, float peak, float duration)
        {
            float t = 0f;
            var c = img.color;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                c.a = peak * (1f - t / duration);
                img.color = c;
                yield return null;
            }
            c.a = 0f;
            img.color = c;
        }

        public void SetFade(float alpha)
        {
            EnsureFade();
            var c = _fade.color;
            c.a = alpha;
            _fade.color = c;
            _fade.raycastTarget = alpha > 0.5f;
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            EnsureFade();
            float start = _fade.color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(start, alpha, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            SetFade(alpha);
        }

        private void EnsureFade()
        {
            if (_fade != null) return;
            _fade = NewImage("Fade", _root, new Color(0f, 0f, 0f, 0f));
            Stretch(_fade.rectTransform);
            _fade.transform.SetAsLastSibling();
        }

        public void ShowChapter(string number, string name)
        {
            _chapterNumber.text = number;
            _chapterName.text = name;
            if (_chapterRoutine != null) StopCoroutine(_chapterRoutine);
            _chapterRoutine = StartCoroutine(ChapterRoutine());
        }

        private IEnumerator ChapterRoutine()
        {
            float t = 0f;
            while (t < 4.5f)
            {
                t += Time.unscaledDeltaTime;
                float a = t < 1f ? t : (t > 3.3f ? Mathf.Max(0f, (4.5f - t) / 1.2f) : 1f);
                _chapterGroup.alpha = a;
                _chapterName.rectTransform.localScale = Vector3.one * (1f + 0.03f * t);
                yield return null;
            }
            _chapterGroup.alpha = 0f;
        }

        public void ShowSubtitle(Line line)
        {
            _subtitleQueue.Enqueue(line);
            if (_subtitleRoutine == null) _subtitleRoutine = StartCoroutine(SubtitleRoutine());
        }

        public void ClearSubtitles()
        {
            _subtitleQueue.Clear();
            if (_subtitleRoutine != null) StopCoroutine(_subtitleRoutine);
            _subtitleRoutine = null;
            _subtitleGroup.alpha = 0f;
        }

        private IEnumerator SubtitleRoutine()
        {
            while (_subtitleQueue.Count > 0)
            {
                var line = _subtitleQueue.Dequeue();
                _subtitleText.text = FormatLine(line);
                float hold = Mathf.Clamp(1.8f + line.text.Length * 0.05f, 2.5f, 6.5f);
                float t = 0f;
                while (t < hold + 0.6f)
                {
                    // Diyalog açıkken altyazı bekler
                    if (!DialogueOpen) t += Time.unscaledDeltaTime;
                    float a = t < 0.3f ? t / 0.3f : (t > hold + 0.3f ? Mathf.Max(0f, (hold + 0.6f - t) / 0.3f) : 1f);
                    _subtitleGroup.alpha = DialogueOpen ? 0f : a;
                    yield return null;
                }
                _subtitleGroup.alpha = 0f;
            }
            _subtitleRoutine = null;
        }

        private string FormatLine(Line line)
        {
            if (string.IsNullOrEmpty(line.speaker)) return "<i>" + line.text + "</i>";
            return "<b><color=#" + ColorUtility.ToHtmlStringRGB(SpeakerColor(line.speaker)) + ">" + line.speaker + ":</color></b>  " + line.text;
        }

        private static Color SpeakerColor(string speaker)
        {
            switch (speaker)
            {
                case Story.Ayla: return new Color(1f, 0.55f, 0.5f);
                case Story.Echo: return new Color(0.75f, 0.6f, 1f);
                case Story.Deniz: return new Color(0.6f, 0.9f, 1f);
                case Story.Memory: return Moon;
                default: return Gold;
            }
        }

        private IEnumerator Bump(RectTransform rt, float scale)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f + (scale - 1f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f));
                rt.localScale = Vector3.one * k;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private IEnumerator PulseHeart(RectTransform rt)
        {
            while (true)
            {
                float k = 1f + 0.15f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 7f));
                rt.localScale = Vector3.one * k;
                yield return null;
            }
        }

        // ================================================================ Diyalog

        public void ShowDialogue(IList<Line> lines, Action onDone)
        {
            StartCoroutine(DialogueRoutine(lines, onDone));
        }

        private static bool AdvancePressed()
        {
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                   Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E) ||
                   Input.GetMouseButtonDown(0);
        }

        private IEnumerator DialogueRoutine(IList<Line> lines, Action onDone)
        {
            DialogueOpen = true;
            _dialogue.SetActive(true);
            if (SoundFX.Instance != null) SoundFX.Instance.SetDuck(true);
            yield return null; // dialoğu açan tıklama ilk satırı atlamasın

            for (int li = 0; li < lines.Count; li++)
            {
                var line = lines[li];
                bool narr = string.IsNullOrEmpty(line.speaker);
                _dlgSpeaker.text = narr ? "" : line.speaker;
                _dlgSpeaker.color = SpeakerColor(line.speaker);
                _dlgAccent.color = narr ? new Color(1f, 1f, 1f, 0.3f) : SpeakerColor(line.speaker);
                _dlgBody.fontStyle = narr ? FontStyle.Italic : FontStyle.Normal;
                _dlgBody.color = narr ? new Color(0.9f, 0.88f, 0.86f) : Color.white;
                _dlgHint.text = (li == lines.Count - 1) ? "Space / Tık  — devam" : "Space / Tık  ›";

                string full = line.text;
                float guard = Time.unscaledTime + 0.15f;
                float acc = 0f;
                int shown = 0;
                _dlgBody.text = "";
                while (shown < full.Length)
                {
                    if (Time.unscaledTime > guard && AdvancePressed()) break;
                    acc += Time.unscaledDeltaTime * 48f;
                    int target = Mathf.Min(full.Length, Mathf.FloorToInt(acc));
                    if (target > shown)
                    {
                        shown = target;
                        _dlgBody.text = full.Substring(0, shown);
                        if (SoundFX.Instance != null && shown % 3 == 0 && full[shown - 1] != ' ')
                            SoundFX.Instance.Play(Sfx.Type, 0.07f, UnityEngine.Random.Range(0.85f, 1.15f));
                    }
                    yield return null;
                }
                _dlgBody.text = full;
                yield return null;
                while (!AdvancePressed()) yield return null;
                if (SoundFX.Instance != null) SoundFX.Instance.Play(Sfx.Click, 0.35f);
                yield return null;
            }

            _dialogue.SetActive(false);
            DialogueOpen = false;
            if (SoundFX.Instance != null) SoundFX.Instance.SetDuck(false);
            if (onDone != null) onDone();
        }

        // ================================================================ Ekranlar

        private Action _onTitleStart;

        public void HideAllScreens()
        {
            _titleScreen.SetActive(false);
            _pauseScreen.SetActive(false);
            _gameOverScreen.SetActive(false);
            _victoryScreen.SetActive(false);
        }

        public void ShowTitle(Action onStart)
        {
            HideAllScreens();
            _onTitleStart = onStart;
            _howToPanel.SetActive(false);
            _titleScreen.SetActive(true);
            StartCoroutine(TitleIntro());
            Select(_titleFirst);
        }

        private IEnumerator TitleIntro()
        {
            _titleGroup.alpha = 0f;
            float t = 0f;
            while (t < 1.5f)
            {
                t += Time.unscaledDeltaTime;
                _titleGroup.alpha = Mathf.Clamp01(t / 1.5f);
                yield return null;
            }
            _titleGroup.alpha = 1f;
        }

        public IEnumerator HideTitle()
        {
            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.unscaledDeltaTime;
                _titleGroup.alpha = 1f - Mathf.Clamp01(t / 0.8f);
                yield return null;
            }
            _titleScreen.SetActive(false);
        }

        public void ShowPause(bool show)
        {
            _pauseScreen.SetActive(show);
            if (show)
            {
                RefreshMuteLabel();
                Select(_pauseFirst);
            }
        }

        private void RefreshMuteLabel()
        {
            if (_muteLabel != null && SoundFX.Instance != null)
                _muteLabel.text = SoundFX.Instance.Muted ? "SES: KAPALI" : "SES: AÇIK";
        }

        public void ShowGameOver()
        {
            HideAllScreens();
            _gameOverScreen.SetActive(true);
            Select(_gameOverFirst);
        }

        public void ShowVictory(string stats)
        {
            HideAllScreens();
            SetHudVisible(false);
            _victoryStats.text = stats;
            _victoryScreen.SetActive(true);
            StartCoroutine(VictoryRoutine());
        }

        private IEnumerator VictoryRoutine()
        {
            _victoryGroup.alpha = 0f;
            _victoryContent.alpha = 0f;
            _victoryContent.interactable = false;
            _victoryContent.blocksRaycasts = false;
            _victoryDawn.color = new Color(1f, 1f, 1f, 0f);
            _victoryClock.text = "03:33";
            _victoryClock.color = new Color(1f, 0.4f, 0.4f, 1f);

            float t = 0f;
            while (t < 1.2f) { t += Time.unscaledDeltaTime; _victoryGroup.alpha = t / 1.2f; yield return null; }
            _victoryGroup.alpha = 1f;

            // Saat... tık... ve ilerliyor
            for (int i = 0; i < 3; i++)
            {
                if (SoundFX.Instance != null) SoundFX.Instance.Play(Sfx.ClockTick, 0.8f);
                yield return new WaitForSecondsRealtime(0.6f);
            }
            _victoryClock.text = "03:34";
            if (SoundFX.Instance != null) SoundFX.Instance.Play(Sfx.Victory, 0.9f);
            StartCoroutine(Bump(_victoryClock.rectTransform, 1.15f));

            // Kızıl gökyüzü şafağa dönüşür
            t = 0f;
            while (t < 4f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 4f);
                _victoryDawn.color = new Color(1f, 1f, 1f, k);
                _victoryClock.color = Color.Lerp(new Color(1f, 0.4f, 0.4f, 1f), Color.white, k);
                if (t > 1.5f) _victoryContent.alpha = Mathf.Clamp01((t - 1.5f) / 2f);
                yield return null;
            }
            _victoryContent.alpha = 1f;
            _victoryContent.interactable = true;
            _victoryContent.blocksRaycasts = true;
            Select(_victoryFirst);
        }

        private static void Select(Button b)
        {
            if (b == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(b.gameObject);
        }

        // ================================================================ Yardımcılar

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r, float padX = 0f, float padY = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(padX, padY);
            r.offsetMax = new Vector2(-padX, -padY);
        }

        private static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        private Image NewImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var r = NewRect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite == _panelSprite && sprite != null)
            {
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        private Text NewText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var r = NewRect(name, parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void AddShadow(Graphic g, float dist = 2f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.8f);
            s.effectDistance = new Vector2(dist, -dist);
        }

        private Button NewButton(Transform parent, string label, Action onClick, Vector2 anchor, Vector2 pos, float width = 440f)
        {
            var img = NewImage("Btn_" + label, parent, new Color(0.75f, 0.1f, 0.14f, 0.95f), _panelSprite);
            img.raycastTarget = true;
            Place(img.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(width, 64));
            var ol = img.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 0.35f, 0.35f, 0.55f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.selectedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.targetGraphic = img;
            b.onClick.AddListener(() =>
            {
                if (SoundFX.Instance != null) SoundFX.Instance.Play(Sfx.Click, 0.7f);
                if (onClick != null) onClick();
            });

            var et = img.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => { if (SoundFX.Instance != null) SoundFX.Instance.Play(Sfx.Hover, 0.3f); });
            et.triggers.Add(entry);

            var t = NewText("Label", img.transform, label, 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform);
            AddShadow(t);
            return b;
        }

        // ---------------------------------------------------------------- Prosedürel sprite'lar

        private void MakeSprites()
        {
            _heartSprite = MakeSprite(64, 64, (u, v) =>
            {
                float x = (u - 0.5f) * 2.6f;
                float y = (v - 0.42f) * 2.6f;
                float a = x * x + y * y - 1f;
                float val = a * a * a - x * x * y * y * y;
                float alpha = Mathf.Clamp01(0.5f - val * 10f);
                float shade = 0.8f + 0.2f * v;
                return new Color(shade, shade, shade, alpha);
            });

            _diamondSprite = MakeSprite(64, 64, (u, v) =>
            {
                float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                float alpha = Mathf.Clamp01((0.46f - d) * 40f);
                float shade = 0.75f + 0.25f * (1f - Mathf.Abs(u - 0.4f) * 2f);
                return new Color(shade, shade, shade, alpha);
            });

            _glowSprite = MakeSprite(64, 64, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a);
            });

            _vignetteSprite = MakeSprite(128, 128, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f / 1.4142f;
                float a = Mathf.SmoothStep(0.35f, 1.05f, d);
                return new Color(1f, 1f, 1f, a);
            });

            _dawnSprite = MakeSprite(4, 256, (u, v) =>
            {
                Color bottom = new Color(1f, 0.55f, 0.3f);
                Color mid = new Color(1f, 0.82f, 0.62f);
                Color top = new Color(0.36f, 0.56f, 0.86f);
                return v < 0.45f ? Color.Lerp(bottom, mid, v / 0.45f) : Color.Lerp(mid, top, (v - 0.45f) / 0.55f);
            });

            // Yuvarlatılmış köşeli panel (9-slice)
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(10f - x, x - 21f));
                    float dy = Mathf.Max(0f, Mathf.Max(10f - y, y - 21f));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(10.5f - d));
                }
            tex.SetPixels(px);
            tex.Apply();
            _panelSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
        }

        private static Sprite MakeSprite(int w, int h, Func<float, float, Color> f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
