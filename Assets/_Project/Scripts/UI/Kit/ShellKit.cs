using KMA.Gameplay.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    /// Builds and restyles the shared widgets of every screen outside the minigames, in the menu key art
    /// style: a cream face in a navy border over a hard navy offset shadow. Owns look and press
    /// feedback; callers own position and size. Every call is idempotent.
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
