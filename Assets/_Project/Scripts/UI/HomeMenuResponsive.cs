using UnityEngine;

namespace KMA.Gameplay.UI
{
    public sealed class HomeMenuResponsive : MonoBehaviour
    {
        const float MenuScale = 1.7f;
        const float PanelWidth = 560f;
        RectTransform rect;
        RectTransform parent;

        void Awake()
        {
            rect = (RectTransform)transform;
            parent = (RectTransform)transform.parent;
            Resize();
        }

        void OnRectTransformDimensionsChange() => Resize();
        // Canvas dimensions can settle after Awake, especially in the Game view.
        void LateUpdate() => Resize();

        public static float ScaleFor(Vector2 viewport) => Mathf.Min(MenuScale,
            viewport.y / 650f, viewport.x * .45f / PanelWidth);

        void Resize()
        {
            if (rect == null || parent == null) return;
            if (parent.rect.width <= 0f || parent.rect.height <= 0f) return;
            float scale = ScaleFor(parent.rect.size);
            Vector3 desiredScale = Vector3.one * scale;
            if (rect.localScale != desiredScale) rect.localScale = desiredScale;
            rect.anchorMin = rect.anchorMax = new Vector2(.045f, .46f);
        }
    }
}
