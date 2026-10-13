using KMA.Gameplay.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    /// Builds and restyles the shared widgets of every screen outside the minigames, in the menu key art
    /// style: a cream face in a navy border over a hard navy offset shadow. Owns look and press
    /// feedback; callers own position and size. The Style* restylers are idempotent; the factories
    /// (Chip, Toggle, Slider, Title, Button) create new objects on every call.
    public static class ShellKit
    {
        public const string ShadowName = "ShellShadow";
        public const string BorderName = "ShellBorder";
        public const string FaceName = "ShellFace";
        public const string BodyName = "Body";
        public const string AccentPrefix = "ShellAccent";

        /// Layers shadow, border and face as the first three children of root, keeping any content above them.
        public static ShellCardHandle StyleSurface(RectTransform root, float radius, float border, Vector2 shadow, Color face)
        {
            Image shadowImage = Layer(root, ShadowName, 0, radius, ShellTheme.Ink);
            UiKit.Stretch(shadowImage.rectTransform, shadow, shadow);
            Image borderImage = Layer(root, BorderName, 1, radius, ShellTheme.Ink);
            UiKit.Stretch(borderImage.rectTransform);
            Image faceImage = Layer(root, FaceName, 2, Mathf.Max(0f, radius - border), face);
            UiKit.Stretch(faceImage.rectTransform, Vector2.one * border, -Vector2.one * border);
            return new ShellCardHandle(root, shadowImage, borderImage, faceImage);
        }

        public static ShellCardHandle Card(Transform parent, string name, bool decorated = false) =>
            StyleCard(UiKit.Rect(parent, name), decorated);

        public static ShellCardHandle StyleCard(RectTransform root, bool decorated = false)
        {
            ShellCardHandle card = StyleSurface(root, ShellTheme.CardRadius, ShellTheme.CardBorder, ShellTheme.CardShadow,
                ShellTheme.Cream);
            Decorate(root, decorated);
            return card;
        }

        public static ShellCardHandle Tile(Transform parent, string name, Color? face = null) =>
            StyleTile(UiKit.Rect(parent, name), face);

        public static ShellCardHandle StyleTile(RectTransform root, Color? face = null) =>
            StyleSurface(root, ShellTheme.ControlRadius, ShellTheme.ControlBorder, Vector2.zero, face ?? ShellTheme.White);

        public static ShellChipHandle Chip(Transform parent, string name, string text, Color? face = null)
        {
            RectTransform rect = UiKit.Rect(parent, name);
            ShellCardHandle card = StyleSurface(rect, ShellTheme.ControlRadius, ShellTheme.ControlBorder,
                ShellTheme.ControlShadow, face ?? ShellTheme.Cream);
            TMP_Text label = Label(rect, "Label", text, MinigameUiTheme.Body, ShellTheme.Ink, VietFontRole.Hud);
            UiKit.Stretch(label.rectTransform,
                new Vector2(MinigameUiTheme.SpaceSm + ShellTheme.ControlBorder, ShellTheme.ControlBorder),
                new Vector2(-MinigameUiTheme.SpaceSm - ShellTheme.ControlBorder, -ShellTheme.ControlBorder));
            return new ShellChipHandle(card, label);
        }

        /// Circle layers for a round badge (map stops): drop disc, navy rim and a coloured face.
        public static ShellCardHandle Badge(RectTransform root, Color face)
        {
            Image shadow = Circle(root, ShadowName, 0, ShellTheme.Ink);
            UiKit.Stretch(shadow.rectTransform, ShellTheme.ControlShadow, ShellTheme.ControlShadow);
            Image border = Circle(root, BorderName, 1, ShellTheme.Ink);
            UiKit.Stretch(border.rectTransform);
            Image faceImage = Circle(root, FaceName, 2, face);
            UiKit.Stretch(faceImage.rectTransform, Vector2.one * ShellTheme.CardBorder, -Vector2.one * ShellTheme.CardBorder);
            return new ShellCardHandle(root, shadow, border, faceImage);
        }

        /// A bold label on the shared type scale, never below the font floor.
        public static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
            VietFontRole role = VietFontRole.BodyBold, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = UiKit.Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = VietText.Fix(text);
            label.fontSize = Mathf.Max(size, MinigameUiTheme.MinimumFontSize);
            label.alignment = alignment;
            label.raycastTarget = false;
            VietTypography.Apply(label, role);
            label.enableVertexGradient = false;
            label.color = color;
            UiKit.FitLabel(label, label.fontSize);
            return label;
        }

        public static Color FaceColor(ShellButtonVariant variant) => variant switch
        {
            ShellButtonVariant.Primary => ShellTheme.Gold,
            ShellButtonVariant.Danger => ShellTheme.AccentRed,
            _ => ShellTheme.Cream,
        };

        public static Color LabelColor(ShellButtonVariant variant) => ShellTheme.Ink;

        public static ShellButtonHandle Button(Transform parent, string name, string text, ShellButtonVariant variant)
        {
            RectTransform rect = UiKit.Rect(parent, name);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, ShellTheme.ControlHeight);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            return StyleButton(button, variant, text);
        }

        public static ShellButtonHandle IconButton(Transform parent, string name, string glyph)
        {
            ShellButtonHandle handle = Button(parent, name, glyph, ShellButtonVariant.Secondary);
            ((RectTransform)handle.Button.transform).sizeDelta = Vector2.one * ShellTheme.IconButtonSize;
            VietTypography.Apply(handle.Label, VietFontRole.Symbol);
            handle.Label.fontSize = MinigameUiTheme.Headline;
            UiKit.FitLabel(handle.Label, MinigameUiTheme.Headline);
            return handle;
        }

        /// Restyles any uGUI Button, kit-, prefab- or scene-authored, as a Shell button. Idempotent; keeps listeners.
        public static ShellButtonHandle StyleButton(UnityEngine.UI.Button button, ShellButtonVariant variant, string text = null)
        {
            var root = (RectTransform)button.transform;
            RemoveLegacyButtonParts(root);

            Image hit = UiKit.GetOrAdd<Image>(root.gameObject);
            hit.sprite = null;
            hit.color = Color.clear;
            hit.raycastTarget = true;

            Image shadow = Layer(root, ShadowName, 0, ShellTheme.ControlRadius, ShellTheme.Ink);
            UiKit.Stretch(shadow.rectTransform, ShellTheme.ControlShadow, ShellTheme.ControlShadow);
            Transform existingBody = root.Find(BodyName);
            RectTransform body = existingBody != null ? (RectTransform)existingBody : UiKit.Rect(root, BodyName);
            body.SetSiblingIndex(1);
            UiKit.Stretch(body);
            UiKit.GetOrAdd<LayoutElement>(body.gameObject).ignoreLayout = true;
            Image border = Layer(body, BorderName, 0, ShellTheme.ControlRadius, ShellTheme.Ink);
            UiKit.Stretch(border.rectTransform);
            Image face = Layer(body, FaceName, 1, Mathf.Max(0f, ShellTheme.ControlRadius - ShellTheme.ControlBorder),
                FaceColor(variant));
            UiKit.Stretch(face.rectTransform, Vector2.one * ShellTheme.ControlBorder, -Vector2.one * ShellTheme.ControlBorder);

            TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);
            if (label == null && (text != null || root.GetComponentInChildren<Text>(true) != null))
                label = UiKit.EnsureTmpLabel(root, MinigameUiTheme.BodyLarge, LabelColor(variant));
            if (label != null)
            {
                label.transform.SetParent(body, false);
                label.transform.SetAsLastSibling();
                UiKit.Stretch(label.rectTransform, new Vector2(MinigameUiTheme.SpaceSm, 0f), new Vector2(-MinigameUiTheme.SpaceSm, 0f));
                if (text != null) label.text = VietText.Fix(text);
                label.fontSize = MinigameUiTheme.BodyLarge;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                VietTypography.Apply(label, VietFontRole.Hud);
                UiKit.FitLabel(label, MinigameUiTheme.BodyLarge);
            }

            button.transition = Selectable.Transition.None;
            button.targetGraphic = face;
            face.raycastTarget = true;
            var feedback = UiKit.GetOrAdd<ShellPressFeedback>(root.gameObject);
            feedback.Configure(body, face, label, FaceColor(variant), LabelColor(variant));
            return new ShellButtonHandle(button, body, new ShellCardHandle(root, shadow, border, face), label, feedback);
        }

        public static ShellTitleHandle Title(Transform parent, string name, string text, float size, bool warning = false)
        {
            RectTransform root = UiKit.Rect(parent, name);
            TMP_Text drop = TitleText(root, "Drop", text, size, ShellTheme.Gold);
            UiKit.Stretch(drop.rectTransform, ShellTheme.TitleDrop, ShellTheme.TitleDrop);
            TMP_Text face = TitleText(root, "Text", text, size, warning ? ShellTheme.TitleRed : ShellTheme.Ink);
            UiKit.Stretch(face.rectTransform);
            return new ShellTitleHandle(root, face, drop);
        }

        public static void SetTitleText(Transform titleRoot, string text)
        {
            foreach (string part in new[] { "Drop", "Text" })
            {
                TMP_Text label = titleRoot.Find(part)?.GetComponent<TMP_Text>();
                if (label != null) label.text = VietText.Fix(text);
            }
        }

        public static ShellToggleHandle Toggle(Transform parent, string name)
        {
            RectTransform root = UiKit.Rect(parent, name);
            ShellCardHandle track = StyleSurface(root, ShellTheme.ControlRadius, ShellTheme.ControlBorder,
                ShellTheme.ControlShadow, ShellTheme.Cream);
            Image check = UiKit.Shape(track.Face.transform, "Checkmark",
                Mathf.Max(0f, ShellTheme.ControlRadius - ShellTheme.ControlBorder), ShellTheme.Gold);
            UiKit.Stretch(check.rectTransform);
            TMP_Text value = Label(root, "Value", "BẬT", MinigameUiTheme.Body, ShellTheme.Ink, VietFontRole.Hud);
            UiKit.Stretch(value.rectTransform);
            track.Face.raycastTarget = true;
            var toggle = root.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = track.Face;
            toggle.graphic = check;
            toggle.transition = Selectable.Transition.None;
            toggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.None;
            return new ShellToggleHandle(toggle, track, check, value);
        }

        public static ShellSliderHandle Slider(Transform parent, string name)
        {
            RectTransform root = UiKit.Rect(parent, name);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            RectTransform trackRect = UiKit.Rect(root, "Track");
            UiKit.Anchor(trackRect, new Vector2(0f, .3f), new Vector2(1f, .7f));
            ShellCardHandle track = StyleSurface(trackRect, ShellTheme.ControlRadius, ShellTheme.ControlBorder,
                ShellTheme.ControlShadow, ShellTheme.Cream);
            RectTransform fillArea = UiKit.Rect(track.Face.transform, "FillArea");
            UiKit.Stretch(fillArea);
            Image fill = UiKit.Shape(fillArea, "Fill", Mathf.Max(0f, ShellTheme.ControlRadius - ShellTheme.ControlBorder),
                ShellTheme.Gold);
            UiKit.Stretch(fill.rectTransform);

            RectTransform area = UiKit.Rect(root, "HandleSlideArea");
            area.anchorMin = new Vector2(0f, .5f);
            area.anchorMax = new Vector2(1f, .5f);
            area.sizeDelta = new Vector2(-MinigameUiTheme.SliderKnob, MinigameUiTheme.SliderKnob);
            Image knob = UiKit.Disc(area, "Handle", false, ShellTheme.Ink);
            knob.rectTransform.sizeDelta = Vector2.one * MinigameUiTheme.SliderKnob;
            Image ring = UiKit.Disc(knob.transform, "Ring", true, ShellTheme.Gold);
            UiKit.Stretch(ring.rectTransform);

            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.transition = Selectable.Transition.None;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return new ShellSliderHandle(slider, track, fill, knob);
        }

        static TMP_Text TitleText(RectTransform parent, string name, string text, float size, Color color)
        {
            TMP_Text label = Label(parent, name, text, size, color, VietFontRole.Title);
            label.fontStyle = FontStyles.Italic;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        static void RemoveLegacyButtonParts(RectTransform root)
        {
            foreach (string stale in new[] { "Fill", "Face", "Icon", "HoverArrow", "DisabledShade", "Shadow", "Visual" })
            {
                Transform old = root.Find(stale);
                if (old != null) { old.name = "Stale"; Destroy(old.gameObject); }
            }
            foreach (Shadow effect in root.GetComponents<Shadow>()) Destroy(effect);
            foreach (KitPressFeedback old in root.GetComponents<KitPressFeedback>()) Destroy(old);
            foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name == "HomeKeyArtButton") Destroy(behaviour);
        }

        static void Decorate(RectTransform root, bool decorated)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (!child.name.StartsWith(AccentPrefix)) continue;
                child.name = "Stale";
                Destroy(child.gameObject);
            }
            if (!decorated) return;
            AccentSquare(root, AccentPrefix + "Top", new Vector2(0f, 1f), new Vector2(ShellTheme.AccentSquare * 2f, 0f));
            AccentSquare(root, AccentPrefix + "Bottom", new Vector2(1f, 0f), new Vector2(-ShellTheme.AccentSquare * 2f, 0f));
        }

        static void AccentSquare(RectTransform root, string name, Vector2 anchor, Vector2 position)
        {
            RectTransform square = UiKit.Rect(root, name);
            UiKit.Place(square, anchor, new Vector2(.5f, .5f), position, Vector2.one * ShellTheme.AccentSquare);
            UiKit.GetOrAdd<LayoutElement>(square.gameObject).ignoreLayout = true;
            StyleSurface(square, 3f, ShellTheme.ControlBorder * .75f, Vector2.zero, ShellTheme.AccentRed);
            square.SetSiblingIndex(3);
        }

        static Image Layer(RectTransform root, string name, int index, float radius, Color color)
        {
            // A zero radius would make SetRadius fall back to the 24px corner sprite; keep a 1px corner.
            radius = Mathf.Max(radius, 1f);
            Transform existing = root.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : UiKit.Shape(root, name, radius, color);
            UiKit.SetRadius(image, radius);
            return Finish(image, index, color);
        }

        static Image Circle(RectTransform root, string name, int index, Color color)
        {
            Transform existing = root.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : UiKit.Disc(root, name, false, color);
            image.sprite = UiKitAssets.Load().Circle;
            image.type = Image.Type.Simple;
            return Finish(image, index, color);
        }

        static Image Finish(Image image, int index, Color color)
        {
            image.color = color;
            image.raycastTarget = false;
            UiKit.GetOrAdd<LayoutElement>(image.gameObject).ignoreLayout = true;
            image.transform.SetSiblingIndex(index);
            return image;
        }

        static void Destroy(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
