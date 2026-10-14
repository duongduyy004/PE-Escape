using System.Collections.Generic;
using KMA.EditorTools;
using KMA.Gameplay;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KMA.Tests.EditorTools
{
    public sealed class StartLecturerSceneTests
    {
        [OneTimeSetUp]
        public void ImportOnce() => CharacterArt.ImportAll();

        // The lecturer faces the athletes: left of her in Football and Frog Jump, right of her in Sprint and Volleyball.
        [TestCase("MG_Sprint", false)]
        [TestCase("MG_Football", true)]
        [TestCase("MG_FrogJump", true)]
        [TestCase("MG_Volleyball", false)]
        public void EverySportsSceneHasOneRedShirtLecturerFacingTheAthletes(string scene, bool facesLeft)
        {
            EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{scene}.unity", OpenSceneMode.Single);
            StartLecturer[] lecturers = Object.FindObjectsByType<StartLecturer>(FindObjectsSortMode.None);
            Assert.That(lecturers, Has.Length.EqualTo(1));
            var body = lecturers[0].GetComponent<SpriteRenderer>();
            Assert.That(CharacterArt.IsPoseOf(body.sprite, CharacterArt.Boss), Is.True);
            Assert.That(body.flipX, Is.EqualTo(facesLeft));
            Assert.That(lecturers[0].FacesLeft, Is.EqualTo(facesLeft));
            var wiring = new SerializedObject(lecturers[0]);
            foreach (string gesture in new[] { "count", "command", "strictLook", "taunt", "cheer", "cheerBothArms", "penalty", "penaltySide" })
                Assert.That(CharacterArt.IsPoseOf((Sprite)wiring.FindProperty(gesture).objectReferenceValue, CharacterArt.Boss),
                    Is.True, gesture);
        }

        [Test]
        public void SheCountsDownRaisesTheWhistleAndBlowsItWhenPlayStarts()
        {
            var go = new GameObject("lecturer");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                var lecturer = go.AddComponent<StartLecturer>();
                Sprite wait = Boss("idleBoss");
                Sprite raise = Boss("whistle0");
                Sprite blow = Boss("whistle1");
                lecturer.Configure(renderer, wait, raise, blow, true, 3f);
                ConfigureGestures(lecturer);

                lecturer.OnPhaseChanged(MinigamePhase.Tutorial);
                Assert.That(renderer.sprite, Is.SameAs(wait));
                lecturer.OnPhaseChanged(MinigamePhase.Countdown);
                Assert.That(renderer.sprite, Is.SameAs(Boss("count")));
                lecturer.Tick(StartLecturer.CountSeconds + .01f);
                Assert.That(renderer.sprite, Is.SameAs(raise));
                lecturer.Tick(5f);
                Assert.That(renderer.sprite, Is.SameAs(raise), "she holds the whistle up until play starts");
                lecturer.OnPhaseChanged(MinigamePhase.Play);
                Assert.That(renderer.sprite, Is.SameAs(blow));
                lecturer.Tick(StartLecturer.BlastSeconds + .01f);
                Assert.That(renderer.sprite, Is.SameAs(Boss("command")), "she points the athletes off");
                lecturer.Tick(3f);
                Assert.That(lecturer.Alpha, Is.EqualTo(0f));
                lecturer.OnPhaseChanged(MinigamePhase.Countdown);
                Assert.That(lecturer.Alpha, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SheKeepsGesturingThroughTheTutorialAndPlay()
        {
            var go = new GameObject("lecturer");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                var lecturer = go.AddComponent<StartLecturer>();
                lecturer.Configure(renderer, Boss("idleBoss"), Boss("whistle0"), Boss("whistle1"), false, 0f);
                ConfigureGestures(lecturer);

                foreach (MinigamePhase phase in new[] { MinigamePhase.Tutorial, MinigamePhase.Play })
                {
                    lecturer.OnPhaseChanged(phase);
                    var seen = new HashSet<StartLecturer.Pose>();
                    for (int i = 0; i < 300; i++)
                    {
                        lecturer.Tick(.1f);
                        seen.Add(lecturer.Current);
                    }
                    Assert.That(seen, Is.SupersetOf(new[]
                    {
                        StartLecturer.Pose.Waiting, StartLecturer.Pose.Command,
                        StartLecturer.Pose.StrictLook, StartLecturer.Pose.Taunt
                    }), phase.ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCase(true, "cheer0", "cheer1")]
        [TestCase(false, "penalty0", "penalty1")]
        public void SheCheersAPassAndPointsDownAtAFail(bool passed, string first, string second)
        {
            var go = new GameObject("lecturer");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                var lecturer = go.AddComponent<StartLecturer>();
                lecturer.Configure(renderer, Boss("idleBoss"), Boss("whistle0"), Boss("whistle1"), false, 1f);
                ConfigureGestures(lecturer);
                lecturer.OnPhaseChanged(MinigamePhase.Play);
                lecturer.Tick(2f);

                lecturer.OnPhaseChanged(MinigamePhase.Resolve);
                lecturer.OnResultDecided(passed);
                Assert.That(lecturer.Alpha, Is.EqualTo(1f));
                Assert.That(renderer.sprite, Is.SameAs(Boss(first)));
                lecturer.Tick(.65f);
                Assert.That(renderer.sprite, Is.SameAs(Boss(second)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AMissingGestureFallsBackToTheWaitingPose()
        {
            var go = new GameObject("lecturer");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                var lecturer = go.AddComponent<StartLecturer>();
                Sprite wait = Boss("idleBoss");
                lecturer.Configure(renderer, wait, Boss("whistle0"), Boss("whistle1"), false, 0f);
                lecturer.OnResultDecided(true);
                Assert.That(renderer.sprite, Is.SameAs(wait));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WhistleBlastsWhistleAndReturnsToSupervising()
        {
            var go = new GameObject("lecturer");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                var lecturer = go.AddComponent<StartLecturer>();
                Sprite wait = Boss("idleBoss");
                Sprite raise = Boss("whistle0");
                Sprite blow = Boss("whistle1");
                lecturer.Configure(renderer, wait, raise, blow, false, 0f);
                ConfigureGestures(lecturer);

                lecturer.Whistle();
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.WhistleBlown));
                Assert.That(renderer.sprite, Is.SameAs(blow));
                lecturer.Tick(StartLecturer.BlastSeconds + .01f);
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.Command));
                lecturer.Tick(.81f);
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.StrictLook));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WritingScoreShowsHoldThenWriteThenWait()
        {
            var go = new GameObject("lecturer");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                var lecturer = go.AddComponent<StartLecturer>();
                lecturer.Configure(renderer, Boss("idleBoss"), Boss("whistle0"), Boss("whistle1"), false, 0f);
                ConfigureGestures(lecturer);
                Sprite hold = Boss("scoreHold");
                Sprite write = Boss("scoreWrite");
                lecturer.ConfigureScore(hold, write);

                lecturer.WriteScore();
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.ScoreHold));
                Assert.That(renderer.sprite, Is.SameAs(hold));
                lecturer.Tick(.36f);
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.ScoreWrite));
                Assert.That(renderer.sprite, Is.SameAs(write));
                lecturer.Tick(1.41f);
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.Waiting));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PointThenWriteShowsPenaltyThenWriteThenWait()
        {
            var go = new GameObject("lecturer");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                var lecturer = go.AddComponent<StartLecturer>();
                lecturer.Configure(renderer, Boss("idleBoss"), Boss("whistle0"), Boss("whistle1"), false, 0f);
                ConfigureGestures(lecturer);
                Sprite write = Boss("scoreWrite");
                lecturer.ConfigureScore(Boss("scoreHold"), write);

                lecturer.PointThenWrite();
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.Penalty));
                Assert.That(renderer.sprite, Is.SameAs(Boss("penalty0")));
                lecturer.Tick(.71f);
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.ScoreWrite));
                Assert.That(renderer.sprite, Is.SameAs(write));
                lecturer.Tick(1.41f);
                Assert.That(lecturer.Current, Is.EqualTo(StartLecturer.Pose.Waiting));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FootballTeacherStandsOutsidePenaltyArea()
        {
            FootballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MG_Football.unity", OpenSceneMode.Single);
            var lecturer = Object.FindFirstObjectByType<StartLecturer>();
            Assert.That(lecturer, Is.Not.Null);
            // World position converted to screen coordinates: must be outside the penalty arc (x > 850 in screen px)
            float screenX = lecturer.transform.position.x / FootballPresentation.PixelToWorld + 600f;
            Assert.That(screenX, Is.GreaterThan(850f), "Football teacher must stand outside penalty area arc (x > 850)");
            Assert.That(lecturer.FacesLeft, Is.True, "Football teacher faces the kicker");
        }

        [Test]
        public void VolleyballTeacherScaleMatchesAthletesAndDoesNotBlockCourt()
        {
            VolleyballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MG_Volleyball.unity", OpenSceneMode.Single);
            var lecturer = Object.FindFirstObjectByType<StartLecturer>();
            Assert.That(lecturer, Is.Not.Null);
            Assert.That(lecturer.transform.localScale.x, Is.EqualTo(VolleyballSceneConfigurator.TeacherScale),
                "Teacher scale compensates for BossPE transparent margins");
            // Teacher is to the left of baseline (-CourtSpace.HalfLength)
            float worldBaselineX = -CourtSpace.HalfLength * CourtSpace.PixelsPerMetreX / CourtSpace.BackgroundPixelsPerUnit * CourtSpace.WorldStretchX;
            Assert.That(lecturer.transform.position.x, Is.LessThan(worldBaselineX),
                "Teacher stands outside the court boundary");
        }

        static Sprite Boss(string pose) => CharacterArt.Load(CharacterArt.Boss, pose);

        static void ConfigureGestures(StartLecturer lecturer) =>
            lecturer.ConfigureGestures(Boss("count"), Boss("command"), Boss("strictLook"), Boss("taunt"),
                Boss("cheer0"), Boss("cheer1"), Boss("penalty0"), Boss("penalty1"));
    }
}
