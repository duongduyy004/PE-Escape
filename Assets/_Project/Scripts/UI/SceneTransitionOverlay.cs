using KMA.Gameplay.Core;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    // Shown between scenes: the menu's navy field and gold track lines behind a cream menu
    // block holding the bobbing sport badge, an italic "loading" title and a slanted bar.
    public sealed class SceneTransitionOverlay : MonoBehaviour
    {
        const float CardWidth = 640f;
        const float CardHeight = 300f;
        const float CardStroke = 4f;
        static readonly Vector2 CardShadow = new Vector2(6f, -6f);
        const float BadgeScale = 1f;
        const float BadgeBob = 6f;
        const float BadgeBobSpeed = 4f;
        const float TitleSize = 64f;
        const float PercentSize = 30f;
        static readonly Vector2 BarSize = new Vector2(440f, 40f);
        const float BarStroke = 4f;
        const float DotSeconds = .35f;
        const float FadeInSeconds = .1f;
        const float FadeOutSeconds = .2f;
        const string Title = "ĐANG TẢI";

        static SceneTransitionOverlay instance;

        GameObject panel;
        RectTransform badge;
        float badgeY;
        TMP_Text title;
        TMP_Text titleShadow;
        TMP_Text percent;
        RectTransform fill;
        CanvasGroup canvasGroup;
        Slider loadingBar;
        SceneRouter boundRouter;
        bool initialized;
        float shownFor;
        int dots = -1;

        public static SceneTransitionOverlay Instance => instance;
        public bool IsVisible { get; private set; }
        public bool IsBlockingInput => canvasGroup != null && canvasGroup.blocksRaycasts;
        public float Progress => loadingBar != null ? loadingBar.value : 0f;

        public static SceneTransitionOverlay EnsurePersistentInstance()
        {
            if (instance != null)
                return instance;

            var existing = FindFirstObjectByType<SceneTransitionOverlay>();
            if (existing != null)
                return existing;

            return new GameObject(nameof(SceneTransitionOverlay)).AddComponent<SceneTransitionOverlay>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isPlaying)
                EnsurePersistentInstance();
        }

        void Awake() => Initialize();

        // Unity's batch EditMode test runner does not reliably dispatch Awake() for
        // components added at runtime within a single synchronous [Test]. Tests call
        // this directly instead of depending on Awake firing.
        public void InitializeForTest() => Initialize();

        void Initialize()
        {
            if (initialized)
                return;
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            initialized = true;
            instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
            Build();

            TryBindToRouter();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (boundRouter != null)
                Unbind(boundRouter);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBindToRouter();

        void TryBindToRouter()
        {
            if (boundRouter != null)
                return;

            var router = SceneRouter.Instance;
            if (router == null)
                return;

            Bind(router);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void Bind(SceneRouter router)
        {
            if (router == null || boundRouter == router)
                return;
            if (boundRouter != null)
                Unbind(boundRouter);

            router.SceneLoadStarted += Show;
            router.SceneLoadProgressChanged += SetProgress;
            router.SceneLoadCompleted += Hide;
            router.SceneLoadFailed += OnLoadFailed;
            boundRouter = router;
        }

        public void Unbind(SceneRouter router)
        {
            if (router == null)
                return;
            router.SceneLoadStarted -= Show;
            router.SceneLoadProgressChanged -= SetProgress;
            router.SceneLoadCompleted -= Hide;
            router.SceneLoadFailed -= OnLoadFailed;
            if (boundRouter == router)
                boundRouter = null;
        }

        public void Show()
        {
            IsVisible = true;
            shownFor = 0f;
            dots = -1;
            SetProgress(0f);
            UpdateTitle();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            panel.SetActive(true);
        }

        // Input is released at once; the panel then fades out over the newly loaded scene.
        public void Hide()
        {
            IsVisible = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (!Application.isPlaying || canvasGroup.alpha <= 0f)
            {
                canvasGroup.alpha = 0f;
                panel.SetActive(false);
            }
        }

        void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (loadingBar != null)
                loadingBar.value = value;
            if (fill != null)
            {
                fill.anchoredPosition = new Vector2(-(1f - value) * (BarSize.x - BarStroke * 2f), 0f);
                fill.gameObject.SetActive(value > 0f);
            }
            if (percent != null)
                percent.text = VietText.Fix($"{Mathf.FloorToInt(value * 100f)}%");
        }

        void OnLoadFailed(string _) => Hide();

        void Update()
        {
            if (panel == null || !panel.activeSelf)
                return;

            float delta = Time.unscaledDeltaTime;
            if (IsVisible)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, delta / FadeInSeconds);
                shownFor += delta;
                UpdateTitle();
                badge.anchoredPosition = new Vector2(0f, badgeY + Mathf.Sin(shownFor * BadgeBobSpeed) * BadgeBob);
            }
            else
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, delta / FadeOutSeconds);
                if (canvasGroup.alpha <= 0f)
                    panel.SetActive(false);
            }
        }

        void UpdateTitle()
        {
            int count = 1 + Mathf.FloorToInt(shownFor / DotSeconds) % 3;
            if (count == dots)
                return;
            dots = count;
            // Pad with hidden dots so the centred title does not shift as they appear.
            string value = VietText.Fix(Title) + new string('.', count) + "<alpha=#00>" + new string('.', 3 - count);
            title.text = value;
            titleShadow.text = value;
        }

        void Build()
        {
            panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);

            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            var scaler = panel.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            panel.AddComponent<GraphicRaycaster>();
            canvasGroup = panel.AddComponent<CanvasGroup>();
            Color navy = HomeMenuStyle.Navy;

            var backdrop = Rect(panel.transform, "Backdrop");
            Stretch(backdrop, Vector2.zero);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = navy;
            backdropImage.raycastTarget = true;

            // The splash's gold track lines, running behind the card and out past its sides.
            for (int i = 0; i < 3; i++)
            {
                var line = Rect(panel.transform, "TrackLine" + i);
                Place(line, new Vector2((i - 1) * 60f, -40f + i * 34f), new Vector2(1240f - i * 160f, 12f));
                line.localRotation = Quaternion.Euler(0f, 0f, UITheme.Shared.Menu.titleAngle);
                var image = line.gameObject.AddComponent<Image>();
                image.color = MinigameUiTheme.WithAlpha(HomeMenuStyle.Gold, .55f);
                image.raycastTarget = false;
            }

            var card = Rect(panel.transform, "Card");
            Place(card, Vector2.zero, new Vector2(CardWidth, CardHeight));
            var border = card.gameObject.AddComponent<Image>();
            border.color = navy;
            MapPresentationBuilder.UseRoundedSurface(border);
            border.raycastTarget = false;
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = MinigameUiTheme.WithAlpha(Color.black, .45f);
            shadow.effectDistance = CardShadow;
            var face = Rect(card, "Fill");
            Stretch(face, Vector2.one * CardStroke);
            var faceImage = face.gameObject.AddComponent<Image>();
            faceImage.color = UITheme.Shared.TextPrimary;
            MapPresentationBuilder.UseRoundedSurface(faceImage);
            faceImage.raycastTarget = false;

            // The badge sits on the card's top edge, like a medal pinned to it.
            badgeY = CardHeight * .5f;
            badge = ShellArt.CreateBadge(card, "SportBadge");
            Place(badge, new Vector2(0f, badgeY), Vector2.one * ShellTheme.BadgeSize);
            badge.localScale = Vector3.one * BadgeScale;
            foreach (RectTransform part in badge)
                part.anchorMin = part.anchorMax = Vector2.one * .5f;

            titleShadow = Label(card, "TitleShadow", string.Empty, TitleSize, VietFontRole.BodyBold,
                new Vector2(3f, 7f), new Vector2(CardWidth - 40f, 80f), HomeMenuStyle.Gold);
            title = Label(card, "Title", string.Empty, TitleSize, VietFontRole.BodyBold,
                new Vector2(0f, 10f), new Vector2(CardWidth - 40f, 80f), navy);
            titleShadow.fontStyle = title.fontStyle = FontStyles.Italic;

            float barX = -50f;
            CreateBar(card, new Vector2(barX, -80f), navy);
            percent = Label(card, "Percent", "0%", PercentSize, VietFontRole.Hud,
                new Vector2(barX + BarSize.x * .5f + 68f, -80f), new Vector2(100f, BarSize.y + 10f), navy);
            percent.alignment = TextAlignmentOptions.Left;

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            UpdateTitle();
            panel.SetActive(false);
        }

        // The splash bar's construction: a slanted navy track, a masked groove, and a full-width
        // slanted red fill slid in from the left so its leading edge keeps the menu angle.
        void CreateBar(RectTransform parent, Vector2 position, Color navy)
        {
            var track = Rect(parent, "LoadingBar");
            Place(track, position, BarSize);
            int width = Mathf.RoundToInt(BarSize.x);
            int height = Mathf.RoundToInt(BarSize.y);
            var background = track.gameObject.AddComponent<Image>();
            background.sprite = ShellArt.SlantSprite(false, width, height, 0f);
            background.color = navy;
            background.raycastTarget = false;
            loadingBar = track.gameObject.AddComponent<Slider>();
            loadingBar.interactable = false;
            loadingBar.transition = Selectable.Transition.None;
            loadingBar.minValue = 0f;
            loadingBar.maxValue = 1f;

            int stroke = Mathf.RoundToInt(BarStroke);
            var clip = Rect(track, "FillMask");
            Stretch(clip, Vector2.one * BarStroke);
            var groove = clip.gameObject.AddComponent<Image>();
            groove.sprite = ShellArt.SlantSprite(false, width - stroke * 2, height - stroke * 2, 0f);
            groove.color = Color.Lerp(UITheme.Shared.TextPrimary, navy, .15f);
            groove.raycastTarget = false;
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            fill = Rect(clip, "Fill");
            Stretch(fill, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = groove.sprite;
            fillImage.color = HomeMenuStyle.Red;
            fillImage.raycastTarget = false;
        }

        static TMP_Text Label(Transform parent, string name, string value, float size, VietFontRole role,
            Vector2 position, Vector2 box, Color color)
        {
            var rect = Rect(parent, name);
            Place(rect, position, box);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = VietText.Fix(value);
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            VietTypography.Apply(text, role);
            text.color = color;
            return text;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, Vector2 inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = inset;
            rect.offsetMax = -inset;
        }
    }
}
