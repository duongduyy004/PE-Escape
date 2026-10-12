using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public enum VietFontRole { Body, BodyBold, Title, ButtonPrimary, ButtonSecondary, Hud, Symbol }

    public static class VietTypography
    {
        static VietFontLibrary library;
        public static VietFontLibrary Library => library != null ? library :
            library = Resources.Load<VietFontLibrary>("VietFontLibrary");

        public static VietFontRole RoleOf(TMP_Text text)
        {
            string value = VietText.Fix(text.text ?? string.Empty);
            if (value == "←" || value == "→" || text.name == "Arrow") return VietFontRole.Symbol;
            if (value == "THỂ CHẤT KMA" || text.name == "TitleTop" || text.name == "TitleKMA")
                return VietFontRole.Title;
            var button = text.GetComponentInParent<Button>(true);
            if (button != null)
            {
                string name = button.name.ToLowerInvariant();
                return name.Contains("back") || name.Contains("settings") || name.Contains("cancel") ||
                    name.Contains("quit") || name.Contains("menu") ? VietFontRole.ButtonSecondary : VietFontRole.ButtonPrimary;
            }
            string path = text.name.ToLowerInvariant();
            for (var parent = text.transform.parent; parent != null && parent.GetComponent<Canvas>() == null; parent = parent.parent)
                path += "/" + parent.name.ToLowerInvariant();
            if (path.Contains("score") || path.Contains("time") || path.Contains("distance") ||
                path.Contains("countdown") || path.Contains("remaining") || path.Contains("combo") ||
                path.Contains("rank") || path.Contains("power") || path.Contains("playerlabel")) return VietFontRole.Hud;
            return (text.fontStyle & FontStyles.Bold) != 0 || (Library != null && text.font == Library.bold)
                ? VietFontRole.BodyBold : VietFontRole.Body;
        }

        public static void Apply(TMP_Text text) => Apply(text, RoleOf(text));

        public static void Apply(TMP_Text text, VietFontRole role)
        {
            var fonts = Library;
            if (fonts == null) throw new InvalidOperationException("Run Tools/KMA/Setup Vietnamese Fonts first.");
            text.text = VietText.Fix(text.text);
            text.enableVertexGradient = role == VietFontRole.Title;
            switch (role)
            {
                case VietFontRole.Title:
                    text.font = fonts.title;
                    text.fontSharedMaterial = fonts.titleMaterial;
                    text.color = Color.white;
                    var top = new Color32(255, 224, 102, 255);
                    var bottom = new Color32(255, 180, 0, 255);
                    text.colorGradient = new VertexGradient(top, top, bottom, bottom);
                    break;
                case VietFontRole.ButtonPrimary:
                case VietFontRole.ButtonSecondary:
                    text.font = fonts.buttonHud;
                    text.fontSharedMaterial = role == VietFontRole.ButtonPrimary ? fonts.primaryMaterial : fonts.secondaryMaterial;
                    float maxSize = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
                    float minSize = text.enableAutoSizing ? text.fontSizeMin : Mathf.Max(10f, text.fontSize * .6f);
                    text.enableAutoSizing = true;
                    text.fontSizeMax = maxSize;
                    text.fontSizeMin = Mathf.Min(maxSize, minSize);
                    break;
                case VietFontRole.Hud:
                    text.font = fonts.buttonHud;
                    text.fontSharedMaterial = fonts.buttonHud.material;
                    break;
                case VietFontRole.Symbol:
                    text.font = fonts.title;
                    text.fontSharedMaterial = fonts.title.material;
                    break;
                default:
                    text.font = role == VietFontRole.BodyBold ? fonts.bold : fonts.regular;
                    text.fontSharedMaterial = role == VietFontRole.BodyBold ? fonts.bodyBoldMaterial : fonts.bodyMaterial;
                    break;
            }
            // Weight is already baked into the selected Bold/Black font; avoid synthetic expansion.
            text.fontStyle &= ~FontStyles.Bold;
            text.extraPadding = true;
            text.lineSpacing = Mathf.Max(text.lineSpacing, 15f);
            FitWithinBounds(text);
        }
        /// Preserve authored wrapping and size limits; shrink before using a bounded fallback.
        public static void FitWithinBounds(TMP_Text text)
        {
            // World-space TMP meshes use world units rather than UI-sized text boxes.
            if (!(text is TextMeshProUGUI)) return;
            float maxSize = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
            float minSize = text.enableAutoSizing ? text.fontSizeMin :
                Mathf.Min(maxSize, Mathf.Max(KMA.UI.Kit.MinigameUiTheme.MinimumFontSize, maxSize * .6f));
            text.enableAutoSizing = true;
            text.fontSizeMax = maxSize;
            text.fontSizeMin = Mathf.Min(maxSize, minSize);
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
