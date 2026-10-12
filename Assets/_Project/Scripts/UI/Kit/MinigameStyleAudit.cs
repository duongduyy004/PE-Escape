using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KMA.Gameplay.UI;

namespace KMA.UI.Kit
{
    /// The rules every minigame UI tree must follow, shared by EditMode and PlayMode guard tests.
    public static class MinigameStyleAudit
    {
        public static List<string> Audit(GameObject root)
        {
            var problems = new List<string>();
            UiKitAssets assets = UiKitAssets.Load();
            var kitSprites = new HashSet<Sprite>(assets.AllSprites());

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (IsExempt(image))
                    continue;
                string path = PathOf(image.transform);
                bool sprintFoot = image.name == "FootIcon" && image.sprite != null &&
                    (image.sprite.name == "SprintLeftFootIcon" || image.sprite.name == "SprintRightFootIcon");
                bool spriteOk = image.sprite == null ? IsScrim(image.color) : kitSprites.Contains(image.sprite) || sprintFoot;
                if (!spriteOk)
                    problems.Add($"{path}: sprite {(image.sprite == null ? "none" : image.sprite.name)} is not a UI kit sprite");
                if (!IsToken(image.color))
                    problems.Add($"{path}: colour #{ColorUtility.ToHtmlStringRGBA(image.color)} is not a theme token");
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string path = PathOf(text.transform);
                var fonts = VietTypography.Library;
                if (fonts == null || !Array.Exists(new[] { fonts.title, fonts.buttonHud, fonts.regular, fonts.bold }, font => font == text.font))
                    problems.Add($"{path}: font {(text.font == null ? "none" : text.font.name)} is not a KMA Vietnamese font");
                float smallest = text.enableAutoSizing ? text.fontSizeMin : text.fontSize;
                if (text is TextMeshProUGUI && smallest < MinigameUiTheme.MinimumFontSize)
                    problems.Add($"{path}: text size {smallest} is below {MinigameUiTheme.MinimumFontSize}");
                if (!IsToken(text.color))
                    problems.Add($"{path}: text colour #{ColorUtility.ToHtmlStringRGBA(text.color)} is not a theme token");
            }

            foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
                problems.Add($"{PathOf(legacy.transform)}: legacy UnityEngine.UI.Text");

            return problems;
        }

        static bool IsExempt(Image image) =>
            (image.GetComponentInParent<GameplayDialogueView>(true)?.IsArtwork(image) ?? false) || image.color.a < .01f || image.name == "Icon" || HasAncestor(image.transform, "FinishLine");

        static bool IsScrim(Color color) => MinigameUiTheme.RgbEquals(color, MinigameUiTheme.Scrim);

        static bool IsToken(Color color) =>
            Array.Exists(MinigameUiTheme.Palette(), token => MinigameUiTheme.RgbEquals(token, color));

        static bool HasAncestor(Transform node, string name)
        {
            for (Transform current = node; current != null; current = current.parent)
                if (current.name == name)
                    return true;
            return false;
        }

        static string PathOf(Transform node)
        {
            string path = node.name;
            for (Transform current = node.parent; current != null; current = current.parent)
                path = current.name + "/" + path;
            return path;
        }
    }
}
