using System.Collections.Generic;
using System.IO;
using System.Linq;
using KMA.EditorTools;
using KMA.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace KMA.Tests.EditorTools
{
    public sealed class StudentJourneyContentBuilderTests
    {
        [Test]
        public void RegenerationPreservesCurrentChallengeTimings()
        {
            ChallengeCatalog catalog = ChallengeCatalog.LoadDefault();
            var snapshots = catalog.Ordered.Select(AssetDatabase.GetAssetPath)
                .Append(AssetDatabase.GetAssetPath(catalog))
                .Append("Assets/_Project/Resources/Journey/SprintBalance.asset")
                .Distinct().ToDictionary(path => path, File.ReadAllBytes);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    StudentJourneyContentBuilder.BuildChallenges();
                    Assert.That(catalog.Get("sprint_practice").TimeLimit, Is.EqualTo(20f));
                    Assert.That(catalog.Get("sprint_exam").TimeLimit, Is.EqualTo(15f));
                    Assert.That(catalog.Get("sprint_exam").Distance, Is.EqualTo(150f));
                    Assert.That(catalog.Get("volleyball_practice").TimeLimit, Is.Zero);
                    Assert.That(catalog.Get("volleyball_exam").TimeLimit, Is.Zero);
                }
            }
            finally
            {
                foreach (var item in snapshots)
                {
                    File.WriteAllBytes(item.Key, item.Value);
                    AssetDatabase.ImportAsset(item.Key, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        [Test]
        public void RegeneratedFootballLessonsKeepNormalAndLearnKeepsKeeperOff()
        {
            ChallengeCatalog catalog = ChallengeCatalog.LoadDefault();
            var snapshots = catalog.Ordered.Select(AssetDatabase.GetAssetPath)
                .Append(AssetDatabase.GetAssetPath(catalog))
                .ToDictionary(path => path, File.ReadAllBytes);
            try
            {
                StudentJourneyContentBuilder.BuildChallenges();
                foreach (ChallengeDefinition lesson in catalog.Ordered.Where(item => item.Subject == SubjectId.Football))
                    Assert.That(lesson.Difficulty, Is.EqualTo(ChallengeDifficulty.Normal), lesson.Id);
                Assert.That(catalog.Get("soccer_learn").KeeperEnabled, Is.False);
                Assert.That(catalog.Get("soccer_practice").KeeperEnabled, Is.True);
                Assert.That(catalog.Get("soccer_exam").KeeperEnabled, Is.True);
                Assert.That(catalog.Get("volleyball_learn").Difficulty, Is.EqualTo(ChallengeDifficulty.Easy));
            }
            finally
            {
                foreach (KeyValuePair<string, byte[]> snapshot in snapshots)
                {
                    File.WriteAllBytes(snapshot.Key, snapshot.Value);
                    AssetDatabase.ImportAsset(snapshot.Key, ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
