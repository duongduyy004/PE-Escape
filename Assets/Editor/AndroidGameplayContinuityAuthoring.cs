#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KMA.EditorTools
{
    public static class AndroidGameplayContinuityAuthoring
    {
        [MenuItem("KMA/Android/Apply UI and Gameplay Continuity")]
        public static void Apply()
        {
            // Sprint artwork is authored separately from its dialogue bindings.
            DemoSprintArtConfigurator.Configure();
            DialogueIntegrationAuthoring.BuildAll();
            ShellSceneAuthoring.Validate();
            Debug.Log("[KMA] Android UI and gameplay continuity authored and reopened successfully.");
        }
    }
}
#endif
