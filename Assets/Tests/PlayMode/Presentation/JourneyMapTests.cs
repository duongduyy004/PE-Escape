using System;
using System.Collections;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KMA.Tests.Presentation
{
    public sealed class JourneyMapTests
    {
        GameObject root;
        MapScreen screen;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("JourneyMap", typeof(RectTransform));
            screen = root.AddComponent<MapScreen>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator OpeningTheLessonPopupRevealsEveryCard()
        {
            MapPresentationBuilder.Build(screen, new GameSession());
            Assert.That(screen.LessonList.IsOpen, Is.False);
            for (int open = 0; open < 2; open++)
            {
                screen.SelectSubject(SubjectId.Sprint);
                yield return new WaitForSecondsRealtime(1.2f);
                for (int i = 1; i <= 3; i++)
                    Assert.That(screen.LessonList.transform.Find($"Lesson{i}").GetComponent<CanvasGroup>().alpha,
                        Is.EqualTo(1f), $"Lesson{i} on open #{open + 1}");
                screen.LessonList.Close();
            }
        }

        [Test]
        public void NewJourneyShowsCourseOrderLocksFutureSubjectsAndListsCheckpointLessons()
        {
            MapPresentationBuilder.Build(screen, new GameSession());

            Assert.That(screen.Nodes[0].SubjectId, Is.EqualTo(SubjectId.Sprint));
            Assert.That(screen.Nodes[0].IsInteractable, Is.True);
            Assert.That(screen.Nodes[1].IsInteractable, Is.False);
            Assert.That(screen.Nodes[2].IsInteractable, Is.False);
            Assert.That(screen.LessonList.CurrentChallengeId, Is.EqualTo("sprint_learn"));
            Assert.That(screen.LessonList.LessonIds,
                Is.EqualTo(new[] { "sprint_learn", "sprint_practice", "sprint_exam" }));
            Assert.That(screen.BudgetLabel.text, Is.EqualTo("Lượt thi: 5/5"));
        }

        [Test]
        public void PassingSprintExamUnlocksVolleyballAndMovesCheckpointToItsFirstLesson()
        {
            var session = new GameSession();
            Play(session, "sprint_learn", true);
            Play(session, "sprint_practice", true);
            Assert.That(session.Journey.IsSubjectUnlocked(SubjectId.Volleyball), Is.False);
            Play(session, "sprint_exam", true);

            MapPresentationBuilder.Build(screen, session);
            screen.RefreshJourney(session);

            Assert.That(screen.Nodes[1].IsInteractable, Is.True);
            Assert.That(screen.Nodes[2].IsInteractable, Is.False);
            Assert.That(screen.LessonList.CurrentChallengeId, Is.EqualTo("volleyball_learn"));
        }

        [Test]
        public void ReviewDoesNotMoveCheckpointAndZeroLivesLocksTheCurrentExam()
        {
            var session = new GameSession();
            Play(session, "sprint_learn", true);
            Play(session, "sprint_practice", true);
            Play(session, "sprint_exam", true);
            MapPresentationBuilder.Build(screen, session);
            ChallengeAttemptMode selectedMode = default;
            screen.ChallengeRequested += (_, mode) => selectedMode = mode;
            screen.SelectSubject(SubjectId.Sprint);
            screen.LessonList.transform.Find("Lesson1").GetComponent<Button>().onClick.Invoke();
            Play(screen);
            Assert.That(selectedMode, Is.EqualTo(ChallengeAttemptMode.Review));
            Assert.That(session.Journey.CheckpointChallengeId, Is.EqualTo("volleyball_learn"));

            Play(session, "volleyball_learn", true);
            Play(session, "volleyball_practice", true);
            session.Journey.SetAttemptsRemaining(0);
            screen.RefreshJourney(session);

            Assert.That(screen.LessonList.CurrentChallengeId, Is.EqualTo("volleyball_exam"));
            Assert.That(screen.LessonList.SelectedLessonIndex, Is.EqualTo(2), "The current exam is preselected.");
            Assert.That(PlayButton(screen).interactable, Is.False);
            Assert.That(screen.LessonList.transform.Find("DetailCard/Status").GetComponent<TMP_Text>().text,
                Does.Contain(VietText.Fix("Hết lượt thi")));

            var fresh = new GameSession();
            fresh.Journey.SetAttemptsRemaining(0);
            screen.RefreshJourney(fresh);
            Assert.That(screen.LessonList.CurrentChallengeId, Is.EqualTo("sprint_learn"));
            Assert.That(PlayButton(screen).interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator OutOfLivesPlayButtonUsesTheReadableDisabledTokens()
        {
            var session = new GameSession();
            Play(session, "sprint_learn", true);
            Play(session, "sprint_practice", true);
            session.Journey.SetAttemptsRemaining(0);
            MapPresentationBuilder.Build(screen, session);
            screen.RefreshJourney(session);
            screen.SelectSubject(SubjectId.Sprint);
            yield return null;

            Transform continueButton = PlayButton(screen).transform;
            Assert.That(continueButton.gameObject.activeSelf, Is.True);
            Assert.That(continueButton.GetComponent<Button>().interactable, Is.False);
            Assert.That(continueButton.Find(KMA.UI.Kit.ShellKit.BodyName + "/" + KMA.UI.Kit.ShellKit.FaceName)
                .GetComponent<Image>().color, Is.EqualTo(KMA.UI.Kit.ShellTheme.DisabledFace));
            Assert.That(continueButton.GetComponentInChildren<TMP_Text>(true).color,
                Is.EqualTo(KMA.UI.Kit.ShellTheme.DisabledText));
            CanvasGroup group = continueButton.GetComponent<CanvasGroup>();
            Assert.That(group == null || group.alpha == 1f, Is.True, "disabled buttons must not fade into the panel");
        }

        [Test]
        public void CompletedCourseShowsSummaryAndLessonReplayUsesFreePlay()
        {
            var session = new GameSession();
            foreach (ChallengeDefinition challenge in session.Journey.Catalog.Ordered)
                Play(session, challenge.Id, true);
            MapPresentationBuilder.Build(screen, session);

            Assert.That(session.Journey.CourseComplete, Is.True);
            Assert.That(screen.CourseSummary, Is.Not.Null);
            Assert.That(screen.CourseSummary.gameObject.activeSelf, Is.True);
            Assert.That(screen.CourseSummary.ScoreRows.Count, Is.EqualTo(4));
            ChallengeAttemptMode mode = default;
            screen.ChallengeRequested += (_, requested) => mode = requested;
            screen.LessonList.transform.Find("Lesson1").GetComponent<Button>().onClick.Invoke();
            Play(screen);
            Assert.That(mode, Is.EqualTo(ChallengeAttemptMode.FreePlay));
        }

        [Test]
        public void TappingAStageOnlySelectsItAndALockedStageCannotBePlayed()
        {
            MapPresentationBuilder.Build(screen, new GameSession());
            string requested = null;
            ChallengeAttemptMode requestedMode = default;
            screen.ChallengeRequested += (id, mode) => { requested = id; requestedMode = mode; };
            screen.SelectSubject(SubjectId.Sprint);
            Transform panel = screen.LessonList.transform;
            Assert.That(screen.LessonList.SelectedLessonIndex, Is.EqualTo(0), "The next lesson opens preselected.");

            panel.Find("Lesson2").GetComponent<Button>().onClick.Invoke();
            Assert.That(requested, Is.Null, "Tapping a stage selects it; it does not start it.");
            Assert.That(screen.LessonList.SelectedLessonIndex, Is.EqualTo(1));
            Assert.That(panel.Find("Lesson2/StageIcon/SelectedRing").gameObject.activeSelf, Is.True);
            Assert.That(panel.Find("Lesson1/StageIcon/SelectedRing").gameObject.activeSelf, Is.False);
            Assert.That(PlayButton(screen).interactable, Is.False, "A locked stage cannot be played.");
            Assert.That(panel.Find("DetailCard/Status").GetComponent<TMP_Text>().text,
                Does.Contain(VietText.Fix("HỌC")), "A locked stage explains which stage opens it.");

            screen.LessonList.Close();
            screen.SelectSubject(SubjectId.Sprint);
            Assert.That(screen.LessonList.SelectedLessonIndex, Is.EqualTo(0), "Reopening returns to the next lesson.");
            Play(screen);
            Assert.That(requested, Is.EqualTo("sprint_learn"));
            Assert.That(requestedMode, Is.EqualTo(ChallengeAttemptMode.Journey));
        }

        static Button PlayButton(MapScreen screen) =>
            screen.LessonList.transform.Find("DetailCard/PlayButton").GetComponent<Button>();

        static void Play(MapScreen screen) => PlayButton(screen).onClick.Invoke();

        sealed class TestClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        }

        [UnityTest]
        public IEnumerator ALifeRegeneratedOnTheMapUnlocksTheExamAndIsSaved()
        {
            var clock = new TestClock();
            var session = new GameSession(null, clock);
            Play(session, "sprint_learn", true);
            Play(session, "sprint_practice", true);
            session.Journey.SetAttemptsRemaining(0);
            session.RefreshLives();
            Assert.That(session.TimeUntilNextLife, Is.EqualTo(TimeSpan.FromMinutes(5)));

            MapPresentationBuilder.Build(screen, session);
            int persists = 0;
            screen.ConfigureLifePersistence(() => { persists++; return true; });
            screen.SelectSubject(SubjectId.Sprint);
            Assert.That(screen.LessonList.SelectedLessonIndex, Is.EqualTo(2));
            Button exam = PlayButton(screen);
            Assert.That(exam.interactable, Is.False);
            Assert.That(screen.BudgetLabel.text, Is.EqualTo("Lượt thi: 0/5"));
            yield return null;
            Assert.That(persists, Is.Zero, "Nothing regenerated yet.");

            clock.UtcNow += TimeSpan.FromMinutes(5);
            yield return null;

            Assert.That(session.Lives, Is.EqualTo(1));
            Assert.That(exam.interactable, Is.True);
            Assert.That(screen.BudgetLabel.text, Is.EqualTo("Lượt thi: 1/5"));
            Assert.That(screen.Hearts.CurrentHearts, Is.EqualTo(1));
            Assert.That(screen.Hearts.CountdownText, Is.EqualTo("5:00"));
            Assert.That(persists, Is.EqualTo(1), "The regenerated life is saved.");
        }

        static void Play(GameSession session, string id, bool pass)
        {
            ChallengeDefinition definition = session.Journey.Catalog.Get(id);
            Assert.That(session.TryStartChallenge(id, ChallengeAttemptMode.Journey,
                definition.Difficulty, out ChallengeAttemptContext context), Is.True);
            session.SubmitChallengeResult(new ChallengeAttemptResult(context, pass,
                new ChallengeMetrics(completedTargets: definition.TargetCount),
                ChallengeDefinition.IsScored(definition.Kind)
                    ? new MinigameResult(pass, pass ? 8f : 0f, pass ? Rank.A : Rank.F) : null));
        }
    }
}
