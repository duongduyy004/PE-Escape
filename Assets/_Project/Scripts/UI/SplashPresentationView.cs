using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    // Uses the menu's actual badge, typography library and slant rasterizer.
    public sealed class SplashPresentationView : MonoBehaviour
    {
        public Slider LoadingBar { get; private set; }
        public TMP_Text Status { get; private set; }
        public TMP_Text Percent { get; private set; }
        RectTransform safeArea;
        RectTransform layout;
        RectTransform fill;
        RectTransform kma;
        TMP_Text titleTop;
        TMP_Text titleKma;
        CanvasGroup brand;
        UITheme.SplashStyle style;
        float introElapsed;
        Sprite gradient;
        RectTransform versionFooter;

        public static SplashPresentationView Build(Transform root)
        {
            if (root.GetComponent<Canvas>() == null) return null;
            var style = UITheme.Shared.Splash;
            var existing = root.Find("SplashSafeArea/SplashLayout")?.GetComponent<SplashPresentationView>();
            if (existing != null)
            {
                existing.style = style;
                existing.safeArea = (RectTransform)existing.transform.parent;
                existing.layout = (RectTransform)existing.transform;
                existing.brand = existing.GetComponent<CanvasGroup>();
                if (Application.isPlaying) existing.brand.alpha = 0f;
                existing.kma = existing.transform.Find("TitleKMA") as RectTransform;
                existing.titleTop = existing.transform.Find("TitleTop")?.GetComponent<TMP_Text>();
                existing.titleKma = existing.kma?.GetComponent<TMP_Text>();
                if (existing.titleTop != null) existing.titleTop.text = VietText.Fix("CHẠY TRỐN");
                if (existing.titleKma != null) existing.titleKma.text = VietText.Fix("THỂ CHẤT");
                ShellArt.RestyleBadge(existing.transform.Find("SportBadge") as RectTransform);
                existing.Status = existing.transform.Find("LoadingStatus")?.GetComponent<TMP_Text>();
                existing.Percent = existing.transform.Find("ProgressPercent")?.GetComponent<TMP_Text>();
                existing.LoadingBar = existing.GetComponentInChildren<Slider>(true);
                existing.fill = existing.transform.Find("LoadingBar/FillMask/Fill") as RectTransform;
                existing.versionFooter = existing.safeArea.Find("VersionFooter") as RectTransform;
                existing.SetDisplayedProgress(0f);
                return existing;
            }
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = style.referenceResolution;
                scaler.matchWidthOrHeight = .5f;
            }
            var art = root.Find("Illustration")?.GetComponent<Image>();
            // Legacy scene objects remain available for scene references but do not render.
            foreach (Transform child in root)
                if (child != (art != null ? art.transform : null)) child.gameObject.SetActive(false);
            if (art != null)
            {
                art.gameObject.SetActive(true);
                art.transform.SetAsFirstSibling();
                art.color = Color.white;
                art.raycastTarget = false;
                art.preserveAspect = false;
                var fitter = art.GetComponent<AspectRatioFitter>() ?? art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = art.sprite != null ? art.sprite.rect.width / art.sprite.rect.height : 16f / 9f;
                art.rectTransform.anchorMin = Vector2.zero;
                art.rectTransform.anchorMax = Vector2.one;
                art.rectTransform.offsetMin = art.rectTransform.offsetMax = Vector2.zero;
            }
            var overlay = Rect(root, "SplashNavyGradient");
            Stretch(overlay);
            var overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.sprite = Gradient(style);
            overlayImage.raycastTarget = true;

            var safe = Rect(root, "SplashSafeArea");
            Stretch(safe);
            var panel = Rect(safe, "SplashLayout");
            Place(panel, Vector2.zero, style.contentSize);
            var view = panel.gameObject.AddComponent<SplashPresentationView>();
            view.style = style;
            view.gradient = overlayImage.sprite;
            view.safeArea = safe;
            view.layout = panel;
            view.brand = panel.gameObject.AddComponent<CanvasGroup>();
            view.brand.alpha = 0f;
            var badge = ShellArt.CreateBadge(panel, "SportBadge");
            Place(badge, new Vector2(0f, style.badgeY), Vector2.one * ShellTheme.BadgeSize);
            badge.localScale = Vector3.one * (style.badgeSize / ShellTheme.BadgeSize);
            // The menu positions its badge parts from the left edge of its panel.
            // Center those same parts here so the artwork and shine share one axis.
            foreach (RectTransform part in badge)
                part.anchorMin = part.anchorMax = Vector2.one * .5f;
            var top = Label(panel, "TitleTop", "CHẠY TRỐN", 48f * style.titleScale,
                VietFontRole.Title, new Vector2(0f, style.titleTopY), style.titleTopSize);
            var title = Label(panel, "TitleKMA", "THỂ CHẤT", 48f * style.titleScale,
                VietFontRole.Title, new Vector2(0f, style.titleKmaY), style.titleKmaSize);
            foreach (var text in new[] { top, title })
            {
                text.fontStyle = FontStyles.Italic;
                text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ShellTheme.TitleAngle);
            }
            view.kma = title.rectTransform;
            view.titleTop = top;
            view.titleKma = title;
            for (int i = 0; i < 3; i++)
            {
                var line = Rect(panel, "TrackLine" + i);
                Place(line, new Vector2((i - 1) * style.stripeStep.x, style.stripesY + i * style.stripeStep.y),
                    new Vector2(style.stripeSize.x - i * style.stripeShorten, style.stripeSize.y));
                line.localRotation = Quaternion.Euler(0f, 0f, ShellTheme.TitleAngle);
                var image = line.gameObject.AddComponent<Image>();
                image.color = ShellTheme.Gold;
                image.raycastTarget = false;
            }
            var slogan = Label(panel, "Slogan", "Hành trình rèn luyện thể chất", style.sloganSize,
                VietFontRole.Body, new Vector2(0f, style.sloganY), style.sloganBox);
            var shadow = slogan.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UITheme.Shared.ShadowColor;
            shadow.effectDistance = style.sloganShadow;
            view.Status = Label(panel, "LoadingStatus", "Đang chuẩn bị...", style.statusSize,
                VietFontRole.Hud, new Vector2(-style.trackSize.x * .125f, style.statusY),
                new Vector2(style.trackSize.x * .75f, style.statusHeight));
            view.Status.alignment = TextAlignmentOptions.Left;
            view.Percent = Label(panel, "ProgressPercent", "0%", style.statusSize,
                VietFontRole.Hud, new Vector2(style.trackSize.x * .375f, style.statusY),
                new Vector2(style.trackSize.x * .25f, style.statusHeight));
            view.Percent.alignment = TextAlignmentOptions.Right;
            view.CreateTrack();
            view.versionFooter = Footer(safe, "VersionFooter", "v" + Application.version, true, style);
            view.Resize();
            view.SetDisplayedProgress(0f);
            return view;
        }

        void CreateTrack()
        {
            var track = Rect(layout, "LoadingBar");
            Place(track, new Vector2(0f, style.trackY), style.trackSize);
            int width = Mathf.RoundToInt(style.trackSize.x);
            int height = Mathf.RoundToInt(style.trackSize.y);
            var sprite = ShellArt.SlantSprite(false, width, height, style.trackBorder);
            var background = track.gameObject.AddComponent<Image>();
            background.sprite = sprite;
            background.color = MinigameUiTheme.WithAlpha(ShellTheme.Ink, style.trackOpacity);
            background.raycastTarget = false;
            LoadingBar = track.gameObject.AddComponent<Slider>();
            LoadingBar.interactable = false;
            LoadingBar.transition = Selectable.Transition.None;
            LoadingBar.minValue = 0f;
            LoadingBar.maxValue = 1f;
            var clip = Rect(track, "FillMask");
            Stretch(clip, Vector2.one * style.trackBorder);
            var maskImage = clip.gameObject.AddComponent<Image>();
            maskImage.sprite = ShellArt.SlantSprite(false,
                width - Mathf.RoundToInt(style.trackBorder * 2f), height - Mathf.RoundToInt(style.trackBorder * 2f), 0f);
            maskImage.raycastTarget = false;
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            fill = Rect(clip, "Fill");
            Stretch(fill);
            var image = fill.gameObject.AddComponent<Image>();
            image.sprite = maskImage.sprite;
            image.color = ShellTheme.AccentRed;
            image.raycastTarget = false;
            // Slide the full slanted image under the mask: its leading edge keeps
            // the menu angle at every percentage and never crosses the white rim.
            var rim = Rect(track, "WhiteRim");
            Stretch(rim);
            var border = rim.gameObject.AddComponent<Image>();
            border.sprite = ShellArt.SlantSprite(true, width, height, style.trackBorder);
            border.color = UITheme.Shared.Card;
            border.raycastTarget = false;
        }

        public void SetDisplayedProgress(float progress)
        {
            if (fill == null) return;
            float width = style.trackSize.x - style.trackBorder * 2f;
            fill.anchoredPosition = new Vector2(-(1f - Mathf.Clamp01(progress)) * width, 0f);
            fill.gameObject.SetActive(progress > 0f);
        }

        void LateUpdate()
        {
            if (style == null) return;
            Resize();
            if (!Application.isEditor && !UnityEngine.Rendering.SplashScreen.isFinished) return;
            introElapsed += Mathf.Min(Time.unscaledDeltaTime, style.maxAnimationDelta);
            float t = Mathf.Clamp01(introElapsed / style.introDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            brand.alpha = eased;
            layout.anchoredPosition = Vector2.down * (style.introSlide * (1f - eased));
            kma.localScale = Vector3.one * Mathf.Lerp(style.kmaStartScale, 1f, eased);
        }

        void Resize()
        {
            if (safeArea == null) return;
            var dimensions = new Vector2Int(Screen.width, Screen.height);
            if (dimensions.x > 0 && dimensions.y > 0)
            {
                Rect area = Screen.safeArea;
                safeArea.anchorMin = new Vector2(area.xMin / dimensions.x, area.yMin / dimensions.y);
                safeArea.anchorMax = new Vector2(area.xMax / dimensions.x, area.yMax / dimensions.y);
                safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;

            }
            Vector2 available = safeArea.rect.size - style.contentPadding * 2f;
            float scale = Mathf.Clamp(Mathf.Min(available.x / style.contentSize.x,
                available.y / style.contentSize.y), 0f, 1f);
            layout.localScale = Vector3.one * scale;
            float titleScale = style.titleScale * HomeMenuResponsive.ScaleFor(safeArea.rect.size);
            float titleSize = Mathf.Max(24f, 48f * titleScale);
            titleTop.fontSize = titleSize;
            titleKma.fontSize = titleSize;
            titleTop.fontSizeMax = titleKma.fontSizeMax = titleSize;
            titleTop.fontSizeMin = Mathf.Min(titleTop.fontSizeMin, titleTop.fontSizeMax);
            titleKma.fontSizeMin = Mathf.Min(titleKma.fontSizeMin, titleKma.fontSizeMax);
            float footerWidth = Mathf.Max(0f, Mathf.Min(style.footerSize.x, safeArea.rect.width * .5f - style.footerInset.x * 2f));
            if (versionFooter != null) versionFooter.sizeDelta = new Vector2(footerWidth, style.footerSize.y);
        }

        void OnDestroy()
        {
            if (gradient == null) return;
            Destroy(gradient.texture);
            Destroy(gradient);
        }

        static RectTransform Footer(RectTransform parent, string name, string value, bool right, UITheme.SplashStyle style)
        {
            var text = Label(parent, name, value, style.footerFontSize, VietFontRole.Body, Vector2.zero, style.footerSize);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            rect.pivot = new Vector2(right ? 1f : 0f, 0f);
            rect.anchoredPosition = new Vector2(right ? -style.footerInset.x : style.footerInset.x, style.footerInset.y);
            // Each footer owns half of the safe width even on narrow screens.
            rect.sizeDelta = new Vector2(Mathf.Min(style.footerSize.x, ((RectTransform)parent.parent).rect.width * .5f - style.footerInset.x * 2f), style.footerSize.y);
            text.enableAutoSizing = true;
            text.fontSizeMax = style.footerFontSize;
            text.fontSizeMin = style.footerFontSize;
            text.alignment = right ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;
            text.color = MinigameUiTheme.WithAlpha(UITheme.Shared.Card, style.footerOpacity);
            return rect;
        }

        static TMP_Text Label(Transform parent, string name, string value, float size, VietFontRole role,
            Vector2 position, Vector2 box)
        {
            var rect = Rect(parent, name);
            Place(rect, position, box);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = VietText.Fix(value);
            text.fontSize = size;
            text.color = UITheme.Shared.Card;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            VietTypography.Apply(text, role);
            return text;
        }

        static Sprite Gradient(UITheme.SplashStyle style)
        {
            var texture = new Texture2D(1, 128, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 128; y++)
                texture.SetPixel(0, y, MinigameUiTheme.WithAlpha(ShellTheme.Ink,
                    Mathf.Lerp(style.overlayBottomOpacity, style.overlayTopOpacity, y / 127f)));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 128f), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, Vector2 inset = default)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = inset;
            rect.offsetMax = -inset;
        }
    }
}
