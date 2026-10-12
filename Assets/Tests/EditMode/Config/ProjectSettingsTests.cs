using NUnit.Framework;
using KMA.EditorTools;
using UnityEditor;
using UnityEditor.Build;

namespace KMA.Tests.Config
{
    public sealed class ProjectSettingsTests
    {
        [Test]
        public void UnityEditorVersionMatchesToolchainContract()
        {
            Assert.That(UnityEngine.Application.unityVersion, Is.EqualTo("6000.3.23f1"));
        }

        [Test]
        public void ProjectConfiguratorApplyRepairsProductNameDrift()
        {
            var originalProductName = PlayerSettings.productName;
            try
            {
                PlayerSettings.productName = "Drifted Product Name";

                ProjectConfigurator.Apply();

                Assert.That(PlayerSettings.productName, Is.EqualTo("Chạy trốn thể chất"));
            }
            finally
            {
                PlayerSettings.productName = originalProductName;
                ProjectConfigurator.Apply();
            }
        }

        [Test]
        public void AndroidIdentityAndSdkLevelsMatchContract()
        {
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo("com.kma.thechat"));
            Assert.That(PlayerSettings.Android.minSdkVersion,
                Is.EqualTo(AndroidSdkVersions.AndroidApiLevel25));
            Assert.That(PlayerSettings.Android.targetSdkVersion,
                Is.EqualTo(AndroidSdkVersions.AndroidApiLevel35));
        }

        [Test]
        public void AndroidRuntimeUsesIl2CppAndArm64Only()
        {
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.Android.targetArchitectures,
                Is.EqualTo(AndroidArchitecture.ARM64));
        }

        [Test]
        public void OrientationAllowsLandscapeOnly()
        {
            Assert.That(PlayerSettings.defaultInterfaceOrientation,
                Is.EqualTo(UIOrientation.AutoRotation));
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
        }

    }
}
