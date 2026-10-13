using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    /// Shell press response: while held, the button body (border, face, label) slides onto its shadow;
    /// after release it eases back over PressRestoreSeconds. A non-interactable Selectable on the same
    /// object paints the Shell disabled tokens and ignores presses. Owns no input.
    public sealed class ShellPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform body;
        [SerializeField] Graphic face;
        [SerializeField] TMP_Text label;
        [SerializeField] Color faceRest = Color.white;
        [SerializeField] Color labelRest = Color.black;
        // Where the body sits at rest; the press slides it from here (map stop badges are not centred).
        [SerializeField] Vector2 restPosition;
        Selectable selectable;
        bool pressed;
        bool painted;
        bool wasDisabled;
        float restoreRemaining;

        public bool IsPressed => pressed;
        public Vector2 Offset => body == null ? Vector2.zero : body.anchoredPosition - restPosition;

        public void Configure(RectTransform bodyRect, Graphic faceGraphic, TMP_Text buttonLabel, Color faceColor, Color labelColor)
        {
            if (body != null && body == bodyRect)
                body.anchoredPosition = restPosition;   // undo a press in progress before re-reading the rest
            body = bodyRect;
            restPosition = body != null ? body.anchoredPosition : Vector2.zero;
            face = faceGraphic;
            label = buttonLabel;
            faceRest = faceColor;
            labelRest = labelColor;
            selectable = GetComponent<Selectable>();
            pressed = false;
            restoreRemaining = 0f;
            SetShift(0f);
            wasDisabled = IsDisabled;
            Paint(wasDisabled);
            painted = true;
        }

        public void OnPointerDown(PointerEventData eventData) => Press();
        public void OnPointerUp(PointerEventData eventData) => Release();

        public void Press()
        {
            if (IsDisabled) return;
            pressed = true;
            restoreRemaining = 0f;
            SetShift(1f);
        }

        public void Release()
        {
            if (!pressed) return;
            pressed = false;
            restoreRemaining = MinigameUiTheme.PressRestoreSeconds;
        }

        public void Tick(float deltaTime)
        {
            bool disabled = IsDisabled;
            if (!painted || disabled != wasDisabled)
            {
                wasDisabled = disabled;
                painted = true;
                if (disabled)
                {
                    pressed = false;
                    restoreRemaining = 0f;
                    SetShift(0f);
                }
                Paint(disabled);
            }
            if (pressed || restoreRemaining <= 0f) return;
            restoreRemaining = Mathf.Max(0f, restoreRemaining - deltaTime);
            SetShift(restoreRemaining / MinigameUiTheme.PressRestoreSeconds);
        }

        void Update() => Tick(Time.unscaledDeltaTime);

        void OnDisable()
        {
            pressed = false;
            restoreRemaining = 0f;
            SetShift(0f);
        }

        bool IsDisabled
        {
            get
            {
                if (selectable == null) selectable = GetComponent<Selectable>();
                return selectable != null && !selectable.interactable;
            }
        }

        void Paint(bool disabled)
        {
            if (face != null) face.color = disabled ? ShellTheme.DisabledFace : faceRest;
            if (label != null) label.color = disabled ? ShellTheme.DisabledText : labelRest;
        }

        void SetShift(float amount)
        {
            if (body != null) body.anchoredPosition = restPosition + ShellTheme.PressedShift * amount;
        }
    }
}
