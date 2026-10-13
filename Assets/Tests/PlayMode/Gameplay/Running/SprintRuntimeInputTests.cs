using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class SprintRuntimeInputTests
    {
        GameObject routerObject;
        GameObject controllerObject;
        InputActionAsset actions;
        Keyboard keyboard;
        Touchscreen touchscreen;
        float originalTimeScale;

        [SetUp]
        public void Setup()
        {
            originalTimeScale = Time.timeScale;
            routerObject = new GameObject("GameplayInputRouter");
            controllerObject = new GameObject("SprintController");
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            if (touchscreen != null)
                InputSystem.RemoveDevice(touchscreen);
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(routerObject);
            if (actions != null)
                Object.DestroyImmediate(actions);
            Time.timeScale = originalTimeScale;
        }

        [Test]
        public void SprintActions_RouteValidAlternationToControllerOncePerTap()
        {
            var controller = controllerObject.AddComponent<KMA.Gameplay.SprintController>();
            controller.ConfigureForTest();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("Sprint");
            var left = map.AddAction("SprintLeft", InputActionType.Button, "<Keyboard>/leftArrow");
            var right = map.AddAction("SprintRight", InputActionType.Button, "<Keyboard>/rightArrow");
            var router = routerObject.AddComponent<KMA.Input.GameplayInputRouter>();
            controller.ConfigureInputRouterForTest(router);
            router.ConfigureSprintForTest(actions);
            var before = controller.Snapshot;
            router.FeedSprintTapForTest(KMA.Input.Side.Left, 1d);
            controller.Simulate(.1f);
            var afterLeft = controller.Snapshot;

            Assert.That(afterLeft.Distance, Is.GreaterThan(before.Distance));
            Assert.That(controller.ExpectedSide, Is.EqualTo(KMA.Gameplay.Side.Right));

            router.FeedSprintTapForTest(KMA.Input.Side.Left, 2d);
            Assert.That(controller.ExpectedSide, Is.EqualTo(KMA.Gameplay.Side.Right));
            Assert.That(controller.IsFalling, Is.True, "an off-beat tap trips the runner");
            Assert.That(controller.Snapshot.Speed, Is.Zero);
            controller.Simulate(KMA.Gameplay.SprintRules.FallSeconds + .05f);
            Assert.That(controller.IsFalling, Is.False);

            var speedBeforeRightTap = controller.Snapshot.Speed;
            router.FeedSprintTapForTest(KMA.Input.Side.Right, 3d);
            Assert.That(controller.Snapshot.Speed, Is.EqualTo(speedBeforeRightTap + 18f));
            controller.Simulate(.1f);
            Assert.That(controller.ExpectedSide, Is.EqualTo(KMA.Gameplay.Side.Left));
            Assert.That(controller.Snapshot.Distance, Is.GreaterThan(afterLeft.Distance));
            Assert.That(left.bindings[0].path, Is.EqualTo("<Keyboard>/leftArrow"));
            Assert.That(right.bindings[0].path, Is.EqualTo("<Keyboard>/rightArrow"));
        }

        [Test]
        public void SprintTapsAreIgnoredWhilePausedForDirectAndRoutedInput()
        {
            var controller = controllerObject.AddComponent<KMA.Gameplay.SprintController>();
            controller.ConfigureForTest();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("Sprint");
            map.AddAction("SprintLeft", InputActionType.Button, "<Keyboard>/leftArrow");
            map.AddAction("SprintRight", InputActionType.Button, "<Keyboard>/rightArrow");
            var router = routerObject.AddComponent<KMA.Input.GameplayInputRouter>();
            controller.ConfigureInputRouterForTest(router);
            router.ConfigureSprintForTest(actions);
            var before = controller.Snapshot;

            Time.timeScale = 0f;
            controller.OnLeftTap();
            router.FeedSprintTapForTest(KMA.Input.Side.Left, 1d);

            Assert.That(controller.Snapshot, Is.EqualTo(before));
            Assert.That(controller.ExpectedSide, Is.EqualTo(KMA.Gameplay.Side.Left));
            Assert.That(controller.CadenceCombo, Is.Zero);
        }

        [Test]
        public void SprintActions_RouteWrongSideThroughRulesWithoutEndingTheRace()
        {
            var controller = controllerObject.AddComponent<KMA.Gameplay.SprintController>();
            controller.ConfigureForTest();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("Sprint");
            map.AddAction("SprintLeft", InputActionType.Button, "<Keyboard>/leftArrow");
            map.AddAction("SprintRight", InputActionType.Button, "<Keyboard>/rightArrow");
            var router = routerObject.AddComponent<KMA.Input.GameplayInputRouter>();
            controller.ConfigureInputRouterForTest(router);
            router.ConfigureSprintForTest(actions);
            var completions = 0;
            controller.Completed += _ => completions++;

            controller.AdvanceToDistance(30f);
            controller.Simulate(0f);

            router.FeedSprintTapForTest(KMA.Input.Side.Right, 1d);

            Assert.That(controller.Snapshot.Speed, Is.Zero);
            Assert.That(controller.IsFalling, Is.True,
                "a wrong-side tap trips the runner");
            Assert.That(controller.ExpectedSide, Is.EqualTo(KMA.Gameplay.Side.Left),
                "a wrong-side tap must not advance the alternation");
            Assert.That(completions, Is.Zero, "a wrong-side tap must never end the race");

            router.FeedSprintTapForTest(KMA.Input.Side.Left, 2d);
            Assert.That(completions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SprintScene_ScreenTapAreaIsOnlyTouchEntryPointAndRoutesOneImpulse()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var inputCanvas = GameObject.Find("Input");
            Assert.That(inputCanvas, Is.Not.Null);
            var canvasRect = inputCanvas.GetComponent<RectTransform>();
            var controller = Object.FindFirstObjectByType<KMA.Gameplay.SprintController>();
            var router = Object.FindFirstObjectByType<KMA.Input.GameplayInputRouter>();
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            var tapAreas = Object.FindObjectsByType<KMA.Input.ScreenTapArea>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Assert.That(canvasRect.lossyScale.x, Is.GreaterThan(0f));
            Assert.That(canvasRect.lossyScale.y, Is.GreaterThan(0f));
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.InputActionsReady, Is.False);
            Assert.That(router, Is.Not.Null);
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(tapAreas, Has.Length.EqualTo(2));

            foreach (var tapArea in tapAreas)
            {
                var areaRect = (RectTransform)tapArea.transform;
                Assert.That(areaRect.lossyScale.x, Is.GreaterThan(0f));
                Assert.That(areaRect.rect.width, Is.GreaterThan(0f));
                Assert.That(areaRect.rect.height, Is.GreaterThan(0f));
            }

            controller.ConfigureForTest();
            float speedBeforeKeyboardAction = controller.Snapshot.Speed;
            router.FeedSprintTapForTest(KMA.Input.Side.Left, 1d);
            Assert.That(controller.Snapshot.Speed, Is.EqualTo(speedBeforeKeyboardAction + 18f));

            var rightTapArea = System.Array.Find(tapAreas,
                area => ((RectTransform)area.transform).anchorMin.x >= .5f);
            Assert.That(rightTapArea, Is.Not.Null);
            var screenPosition = new Vector2(Screen.width * .75f, Screen.height * .2f);
            var pointer = new PointerEventData(eventSystem)
            {
                pointerId = 1001,
                position = screenPosition,
                button = PointerEventData.InputButton.Left
            };
            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, raycastResults);
            var tapHit = raycastResults.Find(result => result.gameObject == rightTapArea.gameObject);

            float speedBeforeScreenTap = controller.Snapshot.Speed;
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            QueueTouch(touchscreen, UnityEngine.InputSystem.TouchPhase.Began, screenPosition, Vector2.zero);
            Assert.That(controller.Snapshot.Speed, Is.EqualTo(speedBeforeScreenTap));
            Assert.That(tapHit.gameObject, Is.EqualTo(rightTapArea.gameObject));
            pointer.pointerCurrentRaycast = tapHit;
            ExecuteEvents.Execute(rightTapArea.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(controller.Snapshot.Speed, Is.EqualTo(speedBeforeScreenTap + 18f));
            ExecuteEvents.Execute(rightTapArea.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            QueueTouch(touchscreen, UnityEngine.InputSystem.TouchPhase.Ended, screenPosition, Vector2.zero);
        }

        static void Tap(KeyControl key)
        {
            InputSystem.QueueStateEvent((Keyboard)key.device, new KeyboardState(key.keyCode));
            InputSystem.Update();
            InputSystem.QueueStateEvent((Keyboard)key.device, default(KeyboardState));
            InputSystem.Update();
        }

        static void QueueTouch(Touchscreen touchscreen, UnityEngine.InputSystem.TouchPhase phase,
            Vector2 position, Vector2 delta)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 1,
                phase = phase,
                position = position,
                delta = delta
            });
            InputSystem.Update();
        }
    }
}
