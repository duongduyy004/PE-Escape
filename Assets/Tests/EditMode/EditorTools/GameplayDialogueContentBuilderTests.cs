using System.Linq;
using System.Collections.Generic;
using KMA.Gameplay.UI;
using NUnit.Framework;
namespace KMA.Tests.EditorTools
{
    public sealed class GameplayDialogueContentBuilderTests
    {
        [Test] public void RegenerationPreservesEditedContentAndSpeed()
        {
            string Before()=>string.Join("|",GameplayDialogueLibrary.LoadDefault().Events.SelectMany(e=>e.Lines.Select(l=>e.Id+":"+l.Text+":"+string.Join(",",l.Poses.Select(p=>UnityEditor.AssetDatabase.GetAssetPath(p))))));
            string before=Before();KMA.EditorTools.GameplayDialogueContentBuilder.Build();KMA.EditorTools.GameplayDialogueContentBuilder.Build();KMA.EditorTools.StudentJourneyContentBuilder.BuildChallenges();
            Assert.That(Before(),Is.EqualTo(before));
            var tuning=UnityEditor.AssetDatabase.LoadAssetAtPath<KMA.Gameplay.FrogJump.FrogJumpBalanceConfig>("Assets/_Project/ScriptableObjects/FrogJump/FrogJumpBalance.asset");
            Assert.That(tuning,Is.Not.Null);Assert.That(tuning.Tuning.sweepSeconds,Is.EqualTo(1f));
            Assert.That(KMA.Gameplay.ChallengeCatalog.LoadDefault().Ordered.Count,Is.EqualTo(10));
        }
        [Test] public void AuthoredLibraryHasAllSpokenEventsAndCorrectReactions()
        {
            var library=GameplayDialogueLibrary.LoadDefault();Assert.That(library,Is.Not.Null);
            Assert.That(library.Validate(out string error),Is.True,error);
            Assert.That(library.Events.Count,Is.EqualTo(55));
            Assert.That(library.Events.Select(e=>e.Id).Distinct().Count(),Is.EqualTo(55));
            var lines=library.Resolve("sp.practice.start",new Dictionary<string,string>{{"time","20"}}).Lines;
            Assert.That(lines[0].Text,Does.Contain("20 giây"));
            foreach(var e in library.Events)
                foreach(var line in e.Lines.Where(l=>l.Speaker==DialogueSpeaker.Teacher))
                    foreach(var pose in line.Poses)
                        Assert.That(pose.name,Does.Not.Contain("congratulate").And.Not.Contain("clap").And.Not.Contain("cheer"),e.Id);
            Assert.That(library.TryGet("frog.fall.first",out var fall),Is.True);
            Assert.That(fall.Lines[0].DurationSeconds,Is.EqualTo(.7f));
            Assert.That(fall.Lines[1].DurationSeconds,Is.EqualTo(1.8f));
            Assert.That(fall.Lines[1].Icon,Is.Not.Null);
        }
        [Test] public void MissingPointingIconIsAConfigurationError()
        {
            var library=GameplayDialogueLibrary.LoadDefault();Assert.That(library,Is.Not.Null);
            library.TryGet("frog.fall.first",out var fall);var icon=fall.Lines[1].Icon;
            try {fall.Lines[1].Icon=null;Assert.That(library.Validate(out string error),Is.False);Assert.That(error,Does.Contain("PointingHandRight"));}
            finally {fall.Lines[1].Icon=icon;}
        }
    }
}
