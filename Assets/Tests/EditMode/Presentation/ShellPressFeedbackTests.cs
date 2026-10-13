using KMA.UI.Kit;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    public sealed class ShellPressFeedbackTests
    {
        GameObject root;

        [SetUp] public void SetUp() => root = new GameObject("PressRoot", typeof(RectTransform));
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void PressSlidesTheBodyOntoTheShadowAndReleaseEasesBack()
        {
            ShellButtonHandle handle = ShellKit.Button(root.transform, "Play", "CHƠI", ShellButtonVariant.Primary);
            handle.Feedback.Press();
            Assert.That(handle.Feedback.IsPressed, Is.True);
            Assert.That(handle.Feedback.Offset, Is.EqualTo(ShellTheme.PressedShift));
            handle.Feedback.Tick(1f);
            Assert.That(handle.Feedback.Offset, Is.EqualTo(ShellTheme.PressedShift), "held presses stay down");

            handle.Feedback.Release();
            handle.Feedback.Tick(MinigameUiTheme.PressRestoreSeconds * .5f);
            Assert.That(handle.Feedback.Offset.magnitude, Is.GreaterThan(0f).And.LessThan(ShellTheme.PressedShift.magnitude));
            handle.Feedback.Tick(MinigameUiTheme.PressRestoreSeconds);
            Assert.That(handle.Feedback.Offset, Is.EqualTo(Vector2.zero));
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellTheme.Gold));
        }

        [Test]
        public void DisabledButtonPaintsDisabledTokensAndIgnoresPresses()
        {
            ShellButtonHandle handle = ShellKit.Button(root.transform, "Continue", "TIẾP TỤC", ShellButtonVariant.Secondary);
            handle.Button.interactable = false;
            handle.Feedback.Tick(0f);
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellTheme.DisabledFace));
            Assert.That(handle.Label.color, Is.EqualTo(ShellTheme.DisabledText));
            handle.Feedback.Press();
            Assert.That(handle.Feedback.IsPressed, Is.False);
            Assert.That(handle.Feedback.Offset, Is.EqualTo(Vector2.zero));

            handle.Button.interactable = true;
            handle.Feedback.Tick(0f);
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(handle.Label.color, Is.EqualTo(ShellTheme.Ink));
        }

        [Test]
        public void PressSlidesFromAnOffCentreRestPosition()
        {
            var badge = UiKit.Rect(root.transform, "Badge");
            badge.anchoredPosition = new Vector2(0f, -120f);
            var feedback = root.AddComponent<ShellPressFeedback>();
            feedback.Configure(badge, null, null, Color.white, Color.white);
            Assert.That(badge.anchoredPosition, Is.EqualTo(new Vector2(0f, -120f)), "configuring must not move the badge");
            feedback.Press();
            Assert.That(badge.anchoredPosition, Is.EqualTo(new Vector2(0f, -120f) + ShellTheme.PressedShift));
            feedback.Release();
            feedback.Tick(MinigameUiTheme.PressRestoreSeconds);
            Assert.That(badge.anchoredPosition, Is.EqualTo(new Vector2(0f, -120f)));
        }

        [Test]
        public void ADisabledButtonIsPaintedDisabledFromItsFirstFrame()
        {
            var go = new GameObject("Locked", typeof(RectTransform), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(root.transform, false);
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.interactable = false;
            ShellButtonHandle handle = ShellKit.StyleButton(button, ShellButtonVariant.Primary, "KHÓA");
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellTheme.DisabledFace));
        }
    }
}
