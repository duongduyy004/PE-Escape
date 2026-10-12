using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace KMA.EditorTools
{
    public static class ProjectConfigurator
    {
        const string ProductName = "Chạy trốn thể chất";
        const string AndroidApplicationId = "com.kma.thechat";
        const string AndroidIconPath = "Assets/_Project/Art/Brand/AppIcon.png";
        const string AndroidAdaptiveForegroundPath = "Assets/_Project/Art/Brand/GameLogo.png";
        const int AndroidMinApiLevel = 25;
        const int AndroidTargetApiLevel = 35;
        const int DspBufferSize = 256;

        [MenuItem("KMA/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.productName = ProductName;
            ApplyAndroidSettings();
            ApplyAndroidBranding();
            ApplyOrientation();
            ApplyAudioSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] Project settings applied.");
        }

        static void ApplyAndroidSettings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidApplicationId);
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)AndroidMinApiLevel;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)AndroidTargetApiLevel;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
        }

        static void ApplyAndroidBranding()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidIconPath);
            Texture2D foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidAdaptiveForegroundPath);
            if (icon == null || foreground == null)
                throw new InvalidOperationException("Android app branding images are missing.");

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var slot in slots)
                {
                    var textures = new Texture2D[slot.maxLayerCount];
                    textures[0] = icon;
                    for (int layer = 1; layer < textures.Length; layer++)
                        textures[layer] = foreground;
                    slot.SetTextures(textures);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }
        }

        static void ApplyOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        static void ApplyAudioSettings()
        {
            var audioManager = AssetDatabase
                .LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")
                .FirstOrDefault();
            if (audioManager == null)
            {
                throw new InvalidOperationException("Could not load ProjectSettings/AudioManager.asset.");
            }

            var serializedAudioManager = new SerializedObject(audioManager);
            var requestedBufferSize = serializedAudioManager
                .FindProperty("m_RequestedDSPBufferSize");
            if (requestedBufferSize == null)
            {
                throw new InvalidOperationException(
                    "AudioManager no longer exposes m_RequestedDSPBufferSize.");
            }

            requestedBufferSize.intValue = DspBufferSize;
            serializedAudioManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audioManager);

            var runtimeConfiguration = AudioSettings.GetConfiguration();
            runtimeConfiguration.dspBufferSize = DspBufferSize;
            if (!AudioSettings.Reset(runtimeConfiguration))
            {
                throw new InvalidOperationException("Unity rejected the DSP buffer configuration.");
            }
        }
    }
}
