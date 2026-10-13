using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    public sealed class ShellButtonFamilyTests
    {
        [Test]
        public void BrutalPrefabIsGone() =>
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Btn_Brutal.prefab"), Is.Null);

        [Test]
        public void ShellDisabledColoursComeFromTheTheme()
        {
            UITheme.ShellStyle shell = UITheme.Shared.Shell;
            Assert.That(shell.disabledFace.a, Is.EqualTo(1f).Within(.01f));
            Assert.That(ShellTheme.DisabledText, Is.EqualTo(shell.disabledText));
        }
    }
}
