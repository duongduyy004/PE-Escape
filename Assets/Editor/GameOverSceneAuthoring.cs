#if UNITY_EDITOR
using KMA.Gameplay.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KMA.EditorTools
{
    public static class GameOverSceneAuthoring
    {
        const string ScenePath = "Assets/_Project/Scenes/GameOver.unity";
        // The same bright backdrop the Home key art is painted over.
        const string IllustrationPath = "Assets/_Project/Art/UI/MenuKeyArtBackdrop.png";

        [MenuItem("KMA/GameOver/Apply Presentation")]
        public static void Apply()
        {
            // The assembler strips minigame HUD/phase/result roots from non-gameplay scenes.
            MinigameUIAssembler.AssembleScenePath(ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var screen = Object.FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
            screen.SetBackground(AssetDatabase.LoadAssetAtPath<Sprite>(IllustrationPath));
            // Build replaces an older-style baked layout with the menu-style card and keeps the scene's buttons.
            GameOverPresentationBuilder.Rebuild(screen, null);
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
