using System;
using System.Collections.Generic;
using KMA.Gameplay.UI;
using TMPro;
using UnityEngine;

namespace KMA.UI.Kit
{
    /// Helpers shared by the Kit (minigame) and Shell style audits.
    public static class StyleAuditShared
    {
        public static bool IsExempt(Component component) =>
            component.GetComponentInParent<StyleExempt>(true) != null;

        public static bool InPalette(Color color, Color[] palette) =>
            Array.Exists(palette, token => MinigameUiTheme.RgbEquals(token, color));

        public static void CheckText(TMP_Text text, float minimum, Color[] palette, bool checkColours, List<string> problems)
        {
            string path = PathOf(text.transform);
            var fonts = VietTypography.Library;
            if (fonts == null || !Array.Exists(new[] { fonts.title, fonts.buttonHud, fonts.regular, fonts.bold }, font => font == text.font))
                problems.Add($"{path}: font {(text.font == null ? "none" : text.font.name)} is not a KMA Vietnamese font");
            float smallest = text.enableAutoSizing ? text.fontSizeMin : text.fontSize;
            if (text is TextMeshProUGUI && smallest < minimum)
                problems.Add($"{path}: text size {smallest} is below {minimum}");
            if (checkColours && text.color.a >= .01f && !InPalette(text.color, palette))
                problems.Add($"{path}: text colour #{ColorUtility.ToHtmlStringRGBA(text.color)} is not a theme token");
        }

        public static string PathOf(Transform node)
        {
            string path = node.name;
            for (Transform current = node.parent; current != null; current = current.parent)
                path = current.name + "/" + path;
            return path;
        }
    }
}
