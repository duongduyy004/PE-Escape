using KMA.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class SprintGuideTests
    {
        [Test]
        public void LearnAsksForTheRhythmCountWithoutAClock()
        {
            var pages = SprintGuide.Build(ChallengeKind.Learn, 12, 150f, 22f, 3);
            Assert.That(pages.Count, Is.EqualTo(3));
            Assert.That(pages[0].Title, Is.EqualTo("MỤC TIÊU"));
            Assert.That(pages[0].Instruction,
                Is.EqualTo("Bấm TRÁI, PHẢI luân phiên đúng 12 nhịp liên tiếp. Bài này không tính giờ."));
            Assert.That(pages[1].Title, Is.EqualTo("ĐIỀU KHIỂN"));
            Assert.That(pages[2].Instruction, Is.EqualTo("Bấm nhầm bên sẽ đưa chuỗi về 0. Bạn cần thực hiện lại đủ 12 nhịp liên tiếp."));
        }

        [Test]
        public void ExamStatesTheRaceAndAddsTheFailurePage()
        {
            var pages = SprintGuide.Build(ChallengeKind.Exam, 0, 150f, 15f, 3);
            Assert.That(pages.Count, Is.EqualTo(4));
            Assert.That(pages[0].Instruction, Is.EqualTo("Về đích 150 m trong tối đa 15 giây. Có 3 sinh viên chạy cùng."));
            Assert.That(pages[1].Instruction, Is.EqualTo(
                "Bấm TRÁI rồi PHẢI, luân phiên để chạy. Giữ nhịp đúng để tăng chuỗi và bứt tốc. Ngừng bấm thì tốc độ giảm."));
            Assert.That(pages[2].Instruction, Is.EqualTo(
                "Bấm nhầm bên sẽ mất chuỗi và tăng tốc rất ít. Hết giờ mà chưa về đích là chưa đạt. " +
                "Điểm tính theo độ chính xác, thứ hạng và thời gian còn lại."));
            Assert.That(pages[3].Title, Is.EqualTo("NẾU CHƯA ĐẠT"));
        }

        [Test]
        public void FreePlayHasNoFailurePageAndNoRivalSentenceWithoutRivals()
        {
            var pages = SprintGuide.Build(null, 0, 150f, 22f, 0);
            Assert.That(pages.Count, Is.EqualTo(3));
            Assert.That(pages[0].Instruction, Is.EqualTo("Về đích 150 m trong tối đa 22 giây."));
        }

        [TestCase("sprint_practice")]
        [TestCase("sprint_exam")]
        public void ObjectiveTextMatchesTheLessonLimits(string id)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ChallengeDefinition>(
                $"Assets/_Project/ScriptableObjects/Journey/{id}.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Objective,
                Is.EqualTo($"Chạy {definition.Distance:0} m trong tối đa {definition.TimeLimit:0} giây."));
        }
    }
}
