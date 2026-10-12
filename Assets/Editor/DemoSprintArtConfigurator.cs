#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KMA.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KMA.EditorTools
{
    /// <summary>Repeatable demo artwork authoring through Unity's asset and scene APIs.</summary>
    public static class DemoSprintArtConfigurator
    {
        const string Art = "Assets/_Project/Art/";
        const string Animations = "Assets/_Project/Animations/";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Gameplay/PlayerRunnerVisual.prefab";
        const string RivalPrefab = "Assets/_Project/Prefabs/Gameplay/RivalRunner.prefab";
        const string BaseController = Animations + "RivalRunner.controller";
        const float RunnerVisualScale = 1.25f;

        static readonly string[] States = { "Idle", "Run", "Burst", "Stumble", "Celebrate", "Fail" };

        /// The player and every rival draw from a different pack character, so a glance at the
        /// track tells four runners apart. PlayerCharacter also owns the authored lane order.
        const string PlayerCharacter = CharacterArt.Hero;
        static readonly Dictionary<int, string> RivalCharacterByLane = new Dictionary<int, string>
        {
            { 1, "MalePerson" },
            { 3, "FemalePerson" },
            { 4, "FemaleAdventurer" }
        };

        /// <summary>One pack character's eight authored poses.</summary>
        sealed class RunnerArt
        {
            public string Folder;
            public Sprite Idle;
            public Sprite Hit;
            public Sprite FallDown;
            public Sprite[] Run;
            public Sprite[] Cheer;
        }

        [MenuItem("KMA/Demo/Configure Sprint Artwork")]
        public static void Configure()
        {
            AssetDatabase.Refresh();
            CampusBackdropAuthoring.EnsureArt();

            var characters = RivalCharacterByLane.Values.Concat(new[] { PlayerCharacter }).Distinct().ToArray();
            // Every pose is imported before any is loaded; see CharacterArt.ImportAll for why.
            CharacterArt.ImportAll();
            var artByCharacter = characters.ToDictionary(folder => folder, LoadCharacter);

            // The base controller's own clips belong to lane 1's character; every other character
            // gets a parallel clip set reached through an override controller.
            var baseCharacter = artByCharacter[RivalCharacterByLane[1]];
            foreach (string state in States)
                AuthorClip(state, baseCharacter, Animations + "RivalRunner_" + state + ".anim");
            var animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseController);

            var controllerByCharacter = new Dictionary<string, RuntimeAnimatorController>
            {
                { baseCharacter.Folder, animatorController }
            };
            foreach (var art in artByCharacter.Values.Where(value => value.Folder != baseCharacter.Folder))
                controllerByCharacter[art.Folder] = AuthorOverrideController(art, animatorController);

            AuthorRival(baseCharacter.Idle);
            AuthorPlayer(artByCharacter[PlayerCharacter].Idle, controllerByCharacter[PlayerCharacter]);

            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MG_Sprint.unity");
            var player = GameObject.Find("Player");
            if (player == null) throw new InvalidOperationException("Sprint Player root is missing.");
            foreach (Transform child in player.transform.Cast<Transform>().ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            var playerVisual = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), player.transform);
            // Same contract as the rivals: the Player root carries the lane centre.
            playerVisual.transform.localPosition = Vector3.zero;

            foreach (var rival in UnityEngine.Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None))
            {
                if (!RivalCharacterByLane.TryGetValue(rival.Lane, out string folder))
                    throw new InvalidOperationException("No authored character for Sprint lane " + rival.Lane);
                var rivalArt = artByCharacter[folder];
                var visual = rival.transform.Find("Visual");
                // The lane root already sits on the painted lane centre (SprintRivalMapping) and the
                // sprite pivot is at the feet, so the visual rides the root instead of a tuned offset.
                visual.localPosition = Vector3.zero;
                var renderer = visual.GetComponent<SpriteRenderer>();
                renderer.sprite = rivalArt.Idle;
                renderer.sortingOrder = 10 + rival.Lane;
                renderer.color = Color.white;
                var animator = rival.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controllerByCharacter[folder];
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }

            var parallax = UnityEngine.Object.FindFirstObjectByType<SprintParallax>();
            var serialized = new SerializedObject(parallax);
            var layers = serialized.FindProperty("layers");
            var sprites = new[]
            {
                CampusBackdropAuthoring.Load("CampusSky"),
                CampusBackdropAuthoring.Load("CampusSkyline"),
                CampusBackdropAuthoring.Load("SprintTrack")
            };
            float trackTop = SprintTrackLayout.WorldYForRow(460f);
            const float SkylineHeight = 2.3f;
            for (int i = 0; i < sprites.Length; i++)
            {
                var sprite = sprites[i];
                var layer = layers.GetArrayElementAtIndex(i);
                foreach (string field in new[] { "first", "second" })
                {
                    var tile = (Transform)layer.FindPropertyRelative(field).objectReferenceValue;
                    var renderer = tile.GetComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.sharedMaterial = SpriteMaterial();
                    renderer.color = Color.white;
                    renderer.flipX = field == "second"; // the mirrored second tile hides the raster seam, as before
                    renderer.sortingOrder = -30 + i * 10;
                    float parentScaleY = tile.parent == null ? 1f : tile.parent.lossyScale.y;
                    float scaleX = 25.6f / sprite.bounds.size.x;
                    Vector3 world = tile.position;
                    if (i == 2)
                    {
                        // The track carries the painted lanes runner Y is derived from (SprintTrackLayout).
                        tile.localScale = new Vector3(scaleX, SprintTrackLayout.BackdropScaleY / parentScaleY, 1f);
                        world.y = SprintTrackLayout.BackdropCenterY;
                    }
                    else if (i == 1)
                    {
                        tile.localScale = new Vector3(scaleX, SkylineHeight / sprite.bounds.size.y / parentScaleY, 1f);
                        world.y = trackTop + SkylineHeight * .5f;
                    }
                    else
                    {
                        tile.localScale = new Vector3(scaleX, 10.8f / sprite.bounds.size.y / parentScaleY, 1f);
                        world.y = 0f;
                    }
                    tile.position = world;
                }
            }
            SprintSceneConfigurator.ConfigureDialogueInScene(scene);
            EditorSceneManager.SaveScene(scene);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Brand/AppIcon.png");
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var slot in slots)
                {
                    var textures = new Texture2D[slot.maxLayerCount];
                    textures[0] = icon;
                    for (int layer = 1; layer < textures.Length; layer++)
                        textures[layer] = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Brand/GameLogo.png");
                    slot.SetTextures(textures);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] Sprint sprite clips, player prefab, parallax artwork and app icon configured.");
        }

        static Sprite ImportSprite(string relativePath)
        {
            string path = Art + relativePath;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Missing demo art: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>One character's Sprint poses; CharacterArt.ImportAll must have run first.</summary>
        // The hero has its own trip and sit-down falls; the rivals keep the shared poses.
        static RunnerArt LoadCharacter(string folder) => new RunnerArt
        {
            Folder = folder,
            Idle = CharacterArt.Load(folder, "idle"),
            Hit = CharacterArt.Load(folder, folder == PlayerCharacter ? "fallForwardRight" : "hurt"),
            FallDown = CharacterArt.Load(folder, folder == PlayerCharacter ? "fallSitRight" : "fallDown"),
            Run = CharacterArt.Frames(folder, "run0", "run1", "run2"),
            Cheer = CharacterArt.Frames(folder, "cheer0", "cheer1")
        };

        /// <summary>Frames for one state, so Celebrate cheers and Fail drops instead of reusing Idle and Hit.</summary>
        static Sprite[] SelectFrames(string state, RunnerArt art) => state switch
        {
            "Run" or "Burst" => new[] { art.Run[0], art.Run[1], art.Run[2], art.Run[1], art.Run[0] },
            "Celebrate" => new[] { art.Cheer[0], art.Cheer[1] },
            "Fail" => new[] { art.FallDown, art.FallDown },
            "Stumble" => new[] { art.Hit, art.Hit },
            _ => new[] { art.Idle, art.Idle }
        };

        static void AuthorClip(string state, RunnerArt art, string clipPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.ClearCurves();
            bool running = state == "Run" || state == "Burst";
            float interval = state == "Burst" ? .075f : .12f;
            var frames = SelectFrames(state, art);
            float duration = running ? interval * 4 : .6f;
            // Two-frame states hold each pose for half the loop, so Celebrate reads as a cheer
            // rather than parking the second frame on the loop boundary where it never shows.
            float step = running ? interval : duration / 2f;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("Visual", typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((sprite, index) => new ObjectReferenceKeyframe
                { time = index * step, value = sprite }).ToArray());
            // Scale motion retains authored state feedback without fighting race-owned localPosition.x.
            clip.SetCurve("Visual", typeof(Transform), "m_LocalScale.y",
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(duration / 2, state == "Celebrate" ? 1.12f : 1.03f),
                    new Keyframe(duration, 1f)));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        /// <summary>Gives one character its own clip set behind the shared state machine.</summary>
        static RuntimeAnimatorController AuthorOverrideController(RunnerArt art, RuntimeAnimatorController baseController)
        {
            var clips = States.ToDictionary(state => state,
                state => AuthorClipAndLoad(state, art, Animations + art.Folder + "_" + state + ".anim"));

            string path = Animations + art.Folder + ".overrideController";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (controller == null)
            {
                controller = new AnimatorOverrideController();
                AssetDatabase.CreateAsset(controller, path);
            }

            controller.runtimeAnimatorController = baseController;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
            controller.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                // Base clips are named RivalRunner_<State>; the suffix picks this character's twin.
                string state = overrides[i].Key.name.Substring(overrides[i].Key.name.IndexOf('_') + 1);
                if (!clips.TryGetValue(state, out var replacement))
                    throw new InvalidOperationException("No authored clip for state " + state + " on " + art.Folder);
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, replacement);
            }

            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimationClip AuthorClipAndLoad(string state, RunnerArt art, string clipPath)
        {
            AuthorClip(state, art, clipPath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        }

        static void AuthorRival(Sprite idle)
        {
            var root = PrefabUtility.LoadPrefabContents(RivalPrefab);
            var renderer = root.transform.Find("Visual").GetComponent<SpriteRenderer>();
            renderer.sprite = idle;
            renderer.sharedMaterial = SpriteMaterial();
            renderer.color = Color.white;
            renderer.transform.localScale = Vector3.one;
            // The prefab root is outside the animated Visual child, so every state keeps this
            // visual increase while race X and lane Y remain owned by the existing presenter.
            root.transform.localScale = Vector3.one * (1.3f * RunnerVisualScale);
            root.GetComponent<Animator>().applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(root, RivalPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void AuthorPlayer(Sprite idle, RuntimeAnimatorController controller)
        {
            var root = new GameObject("PlayerRunnerVisual");
            // Clips key only the Visual child. Keeping this scale on its parent prevents an
            // Idle/Run/Burst curve from resetting the larger sprite between states.
            root.transform.localScale = Vector3.one * RunnerVisualScale;
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            root.AddComponent<RunnerVisualPresenter>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = idle;
            renderer.sharedMaterial = SpriteMaterial();
            renderer.sortingOrder = 12;
            var label = new GameObject("PlayerLabel");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0, 1.65f, 0);
            var text = label.AddComponent<TMPro.TextMeshPro>();
            text.text = VietText.Fix("BẠN");
            text.fontSize = 48 * .065f;
            text.rectTransform.sizeDelta = new Vector2(2f, .6f);
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = new Color32(255, 202, 58, 255);
            KMA.Gameplay.UI.VietTypography.Apply(text, KMA.Gameplay.UI.VietFontRole.Hud);
            text.sortingOrder = 20;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            UnityEngine.Object.DestroyImmediate(root);
        }

        static Material SpriteMaterial()
        {
            const string path = "Assets/_Project/Art/DemoSpriteUnlit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("URP sprite unlit shader is missing.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
#endif
