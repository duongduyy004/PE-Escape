using System.Linq;
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class JourneyDialogueDataTests
    {
        static readonly string[] NodeIds = { "opening", "sprint_intro", "sprint_exam", "sprint_pass",
            "volleyball_intro", "volleyball_exam", "volleyball_pass", "soccer_intro", "soccer_exam",
            "soccer_pass", "chess_intro", "supplementary", "course_complete" };

        [Test]
        public void DefaultDialogueLibraryContainsShortNodesForEveryMilestone()
        {
            JourneyDialogueLibrary library = JourneyDialogueLibrary.LoadDefault();
            Assert.That(library.Validate(out string error), Is.True, error);
            Assert.That(library.Nodes.Select(node => node.Id), Is.EquivalentTo(NodeIds));
            foreach (string id in NodeIds)
            {
                Assert.That(library.Get(id).Lines.Count, Is.InRange(2, 4), id);
                foreach (JourneyDialogueLine line in library.Get(id).Lines)
                    Assert.That(library.GetCharacter(line.CharacterId).GetPose(line.Pose), Is.Not.Null,
                        $"{id}/{line.CharacterId}/{line.Pose}");
            }
        }

        [Test]
        public void DefaultCastIsTheFourNamedCharacters()
        {
            JourneyDialogueLibrary library = JourneyDialogueLibrary.LoadDefault();
            Assert.That(library.Cast.Select(c => c.Id + "=" + c.DisplayName), Is.EquivalentTo(new[]
            {
                "tan_thu=SV năm 1", "mai_toang=Mai", "anh_khoa_tren=Anh Tuấn", "co_the_chat=Giảng viên thể chất"
            }));
            Assert.That(library.Player.Id, Is.EqualTo("tan_thu"));
            foreach (JourneyCharacter character in library.Cast)
                foreach (DialoguePose pose in System.Enum.GetValues(typeof(DialoguePose)))
                    Assert.That(character.HasPose(pose), Is.True, $"{character.Id}/{pose}");
        }

        [Test]
        public void EditedCastMatchesGameplayHero()
        {
            var library = JourneyDialogueLibrary.LoadDefault();
            Assert.That(library.GetCharacter("tan_thu").GetPose(DialoguePose.Idle).name,
                Is.EqualTo("MaleAdventurer_fear"));
            Assert.That(library.GetCharacter("anh_khoa_tren").GetPose(DialoguePose.Idle).name,
                Is.EqualTo("MalePerson_idle"));
            Assert.That(library.GetCharacter("co_the_chat").GetPose(DialoguePose.Cheer).name,
                Is.EqualTo("BossPE_scoreWrite"));
            Assert.That(library.Nodes.Sum(node => node.Lines.Count), Is.EqualTo(35));
            foreach (var node in library.Nodes)
                foreach (var line in node.Lines)
                    Assert.That(DialogueEmoji.FindCodes(line.Text), Is.Empty, node.Id);
            Assert.That(library.Get("sprint_exam").Lines[0].Text, Does.Contain("15 giây"));
            Assert.That(library.Get("sprint_exam").Lines[0].Sticker, Is.EqualTo("15 GIÂY"));
        }

        [Test]
        public void CourseCompleteNoLongerRepeatsTheSoccerPassLine()
        {
            JourneyDialogueLibrary library = JourneyDialogueLibrary.LoadDefault();
            var soccerPass = library.Get("soccer_pass").Lines.Select(line => line.Text).ToList();
            foreach (JourneyDialogueLine line in library.Get("course_complete").Lines)
                Assert.That(soccerPass, Does.Not.Contain(line.Text));
        }

        [Test]
        public void SeenMarkersAreSavedAndRestoredWithJourneyProgress()
        {
            var journey = new JourneyProgress(ChallengeCatalog.LoadDefault());
            Assert.That(journey.IsDialogueSeen("opening"), Is.False);
            journey.MarkDialogueSeen("opening");
            Assert.That(journey.IsDialogueSeen("opening"), Is.True);
            var restored = new JourneyProgress(ChallengeCatalog.LoadDefault());
            restored.Restore(journey.ToData(), GameSession.MaxLives);
            Assert.That(restored.IsDialogueSeen("opening"), Is.True);
        }
    }
}
