using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class SettingsPresentationTests
    {
        GameObject root;

        [SetUp] public void SetUp() => root = new GameObject("settings", typeof(RectTransform));
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void SettingsPanelFollowsTheShellStyle()
        {
            var screen = root.AddComponent<SettingsScreen>();
            SettingsPresentationBuilder.Build(screen);
            Transform panel = root.transform.Find("SettingsPanel");
            Assert.That(panel.Find(ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(root.GetComponent<Image>().color, Is.EqualTo(UITheme.Shared.Scrim));
            var problems = ShellStyleAudit.Audit(root);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void BackStillRoutesBack()
        {
            var screen = root.AddComponent<SettingsScreen>();
            SettingsPresentationBuilder.Build(screen);
            Button back = root.transform.Find("SettingsPanel/BackButton").GetComponent<Button>();
            Assert.That(back.onClick.GetPersistentEventCount() + CountRuntime(back), Is.GreaterThan(0));
            Assert.That(back.GetComponent<ShellPressFeedback>(), Is.Not.Null);
        }

        static int CountRuntime(Button button)
        {
            var field = typeof(UnityEngine.Events.UnityEventBase).GetField("m_Calls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var calls = field.GetValue(button.onClick);
            var runtime = calls.GetType().GetField("m_RuntimeCalls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(calls);
            return ((System.Collections.ICollection)runtime).Count;
        }
    }
}
