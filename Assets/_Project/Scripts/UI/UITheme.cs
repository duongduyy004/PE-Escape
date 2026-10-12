using System;
using KMA.UI.Kit;
using UnityEngine;

namespace KMA.Gameplay.UI
{
    [CreateAssetMenu(menuName = "KMA/UI Theme", fileName = "UITheme")]
    public sealed class UITheme : ScriptableObject
    {
        [Serializable] public sealed class DialogueStyle
        {
            public Vector2 size = new Vector2(560f, 196f);
            public Vector2 anchor = new Vector2(1f, .86f);
            public Vector2 offset = new Vector2(-80f, -16f);
            public Vector2 volleyballAnchor = new Vector2(1f,.88f);
            public Vector2 frogAnchor = new Vector2(1f,.82f);
            public Vector2 resultAnchor = new Vector2(1f,.60f), resultOffset = new Vector2(-24f,0f);
            public Vector2 gameOverAnchor = new Vector2(1f,.60f), gameOverOffset = new Vector2(-24f,0f), gameOverSize = new Vector2(400f,400f);
            public float gameOverContentWidth = 900f, gameOverNameHeight = 160f;
            public Vector2 chessBubbleSize = new Vector2(320f,280f);
            public float chessSpeechY=.61f, chessNameTop=120f, chessNameHeight=80f, chessSpeakerHeight=80f, chessPadding=16f, chessBoardWidth=804f;
            public float portraitWidth = 90f, padding = 12f, nameSize = 28f, textSize = 28f, iconSize = 32f;
        }
        [SerializeField] DialogueStyle dialogue = new DialogueStyle();
        public DialogueStyle Dialogue => dialogue;

        [SerializeField] private Color primary = new Color32(0xFF, 0x59, 0x5E, 0xFF);
        [SerializeField] private Color accent = new Color32(0xFF, 0xCA, 0x3A, 0xFF);
        [SerializeField] private Color background = new Color32(0x19, 0x82, 0xC4, 0xFF);
        [SerializeField] private Color success = new Color32(0x8A, 0xCB, 0x88, 0xFF);
        [SerializeField] private Color card = Color.white;
        [SerializeField] private Color muted = new Color32(0xE2, 0xE8, 0xF0, 0xFF);
        [SerializeField] private Color mutedForeground = new Color32(0x47, 0x55, 0x69, 0xFF);
        [SerializeField] private Color border = Color.black;
        [SerializeField] private float spacing = 8f;
        [Min(0f)] [SerializeField] private float cornerRadius = 24f;
        [Min(0f)] [SerializeField] private float borderWidth = 4f;
        [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -4f);

        // UiKitAssets lives in Resources and references this asset, including in player builds.
        public static UITheme Shared
        {
            get
            {
                UITheme theme = UiKitAssets.Load().Theme;
                return theme != null ? theme
                    : throw new InvalidOperationException("Assign UITheme on the shared UiKitAssets asset.");
            }
        }

        [Header("Shared surfaces")]
        [SerializeField] private Color disabledSurface = new Color32(74, 92, 110, 255);
        [SerializeField] private Color disabledText = new Color32(190, 201, 212, 255);
        [SerializeField] private Color surface = new Color32(8, 35, 61, 255);
        [SerializeField] private Color textPrimary = new Color32(255, 249, 231, 255);
        [SerializeField] private Color textOutline = new Color32(3, 18, 33, 255);
        [SerializeField] private Color player = new Color32(58, 230, 255, 255);
        [SerializeField] private Color resultSuccess = new Color32(94, 222, 140, 255);
        [SerializeField] private Color scrim = new Color(3f / 255f, 18f / 255f, 33f / 255f, .7f);
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, .35f);
        [Header("Screen palette variants")]
        [SerializeField] private Color settingsBackdrop = new Color(.02f, .06f, .1f, .85f);
        public Color SettingsBackdrop => settingsBackdrop;
        [SerializeField] private Color settingsHint = new Color32(190, 219, 239, 255);
        public Color SettingsHint => settingsHint;
        [SerializeField] private Color settingsTrack = new Color32(48, 74, 99, 255);
        public Color SettingsTrack => settingsTrack;
        [SerializeField] private Color mapBackgroundTint = new Color32(10, 48, 82, 255);
        public Color MapBackgroundTint => mapBackgroundTint;
        [SerializeField] private Color mapHint = new Color32(201, 226, 245, 255);
        public Color MapHint => mapHint;
        [SerializeField] private Color mapLivesSurface = new Color32(13, 57, 92, 238);
        public Color MapLivesSurface => mapLivesSurface;
        [SerializeField] private Color mapLockedBorder = new Color32(117, 138, 156, 255);
        public Color MapLockedBorder => mapLockedBorder;
        [SerializeField] private Color mapReadyText = new Color32(12, 105, 94, 255);
        public Color MapReadyText => mapReadyText;
        [SerializeField] private Color mapActionText = new Color32(163, 104, 0, 255);
        public Color MapActionText => mapActionText;
        [SerializeField] private Color mapLockIcon = new Color32(38, 60, 77, 255);
        public Color MapLockIcon => mapLockIcon;
        [SerializeField] private Color mapButtonHighlight = new Color32(255, 236, 170, 255);
        public Color MapButtonHighlight => mapButtonHighlight;
        [SerializeField] private Color mapCompleteBorder = new Color32(65, 170, 104, 255);
        public Color MapCompleteBorder => mapCompleteBorder;
        [SerializeField] private Color mapLockedCard = new Color32(184, 199, 211, 255);
        public Color MapLockedCard => mapLockedCard;
        [SerializeField] private Color mapLockedIcon = new Color32(124, 144, 160, 255);
        public Color MapLockedIcon => mapLockedIcon;
        [SerializeField] private Color mapLockedText = new Color32(56, 75, 90, 255);
        public Color MapLockedText => mapLockedText;
        [Header("Chapter lesson journey")]
        [SerializeField] private LessonJourneyStyle lessonJourney = new LessonJourneyStyle();
        public LessonJourneyStyle LessonJourney => lessonJourney;
        [Header("Menu palette and geometry")]
        [SerializeField] private MenuStyle menu = new MenuStyle();
        [Header("Splash presentation")]
        [SerializeField] private SplashStyle splash = new SplashStyle();
        public SplashStyle Splash => splash;
        [Header("Animation (unscaled seconds)")]
        [SerializeField] private MotionStyle motion = new MotionStyle();

        public Color Surface => surface;
        public Color TextPrimary => textPrimary;
        public Color TextOutline => textOutline;
        public Color Player => player;
        public Color ResultSuccess => resultSuccess;
        public Color Scrim => scrim;
        public Color ShadowColor => shadowColor;
        public MenuStyle Menu => menu;
        public MotionStyle Motion => motion;

        [Serializable]
        public sealed class LessonJourneyStyle
        {
            public Color sprint = new Color32(49, 162, 222, 255);
            public Color volleyball = new Color32(245, 158, 46, 255);
            public Color football = new Color32(53, 169, 91, 255);
            public Color chess = new Color32(232, 90, 72, 255);
            // The lesson panel is a popup centred over the map, opened by tapping a stop.
            public Vector2 panelAnchorMin = new Vector2(.06f, .20f);
            public Vector2 panelAnchorMax = new Vector2(.94f, .70f);
            public Vector2 courseAnchorMin = new Vector2(0f, .41f);
            public Vector2 courseAnchorMax = new Vector2(1f, .915f);
            public Vector2 summaryAnchorMin = new Vector2(.05f, .84f);
            public Vector2 summaryAnchorMax = new Vector2(.95f, .915f);
            public float borderWidth = 3f;
            public float headingSize = 36f;
            public float stageSize = 36f;
            public float bodySize = 28f;
            public float captionSize = 26f;
            public float objectiveSize = 26f;
            // Level-select layout, in panel fractions: stage nodes on a dotted road, details below.
            public float nodeRowBottom = .36f;
            public float nodeRowTop = .82f;
            public float nodeHalfWidth = .12f;
            public float nodeLineY = .64f;
            public Vector2 detailAnchorMin = new Vector2(.04f, .04f);
            public Vector2 detailAnchorMax = new Vector2(.96f, .33f);
            // Stage node sizes, in canvas pixels.
            public float nodeSize = 124f;
            public float nodeCurrentScale = 1.16f;
            public float nodeBadgeSize = 46f;
            public float nodeLabelHeight = 52f;
            public float lessonDotSize = 14f;
            public float revealDuration = .20f;
            public float revealStagger = .05f;
            public float revealScale = .94f;
            public float glowSpeed = 2.4f;
            public Vector2 glowAlpha = new Vector2(.12f, .32f);
            // Journey map: the map zone fills everything under the compact header.
            public Vector2 mapAnchorMin = new Vector2(0f, .02f);
            public Vector2 mapAnchorMax = new Vector2(1f, .90f);
            // Fixed-size stop: tag + badge area + name + star/status pill.
            public Vector2 stopSize = new Vector2(320f, 400f);
            public float stopBadgeSize = 170f;
            public float stopCurrentScale = 1.3f;
            public float stopTagHeight = 52f;
            // Stops grow past their authored size when the map zone has room, so they read on phones.
            public float stopMaxScale = 1.35f;
            // Badge-centre positions inside the map zone (x of width, y of height).
            public float[] stopX = { .12f, .37f, .63f, .88f };
            public float[] stopY = { .38f, .62f, .38f, .62f };
            public float roadOutlineWidth = 30f;
            public float roadFillWidth = 16f;
            public float roadDotSize = 12f;
        }

        [Serializable]
        public sealed class MenuStyle
        {
            public Color navy = new Color32(11, 42, 74, 255);
            public Color gold = new Color32(255, 201, 40, 255);
            public Color goldLight = new Color32(255, 224, 102, 255);
            public Color goldDark = new Color32(255, 180, 0, 255);
            public Color disabledBorder = new Color(1f, 1f, 1f, .24f);
            public Color disabledFill = new Color(.04f, .13f, .22f, .34f);
            public Color disabledText = new Color(1f, 1f, 1f, .42f);
            public Color red = new Color32(226, 85, 61, 255);
            public Color grass = new Color32(107, 164, 58, 255);
            [Range(-45f, 45f)] public float titleAngle = -8f;
            [Range(0f, 40f)] public float buttonSlantAngle = 12.094757f;
            [Range(.5f, 1f)] public float pressScale = .96f;
        }

        [Serializable]
        public sealed class SplashStyle
        {
            public Vector2 contentSize = new Vector2(720f, 840f);
            public Vector2 contentPadding = new Vector2(32f, 96f);
            public Vector2 referenceResolution = new Vector2(1920f, 1080f);
            public float badgeSize = 160f;
            public float badgeY = 320f;
            public float titleScale = 1.3f;
            public Vector2 titleTopSize = new Vector2(640f, 140f);
            public Vector2 titleKmaSize = new Vector2(640f, 230f);
            public float titleTopY = 160f;
            public float titleKmaY = 10f;
            public float stripesY = -155f;
            public Vector2 stripeSize = new Vector2(210f, 3f);
            public Vector2 stripeStep = new Vector2(18f, -8f);
            public float stripeShorten = 28f;
            public float sloganY = -215f;
            public float sloganSize = 26f;
            public Vector2 sloganBox = new Vector2(600f, 52f);
            public Vector2 sloganShadow = new Vector2(1f, -2f);
            public float statusY = -285f;
            public float statusSize = 22f;
            public float statusHeight = 38f;
            public float trackY = -323f;
            public Vector2 trackSize = new Vector2(460f, 20f);
            public float trackBorder = 2f;
            [Range(0f, 1f)] public float trackOpacity = .65f;
            [Range(0f, 1f)] public float overlayTopOpacity = .70f;
            [Range(0f, 1f)] public float overlayBottomOpacity = .85f;
            public Vector2 footerInset = new Vector2(32f, 24f);
            public Vector2 footerSize = new Vector2(520f, 40f);
            public float footerFontSize = 20f;
            [Range(0f, 1f)] public float footerOpacity = .55f;
            [Min(.001f)] public float maxAnimationDelta = .05f;
            [Min(.01f)] public float introDuration = .5f;
            public float introSlide = 24f;
            [Range(0f, 1f)] public float kmaStartScale = .9f;
            [Min(.01f)] public float exitDuration = .35f;
            [Min(.01f)] public float progressSmoothSeconds = .18f;
        }

        [Serializable]
        public sealed class MotionStyle
        {
            [Min(.001f)] public float pressRestore = .1f;
            [Range(.5f, 1f)] public float pressScale = .94f;
            [Range(0f, 1f)] public float pressLighten = .15f;
            [Min(0f)] public float buttonFade = .08f;
            [Min(0f)] public float resultScrim = .12f;
            [Min(0f)] public float resultModal = .18f;
            [Min(0f)] public float resultTitle = .14f;
            [Min(0f)] public float resultScore = .35f;
            [Min(0f)] public float resultRank = .16f;
            [Min(.001f)] public float badgeShineDuration = .8f;
            [Min(.001f)] public float badgeShineCycle = 4.5f;
        }

        public Color Primary => primary;
        public Color Accent => accent;
        public Color Background => background;
        public Color Success => success;
        public Color Card => card;
        public Color Muted => muted;
        public Color MutedForeground => mutedForeground;
        public Color Border => border;
        public float Spacing => spacing;
        public float CornerRadius => Mathf.Max(0f, cornerRadius);
        public float BorderWidth => Mathf.Max(0f, borderWidth);
        public Vector2 ShadowOffset => shadowOffset;
        public Color DisabledSurface => disabledSurface;
        public Color DisabledText => disabledText;
    }
}
