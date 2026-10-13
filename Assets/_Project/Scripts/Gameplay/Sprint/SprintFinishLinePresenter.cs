using UnityEngine;

namespace KMA.Gameplay
{
    /// Moves the world-space finish line. It lives on the track with the runners, so the spot where it
    /// rests is the spot where they finish on every screen aspect.
    public sealed class SprintFinishLinePresenter : MonoBehaviour
    {
        SprintController controller;
        GameObject finishRoot;

        public bool IsVisible { get; private set; }
        public GameObject FinishRoot => finishRoot;

        public void Configure(SprintController sprintController, GameObject finishLineRoot)
        {
            controller = sprintController;
            finishRoot = finishLineRoot;
            RefreshForTest();
        }

        public void RefreshForTest()
        {
            // Driven by the leading runner, so a rival who runs ahead stops on a line that is already drawn.
            // The reveal is authored on a 0-100 scale; map the real race distance onto it.
            float distance = controller == null ? 0f
                : controller.LeadDistance * SprintUiLayout.FinishDistance / Mathf.Max(1f, controller.TargetDistance);
            IsVisible = controller != null && SprintUiLayout.FinishVisible(distance);
            if (finishRoot == null)
                return;

            finishRoot.SetActive(IsVisible);
            if (!IsVisible)
                return;

            // The line enters from just past the right edge at the reveal distance and slides onto
            // FinishX as the runner closes on it, rather than appearing on the track all at once.
            Transform line = finishRoot.transform;
            Vector3 position = line.position;
            position.x = SprintTrackLayout.FinishLineX(SprintUiLayout.FinishReveal01(distance), EntryX());
            line.position = position;
        }

        static float EntryX()
        {
            Camera cam = Camera.main;
            float rightEdge = cam != null && cam.orthographic
                ? cam.transform.position.x + cam.orthographicSize * cam.aspect
                : SprintTrackLayout.FinishX;
            return Mathf.Max(SprintTrackLayout.FinishX, rightEdge + SprintTrackLayout.FinishWidth);
        }

        void Update() => RefreshForTest();
    }
}
