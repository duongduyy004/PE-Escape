using System.Linq;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class ShellStyleAuditTests
    {
        GameObject root;

        [SetUp] public void SetUp() => root = new GameObject("Screen", typeof(RectTransform));
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void ShellKitOutputPassesTheAudit()
        {
            ShellCardHandle card = ShellKit.Card(root.transform, "Card", decorated: true);
            ShellKit.Title(card.Root, "Title", "CÀI ĐẶT", 54f);
            ShellKit.Button(card.Root, "Back", "QUAY LẠI", ShellButtonVariant.Secondary);
            ShellKit.Chip(card.Root, "Lives", "5/5");
            ShellKit.Toggle(card.Root, "Toggle");
            ShellKit.Slider(card.Root, "Slider");
            var problems = ShellStyleAudit.Audit(root);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void AuditCatchesOffStyleSpritesColoursSizesEffectsAndLegacyText()
        {
            var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            knob.transform.SetParent(root.transform, false);
            knob.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            knob.color = new Color32(255, 152, 0, 255);
            knob.gameObject.AddComponent<Outline>();
            var small = new GameObject("Small", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            small.transform.SetParent(root.transform, false);
            small.fontSize = 20f;
            new GameObject("Legacy", typeof(RectTransform), typeof(Text)).transform.SetParent(root.transform, false);

            var problems = ShellStyleAudit.Audit(root);
            Assert.That(problems.Any(p => p.Contains("Knob") && p.Contains("sprite")), Is.True, string.Join("\n", problems));
            Assert.That(problems.Any(p => p.Contains("Knob") && p.Contains("colour")), Is.True);
            Assert.That(problems.Any(p => p.Contains("Knob") && p.Contains("Outline")), Is.True);
            Assert.That(problems.Any(p => p.Contains("Small") && p.Contains("size")), Is.True);
            Assert.That(problems.Any(p => p.Contains("legacy")), Is.True);
        }

        [Test]
        public void ExemptSubtreesAreSkipped()
        {
            var icon = new GameObject("IconGlyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(root.transform, false);
            icon.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            icon.color = new Color32(255, 152, 0, 255);
            icon.gameObject.AddComponent<StyleExempt>();
            var clear = new GameObject("Hit", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            clear.transform.SetParent(root.transform, false);
            clear.color = Color.clear;
            Assert.That(ShellStyleAudit.Audit(root), Is.Empty);
        }

        [Test]
        public void SplashOptionsOnlyCheckFontsAndTheLowerFloor()
        {
            var text = new GameObject("Status", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(root.transform, false);
            KMA.Gameplay.UI.VietTypography.Apply(text, KMA.Gameplay.UI.VietFontRole.Hud);
            text.enableAutoSizing = false;
            text.fontSize = 24f;
            text.color = new Color32(255, 152, 0, 255);
            Assert.That(ShellStyleAudit.Audit(root, ShellAuditOptions.Splash), Is.Empty);
            text.fontSize = 20f;
            Assert.That(ShellStyleAudit.Audit(root, ShellAuditOptions.Splash).Any(p => p.Contains("size")), Is.True);
        }

        [Test]
        public void MinigameAuditNowRejectsOutlineOutsideBarsAndHonoursTheMarker()
        {
            Image panel = UiKit.Panel(root.transform, "Panel");
            panel.gameObject.AddComponent<Outline>();
            Assert.That(MinigameStyleAudit.Audit(root).Any(p => p.Contains("Outline")), Is.True);
            panel.gameObject.AddComponent<StyleExempt>();
            Assert.That(MinigameStyleAudit.Audit(root), Is.Empty);
        }
    }
}
