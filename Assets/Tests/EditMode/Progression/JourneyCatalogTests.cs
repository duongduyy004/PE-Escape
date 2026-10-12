using System.Linq;
using System.Reflection;
using KMA.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class JourneyCatalogTests
    {
        [Test]
        public void DefaultCatalog_HasTenChallengesInCourseOrderWithSpecifiedRules()
        {
            ChallengeCatalog catalog = ChallengeCatalog.LoadDefault();

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Validate(out string error), Is.True, error);
            Assert.That(catalog.Ordered.Select(x => x.Id).Distinct().Count(), Is.EqualTo(10));
            Assert.That(catalog.Ordered.Select(x => x.Id), Is.EqualTo(new[]
            {
                "sprint_learn", "sprint_practice", "sprint_exam",
                "volleyball_learn", "volleyball_practice", "volleyball_exam",
                "soccer_learn", "soccer_practice", "soccer_exam", "chess_final"
            }));
            Assert.That(catalog.Ordered.Select(x => x.Subject).Distinct(), Is.EqualTo(new[]
            {
                SubjectId.Sprint, SubjectId.Volleyball, SubjectId.Football, SubjectId.Chess
            }));
            Assert.That(catalog.Get("sprint_learn").TargetCount, Is.EqualTo(50));
            Assert.That(catalog.Get("sprint_practice").Distance, Is.EqualTo(150f));
            Assert.That(catalog.Get("sprint_practice").TimeLimit, Is.EqualTo(20f));
            Assert.That(catalog.Get("sprint_exam").Distance, Is.EqualTo(150f));
            Assert.That(catalog.Get("sprint_exam").TimeLimit, Is.EqualTo(15f));
            Assert.That(catalog.Get("sprint_practice").TimeLimit,
                Is.GreaterThan(catalog.Get("sprint_exam").TimeLimit));
            Assert.That(catalog.Get("volleyball_learn").TargetCount, Is.EqualTo(3));
            Assert.That(catalog.Get("volleyball_practice").TargetCount, Is.EqualTo(5));
            Assert.That(catalog.Get("volleyball_exam").TargetCount, Is.EqualTo(10));
            Assert.That(catalog.Get("volleyball_practice").TimeLimit, Is.EqualTo(120f), "Practice is a race to five inside two minutes.");
            Assert.That(catalog.Get("volleyball_exam").TimeLimit, Is.Zero, "Volleyball is won on points, not on a clock.");
            Assert.That(catalog.Get("soccer_learn").TargetCount, Is.EqualTo(3));
            Assert.That(catalog.Get("soccer_learn").KeeperEnabled, Is.False);
            Assert.That(catalog.Get("soccer_practice").TargetCount, Is.EqualTo(2));
            Assert.That(catalog.Get("soccer_practice").Difficulty, Is.EqualTo(ChallengeDifficulty.Normal));
            Assert.That(catalog.Get("soccer_exam").TargetCount, Is.EqualTo(3));
            Assert.That(catalog.Get("soccer_exam").Difficulty, Is.EqualTo(ChallengeDifficulty.Normal));
            Assert.That(catalog.Get("sprint_learn").TimeLimit, Is.Zero);
            Assert.That(catalog.Get("volleyball_learn").TimeLimit, Is.Zero);
            Assert.That(catalog.Get("soccer_learn").TimeLimit, Is.Zero);
            Assert.That(catalog.Get("soccer_practice").TimeLimit, Is.Zero);
            Assert.That(catalog.Get("soccer_exam").TimeLimit, Is.Zero);
        }

        [Test]
        public void CatalogValidation_RejectsDuplicateAndMissingDefinitions()
        {
            ChallengeCatalog catalog = CreateCatalog(out ChallengeDefinition first, out ChallengeDefinition second);
            SetField(first, "id", "duplicate");
            SetField(second, "id", "duplicate");

            Assert.That(catalog.Validate(out _), Is.False);
            SetField(second, "id", "");
            Assert.That(catalog.Validate(out _), Is.False);
        }

        [Test]
        public void CatalogValidation_RejectsNonFiniteAndNegativeValues()
        {
            ChallengeCatalog catalog = CreateCatalog(out ChallengeDefinition definition, out _);
            SetField(definition, "distance", float.NaN);
            Assert.That(catalog.Validate(out _), Is.False);
            SetField(definition, "distance", -1f);
            Assert.That(catalog.Validate(out _), Is.False);
            SetField(definition, "distance", 0f);
            SetField(definition, "timeLimit", float.PositiveInfinity);
            Assert.That(catalog.Validate(out _), Is.False);
            SetField(definition, "timeLimit", -1f);
            Assert.That(catalog.Validate(out _), Is.False);
            SetField(definition, "timeLimit", 0f);
            SetField(definition, "targetCount", -1);
            Assert.That(catalog.Validate(out _), Is.False);
        }

        [Test]
        public void Get_RejectsUnknownChallengeId()
        {
            ChallengeCatalog catalog = ChallengeCatalog.LoadDefault();
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => catalog.Get("missing"));
        }

        static ChallengeCatalog CreateCatalog(out ChallengeDefinition first, out ChallengeDefinition second)
        {
            ChallengeCatalog source = ChallengeCatalog.LoadDefault();
            var catalog = Object.Instantiate(source);
            ChallengeDefinition[] definitions = source.Ordered.Select(Object.Instantiate).ToArray();
            first = definitions[0];
            second = definitions[1];
            SetField(catalog, "challenges", definitions);
            return catalog;
        }

        static void SetField(Object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Missing serialized field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
