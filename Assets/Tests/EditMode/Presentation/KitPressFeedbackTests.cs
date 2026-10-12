using KMA.UI.Kit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class KitPressFeedbackTests
    {
        GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("FeedbackRoot", typeof(RectTransform));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void PressLightensAndShrinksThenReleaseEasesBackOverTheRestoreTime()
        {
            var face = root.AddComponent<Image>();
            face.color = MinigameUiTheme.Accent;
            var feedback = root.AddComponent<KitPressFeedback>();
            feedback.Configure(face, (RectTransform)root.transform);

            feedback.Press();
            Assert.That(feedback.IsPressed, Is.True);
            Assert.That(feedback.Scale, Is.EqualTo(MinigameUiTheme.PressScale).Within(.001f));
            Assert.That(face.color, Is.EqualTo(MinigameUiTheme.Lighten(MinigameUiTheme.Accent, MinigameUiTheme.PressLighten)));

            feedback.Tick(1f);
            Assert.That(feedback.Scale, Is.EqualTo(MinigameUiTheme.PressScale).Within(.001f), "held presses stay pressed");

            feedback.Release();
            feedback.Tick(MinigameUiTheme.PressRestoreSeconds * .5f);
            Assert.That(feedback.Scale, Is.GreaterThan(MinigameUiTheme.PressScale).And.LessThan(1f));
            feedback.Tick(MinigameUiTheme.PressRestoreSeconds);
            Assert.That(feedback.Scale, Is.EqualTo(1f).Within(.001f));
            Assert.That(face.color, Is.EqualTo(MinigameUiTheme.Accent));
        }

        [Test]
        public void IconOpacityStaysPressedUntilReleaseAndResetsWhenDisabled()
        {
            var face = root.AddComponent<Image>();
            face.color = Color.white;
            var feedback = root.AddComponent<KitPressFeedback>();
            feedback.Configure(face, (RectTransform)root.transform, .55f);

            feedback.OnPointerDown(null);
            feedback.Tick(1f);
            Assert.That(face.color.a, Is.EqualTo(.55f).Within(.001f));
            feedback.OnPointerUp(null);
            feedback.Tick(MinigameUiTheme.PressRestoreSeconds);
            Assert.That(face.color, Is.EqualTo(Color.white));

            feedback.OnPointerDown(null);
            feedback.enabled = false;
            Assert.That(face.color, Is.EqualTo(Color.white));
            Assert.That(feedback.IsPressed, Is.False);
        }

        [Test]
        public void SetRestColorRecoloursAnIdleFace()
        {
            var face = root.AddComponent<Image>();
            var feedback = root.AddComponent<KitPressFeedback>();
            feedback.Configure(face, (RectTransform)root.transform);
            feedback.SetRestColor(MinigameUiTheme.Energy);
            Assert.That(face.color, Is.EqualTo(MinigameUiTheme.Energy));
        }

        [Test]
        public void DisabledButtonsUseTheDisabledTokensAtFullOpacity()
        {
            ButtonHandle handle = UiKit.Button(root.transform, "Shoot", "GIỮ ĐỂ SÚT", ButtonVariant.Primary);
            Color restLabel = handle.Label.color;
            handle.Button.interactable = false;
            handle.Feedback.Tick(0f);
            CanvasGroup group = handle.Button.GetComponent<CanvasGroup>();
            Assert.That(group == null || group.alpha == 1f, Is.True, "disabled buttons must not fade into the background");
            Assert.That(handle.Face.color, Is.EqualTo(MinigameUiTheme.DisabledSurface));
            Assert.That(handle.Label.color, Is.EqualTo(MinigameUiTheme.DisabledText));
            Assert.That(MinigameUiTheme.ContrastRatio(MinigameUiTheme.DisabledText, MinigameUiTheme.DisabledSurface),
                Is.GreaterThanOrEqualTo(3f));
            handle.Feedback.Press();
            Assert.That(handle.Feedback.IsPressed, Is.False);

            handle.Button.interactable = true;
            handle.Feedback.Tick(0f);
            Assert.That(handle.Face.color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(handle.Label.color, Is.EqualTo(restLabel));
        }

        [Test]
        public void ApplyVariantOnADisabledButtonKeepsTheDisabledTokens()
        {
            ButtonHandle handle = UiKit.Button(root.transform, "Shoot", "GIỮ ĐỂ SÚT", ButtonVariant.Primary);
            handle.Button.interactable = false;
            handle.Feedback.Tick(0f);
            UiKit.ApplyVariant(handle, ButtonVariant.Secondary);
            handle.Feedback.Tick(0f);
            Assert.That(handle.Face.color, Is.EqualTo(MinigameUiTheme.DisabledSurface));
            Assert.That(handle.Label.color, Is.EqualTo(MinigameUiTheme.DisabledText));

            handle.Button.interactable = true;
            handle.Feedback.Tick(0f);
            Assert.That(handle.Face.color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(handle.Label.color, Is.EqualTo(MinigameUiTheme.TextPrimary));
        }

        [Test]
        public void ControlStates_MakeTheHintMoreProminentThanRest()
        {
            Assert.That(KitControlState.Border(ControlState.Hint).a, Is.GreaterThan(KitControlState.Border(ControlState.Rest).a));
            Assert.That(KitControlState.Fill(ControlState.Hint).a, Is.GreaterThan(KitControlState.Fill(ControlState.Rest).a));
            Assert.That(KitControlState.Fill(ControlState.Pressed), Is.Not.EqualTo(KitControlState.Fill(ControlState.Hint)));
            Assert.That(KitControlState.Fill(ControlState.Disabled).a, Is.LessThan(KitControlState.Fill(ControlState.Rest).a));
            Assert.That(MinigameUiTheme.RgbEquals(KitControlState.Fill(ControlState.Rest), MinigameUiTheme.Surface), Is.True);
            Assert.That(MinigameUiTheme.RgbEquals(KitControlState.Border(ControlState.Rest), MinigameUiTheme.Accent), Is.True);
        }
    }
}
