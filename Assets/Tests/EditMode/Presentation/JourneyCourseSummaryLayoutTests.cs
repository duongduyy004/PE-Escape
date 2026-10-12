using System.Collections.Generic;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    public sealed class JourneyCourseSummaryLayoutTests
    {
        static IEnumerable<TestCaseData> LandscapeWidths()
        {
            yield return new TestCaseData(1536f).SetName("narrow-landscape");
            yield return new TestCaseData(1728f).SetName("16x10");
            yield return new TestCaseData(1920f).SetName("16x9");
            yield return new TestCaseData(2160f).SetName("18x9");
            yield return new TestCaseData(2400f).SetName("20x9");
        }

        [TestCaseSource(nameof(LandscapeWidths))]
        public void FourScoresUseTwoBalancedLinesInsideTheReplayColumn(float safeWidth)
        {
            var scores = new[]
            {
                "Chạy nước rút: 10.0 · A",
                "Bóng chuyền: 10.0 · B",
                "Bóng đá: 10.0 · A",
                "Bài kiểm tra cuối: 10.0 · S"
            };
            string[] lines = JourneyCourseSummary.FormatScoreLines(scores).Split('\n');
            float contentWidth = safeWidth - 128f; // 64px inset on both sides in MapPresentationBuilder.
            float textWidth = JourneyCourseSummary.TextColumnWidthForContent(contentWidth);
            var host = new GameObject("SummaryGeometryCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var panel = new GameObject("JourneyCourseSummary", typeof(RectTransform));
                panel.transform.SetParent(host.transform, false);
                TMP_Text label = UiKit.Label(panel.transform, "SummaryText", string.Empty,
                    UITheme.Shared.LessonJourney.captionSize - 16f, UITheme.Shared.TextPrimary);
                label.enableWordWrapping = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;

                Assert.That(lines, Has.Length.EqualTo(2));
                Assert.That(lines[0], Does.Contain(scores[0]).And.Contain(scores[1]));
                Assert.That(lines[1], Does.Contain(scores[2]).And.Contain(scores[3]));
                Assert.That(textWidth, Is.GreaterThan(900f), "the score column remains wide at the narrowest supported landscape width");
                foreach (string line in lines)
                    Assert.That(label.GetPreferredValues(line).x, Is.LessThanOrEqualTo(textWidth),
                        $"summary line is clipped at safe width {safeWidth}");
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
