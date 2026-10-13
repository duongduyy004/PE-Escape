using UnityEngine;

namespace KMA.Gameplay
{
    /// Holds the start and finish lines on the track at their world positions. They never move; the
    /// camera brings them into view as the player runs. Every challenge, Learn included, has the finish line.
    public sealed class SprintFinishLinePresenter : MonoBehaviour
    {
        SprintController controller;
        GameObject finishRoot;
        GameObject startRoot;

        public bool IsVisible { get; private set; }
        public GameObject FinishRoot => finishRoot;
        public GameObject StartRoot => startRoot;

        public void Configure(SprintController sprintController, GameObject finishLineRoot, GameObject startLineRoot)
        {
            controller = sprintController;
            finishRoot = finishLineRoot;
            startRoot = startLineRoot;
            RefreshForTest();
        }

        public void RefreshForTest()
        {
            IsVisible = controller != null;
            if (finishRoot != null)
            {
                finishRoot.SetActive(IsVisible);
                Place(finishRoot, SprintTrackLayout.FinishWorldX(controller == null
                    ? SprintRules.RaceDistance : controller.TargetDistance));
            }
            if (startRoot != null) Place(startRoot, SprintTrackLayout.TrackStartX);
        }

        static void Place(GameObject line, float x)
        {
            Vector3 position = line.transform.position;
            position.x = x;
            line.transform.position = position;
        }

        void Update() => RefreshForTest();
    }
}
