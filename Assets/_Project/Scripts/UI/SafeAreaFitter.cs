using UnityEngine;

namespace KMA.Gameplay.UI
{
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        bool isApplying;
        Rect lastSafeArea;
        Vector2Int lastScreenSize;
        public readonly struct Offsets
        {
            public readonly float left, right, top, bottom;
            public Offsets(float left, float right, float top, float bottom)
            { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
        }
        void OnEnable() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();
        void LateUpdate()
        {
            if (lastSafeArea != Screen.safeArea || lastScreenSize != new Vector2Int(Screen.width, Screen.height))
                Refresh();
        }
        void Refresh() => Apply(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        public Offsets CalculateOffsets(Rect safeArea, Vector2 screenSize) => new Offsets(
            Mathf.Max(0f, safeArea.xMin), Mathf.Max(0f, screenSize.x - safeArea.xMax),
            Mathf.Max(0f, screenSize.y - safeArea.yMax), Mathf.Max(0f, safeArea.yMin));
        public void Apply(Rect safeArea, Vector2Int screenSize)
        {
            var rect = transform as RectTransform;
            if (isApplying || rect == null || screenSize.x <= 0 || screenSize.y <= 0) return;
            bool x = !Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x);
            bool y = !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);
            if (!x && !y) return; // A coincident-anchor Canvas root must never be resized.
            isApplying = true;
            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
            try
            {
                var parent = rect.parent as RectTransform;
                if (parent == null)
                {
                    // Detached previews/tests have no screen-to-parent transform.
                    var inset = CalculateOffsets(safeArea, screenSize);
                    rect.offsetMin = new Vector2(x ? inset.left : rect.offsetMin.x, y ? inset.bottom : rect.offsetMin.y);
                    rect.offsetMax = new Vector2(x ? -inset.right : rect.offsetMax.x, y ? -inset.top : rect.offsetMax.y);
                    return;
                }
                Canvas canvas = GetComponentInParent<Canvas>();
                var screenRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : parent;
                Rect domain = screenRect.rect;
                Vector2 min01 = new Vector2(Mathf.Clamp01(safeArea.xMin / screenSize.x), Mathf.Clamp01(safeArea.yMin / screenSize.y));
                Vector2 max01 = new Vector2(Mathf.Clamp01(safeArea.xMax / screenSize.x), Mathf.Clamp01(safeArea.yMax / screenSize.y));
                Vector3 min = parent.InverseTransformPoint(screenRect.TransformPoint(domain.min + Vector2.Scale(domain.size, min01)));
                Vector3 max = parent.InverseTransformPoint(screenRect.TransformPoint(domain.min + Vector2.Scale(domain.size, max01)));
                Rect available = parent.rect;
                if (available.width <= 0f || available.height <= 0f) return;
                Vector2 anchorMin = rect.anchorMin, anchorMax = rect.anchorMax;
                if (x) { anchorMin.x = Mathf.Clamp01((min.x - available.xMin) / available.width); anchorMax.x = Mathf.Clamp01((max.x - available.xMin) / available.width); }
                if (y) { anchorMin.y = Mathf.Clamp01((min.y - available.yMin) / available.height); anchorMax.y = Mathf.Clamp01((max.y - available.yMin) / available.height); }
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = new Vector2(x ? 0f : rect.offsetMin.x, y ? 0f : rect.offsetMin.y);
                rect.offsetMax = new Vector2(x ? 0f : rect.offsetMax.x, y ? 0f : rect.offsetMax.y);
            }
            finally { isApplying = false; }
        }
    }
}
