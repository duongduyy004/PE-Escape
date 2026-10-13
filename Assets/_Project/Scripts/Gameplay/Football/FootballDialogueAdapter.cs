using System;
using System.Reflection;
using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.Gameplay
{
    public sealed class FootballDialogueAdapter : MonoBehaviour
    {
        [SerializeField] FootballController controller;
        [SerializeField] GameplayDialogueHost host;
        [SerializeField] GameplayOpeningFlow opening;
        [SerializeField] StartLecturer lecturer;
        bool subscribed, lastKick;
        int describedKick = -1;
        int lastWhistledKick = -1;
        int lastResolvedKick = -1;
        Delegate shotWindowHandler, shotResolvedHandler;
        EventInfo shotWindowEvent, shotResolvedEvent;

        void Start() => Configure(controller, host, opening);

        public void Configure(FootballController source, GameplayDialogueHost dialogue, GameplayOpeningFlow flow, StartLecturer startLecturer = null)
        {
            Unsubscribe();
            controller = source;
            host = dialogue;
            opening = flow;
            if (startLecturer != null) lecturer = startLecturer;
            if (lecturer == null) lecturer = FindFirstObjectByType<StartLecturer>();
            if (source == null || host == null) return;
            source.StatePresented += OnState;
            source.ResultDecided += OnResult;
            SubscribeTeacherEvents();
            subscribed = true;
            var kind = source.CurrentChallenge?.Kind;
            opening?.Configure(source, host, kind == ChallengeKind.Learn ? "fb.learn.start" : kind == ChallengeKind.Practice ? "fb.practice.start" : kind == ChallengeKind.Exam ? "fb.exam.start" : null);
        }

        void SubscribeTeacherEvents()
        {
            if (controller == null) return;
            shotWindowEvent = controller.GetType().GetEvent("ShotWindowStarted");
            shotResolvedEvent = controller.GetType().GetEvent("ShotResolved");

            if (shotWindowEvent != null)
            {
                Action<int> onShotWindow = OnShotWindowStarted;
                shotWindowHandler = Delegate.CreateDelegate(shotWindowEvent.EventHandlerType, onShotWindow.Target, onShotWindow.Method);
                shotWindowEvent.AddEventHandler(controller, shotWindowHandler);
            }

            if (shotResolvedEvent != null)
            {
                Action<FootballOutcome> onShotResolved = OnShotResolved;
                shotResolvedHandler = Delegate.CreateDelegate(shotResolvedEvent.EventHandlerType, onShotResolved.Target, onShotResolved.Method);
                shotResolvedEvent.AddEventHandler(controller, shotResolvedHandler);
            }
        }

        void OnShotWindowStarted(int kickIndex)
        {
            if (lecturer == null) lecturer = FindFirstObjectByType<StartLecturer>();
            lecturer?.Whistle();
        }

        void OnShotResolved(FootballOutcome outcome)
        {
            if (lecturer == null) lecturer = FindFirstObjectByType<StartLecturer>();
            if (outcome == FootballOutcome.Goal)
                lecturer?.WriteScore();
            else
                lecturer?.PointThenWrite();
        }

        void OnState(FootballState state)
        {
            var rules = controller.Rules;
            var kind = controller.CurrentChallenge?.Kind;

            if (shotWindowEvent == null && state == FootballState.Aiming)
            {
                if (rules.Kicks != lastWhistledKick)
                {
                    lastWhistledKick = rules.Kicks;
                    if (lecturer == null) lecturer = FindFirstObjectByType<StartLecturer>();
                    lecturer?.Whistle();
                }
            }

            if (state == FootballState.Aiming && kind == ChallengeKind.Exam && rules.Kicks == 4 && !lastKick)
            {
                lastKick = true;
                host.Emit("fb.exam.lastkick", () => rules.State == FootballState.Aiming, budgetSeconds: 3.7f);
            }

            if (shotResolvedEvent == null && state == FootballState.ShotResult && rules.Kicks != lastResolvedKick)
            {
                lastResolvedKick = rules.Kicks;
                if (lecturer == null) lecturer = FindFirstObjectByType<StartLecturer>();
                if (rules.LastOutcome == FootballOutcome.Goal)
                    lecturer?.WriteScore();
                else
                    lecturer?.PointThenWrite();
            }

            if (state != FootballState.ShotResult || rules.Kicks == describedKick) return;
            describedKick = rules.Kicks;
            string id = kind == ChallengeKind.Learn && rules.LastOutcome != FootballOutcome.Goal ? "fb.learn.miss" :
                kind == ChallengeKind.Practice && rules.LastOutcome == FootballOutcome.Saved ? "fb.practice.saved" :
                kind == ChallengeKind.Exam && rules.LastOutcome == FootballOutcome.Goal && rules.Kicks < 5 ? "fb.exam.goal" : null;
            int kick = rules.Kicks;
            if (id != null) host.Emit(id, () => rules.Kicks == kick && (rules.State == FootballState.ShotResult || rules.State == FootballState.Aiming), budgetSeconds: 3.7f);
        }

        void OnResult(bool pass)
        {
            var kind = controller.CurrentChallenge?.Kind;
            string id = pass && kind == ChallengeKind.Learn ? "fb.learn.done" : pass && kind == ChallengeKind.Practice ? "fb.practice.done" : !pass && kind == ChallengeKind.Exam ? "fb.exam.impossible" : null;
            host.BeginResult(id);
            if (id != null) host.Emit(id);
        }

        void Unsubscribe()
        {
            if (subscribed && controller != null)
            {
                controller.StatePresented -= OnState;
                controller.ResultDecided -= OnResult;
            }
            if (shotWindowEvent != null && shotWindowHandler != null && controller != null)
                shotWindowEvent.RemoveEventHandler(controller, shotWindowHandler);
            if (shotResolvedEvent != null && shotResolvedHandler != null && controller != null)
                shotResolvedEvent.RemoveEventHandler(controller, shotResolvedHandler);
            shotWindowHandler = null;
            shotResolvedHandler = null;
            shotWindowEvent = null;
            shotResolvedEvent = null;
            subscribed = false;
        }

        void OnDisable() => Unsubscribe();
    }
}
