using System.Globalization;
using KMA.UI.Kit;
using UnityEngine;

namespace KMA.Gameplay
{
    /// The camera follows only the player, so rivals run out of frame. For each one out of view, a
    /// chip at that screen edge, level with their lane, says how far ahead or behind they are.
    [DefaultExecutionOrder(120)]
    public sealed class SprintOffscreenRivalChips : MonoBehaviour
    {
        public enum Edge { None, Left, Right }

        /// A runner sprite is about a unit wide: only call a rival gone once they are fully past the edge.
        public const float OffscreenMargin = .6f;
        // Wide enough for the longest text, e.g. "LÀN 4 −140.4 m", without the label truncating.
        const float ChipWidth = 300f, ChipHeight = 56f, EdgeInset = 16f;
        // Chips sit just above the lane's feet line, level with the runner's body.
        const float LaneLift = .6f;

        SprintController controller;
        RectTransform root;
        RivalRunnerAI[] rivals = System.Array.Empty<RivalRunnerAI>();
        ChipHandle[] chips = System.Array.Empty<ChipHandle>();

        public int ChipCount => chips.Length;
        public bool IsChipVisible(int index) => chips[index].Background.gameObject.activeSelf;
        public string ChipText(int index) => chips[index].Label.text;

        public static Edge EdgeFor(float rivalX, float cameraX, float halfWidth)
        {
            if (rivalX > cameraX + halfWidth + OffscreenMargin) return Edge.Right;
            if (rivalX < cameraX - halfWidth - OffscreenMargin) return Edge.Left;
            return Edge.None;
        }

        public static string GapText(float rivalPosition, float playerPosition)
        {
            float gap = rivalPosition - playerPosition;
            return (gap >= 0f ? "+" : "−") + Mathf.Abs(gap).ToString("0.0", CultureInfo.InvariantCulture) + " m";
        }

        public void Configure(SprintController owner, RectTransform chromeRoot, RivalRunnerAI[] runners)
        {
            controller = owner;
            root = chromeRoot;
            rivals = runners ?? System.Array.Empty<RivalRunnerAI>();
            chips = new ChipHandle[rivals.Length];
            for (int i = 0; i < rivals.Length; i++)
            {
                chips[i] = UiKit.Chip(chromeRoot, "RivalChip" + i, string.Empty);
                RectTransform rect = chips[i].Background.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(ChipWidth, ChipHeight);
                // Information only: a chip must never swallow a tap meant for the controls.
                chips[i].Background.raycastTarget = false;
                chips[i].Label.raycastTarget = false;
                chips[i].Background.gameObject.SetActive(false);
            }
        }

        void LateUpdate() => RefreshForTest();

        public void RefreshForTest()
        {
            Camera camera = Camera.main;
            bool racing = controller != null && controller.Phase == MinigamePhase.Play && camera != null;
            for (int i = 0; i < chips.Length; i++)
            {
                GameObject chip = chips[i].Background.gameObject;
                RivalRunnerAI rival = rivals[i];
                if (!racing || rival == null || rival.RivalIndex >= controller.RivalCount)
                {
                    chip.SetActive(false);
                    continue;
                }

                float position = controller.GetRivalPosition(rival.RivalIndex);
                Edge edge = EdgeFor(SprintTrackLayout.WorldX(position), camera.transform.position.x,
                    camera.orthographicSize * camera.aspect);
                chip.SetActive(edge != Edge.None);
                if (edge == Edge.None) continue;

                chips[i].Label.text = VietText.Fix($"LÀN {rival.Lane} {GapText(position, controller.PlayerPosition)}");
                Place(chips[i].Background.rectTransform, edge, camera,
                    SprintTrackLayout.LaneCenterYForAuthoredLane(rival.Lane) + LaneLift);
            }
        }

        void Place(RectTransform chip, Edge edge, Camera camera, float worldY)
        {
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(camera.transform.position.x, worldY, 0f));
            Canvas canvas = root.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 local);
            Rect area = root.rect;
            float x = edge == Edge.Right ? area.xMax - ChipWidth * .5f - EdgeInset : area.xMin + ChipWidth * .5f + EdgeInset;
            // root.rect and local are measured from the root's pivot; centre anchors measure from its centre.
            chip.anchoredPosition = new Vector2(x - area.center.x, local.y - area.center.y);
        }
    }
}
