using System;
using System.Collections.Generic;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    /// The how-to-play card. Its own overlay canvas sorts above the pause menu (900), so it can
    /// open from there; the scrim blocks every control underneath.
    public sealed class MinigameGuidePanel : MonoBehaviour
    {
        public const int SortingOrder = 950;
        static readonly Vector2 Centre = new Vector2(.5f, .5f);

        GameObject scrim;
        TMP_Text titleLabel;
        TMP_Text progressLabel;
        TMP_Text bodyLabel;
        Button skipButton;
        Button backButton;
        Button primaryButton;
        TMP_Text primaryLabel;
        GuideNavigator navigator;

        public event Action<GuideMode> Closed;

        public bool IsOpen => navigator != null;
        public GuideMode Mode => navigator?.Mode ?? GuideMode.Review;
        public int PageCount => navigator?.Count ?? 0;
        public int PageIndex => navigator?.Index ?? -1;
        public TutorialStep CurrentPage => navigator?.Current;
        public string PrimaryText => primaryLabel != null ? primaryLabel.text : string.Empty;
        public string ProgressText => progressLabel != null ? progressLabel.text : string.Empty;
        public bool SkipVisible => skipButton != null && skipButton.gameObject.activeSelf;
        public bool BackInteractable => backButton != null && backButton.interactable;
        /// True when the wrapped body is taller than its box (the text would spill out of the card).
        public bool BodyOverflows => bodyLabel != null &&
            bodyLabel.textBounds.size.y > bodyLabel.rectTransform.rect.height + 1f;
        public float BodyFontSize => bodyLabel != null ? bodyLabel.fontSize : 0f;

        public static MinigameGuidePanel Create(Transform parent)
        {
            var root = new GameObject(nameof(MinigameGuidePanel), typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var panel = root.AddComponent<MinigameGuidePanel>();
            panel.Build();
            return panel;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // follows the gameplay canvases (match height, 1920x1080 reference)
            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform scrimRect = UiKit.Rect(transform, "Scrim");
            UiKit.Stretch(scrimRect);
            Image scrimImage = scrimRect.gameObject.AddComponent<Image>();
            scrimImage.color = MinigameUiTheme.WithAlpha(MinigameUiTheme.Scrim, .92f); // hides the game's own start card
            scrimImage.raycastTarget = true;
            scrim = scrimRect.gameObject;

            Image card = UiKit.Panel(scrimRect, "Card", alpha: 1f);
            card.raycastTarget = true;
            UiKit.Place(card.rectTransform, Centre, Centre, Vector2.zero, new Vector2(1200f, 720f));

            titleLabel = UiKit.Label(card.transform, "Title", string.Empty, MinigameUiTheme.Title,
                MinigameUiTheme.TextPrimary);
            UiKit.Place(titleLabel.rectTransform, Centre, Centre, new Vector2(0f, 280f), new Vector2(700f, 80f));

            progressLabel = UiKit.Label(card.transform, "Progress", string.Empty, MinigameUiTheme.Caption,
                MinigameUiTheme.Accent, TextAlignmentOptions.Right);
            UiKit.Place(progressLabel.rectTransform, Centre, Centre, new Vector2(470f, 280f), new Vector2(180f, 60f));

            bodyLabel = UiKit.Label(card.transform, "Body", string.Empty, MinigameUiTheme.BodyLarge,
                MinigameUiTheme.TextPrimary, TextAlignmentOptions.TopLeft);
            UiKit.Place(bodyLabel.rectTransform, Centre, Centre, new Vector2(0f, 10f), new Vector2(1020f, 380f));
            UiKit.FitLabel(bodyLabel, MinigameUiTheme.BodyLarge);
            bodyLabel.richText = true;
            bodyLabel.fontStyle = FontStyles.Normal;
            bodyLabel.paragraphSpacing = 22f;
            bodyLabel.lineSpacing = 6f;
            // Code-built TMP labels default to NoWrap; the body must wrap inside its rect.
            bodyLabel.textWrappingMode = TextWrappingModes.Normal;
            bodyLabel.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform divider = UiKit.Rect(card.transform, "Divider");
            UiKit.Place(divider, Centre, Centre, new Vector2(0f, 222f), new Vector2(1020f, 4f));
            Image dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = MinigameUiTheme.WithAlpha(MinigameUiTheme.Accent, .8f);
            dividerImage.raycastTarget = false;

            skipButton = CreateButton(card.transform, "SkipButton", GuideNavigator.SkipLabel, -380f,
                ButtonVariant.Secondary).Button;
            backButton = CreateButton(card.transform, "BackButton", GuideNavigator.BackLabel, 0f,
                ButtonVariant.Secondary).Button;
            ButtonHandle primary = CreateButton(card.transform, "PrimaryButton", GuideNavigator.NextLabel, 380f,
                ButtonVariant.Primary);
            primaryButton = primary.Button;
            primaryLabel = primary.Label;

            skipButton.onClick.AddListener(PressSkip);
            backButton.onClick.AddListener(PressBack);
            primaryButton.onClick.AddListener(PressPrimary);
            scrim.SetActive(false);
        }

        static ButtonHandle CreateButton(Transform parent, string name, string label, float x, ButtonVariant variant)
        {
            ButtonHandle handle = UiKit.Button(parent, name, label, variant);
            UiKit.Place((RectTransform)handle.Button.transform, Centre, Centre, new Vector2(x, -270f),
                new Vector2(320f, MinigameUiTheme.ButtonHeight));
            return handle;
        }

        public void Open(IReadOnlyList<TutorialStep> pages, GuideMode mode)
        {
            if (IsOpen || pages == null || pages.Count == 0)
                return;
            navigator = new GuideNavigator(pages, mode);
            if (mode == GuideMode.FirstRun)
                GameFreeze.Acquire(this);
            scrim.transform.SetAsLastSibling();
            scrim.SetActive(true);
            Refresh();
        }

        public void PressPrimary()
        {
            if (navigator == null)
                return;
            if (navigator.Primary())
                Close();
            else
                Refresh();
        }

        public void PressBack()
        {
            if (navigator == null)
                return;
            navigator.Back();
            Refresh();
        }

        public void PressSkip()
        {
            if (navigator != null && navigator.ShowsSkip)
                Close();
        }

        void Close()
        {
            GuideMode mode = navigator.Mode;
            navigator = null;
            scrim.SetActive(false);
            GameFreeze.Release(this);
            Closed?.Invoke(mode);
        }

        void OnDestroy() => GameFreeze.Release(this);

        void Refresh()
        {
            TutorialStep page = navigator.Current;
            titleLabel.text = VietText.Fix(page.Title);
            bodyLabel.text = VietText.Fix(GuideBullets.Format(page.Instruction));
            bodyLabel.ForceMeshUpdate();
            progressLabel.text = VietText.Fix(navigator.Progress);
            primaryLabel.text = VietText.Fix(navigator.PrimaryLabel);
            backButton.interactable = navigator.CanGoBack;
            skipButton.gameObject.SetActive(navigator.ShowsSkip);
        }
    }
}
