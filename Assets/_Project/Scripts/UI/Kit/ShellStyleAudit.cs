using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    public readonly struct ShellAuditOptions
    {
        public readonly float MinimumFontSize;
        public readonly bool CheckSprites;
        public readonly bool CheckColours;
        public readonly bool CheckEffects;

        public ShellAuditOptions(float minimumFontSize, bool checkSprites, bool checkColours, bool checkEffects)
        {
            MinimumFontSize = minimumFontSize;
            CheckSprites = checkSprites;
            CheckColours = checkColours;
            CheckEffects = checkEffects;
        }

        public static ShellAuditOptions Default => new ShellAuditOptions(MinigameUiTheme.MinimumFontSize, true, true, true);
        /// The splash is a show-piece: its own slanted art and type, only the font set and a 24 px floor.
        public static ShellAuditOptions Splash => new ShellAuditOptions(24f, false, false, false);
    }

    /// The rules every screen outside the minigames must follow, shared by EditMode and PlayMode guard tests.
    public static class ShellStyleAudit
    {
        public static List<string> Audit(GameObject root) => Audit(root, ShellAuditOptions.Default);

        public static List<string> Audit(GameObject root, ShellAuditOptions options)
        {
            var problems = new List<string>();
            var kitSprites = new HashSet<Sprite>(UiKitAssets.Load().AllSprites());
            Color[] palette = ShellTheme.Palette();

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.color.a < .01f || StyleAuditShared.IsExempt(image)) continue;
                string path = StyleAuditShared.PathOf(image.transform);
                if (options.CheckSprites && image.sprite != null && !kitSprites.Contains(image.sprite))
                    problems.Add($"{path}: sprite {image.sprite.name} is not a UI kit sprite");
                if (options.CheckColours && !StyleAuditShared.InPalette(image.color, palette))
                    problems.Add($"{path}: colour #{ColorUtility.ToHtmlStringRGBA(image.color)} is not a Shell token");
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (!StyleAuditShared.IsExempt(text) && text.color.a >= .01f)
                    StyleAuditShared.CheckText(text, options.MinimumFontSize, palette, options.CheckColours, problems);

            if (options.CheckEffects)
                foreach (Shadow effect in root.GetComponentsInChildren<Shadow>(true))
                    if (!StyleAuditShared.IsExempt(effect))
                        problems.Add($"{StyleAuditShared.PathOf(effect.transform)}: {effect.GetType().Name} effect; Shell draws borders and shadows as layers");

            foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
                problems.Add($"{StyleAuditShared.PathOf(legacy.transform)}: legacy UnityEngine.UI.Text");

            return problems;
        }
    }
}
