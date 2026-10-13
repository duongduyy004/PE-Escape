using System;
using System.Linq;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    // Centered Game Over card in the Home key-art style: a cream panel with a heavy navy border and
    // offset shadow over the bright menu backdrop, navy titles with a gold drop, and rounded buttons.
    public static class GameOverPresentationBuilder
    {
        const string LayoutName = "GameOverLayout";
        const string PanelName = "MenuPanel";
        static readonly Vector2 Center = new Vector2(.5f, .5f);

        static readonly Vector2 PanelSize = new Vector2(800f, 820f);
        static readonly Vector2 PanelCenter = new Vector2(0f, 15f);
        static readonly Vector2 ButtonSize = new Vector2(560f, ShellTheme.ControlHeight);

        static string Subtitle => $"Bạn đã dùng hết {GameSession.MaxLives} lượt thi. Chọn một thao tác bên dưới để tiếp tục.";

        /// Drops the baked layout (keeping the scene's buttons) so authoring can rebuild it.
        public static void Rebuild(GameOverScreen screen, GameSession session)
        {
            if (screen == null) return;
            DropLayout(screen);
            Build(screen, session);
        }

        static void DropLayout(GameOverScreen screen)
        {
            var existing = screen.transform.Find(LayoutName);
            if (existing == null) return;
            foreach (var button in existing.GetComponentsInChildren<Button>(true))
                button.transform.SetParent(screen.transform, false);
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        public static void Build(GameOverScreen screen, GameSession session)
        {
            if (screen == null) return;
            screen.EnsureDialogue();
            var existing = screen.transform.Find(LayoutName);
            if (existing != null && existing.Find(PanelName + "/" + ShellKit.FaceName) == null)
            {
                // A layout from an older style: keep the scene's buttons, rebuild the rest.
                DropLayout(screen);
                existing = null;
            }
            var stats = GameOverStats.From(session);
            if (existing != null)
            {
                RefreshEditedContent(screen);
                SetStat(screen, "StatPassed", $"{stats.SubjectsPassed}/{stats.SubjectsTotal}");
                SetStat(screen, "StatFailed", stats.FailedVisits.ToString());
                SetStat(screen, "StatScore", stats.TotalScore.ToString());
                ApplyBackground(screen, screen.transform.Find("GameOverVeil") as RectTransform);
                return;
            }

            var veil = screen.transform.Find("GameOverVeil") as RectTransform;
            if (veil == null)
            {
                veil = Rect(screen.transform, "GameOverVeil");
                Stretch(veil);
                // Opaque: nothing from a previous scene's HUD may show through behind the result.
                veil.gameObject.AddComponent<Image>().color = ShellTheme.Ink;
            }
            veil.SetAsFirstSibling();
            ApplyBackground(screen, veil);

            var layout = Rect(screen.transform, LayoutName);
            layout.anchorMin = layout.anchorMax = Center;
            layout.sizeDelta = new Vector2(UITheme.Shared.Dialogue.gameOverContentWidth, 1000f);
            layout.anchoredPosition = Vector2.zero;
            layout.gameObject.AddComponent<CanvasGroup>();
            layout.gameObject.AddComponent<GameOverReveal>();

            ShellCardHandle panel = ShellKit.Card(layout, PanelName, decorated: true);
            At(panel.Root, PanelCenter, PanelSize);

            ShellTitleHandle top = ShellKit.Title(layout, "TitleTop", "HẾT LƯỢT THI", MinigameUiTheme.Headline);
            At(top.Root, new Vector2(0f, 375f), new Vector2(620f, 60f));
            ShellTitleHandle main = ShellKit.Title(layout, "TitleMain", "CHƯA QUA HỌC PHẦN", 84f, warning: true);
            At(main.Root, new Vector2(0f, 285f), new Vector2(700f, 110f));

            var subtitle = ShellKit.Label(layout, "Subtitle", Subtitle, MinigameUiTheme.Caption, ShellTheme.BodyText,
                VietFontRole.Body);
            subtitle.textWrappingMode = TextWrappingModes.Normal;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            At(subtitle.rectTransform, new Vector2(0f, 180f), new Vector2(660f, 90f));

            Stat(layout, "StatPassed", $"{stats.SubjectsPassed}/{stats.SubjectsTotal}", "MÔN ĐÃ QUA", -250f);
            Stat(layout, "StatFailed", stats.FailedVisits.ToString(), "LẦN CHƯA ĐẠT", 0f);
            Stat(layout, "StatScore", stats.TotalScore.ToString(), "TỔNG ĐIỂM", 250f);

            Action(screen, layout, "RETRYButton", "CHƠI LẠI", -105f, ShellButtonVariant.Primary);
            Action(screen, layout, "NEW GAMEButton", "CHƠI MỚI", -210f, ShellButtonVariant.Secondary);
            Action(screen, layout, "MAIN MENUButton", "MENU CHÍNH", -315f, ShellButtonVariant.Secondary);
        }

        static void RefreshEditedContent(GameOverScreen screen)
        {
            var layout = (RectTransform)screen.transform.Find(LayoutName);
            layout.sizeDelta = new Vector2(UITheme.Shared.Dialogue.gameOverContentWidth, 1000f);
            ShellKit.SetTitleText(layout.Find("TitleTop"), "HẾT LƯỢT THI");
            ShellKit.SetTitleText(layout.Find("TitleMain"), "CHƯA QUA HỌC PHẦN");
            layout.Find("Subtitle").GetComponent<TMP_Text>().text = VietText.Fix(Subtitle);
            layout.Find("StatFailed/Caption").GetComponent<TMP_Text>().text = VietText.Fix("LẦN CHƯA ĐẠT");
        }

        // Idempotent: the menu backdrop shows at full brightness behind the card, covering any aspect.
        static void ApplyBackground(GameOverScreen screen, RectTransform veil)
        {
            if (veil == null) return;
            var wash = veil.Find("Wash");
            if (wash != null) UnityEngine.Object.DestroyImmediate(wash.gameObject);
            if (screen.Background == null) return;
            Sprite sprite = screen.Background;
            var existing = (RectTransform)veil.Find("Illustration");
            RectTransform artRect = existing != null ? existing : Rect(veil, "Illustration");
            artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(.5f, .5f);
            artRect.anchoredPosition = Vector2.zero;
            var fitter = UiKit.GetOrAdd<AspectRatioFitter>(artRect.gameObject);
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            Image art = UiKit.GetOrAdd<Image>(artRect.gameObject);
            art.sprite = sprite;
            art.color = Color.white;
            art.raycastTarget = false;
        }

        static void SetStat(GameOverScreen screen, string name, string value)
        {
            var label = screen.transform.Find(LayoutName + "/" + name + "/Value")?.GetComponent<TMP_Text>();
            if (label != null) label.text = VietText.Fix(value);
        }

        static void Stat(RectTransform parent, string name, string value, string caption, float x)
        {
            ShellCardHandle card = ShellKit.Tile(parent, name);
            At(card.Root, new Vector2(x, 45f), new Vector2(240f, 130f));
            var number = ShellKit.Label(card.Root, "Value", value, MinigameUiTheme.Title, ShellTheme.Ink, VietFontRole.Hud);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(number.rectTransform, new Vector2(.05f, .42f), new Vector2(.95f, .95f));
            var text = ShellKit.Label(card.Root, "Caption", caption, MinigameUiTheme.Caption, ShellTheme.TitleRed, VietFontRole.Hud);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(text.rectTransform, new Vector2(.03f, .07f), new Vector2(.97f, .45f));
        }

        static void Action(GameOverScreen screen, RectTransform parent, string name, string caption,
            float y, ShellButtonVariant variant)
        {
            var button = screen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => string.Equals(candidate.name, name, StringComparison.Ordinal));
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            At(rect, new Vector2(0f, y), ButtonSize);
            ShellKit.StyleButton(button, variant, caption);
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void At(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Center;
            rect.pivot = Center;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rect) => Anchor(rect, Vector2.zero, Vector2.one);
    }
}
