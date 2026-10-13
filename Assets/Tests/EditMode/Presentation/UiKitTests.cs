using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class UiKitTests
    {
        GameObject root;
        UiKitAssets assets;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("KitRoot", typeof(RectTransform));
            assets = UiKitAssets.Load();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Panel_UsesTheBakedRoundRectTheSurfaceTokenAndASoftShadow()
        {
            Image panel = UiKit.Panel(root.transform, "Panel");
            Assert.That(panel.sprite, Is.SameAs(assets.RoundRect24));
            Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(panel.color, Is.EqualTo(MinigameUiTheme.WithAlpha(MinigameUiTheme.Surface, MinigameUiTheme.SurfaceOpaque)));
            var shadow = panel.GetComponent<Shadow>();
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow, Is.Not.InstanceOf<Outline>());
            Assert.That(shadow.effectDistance, Is.EqualTo(MinigameUiTheme.ShadowOffset));
        }

        [Test]
        public void SetRadius_ScalesTheBakedCornerToTheRequestedRadius()
        {
            Image image = UiKit.Shape(root.transform, "Shape", 12f, Color.white);
            Assert.That(image.sprite, Is.SameAs(assets.RoundRect24));
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(2f).Within(.001f));
            UiKit.SetRadius(image, 36f);
            Assert.That(image.sprite, Is.SameAs(assets.RoundRect36));
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1f).Within(.001f));
        }

        [TestCase(ButtonVariant.Primary)]
        [TestCase(ButtonVariant.Secondary)]
        [TestCase(ButtonVariant.Danger)]
        public void ButtonVariants_KeepReadableKitTextOnTheirFace(ButtonVariant variant)
        {
            ButtonHandle handle = UiKit.Button(root.transform, "Button", "TIẾP TỤC", variant);
            Color background = variant == ButtonVariant.Secondary ? handle.Fill.color : handle.Face.color;
            Assert.That(MinigameUiTheme.ContrastRatio(handle.Label.color, background), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(handle.Label.font, Is.SameAs(assets.Font));
            Assert.That(handle.Label.fontSizeMin, Is.GreaterThanOrEqualTo(MinigameUiTheme.MinimumFontSize));
            Assert.That(handle.Face.sprite, Is.SameAs(assets.RoundRect36));
            Assert.That(((RectTransform)handle.Button.transform).sizeDelta.y, Is.EqualTo(MinigameUiTheme.ButtonHeight));
            Assert.That(handle.Button.transition, Is.EqualTo(Selectable.Transition.None));
        }

        [Test]
        public void ApplyVariant_SwitchesFaceFillAndLabelTogether()
        {
            ButtonHandle handle = UiKit.Button(root.transform, "Button", "DỄ", ButtonVariant.Secondary);
            Assert.That(handle.Fill.gameObject.activeSelf, Is.True);
            UiKit.ApplyVariant(handle, ButtonVariant.Primary);
            Assert.That(handle.Fill.gameObject.activeSelf, Is.False);
            Assert.That(handle.Face.color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(handle.Label.color, Is.EqualTo(MinigameUiTheme.Surface));
            Assert.That(handle.Feedback.RestColor, Is.EqualTo(MinigameUiTheme.Accent));
            UiKit.ApplyVariant(handle, ButtonVariant.Danger);
            Assert.That(handle.Face.color, Is.EqualTo(MinigameUiTheme.Energy));
        }

        [Test]
        public void StyleButton_ReplacesBrutalistPartsAndIsIdempotent()
        {
            var legacy = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            legacy.transform.SetParent(root.transform, false);
            new GameObject("Shadow", typeof(RectTransform), typeof(Image)).transform.SetParent(legacy.transform, false);
            var visual = new GameObject("Visual", typeof(RectTransform), typeof(Image));
            visual.transform.SetParent(legacy.transform, false);
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(visual.transform, false);

            var button = legacy.GetComponent<Button>();
            UiKit.StyleButton(button, ButtonVariant.Primary, "TIẾP");
            UiKit.StyleButton(button, ButtonVariant.Primary, "TIẾP");

            Assert.That(legacy.transform.Find("Shadow"), Is.Null);
            Assert.That(visual.GetComponent<Image>(), Is.Null);
            Assert.That(legacy.GetComponent<Outline>(), Is.Null);
            Assert.That(legacy.GetComponents<KitPressFeedback>(), Has.Length.EqualTo(1));
            int fills = 0;
            foreach (Transform child in legacy.transform)
                if (child.name == "Fill") fills++;
            Assert.That(fills, Is.EqualTo(1));
            Assert.That(legacy.GetComponent<Image>().sprite, Is.SameAs(assets.RoundRect36));
            Assert.That(label.text, Is.EqualTo("TIẾP"));
            Assert.That(label.font, Is.SameAs(assets.Font));
        }

        [Test]
        public void Bar_FillGrowsWithValueAndThePipFollows()
        {
            KitBar bar = UiKit.Bar(root.transform, "Bar", pip: true, label: true);
            bar.SetValue(.42f);
            Assert.That(bar.Value, Is.EqualTo(.42f).Within(.001f));
            Assert.That(bar.Fill.rectTransform.anchorMax.x, Is.EqualTo(.42f).Within(.001f));
            Assert.That(bar.Pip.rectTransform.anchorMin.x, Is.EqualTo(.42f).Within(.001f));
            Assert.That(bar.Fill.color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(bar.Track.color, Is.EqualTo(MinigameUiTheme.Track));
            bar.SetValue(2f);
            Assert.That(bar.Value, Is.EqualTo(1f));
            bar.SetValue(0f);
            Assert.That(bar.Fill.enabled, Is.False, "an empty bar hides its fill instead of drawing a sliver");
            Assert.That(bar.Label, Is.Not.Null);
        }

        [Test]
        public void Joystick_RestsInTheSharedControlColours()
        {
            var area = UiKit.Rect(root.transform, "Area");
            JoystickHandle stick = UiKit.Joystick(area);
            Assert.That(stick.Base.sprite, Is.SameAs(assets.Circle));
            Assert.That(stick.Rim.sprite, Is.SameAs(assets.Ring));
            Assert.That(stick.Base.color, Is.EqualTo(KitControlState.Fill(ControlState.Rest)));
            Assert.That(stick.Rim.color, Is.EqualTo(KitControlState.Border(ControlState.Rest)));
            Assert.That(stick.Base.rectTransform.sizeDelta.x, Is.EqualTo(MinigameUiTheme.JoystickBase));
            Assert.That(stick.Knob.rectTransform.sizeDelta.x, Is.EqualTo(MinigameUiTheme.JoystickKnob));
        }

        [Test]
        public void RoundButton_DrawsRimFaceAndLabelAndPutsFeedbackOnTheHitArea()
        {
            var hitArea = UiKit.Rect(root.transform, "ActionButton");
            RoundButtonHandle round = UiKit.RoundButton(hitArea, "ĐÁNH");
            Assert.That(round.Rim.sprite, Is.SameAs(assets.Ring));
            Assert.That(round.Face.color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(round.Label.color, Is.EqualTo(MinigameUiTheme.Surface));
            Assert.That(round.Feedback.gameObject, Is.SameAs(hitArea.gameObject));
            Assert.That(round.Root.sizeDelta.x, Is.EqualTo(MinigameUiTheme.RoundButton));
            Assert.That(round.Shadow.color,
                Is.EqualTo(MinigameUiTheme.WithAlpha(MinigameUiTheme.TextOutline, MinigameUiTheme.ShadowColor.a)));
        }

        [Test]
        public void StylePauseButton_DrawsTwoBarsOnceAndDropsLegacyText()
        {
            var pause = new GameObject("PausePanel", typeof(RectTransform), typeof(Image), typeof(Button));
            pause.transform.SetParent(root.transform, false);
            var legacyLabel = new GameObject("Label", typeof(RectTransform), typeof(Text));
            legacyLabel.transform.SetParent(pause.transform, false);

            UiKit.StylePauseButton((RectTransform)pause.transform);
            UiKit.StylePauseButton((RectTransform)pause.transform);

            Assert.That(pause.GetComponentsInChildren<Text>(true), Is.Empty);
            int bars = 0;
            foreach (Transform child in pause.transform)
                if (child.name == "BarLeft" || child.name == "BarRight") bars++;
            Assert.That(bars, Is.EqualTo(2));
            Assert.That(pause.GetComponent<Image>().sprite, Is.SameAs(assets.RoundRect20));
            Assert.That(pause.GetComponents<Button>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void Countdown_UsesDisplaySizeAccentAndTheStroke()
        {
            TMP_Text countdown = UiKit.Countdown(root.transform, "Countdown");
            Assert.That(countdown.fontSize, Is.EqualTo(MinigameUiTheme.Display));
            Assert.That(countdown.color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(countdown.fontSharedMaterial, Is.SameAs(assets.Font.material));
            Assert.That(countdown.extraPadding, Is.True);
        }

        [Test]
        public void ControlPlate_NamesItsPartsForTheSprintPresenter()
        {
            var visual = UiKit.Rect(root.transform, "Visual");
            ControlPlateHandle plate = UiKit.ControlPlate(visual, "←", "TRÁI");
            Assert.That(visual.Find("Border"), Is.SameAs(plate.Border.transform));
            Assert.That(visual.Find("Background"), Is.SameAs(plate.Background.transform));
            Assert.That(visual.Find("Arrow").GetComponent<TMP_Text>().text, Is.EqualTo("←"));
            Assert.That(visual.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("TRÁI"));
            Assert.That(plate.Background.color, Is.EqualTo(KitControlState.Fill(ControlState.Rest)));
        }

        [Test]
        public void Slider_UsesAKitKnobAndATransparentHitArea()
        {
            SliderHandle slider = UiKit.Slider(root.transform, "Slider");
            Assert.That(slider.Knob.sprite, Is.SameAs(assets.Circle));
            Assert.That(slider.Knob.color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(slider.Slider.handleRect, Is.SameAs(slider.Knob.rectTransform));
            Assert.That(slider.Slider.GetComponent<Image>().color.a, Is.LessThan(.01f));
            Assert.That(slider.Knob.rectTransform.sizeDelta, Is.EqualTo(Vector2.one * MinigameUiTheme.SliderKnob), "the knob is square, not zero-height");
            var ring = (RectTransform)slider.Knob.transform.Find("Ring");
            Assert.That(ring.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(ring.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(ring.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(ring.offsetMax, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Chip_TextShrinksButNeverBelowTheMinimum()
        {
            ChipHandle chip = UiKit.Chip(root.transform, "Chip", "Di chuyển bằng joystick");
            Assert.That(chip.Background.color,
                Is.EqualTo(MinigameUiTheme.WithAlpha(MinigameUiTheme.Surface, MinigameUiTheme.SurfaceSoft)));
            Assert.That(chip.Label.enableAutoSizing, Is.True);
            Assert.That(chip.Label.fontSizeMin, Is.EqualTo(MinigameUiTheme.MinimumFontSize));
        }

        [Test]
        public void PlacePauseUsesTheKitCornerMargin()
        {
            var pause = (RectTransform)new GameObject("Pause", typeof(RectTransform)).transform;
            UiKit.PlacePause(pause);
            Assert.That(pause.anchoredPosition, Is.EqualTo(new Vector2(-MinigameUiTheme.SpaceMd, -MinigameUiTheme.SpaceMd)));
            Assert.That(pause.sizeDelta, Is.EqualTo(Vector2.one * MinigameUiTheme.ButtonHeight));
            Object.DestroyImmediate(pause.gameObject);
        }
    }
}
