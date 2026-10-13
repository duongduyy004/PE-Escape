using System;
using System.IO;
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class ChessFinalSaveTests
    {
        static readonly string[] AllNine =
        {
            "sprint_learn", "sprint_practice", "sprint_exam", "volleyball_learn", "volleyball_practice",
            "volleyball_exam", "soccer_learn", "soccer_practice", "soccer_exam"
        };

        string directory;
        SaveSystem saveSystem;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "KMA-ChessFinalSaveTests", Guid.NewGuid().ToString("N"));
            saveSystem = new SaveSystem(() => directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        static SaveData Version8(params string[] completed)
        {
            var data = new SaveData
            {
                version = 8,
                lives = 3,
                subjects = new[]
                {
                    new SubjectRecordData { id = SubjectId.Sprint, passed = true, bestScore = 7f, bestRank = Rank.B },
                    new SubjectRecordData { id = SubjectId.Football, passed = completed.Length >= 9, bestScore = 8f, bestRank = Rank.A },
                    new SubjectRecordData { id = SubjectId.Volleyball, passed = completed.Length >= 6, bestScore = 6f, bestRank = Rank.C }
                },
                tutorialSeen = new[] { true, false, true },
                settings = Settings.CreateDefault(),
                journey = new JourneyStateData()
            };
            data.journey.completedChallengeIds.AddRange(completed);
            data.journey.seenDialogueIds.Add("opening");
            return data;
        }

        [Test]
        public void Load_Version8MidCourse_KeepsJourney()
        {
            SaveData old = Version8("sprint_learn", "sprint_practice", "sprint_exam", "volleyball_learn");
            old.journey.failCounts.Add(new JourneyFailCountData { challengeId = "volleyball_practice", count = 1 });
            saveSystem.Save(old);

            SaveData loaded = saveSystem.Load();

            Assert.That(saveSystem.HasLoadedValidSave, Is.True);
            Assert.That(loaded.version, Is.EqualTo(9));
            Assert.That(loaded.subjects.Length, Is.EqualTo(4));
            Assert.That(loaded.tutorialSeen.Length, Is.EqualTo(4));
            Assert.That(loaded.journey.completedChallengeIds, Is.EqualTo(new[]
                { "sprint_learn", "sprint_practice", "sprint_exam", "volleyball_learn" }));
            Assert.That(loaded.journey.failCounts[0].challengeId, Is.EqualTo("volleyball_practice"));
            Assert.That(loaded.journey.seenDialogueIds, Does.Contain("opening"));
            Assert.That(loaded.lives, Is.EqualTo(3));
            Assert.That(loaded.tutorialSeen[0], Is.True);
            Assert.That(loaded.tutorialSeen[1], Is.False);
            Assert.That(loaded.tutorialSeen[2], Is.True);
            Assert.That(loaded.tutorialSeen[3], Is.False);
        }

        [Test]
        public void Load_Version7MidCourse_KeepsJourney()
        {
            SaveData old = Version8("sprint_learn", "sprint_practice", "sprint_exam", "volleyball_learn");
            old.version = 7;
            old.journey.failCounts.Add(new JourneyFailCountData { challengeId = "volleyball_practice", count = 1 });
            saveSystem.Save(old);

            SaveData loaded = saveSystem.Load();

            Assert.That(saveSystem.HasLoadedValidSave, Is.True);
            Assert.That(loaded.version, Is.EqualTo(9));
            Assert.That(loaded.subjects.Length, Is.EqualTo(4));
            Assert.That(loaded.tutorialSeen.Length, Is.EqualTo(4));
            Assert.That(loaded.journey.completedChallengeIds, Is.EqualTo(new[]
                { "sprint_learn", "sprint_practice", "sprint_exam", "volleyball_learn" }));
            Assert.That(loaded.journey.failCounts[0].challengeId, Is.EqualTo("volleyball_practice"));
            Assert.That(loaded.journey.failCounts[0].count, Is.EqualTo(1));
            Assert.That(loaded.journey.seenDialogueIds, Does.Contain("opening"));
            Assert.That(loaded.lives, Is.EqualTo(3));
        }

        [Test]
        public void Load_Version8CompletedCourse_ResumesAtTheChessFinal()
        {
            saveSystem.Save(Version8(AllNine));
            var session = new GameSession();
            session.Restore(saveSystem.Load());
            Assert.That(session.Journey.CourseComplete, Is.False);
            Assert.That(session.Journey.CheckpointChallengeId, Is.EqualTo("chess_final"));
            Assert.That(session.GetRecord(SubjectId.Football).BestScore, Is.EqualTo(8f));
            Assert.That(session.Journey.CelebrationSeen, Is.False);
        }

        [Test]
        public void ChessBestKeepsTheHighestWinAndRoundTrips()
        {
            var session = new GameSession();
            JourneyTestData.CompleteThrough(session, "soccer_exam");
            Assert.That(session.TryStartChallenge("chess_final", ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal, out _), Is.True);
            CommitActive(session, true, 7.5f, 50f, 1, true);
            Assert.That(session.Journey.CelebrationSeen, Is.False);
            Assert.That(session.Journey.MarkCelebrationSeen(), Is.True);

            Assert.That(session.StartSubject(SubjectId.Chess), Is.EqualTo(SessionRoute.Subject));
            CommitActive(session, true, 9.2f, 31f, 0, false);
            Assert.That(session.StartSubject(SubjectId.Chess), Is.EqualTo(SessionRoute.Subject));
            CommitActive(session, false, 0f, 90f, 3, false);

            var restored = new GameSession();
            restored.Restore(session.ToSaveData());
            JourneyChessRecordData best = restored.Journey.ChessBest;
            Assert.That(best.recorded, Is.True);
            Assert.That(best.score, Is.EqualTo(9.2f));
            Assert.That(best.thinkSeconds, Is.EqualTo(31f));
            Assert.That(best.mistakes, Is.EqualTo(0));
            Assert.That(best.hintUsed, Is.False);
            Assert.That(restored.Journey.CelebrationSeen, Is.True);
        }

        [Test]
        public void CelebrationCannotBeMarkedBeforeTheCourseIsComplete()
        {
            var session = new GameSession();
            JourneyTestData.CompleteThrough(session, "soccer_exam");
            Assert.That(session.Journey.MarkCelebrationSeen(), Is.False);
            SaveData data = session.ToSaveData();
            data.journey.celebrationSeen = true;
            var restored = new GameSession();
            restored.Restore(data);
            Assert.That(restored.Journey.CelebrationSeen, Is.False);
        }

        [Test]
        public void OldChessRecordWithoutNewFieldsLoadsAsLegacyAndKeepsItsValues()
        {
            // A version-9 save written before this change: none of the new chess fields exist in the JSON.
            SaveData old = SaveData.CreateDefault();
            old.journey.chessBest = new JourneyChessRecordData
                { recorded = true, score = 7.5f, thinkSeconds = 42f, mistakes = 1, hintUsed = true };
            saveSystem.Save(old);
            string path = Path.Combine(directory, "save.json");
            string json = File.ReadAllText(path);
            foreach (string field in new[] { "scoringVersion", "movesUsed", "optimalMoves", "maxMoves", "allowedSeconds", "gradeLabel" })
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\\s*\"" + field + "\"\\s*:\\s*(\"[^\"]*\"|-?[0-9]+)", string.Empty);
            File.WriteAllText(path, json);
            int start = json.IndexOf("\"chessBest\"", StringComparison.Ordinal);
            string section = json.Substring(start, json.IndexOf('}', start) - start);
            Assert.That(section, Does.Not.Contain("scoringVersion"), "the record really is in the old shape");

            JourneyChessRecordData best = saveSystem.Load().journey.chessBest;

            Assert.That(best.recorded, Is.True);
            Assert.That(best.score, Is.EqualTo(7.5f));
            Assert.That(best.thinkSeconds, Is.EqualTo(42f));
            Assert.That(best.mistakes, Is.EqualTo(1));
            Assert.That(best.hintUsed, Is.True);
            Assert.That(best.scoringVersion, Is.EqualTo(0), "legacy data is marked, not guessed");
            Assert.That(best.movesUsed, Is.EqualTo(0));
            Assert.That(best.gradeLabel, Is.Null.Or.Empty);
        }

        [Test]
        public void NewChessRecordRoundTripsEveryField()
        {
            SaveData data = SaveData.CreateDefault();
            data.journey.chessBest = new JourneyChessRecordData
            {
                recorded = true, score = 9.2f, thinkSeconds = 31f, mistakes = 0, hintUsed = false,
                scoringVersion = ChessResultSummary.CurrentScoringVersion, movesUsed = 2, optimalMoves = 2, maxMoves = 3,
                allowedSeconds = 90, gradeLabel = "A+"
            };
            saveSystem.Save(data);
            JourneyChessRecordData back = saveSystem.Load().journey.chessBest;
            Assert.That(back.scoringVersion, Is.EqualTo(ChessResultSummary.CurrentScoringVersion));
            Assert.That(back.movesUsed, Is.EqualTo(2));
            Assert.That(back.optimalMoves, Is.EqualTo(2));
            Assert.That(back.maxMoves, Is.EqualTo(3));
            Assert.That(back.allowedSeconds, Is.EqualTo(90));
            Assert.That(back.gradeLabel, Is.EqualTo("A+"));
            Assert.That(back.score, Is.EqualTo(9.2f));
        }

        [Test]
        public void LastCommittedResultKeepsTheChessSummary()
        {
            var summary = new ChessResultSummary
            {
                scoringVersion = 1, movesUsed = 3, maxMoves = 3, optimalMoves = 2, allowedSeconds = 95, usedSeconds = 80.5f,
                endReason = "OutOfMoves", gradeLabel = "F", fen = "7k/8/8/8/8/8/R7/1R4K1 w - - 0 1", source = "Generated",
                seed = 12, estimatorVersion = 1,
                moves = new[] { new ChessMoveEntry { uci = "g1f1", q = 0.5f, best = 2, chosen = 4 } }
            };
            var session = new GameSession();
            JourneyTestData.CompleteThrough(session, "soccer_exam");
            Assert.That(session.TryStartChallenge("chess_final", ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal, out ChallengeAttemptContext context), Is.True);
            session.SubmitChallengeResult(new ChallengeAttemptResult(context, false,
                new ChallengeMetrics(elapsed: 80.5f, completedTargets: 3, chess: summary), new MinigameResult(false, 0f, Rank.F)));

            var restored = new GameSession();
            restored.Restore(session.ToSaveData());
            ChessResultSummary back = restored.ToSaveData().journey.lastCommittedResult.ToResult().Metrics.Chess;
            Assert.That(back, Is.Not.Null);
            Assert.That(back, Is.Not.SameAs(summary));
            Assert.That(back.moves.Length, Is.EqualTo(1));
            Assert.That(back.moves[0].uci, Is.EqualTo("g1f1"));
            Assert.That(back.endReason, Is.EqualTo("OutOfMoves"));
            Assert.That(back.fen, Is.EqualTo(summary.fen));
            Assert.That(back.allowedSeconds, Is.EqualTo(95));
        }

        [Test]
        public void LegacyCommittedResultHasNoChessSummary()
        {
            var session = new GameSession();
            JourneyTestData.CompleteThrough(session, "soccer_exam");
            Assert.That(session.TryStartChallenge("chess_final", ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal, out _), Is.True);
            CommitActive(session, false, 0f, 90f, 3, false);
            var restored = new GameSession();
            restored.Restore(session.ToSaveData());
            Assert.That(restored.ToSaveData().journey.lastCommittedResult.ToResult().Metrics.Chess, Is.Null,
                "a result without a summary stays a legacy result, not an empty new one");
        }

        [Test]
        public void OfferingAHigherScoreStoresTheNewMetadataAndALowerOneDoesNot()
        {
            var session = new GameSession();
            JourneyTestData.CompleteThrough(session, "soccer_exam");
            Assert.That(session.TryStartChallenge("chess_final", ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal, out _), Is.True);
            CommitFinal(session, 7.0f, "B", 3);
            Assert.That(session.Journey.ChessBest.gradeLabel, Is.EqualTo("B"));
            Assert.That(session.StartSubject(SubjectId.Chess), Is.EqualTo(SessionRoute.Subject));
            CommitFinal(session, 6.0f, "C", 3);
            Assert.That(session.Journey.ChessBest.score, Is.EqualTo(7.0f), "a lower score never replaces the best");
            Assert.That(session.Journey.ChessBest.gradeLabel, Is.EqualTo("B"));
            Assert.That(session.StartSubject(SubjectId.Chess), Is.EqualTo(SessionRoute.Subject));
            CommitFinal(session, 9.5f, "A+", 2);
            Assert.That(session.Journey.ChessBest.gradeLabel, Is.EqualTo("A+"));
            Assert.That(session.Journey.ChessBest.movesUsed, Is.EqualTo(2));
            Assert.That(session.Journey.ChessBest.scoringVersion, Is.EqualTo(ChessResultSummary.CurrentScoringVersion));
        }

        static void CommitFinal(GameSession session, float score, string grade, int movesUsed)
        {
            ChallengeAttemptContext context = session.Journey.ActiveAttempt;
            var summary = new ChessResultSummary
            {
                scoringVersion = ChessResultSummary.CurrentScoringVersion, movesUsed = movesUsed, maxMoves = 3,
                optimalMoves = 2, allowedSeconds = 90, gradeLabel = grade, score = score, endReason = "Mate"
            };
            session.SubmitChallengeResult(new ChallengeAttemptResult(context, true,
                new ChallengeMetrics(elapsed: 30f, completedTargets: movesUsed, chess: summary),
                new MinigameResult(true, score, ScoreUtil.ToRank(score))));
        }

        static void CommitActive(GameSession session, bool pass, float score, float seconds, int mistakes, bool hint)
        {
            ChallengeAttemptContext context = session.Journey.ActiveAttempt;
            session.SubmitChallengeResult(new ChallengeAttemptResult(context, pass,
                new ChallengeMetrics(elapsed: seconds, completedTargets: 2, mistakes: mistakes, hintUsed: hint),
                new MinigameResult(pass, score, pass ? ScoreUtil.ToRank(score) : Rank.F)));
        }
    }
}
