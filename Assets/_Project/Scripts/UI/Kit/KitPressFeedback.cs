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

        [SerializeField] TMP_Text label;
        CanvasGroup targetGroup;
        bool hasInteractableOverride;
        bool interactableOverride = true;
        Color labelRestColor;
        bool wasDisabled;
        bool pressed;
        float restoreRemaining;

        public bool IsPressed => pressed;
        public float Scale => target == null ? 1f : target.localScale.x;
        public Color RestColor => restColor;

        public void Configure(Graphic faceGraphic, RectTransform scaleTarget)
        {
            face = faceGraphic;
            target = scaleTarget;
            selectable = GetComponent<Selectable>();
            targetGroup = scaleTarget != null ? scaleTarget.GetComponent<CanvasGroup>() : null;
            if (face != null)
                restColor = face.color;
        }

        public void SetInteractable(bool interactable)
        {
            hasInteractableOverride = true;
            interactableOverride = interactable;
            ApplyInteractable();
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
            if (IsDisabled)
                return;
            pressed = true;
            restoreRemaining = 0f;
            if (face != null)
                face.color = MinigameUiTheme.Lighten(restColor, MinigameUiTheme.PressLighten);
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
                face.color = Color.Lerp(MinigameUiTheme.Lighten(restColor, MinigameUiTheme.PressLighten), restColor, t);
            SetScale(Mathf.Lerp(MinigameUiTheme.PressScale, 1f, t));
        }

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

        bool IsDisabled => hasInteractableOverride ? !interactableOverride :
            selectable != null && !selectable.interactable;

        void ApplyInteractable()
        {
            bool disabled = IsDisabled;
            if (disabled == wasDisabled)
                return;
            wasDisabled = disabled;
            if (targetGroup != null) targetGroup.alpha = disabled ? .58f : 1f;
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
