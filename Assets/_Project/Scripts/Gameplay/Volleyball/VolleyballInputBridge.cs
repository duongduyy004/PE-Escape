using UnityEngine;
using UnityEngine.InputSystem;

namespace KMA.Gameplay.Volleyball
{
    // Merges the touch controls with a keyboard fallback (WASD/arrows, Space to hit, J to jump).
    // The keyboard actions live in code because KMA.inputactions' map list is pinned by
    // InputAssetContractTests.
    public sealed class VolleyballInputBridge : MonoBehaviour
    {
        [SerializeField] VirtualJoystick joystick;
        [SerializeField] ActionButton actionButton;
        [SerializeField] JumpButton jumpButton;

        InputAction moveAction;
        InputAction pressAction;
        InputAction jumpAction;
        ActionButton subscribedButton;
        JumpButton subscribedJump;
        int pendingPresses;
        int pendingJumps;
        Vector2? testMove;

        public void Configure(VirtualJoystick stick, ActionButton button, JumpButton jump = null)
        {
            joystick = stick;
            actionButton = button;
            jumpButton = jump;
            if (isActiveAndEnabled)
                SubscribeButtons();
        }

        public ActionButton ActionButton => actionButton;
        public JumpButton JumpButton => jumpButton;

        public Vector2 Move
        {
            get
            {
                if (testMove.HasValue)
                    return testMove.Value;
                if (joystick && joystick.Value.sqrMagnitude > .0001f)
                    return joystick.Value;
                return moveAction == null ? Vector2.zero : Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            }
        }

        public int ConsumePresses()
        {
            int presses = pendingPresses;
            pendingPresses = 0;
            return presses;
        }

        public int ConsumeJumps()
        {
            int jumps = pendingJumps;
            pendingJumps = 0;
            return jumps;
        }

        public void ClearPresses()
        {
            pendingPresses = 0;
            pendingJumps = 0;
        }

        public void FeedMoveForTest(Vector2 move) => testMove = move;

        public void FeedActionForTest() => pendingPresses++;

        public void FeedJumpForTest() => pendingJumps++;

        void OnEnable()
        {
            EnsureActions();
            moveAction.Enable();
            pressAction.Enable();
            jumpAction.Enable();
            SubscribeButtons();
        }

        void OnDisable()
        {
            moveAction?.Disable();
            pressAction?.Disable();
            jumpAction?.Disable();
            UnsubscribeButtons();
        }

        void OnDestroy()
        {
            moveAction?.Dispose();
            pressAction?.Dispose();
            jumpAction?.Dispose();
        }

        void EnsureActions()
        {
            if (moveAction != null)
                return;

            moveAction = new InputAction("VolleyballMove", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            pressAction = new InputAction("VolleyballAction", InputActionType.Button, "<Keyboard>/space");
            pressAction.performed += _ => OnPressed();
            jumpAction = new InputAction("VolleyballJump", InputActionType.Button, "<Keyboard>/j");
            jumpAction.performed += _ => OnJumped();
        }

        void SubscribeButtons()
        {
            if (subscribedButton != actionButton)
            {
                if (subscribedButton)
                    subscribedButton.Pressed -= OnPressed;
                subscribedButton = actionButton;
                if (actionButton)
                    actionButton.Pressed += OnPressed;
            }

            if (subscribedJump != jumpButton)
            {
                if (subscribedJump)
                    subscribedJump.Pressed -= OnJumped;
                subscribedJump = jumpButton;
                if (jumpButton)
                    jumpButton.Pressed += OnJumped;
            }
        }

        void UnsubscribeButtons()
        {
            if (subscribedButton)
                subscribedButton.Pressed -= OnPressed;
            if (subscribedJump)
                subscribedJump.Pressed -= OnJumped;
            subscribedButton = null;
            subscribedJump = null;
        }

        void OnPressed() => pendingPresses++;

        void OnJumped() => pendingJumps++;
    }
}
