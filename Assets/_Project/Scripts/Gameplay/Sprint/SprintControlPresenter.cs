using KMA.UI.Kit;
using System.Collections.Generic;
using KMA.Gameplay.Core;
using KMA.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KMA.Gameplay
{
    /// Visual state for the Sprint LEFT/RIGHT buttons. Owns no input: ScreenTapArea
    /// remains the only component that forwards taps to gameplay.
    public sealed class SprintControlPresenter : MonoBehaviour
    {
        static float PressScale => MinigameUiTheme.PressScale;
        const float PressDuration = .09f;
        const float BreatheHz = 1.2f;
        const float BreatheAmount = .03f;

        SprintController controller;
        HapticsService haptics;
        RectTransform leftVisual;
        RectTransform rightVisual;
        Image leftBackground;
        Image rightBackground;
        Image leftBorder;
        Image rightBorder;
        Side pressedSide;
        float pressRemaining;
        float breathePhase;

        public Side HighlightedSide { get; private set; } = Side.Left;
        public float LeftScale { get; private set; } = 1f;
        public float RightScale { get; private set; } = 1f;

        public void Configure(SprintController sprintController, RectTransform left, RectTransform right,
            Image leftFill, Image rightFill, Image leftFrame, Image rightFrame)
        {
            controller = sprintController;
            leftVisual = left;
            rightVisual = right;
            leftBackground = leftFill;
            rightBackground = rightFill;
            leftBorder = leftFrame;
            rightBorder = rightFrame;
            haptics = Object.FindFirstObjectByType<HapticsService>();
            SetScale(Side.Left, 1f);
            SetScale(Side.Right, 1f);
            RefreshForTest();
        }

        public void BindPressFeedback(ScreenTapArea left, ScreenTapArea right)
        {
            Bind(left, Side.Left);
            Bind(right, Side.Right);
        }

        public void RefreshForTest()
        {
            if (controller == null)
                return;

            HighlightedSide = controller.ExpectedSide;
            ApplyState(Side.Left, leftBackground, leftBorder);
            ApplyState(Side.Right, rightBackground, rightBorder);
        }

        public void PressForTest(Side side)
        {
            if (pressRemaining > 0f)
                SetScale(pressedSide, 1f);

            pressedSide = side;
            pressRemaining = PressDuration;
            SetScale(side, PressScale);
            RefreshForTest();

            if (haptics != null && controller != null && side == controller.ExpectedSide)
                haptics.Light();
        }

        public void TickForTest(float deltaTime)
        {
            breathePhase += deltaTime * BreatheHz * Mathf.PI * 2f;

            if (pressRemaining <= 0f)
                return;

            pressRemaining = Mathf.Max(0f, pressRemaining - deltaTime);
            if (pressRemaining <= 0f)
                SetScale(pressedSide, 1f);
        }

        void Update()
        {
            RefreshForTest();
            TickForTest(Time.unscaledDeltaTime);
            ApplyBreathe();
        }

        void Bind(ScreenTapArea tapArea, Side side)
        {
            if (tapArea == null)
                return;

            EventTrigger trigger = tapArea.GetComponent<EventTrigger>()
                ?? tapArea.gameObject.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            for (int i = trigger.triggers.Count - 1; i >= 0; i--)
            {
                if (trigger.triggers[i].eventID == EventTriggerType.PointerDown)
                    trigger.triggers.RemoveAt(i);
            }

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => PressForTest(side));
            trigger.triggers.Add(entry);
        }

        void ApplyState(Side side, Image background, Image border)
        {
            if (background == null || border == null)
                return;

            bool pressed = pressRemaining > 0f && pressedSide == side;
            bool expected = HighlightedSide == side;
            bool finished = controller != null && (controller.PresentationPhase == MinigamePhase.Resolve ||
                controller.IsWaitingForRivals);

            ControlState state = pressed ? ControlState.Pressed
                : finished ? ControlState.Disabled
                : expected ? ControlState.Hint
                : ControlState.Rest;
            KitControlState.Apply(background, border, state);
        }

        void ApplyBreathe()
        {
            if (pressRemaining > 0f)
                return;

            Side restingSide = HighlightedSide == Side.Left ? Side.Right : Side.Left;
            bool finished = controller != null && (controller.PresentationPhase == MinigamePhase.Resolve ||
                controller.IsWaitingForRivals);
            if (finished)
            {
                SetScaleIfChanged(HighlightedSide, 1f);
                SetScaleIfChanged(restingSide, 1f);
                return;
            }

            float pulse = 1f + Mathf.Sin(breathePhase) * .5f * BreatheAmount + .5f * BreatheAmount;
            SetScale(HighlightedSide, pulse);
            SetScaleIfChanged(restingSide, 1f);
        }

        void SetScaleIfChanged(Side side, float scale)
        {
            float current = side == Side.Left ? LeftScale : RightScale;
            if (current == scale)
                return;

            SetScale(side, scale);
        }

        void SetScale(Side side, float scale)
        {
            RectTransform visual = side == Side.Left ? leftVisual : rightVisual;
            if (visual != null)
                visual.localScale = new Vector3(scale, scale, 1f);

            if (side == Side.Left)
                LeftScale = scale;
            else
                RightScale = scale;
        }
    }
}
