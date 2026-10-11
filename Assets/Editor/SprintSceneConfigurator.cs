#if UNITY_EDITOR
using System;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMA.EditorTools
{
    public static class SprintSceneConfigurator
    {
        public const string ScenePath = "Assets/_Project/Scenes/MG_Sprint.unity";
        const string RivalPrefabPath = "Assets/_Project/Prefabs/Gameplay/RivalRunner.prefab";
        const string RivalTypeName = "KMA.Gameplay.RivalRunnerAI";
        const string ControllerTypeName = "KMA.Gameplay.SprintController";

        [MenuItem("KMA/Sprint/Configure Dialogue")]
        public static void ConfigureDialogue()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            ConfigureDialogueInScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        public static void ConfigureDialogueInScene(Scene scene)
        {
            var source=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SprintController>(true)).Single();
            var host=DialoguePresentationAuthoring.Configure(source.transform,DialogueHudSlot.Sprint);
            var opening=source.GetComponent<GameplayOpeningFlow>() ?? source.gameObject.AddComponent<GameplayOpeningFlow>();
            var o=new SerializedObject(opening);o.FindProperty("game").objectReferenceValue=source;o.FindProperty("host").objectReferenceValue=host;o.ApplyModifiedPropertiesWithoutUndo();
            DialoguePresentationAuthoring.BindResult(source,host);
            var adapter=source.GetComponent<SprintDialogueAdapter>() ?? source.gameObject.AddComponent<SprintDialogueAdapter>();
            var a=new SerializedObject(adapter);a.FindProperty("controller").objectReferenceValue=source;a.FindProperty("host").objectReferenceValue=host;a.FindProperty("opening").objectReferenceValue=opening;
            a.FindProperty("playerAnimator").objectReferenceValue=GameObject.Find("Player")?.GetComponentInChildren<Animator>();a.ApplyModifiedPropertiesWithoutUndo();
        }

        static readonly SprintRivalMapping[] RequiredRivals = SprintRivalMappings.Required;

        [MenuItem("KMA/Sprint/Create or Repair Rivals")]
        public static void CreateOrRepairRivals()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RivalPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Could not load Sprint rival prefab at {RivalPrefabPath}.");

            var controller = SceneComponents(scene, ControllerTypeName).SingleOrDefault();
            if (controller == null)
                throw new InvalidOperationException("MG_Sprint must contain exactly one SprintController.");

            var rivals = SceneComponents(scene, RivalTypeName);
            if (RivalsAreValid(rivals, controller))
            {
                Debug.Log("[KMA] Sprint rivals already valid; no scene save required.");
                return;
            }

            foreach (var rival in rivals)
                UnityEngine.Object.DestroyImmediate(rival.gameObject);

            foreach (var mapping in RequiredRivals)
                CreateRival(scene, prefab, controller, mapping);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[KMA] Sprint rivals repaired and scene saved.");
        }

        static bool RivalsAreValid(MonoBehaviour[] rivals, MonoBehaviour controller)
        {
            if (rivals.Length != RequiredRivals.Length)
                return false;

            var ordered = rivals.OrderBy(RivalLane).ThenBy(rival => rival.name).ToArray();
            for (var i = 0; i < RequiredRivals.Length; i++)
            {
                var rival = ordered[i];
                var mapping = RequiredRivals[i];
                var serializedRival = new SerializedObject(rival);
                if (rival.name != mapping.Name ||
                    RivalLane(rival) != mapping.Lane ||
                    serializedRival.FindProperty("rivalIndex").intValue != mapping.RivalIndex ||
                    serializedRival.FindProperty("controller").objectReferenceValue != controller ||
                    AssetDatabase.GetAssetPath(serializedRival.FindProperty("profileAsset").objectReferenceValue) !=
                    mapping.ProfilePath ||
                    rival.transform.localPosition != mapping.LocalPosition ||
                    PrefabUtility.GetCorrespondingObjectFromSource(rival.gameObject) == null ||
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rival.gameObject) != RivalPrefabPath)
                    return false;
            }

            return true;
        }

        static void CreateRival(Scene scene, GameObject prefab, MonoBehaviour controller, SprintRivalMapping mapping)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var rival = instance.GetComponents<MonoBehaviour>()
                .Single(component => component != null && component.GetType().FullName == RivalTypeName);
            var profile = AssetDatabase.LoadMainAssetAtPath(mapping.ProfilePath);
            if (profile == null)
                throw new InvalidOperationException($"Could not load Sprint rival profile at {mapping.ProfilePath}.");

            var serializedGameObject = new SerializedObject(instance);
            serializedGameObject.FindProperty("m_Name").stringValue = mapping.Name;
            serializedGameObject.ApplyModifiedPropertiesWithoutUndo();

            var serializedTransform = new SerializedObject(instance.transform);
            serializedTransform.FindProperty("m_LocalPosition").vector3Value = mapping.LocalPosition;
            serializedTransform.ApplyModifiedPropertiesWithoutUndo();

            var serializedRival = new SerializedObject(rival);
            serializedRival.FindProperty("controller").objectReferenceValue = controller;
            serializedRival.FindProperty("profileAsset").objectReferenceValue = profile;
            serializedRival.FindProperty("lane").intValue = mapping.Lane;
            serializedRival.FindProperty("rivalIndex").intValue = mapping.RivalIndex;
            serializedRival.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("KMA/Sprint/Remove Legacy Metrics")]
        public static void RemoveLegacyMetrics()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var removed = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform == null || transform.name != "SprintMetrics")
                        continue;

                    UnityEngine.Object.DestroyImmediate(transform.gameObject);
                    removed++;
                    break;
                }
            }

            if (removed == 0)
                Debug.Log("SprintMetrics was already absent from MG_Sprint.");

            // MG_Sprint also carries orphaned, unreferenced legacy HUD placeholders (e.g. "Timer",
            // "Stamina") authored directly at the scene root, predating the shared HUD_Minigame
            // prefab. They have no parent, no children, and nothing references them, so they are
            // safe to delete outright (as are the empty "Rank" and "FX" roots, which nothing references);
            // matching is restricted to scene-root objects so the
            // still-shared elements nested inside the HUD_Minigame prefab instance (handled at
            // runtime by SprintFestivalPresentation.DisableSharedMetrics) are never touched.
            string[] strayLegacyRootNames =
            {
                "Timer", "Phase", "Score", "Status", "Progress", "Stamina", "HeartBar", "Rank", "FX"
            };
            var strayRemoved = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (Array.IndexOf(strayLegacyRootNames, root.name) < 0)
                    continue;
                // Only bare placeholders: a root with children or components is real content.
                if (root.transform.childCount > 0 || root.GetComponents<Component>().Length > 1)
                    continue;

                UnityEngine.Object.DestroyImmediate(root);
                strayRemoved++;
            }

            if (strayRemoved > 0)
                Debug.Log($"Removed {strayRemoved} orphaned legacy HUD root object(s) from {ScenePath}.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Removed {removed} legacy SprintMetrics group(s) from {ScenePath}.");
        }

        const string ApronName = "TrackApron";
        const int PlayerLane = 2;

        /// <summary>
        /// Restores the painted track to its authored size and stands every runner on the centre of
        /// the lane they are drawn to be in. The backdrop reaches the viewport floor on its own, so
        /// no apron is needed; the lowest lane runs under the control buttons, which draw a small
        /// visual inside a full-size tap area.
        /// </summary>
        [MenuItem("KMA/Sprint/Align Track Lanes")]
        public static void AlignTrackLanes()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var backdrops = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .Where(renderer => renderer.sprite != null && renderer.sprite.name == "Track")
                .ToArray();
            if (backdrops.Length == 0)
                throw new InvalidOperationException("MG_Sprint has no Track backdrop tile to align.");

            foreach (var backdrop in backdrops)
            {
                var tile = backdrop.transform;
                float parentScaleY = tile.parent == null ? 1f : tile.parent.lossyScale.y;
                var localScale = tile.localScale;
                localScale.y = SprintTrackLayout.BackdropScaleY / parentScaleY;
                tile.localScale = localScale;

                var position = tile.position;
                position.y = SprintTrackLayout.BackdropCenterY;
                tile.position = position;
            }

            RemoveApron(scene);

            var player = GameObject.Find("Player");
            if (player == null)
                throw new InvalidOperationException("MG_Sprint has no Player root to align.");
            var playerPosition = player.transform.position;
            playerPosition.y = SprintTrackLayout.LaneCenterYForAuthoredLane(PlayerLane);
            player.transform.position = playerPosition;

            foreach (var rival in SceneComponents(scene, RivalTypeName))
            {
                var rivalPosition = rival.transform.position;
                rivalPosition.y = SprintTrackLayout.LaneCenterYForAuthoredLane(RivalLane(rival));
                rival.transform.position = rivalPosition;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[KMA] Sprint track aligned: backdrop scaleY {SprintTrackLayout.BackdropScaleY:F4}, " +
                      $"lane centres {string.Join(", ", Enumerable.Range(0, SprintTrackLayout.LaneCount).Select(lane => SprintTrackLayout.LaneCenterY(lane).ToString("F3")))}.");
        }


        /// The apron only existed while the backdrop was compressed and stopped short of the
        /// viewport floor. At its authored size the backdrop reaches the floor on its own.
        static void RemoveApron(Scene scene)
        {
            var apron = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform != null && transform.name == ApronName);
            if (apron != null)
                UnityEngine.Object.DestroyImmediate(apron.gameObject);
        }

        static int RivalLane(MonoBehaviour rival) => new SerializedObject(rival).FindProperty("lane").intValue;

        static MonoBehaviour[] SceneComponents(Scene scene, string fullTypeName) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
            .Where(component => component != null && component.GetType().FullName == fullTypeName)
            .ToArray();
    }
}
#endif
