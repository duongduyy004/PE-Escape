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

        // Sampled from the painted menu panel (Art/UI/MenuKeyArtPanel.png).
        public static readonly Color Cream = new Color32(0xfb, 0xf4, 0xe4, 0xff);
        public static readonly Color ButtonGold = new Color32(0xf9, 0xc8, 0x2b, 0xff);
        // Deeper than the accent red so the headline stays legible on cream.
        public static readonly Color TitleRed = new Color32(0xc6, 0x28, 0x28, 0xff);
        const float Border = 6f;
        static readonly Vector2 DropOffset = new Vector2(8f, -8f);
        static readonly Vector2 PanelSize = new Vector2(800f, 820f);
        static readonly Vector2 PanelCenter = new Vector2(0f, 15f);
        static readonly Vector2 ButtonSize = new Vector2(560f, 84f);
        const float PanelRadius = 24f;
        const float ButtonRadius = 14f;

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
            if (existing != null && existing.Find(PanelName) == null)
            {
                // A layout from the older dark style: keep the scene's buttons, rebuild the rest.
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
                veil.gameObject.AddComponent<Image>().color = HomeMenuStyle.Navy;
            }
            veil.SetAsFirstSibling();
            ApplyBackground(screen, veil);

            var layout = Rect(screen.transform, LayoutName);
            layout.anchorMin = layout.anchorMax = Center;
            layout.sizeDelta = new Vector2(UITheme.Shared.Dialogue.gameOverContentWidth, 1000f);
            layout.anchoredPosition = Vector2.zero;
            layout.gameObject.AddComponent<CanvasGroup>();
            layout.gameObject.AddComponent<GameOverReveal>();

            Card(layout, PanelName, PanelCenter, PanelSize, PanelRadius, Cream, true);
            Accent(layout, "AccentTop", new Vector2(-350f, 390f));
            Accent(layout, "AccentTitle", new Vector2(352f, 250f));

            var top = Title(layout, "TitleTop", "HẾT LƯỢT THI", 44f, HomeMenuStyle.Navy, new Vector2(0f, 375f), new Vector2(620f, 60f));
            UiKit.FitLabel(top, 44f);
            var main = Title(layout, "TitleMain", "CHƯA QUA HỌC PHẦN", 84f, TitleRed,
                new Vector2(0f, 285f), new Vector2(700f, 110f));
            UiKit.FitLabel(main, 84f);

            var subtitle = UiKit.Label(layout, "Subtitle", Subtitle, 28f, HomeMenuStyle.Navy);
            subtitle.fontStyle = FontStyles.Normal;
            subtitle.textWrappingMode = TextWrappingModes.Normal;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            At(subtitle.rectTransform, new Vector2(0f, 180f), new Vector2(660f, 90f));

            Stat(layout, "StatPassed", $"{stats.SubjectsPassed}/{stats.SubjectsTotal}", "MÔN ĐÃ QUA", -250f);
            Stat(layout, "StatFailed", stats.FailedVisits.ToString(), "LẦN CHƯA ĐẠT", 0f);
            Stat(layout, "StatScore", stats.TotalScore.ToString(), "TỔNG ĐIỂM", 250f);

            Action(screen, layout, "RETRYButton", "CHƠI LẠI", -105f, true);
            Action(screen, layout, "NEW GAMEButton", "CHƠI MỚI", -210f, false);
            Action(screen, layout, "MAIN MENUButton", "MENU CHÍNH", -315f, false);
        }

        static void RefreshEditedContent(GameOverScreen screen)
        {
            var layout = (RectTransform)screen.transform.Find(LayoutName);
            layout.sizeDelta = new Vector2(UITheme.Shared.Dialogue.gameOverContentWidth, 1000f);
            layout.Find("TitleTop").GetComponent<TMP_Text>().text = VietText.Fix("HẾT LƯỢT THI");
            layout.Find("TitleMain").GetComponent<TMP_Text>().text = VietText.Fix("CHƯA QUA HỌC PHẦN");
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

        // A navy rounded border with a navy drop shadow, and a face inset by the border width.
        static Image Card(RectTransform parent, string name, Vector2 position, Vector2 size, float radius,
            Color faceColor, bool drop)
        {
            var border = UiKit.Shape(parent, name, radius, HomeMenuStyle.Navy);
            At(border.rectTransform, position, size);
            if (drop)
            {
                var shadow = border.gameObject.AddComponent<Shadow>();
                shadow.effectColor = HomeMenuStyle.Navy;
                shadow.effectDistance = DropOffset;
            }
            var face = UiKit.Shape(border.transform, "Face", Mathf.Max(0f, radius - Border), faceColor);
            Stretch(face.rectTransform);
            face.rectTransform.offsetMin = Vector2.one * Border;
            face.rectTransform.offsetMax = -Vector2.one * Border;
            return face;
        }

        static void Accent(RectTransform parent, string name, Vector2 position)
        {
            var square = UiKit.Shape(parent, name, 3f, HomeMenuStyle.Red);
            At(square.rectTransform, position, new Vector2(30f, 30f));
        }

        static TMP_Text Title(RectTransform parent, string name, string text, float size, Color color, Vector2 position, Vector2 box)
        {
            var label = UiKit.Label(parent, name, text, size, color);
            VietTypography.Apply(label, VietFontRole.Title);
            // The Title role tints with a gold gradient, which muddies dark text on the cream card.
            label.enableVertexGradient = false;
            label.fontStyle = FontStyles.Bold | FontStyles.Italic;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            At(label.rectTransform, position, box);
            return label;
        }

        static void Stat(RectTransform parent, string name, string value, string caption, float x)
        {
            var card = Rect(parent, name);
            At(card, new Vector2(x, 45f), new Vector2(240f, 130f));
            Card(card, "Frame", Vector2.zero, card.sizeDelta, ButtonRadius, Color.white, false)
                .transform.parent.SetAsFirstSibling();

            var number = UiKit.Label(card, "Value", value, 54f, HomeMenuStyle.Navy);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.FitLabel(number, 54f);
            Anchor(number.rectTransform, new Vector2(.05f, .42f), new Vector2(.95f, .95f));
            var text = UiKit.Label(card, "Caption", caption, 26f, HomeMenuStyle.Red);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.FitLabel(text, 26f);
            Anchor(text.rectTransform, new Vector2(.03f, .07f), new Vector2(.97f, .45f));
        }

        static void Action(GameOverScreen screen, RectTransform parent, string name, string caption,
            float y, bool primary)
        {
            var button = screen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => string.Equals(candidate.name, name, StringComparison.Ordinal));
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            At(rect, new Vector2(0f, y), ButtonSize);
            foreach (var stale in new[] { "Fill", "Icon", "HoverArrow", "DisabledShade", "Face" })
            {
                var old = rect.Find(stale);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            foreach (var old in button.GetComponents<HomeMenuButton>()) UnityEngine.Object.DestroyImmediate(old);
            foreach (var old in button.GetComponents<HomeKeyArtButton>()) UnityEngine.Object.DestroyImmediate(old);
            foreach (var old in button.GetComponents<Outline>()) UnityEngine.Object.DestroyImmediate(old);

            var border = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
            UiKit.SetRadius(border, ButtonRadius);
            border.color = HomeMenuStyle.Navy;
            border.raycastTarget = true;
            var shadow = UiKit.GetOrAdd<Shadow>(button.gameObject);
            shadow.effectColor = HomeMenuStyle.Navy;
            shadow.effectDistance = new Vector2(5f, -5f);
            var face = UiKit.Shape(rect, "Face", ButtonRadius - 4f, primary ? ButtonGold : Cream);
            Stretch(face.rectTransform);
            face.rectTransform.offsetMin = Vector2.one * 4f;
            face.rectTransform.offsetMax = -Vector2.one * 4f;
            face.transform.SetAsFirstSibling();

            var label = UiKit.EnsureTmpLabel(rect, 34f, HomeMenuStyle.Navy);
            label.text = VietText.Fix(caption);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.FitLabel(label, 34f);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16f, 4f);
            label.rectTransform.offsetMax = new Vector2(-16f, -4f);
            label.transform.SetAsLastSibling();

            button.transition = Selectable.Transition.None;
            button.targetGraphic = border;
            var press = UiKit.GetOrAdd<KitPressFeedback>(button.gameObject);
            press.Configure(face, rect);
            press.BindLabel(label);
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
