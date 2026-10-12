using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    /// Shared press response: while held, the face lightens and the target shrinks; after release
    /// both ease back over PressRestoreSeconds. A non-interactable Selectable on the same object
    /// paints the DisabledSurface/DisabledText tokens at full opacity and ignores presses. Owns no input.
    public sealed class KitPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] Graphic face;
        [SerializeField] RectTransform target;
        [SerializeField] Color restColor = Color.white;
        [SerializeField] Selectable selectable;
        [SerializeField, Range(0f, 1f)] float pressedOpacity = 1f;

        [SerializeField] TMP_Text label;
        Color labelRestColor;
        bool wasDisabled;
        bool pressed;
        float restoreRemaining;

        public bool IsPressed => pressed;
        public float Scale => target == null ? 1f : target.localScale.x;
        public Color RestColor => restColor;

        public void Configure(Graphic faceGraphic, RectTransform scaleTarget, float opacityWhilePressed = 1f)
        {
            pressedOpacity = Mathf.Clamp01(opacityWhilePressed);
            face = faceGraphic;
            target = scaleTarget;
            selectable = GetComponent<Selectable>();
            if (face != null)
                restColor = face.color;
        }

        public void SetRestColor(Color color)
        {
            restColor = color;
            if (!wasDisabled && !pressed && restoreRemaining <= 0f && face != null)
                face.color = color;
        }

        public void OnPointerDown(PointerEventData eventData) => Press();
        public void OnPointerUp(PointerEventData eventData) => Release();

        public void Press()
        {
            if (selectable != null && !selectable.interactable)
                return;
            pressed = true;
            restoreRemaining = 0f;
            if (face != null)
                face.color = PressedColor;
            SetScale(MinigameUiTheme.PressScale);
        }

        public void Release()
        {
            if (!pressed)
                return;
            pressed = false;
            restoreRemaining = MinigameUiTheme.PressRestoreSeconds;
        }

        public void Tick(float deltaTime)
        {
            ApplyInteractable();
            if (pressed || restoreRemaining <= 0f)
                return;

            restoreRemaining = Mathf.Max(0f, restoreRemaining - deltaTime);
            float t = 1f - restoreRemaining / MinigameUiTheme.PressRestoreSeconds;
            if (face != null)
                face.color = Color.Lerp(PressedColor, restColor, t);
            SetScale(Mathf.Lerp(MinigameUiTheme.PressScale, 1f, t));
        }

        Color PressedColor => MinigameUiTheme.WithAlpha(
            MinigameUiTheme.Lighten(restColor, MinigameUiTheme.PressLighten), restColor.a * pressedOpacity);

        void Update() => Tick(Time.unscaledDeltaTime);

        // OnDisable restores the rest colours, so a disabled button re-activated mid-frame repaints at once.
        void OnEnable() => ApplyInteractable();

        void OnDisable()
        {
            pressed = false;
            restoreRemaining = 0f;
            if (face != null)
                face.color = restColor;
            if (wasDisabled && label != null)
                label.color = labelRestColor;
            wasDisabled = false;
            SetScale(1f);
        }

        /// Records the label colour a re-enabled button returns to (used while the button is disabled).
        public void SetLabelRestColor(Color color)
        {
            labelRestColor = color;
            if (!wasDisabled && label != null)
                label.color = color;
        }

        public void BindLabel(TMP_Text buttonLabel)
        {
            label = buttonLabel;
            if (label != null)
                labelRestColor = label.color;
        }

        void ApplyInteractable()
        {
            if (selectable == null)
                return;
            bool disabled = !selectable.interactable;
            if (disabled == wasDisabled)
                return;
            wasDisabled = disabled;
            if (disabled)
            {
                pressed = false;
                restoreRemaining = 0f;
                SetScale(1f);
                if (label != null)
                    labelRestColor = label.color;
                if (face != null)
                    face.color = MinigameUiTheme.DisabledSurface;
                if (label != null)
                    label.color = MinigameUiTheme.DisabledText;
            }
            else
            {
                if (face != null)
                    face.color = restColor;
                if (label != null)
                    label.color = labelRestColor;
            }
        }

        void SetScale(float scale)
        {
            if (target != null)
                target.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
