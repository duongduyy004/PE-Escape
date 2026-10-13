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
            Color[] palette = MinigameUiTheme.Palette();

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (IsExempt(image))
                    continue;
                string path = StyleAuditShared.PathOf(image.transform);
                bool sprintFoot = image.name == "FootIcon" && image.sprite != null &&
                    (image.sprite.name == "SprintLeftFootIcon" || image.sprite.name == "SprintRightFootIcon" || image.sprite.name == "SprintShoe");
                bool spriteOk = image.sprite == null ? IsScrim(image.color) : kitSprites.Contains(image.sprite) || sprintFoot;
                if (!spriteOk)
                    problems.Add($"{path}: sprite {(image.sprite == null ? "none" : image.sprite.name)} is not a UI kit sprite");
                if (!StyleAuditShared.InPalette(image.color, palette))
                    problems.Add($"{path}: colour #{ColorUtility.ToHtmlStringRGBA(image.color)} is not a theme token");
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (!StyleAuditShared.IsExempt(text))
                    StyleAuditShared.CheckText(text, MinigameUiTheme.MinimumFontSize, palette, true, problems);

            foreach (Outline outline in root.GetComponentsInChildren<Outline>(true))
                if (!StyleAuditShared.IsExempt(outline) && outline.GetComponentInParent<KitBar>(true) == null &&
                    outline.name != "Countdown")
                    problems.Add($"{StyleAuditShared.PathOf(outline.transform)}: Outline effect; kit borders are layers");

            foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
                problems.Add($"{StyleAuditShared.PathOf(legacy.transform)}: legacy UnityEngine.UI.Text");

            return problems;
        }

        static bool IsExempt(Image image) =>
            StyleAuditShared.IsExempt(image) ||
            (image.GetComponentInParent<GameplayDialogueView>(true)?.IsArtwork(image) ?? false) || image.color.a < .01f ||
            image.name == "Icon" || HasAncestor(image.transform, "FinishLine");

        static bool IsScrim(Color color) => MinigameUiTheme.RgbEquals(color, MinigameUiTheme.Scrim);

        static bool HasAncestor(Transform node, string name)
        {
            for (Transform current = node; current != null; current = current.parent)
                if (current.name == name)
                    return true;
            return false;
        }
    }
}
