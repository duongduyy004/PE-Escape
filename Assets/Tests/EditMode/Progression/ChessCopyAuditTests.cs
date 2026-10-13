using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Progression
{
    /// The chess exam no longer has a fixed time or a mistake allowance (spec sections 1 and 5), so no shipped copy
    /// about it may say so. Other subjects legitimately use some of these phrases, so shared files are checked
    /// only inside their chess blocks.
    public sealed class ChessCopyAuditTests
    {
        static readonly string[] Stale = { "90 giây", "90 GIÂY", "hai lần sửa", "2 lần sửa", "sửa sai", "Sai quá", "trong hai nước", "trong 2 nước" };

        static string Root => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));

        static string[] Lines(string file) => File.ReadAllLines(Path.Combine(Root, file));

        static string Block(string file, string marker, int count)
        {
            string[] lines = Lines(file);
            int start = System.Array.FindIndex(lines, l => l.Contains(marker));
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"{file} has no \"{marker}\"");
            return string.Join("\n", lines.Skip(start).Take(count));
        }

        static void AssertClean(string label, string text)
        {
            foreach (string phrase in Stale)
                Assert.That(text, Does.Not.Contain(phrase), $"{label} still says \"{phrase}\"");
        }

        [TestCase("Assets/_Project/Scripts/Gameplay/Chess/ChessGuide.cs")]
        [TestCase("Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs")]
        [TestCase("Assets/_Project/Scripts/Gameplay/Chess/ChessFinalHud.cs")]
        public void ChessSourceFilesCarryNoStaleCopy(string file) => AssertClean(file, File.ReadAllText(Path.Combine(Root, file)));

        [Test]
        public void ChessDialogueLinesCarryNoStaleCopy() => AssertClean("GameplayDialogueContentBuilder chess lines",
            string.Join("\n", Lines("Assets/Editor/GameplayDialogueContentBuilder.cs")
                .Where(l => l.Contains("Id=\"chess.") || l.Contains("Id=\"ch.extra."))));

        [Test]
        public void JourneyChessChallengeAndIntroCarryNoStaleCopy()
        {
            const string file = "Assets/Editor/StudentJourneyContentBuilder.cs";
            AssertClean("chess_final spec", Block(file, "new ChallengeSpec(\"chess_final\"", 3));
            AssertClean("chess_intro node", Block(file, "Node(\"chess_intro\"", 4));
        }

        [TestCase("PLAN.md", "Bài kiểm tra cuối (cờ vua)")]
        public void DocumentedChessRowCarriesNoStaleCopy(string file, string marker) =>
            AssertClean(file + " chess row", Block(file, marker, 1));
    }
}
