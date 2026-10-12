using KMA.UI.Kit;
using System.Collections.Generic;
using KMA.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    public sealed class SprintUiLayoutTests
    {
        static IEnumerable<TestCaseData> LandscapeSafeAreas()
        {
            yield return new TestCaseData(new Rect(0f, 0f, 1920f, 1080f)).SetName("16x9");
            yield return new TestCaseData(new Rect(0f, 0f, 1728f, 1080f)).SetName("16x10");
            yield return new TestCaseData(new Rect(0f, 0f, 2160f, 1080f)).SetName("18x9");
            yield return new TestCaseData(new Rect(0f, 0f, 2340f, 1080f)).SetName("19.5x9");
            yield return new TestCaseData(new Rect(0f, 0f, 2400f, 1080f)).SetName("20x9");
            yield return new TestCaseData(new Rect(120f, 0f, 2232f, 1080f)).SetName("20x9-notch");
        }

        [TestCase(0, 0.125f)]
        [TestCase(1, 0.375f)]
        [TestCase(2, 0.625f)]
        [TestCase(3, 0.875f)]
        public void LaneCenter01_CentersFourEqualLanes(int lane, float expected) =>
            Assert.That(SprintUiLayout.LaneCenter01(lane, 4), Is.EqualTo(expected).Within(.0001f));

        [TestCase(69.9f, false)]
        [TestCase(70f, true)]
        [TestCase(100f, true)]
        public void FinishVisible_UsesApprovedThreshold(float distance, bool expected) =>
            Assert.That(SprintUiLayout.FinishVisible(distance), Is.EqualTo(expected));

        [TestCaseSource(nameof(LandscapeSafeAreas))]
        public void ControlVisualIsSmallerThanTheTapAreaItSitsIn(Rect safe)
        {
            Rect visual01 = SprintUiLayout.ControlVisualRect01(true);

            Assert.That(visual01.width, Is.LessThan(1f).And.GreaterThan(0f));
            Assert.That(visual01.height, Is.LessThan(1f).And.GreaterThan(0f));
            Assert.That(visual01.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(visual01.xMax, Is.LessThanOrEqualTo(1f));
            Assert.That(visual01.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(visual01.yMax, Is.LessThanOrEqualTo(1f));

            // The shrink must be real at every aspect: the drawn button covers well under half the
            // area a thumb can hit.
            Rect tap = SprintUiLayout.ControlRect(safe, true);
            float drawnArea = tap.width * visual01.width * tap.height * visual01.height;
            Assert.That(drawnArea, Is.LessThan(tap.width * tap.height * .5f));
        }

        [Test]
        public void ControlsUseThirtyPercentLargerVisualsAtTheOuterBottomCorners()
        {
            Rect left = SprintUiLayout.ControlVisualRect01(true);
            Rect right = SprintUiLayout.ControlVisualRect01(false);

            Assert.That(left.width, Is.EqualTo(.52f * 1.3f).Within(.0001f));
            Assert.That(left.height, Is.EqualTo(.50f * 1.3f).Within(.0001f));
            Assert.That(left.xMin, Is.Zero);
            Assert.That(right.xMax, Is.EqualTo(1f));
            Assert.That(left.yMin, Is.Zero);
            Assert.That(right.yMin, Is.Zero);
        }

        [TestCaseSource(nameof(LandscapeSafeAreas))]
        public void ShrinkingTheVisualDoesNotShrinkTheHitBox(Rect safe)
        {
            // The tap area is what ScreenTapArea raycasts against; it is sized from ControlRect
            // alone and must not be derived from the visual.
            Rect left = SprintUiLayout.ControlRect(safe, true);
            Rect right = SprintUiLayout.ControlRect(safe, false);

            Assert.That(left.height, Is.EqualTo(safe.height * .26f).Within(.01f));
            Assert.That(left.width, Is.EqualTo(safe.height * .43f).Within(.01f));
            Assert.That(right.size, Is.EqualTo(left.size));
        }

        [TestCase(60f, 0f)]
        [TestCase(70f, 0f)]
        [TestCase(85f, .5f)]
        [TestCase(100f, 1f)]
        [TestCase(120f, 1f)]
        public void FinishReveal01_RunsFromTheRevealDistanceToTheLine(float distance, float expected) =>
            Assert.That(SprintUiLayout.FinishReveal01(distance), Is.EqualTo(expected).Within(.0001f));

        [Test]
        public void FinishLineStartsAtTheEntryPointSoItNeverPopsIn()
        {
            const float entryX = 12f;
            Assert.That(SprintTrackLayout.FinishLineX(0f, entryX), Is.EqualTo(entryX).Within(.0001f),
                "At the reveal distance the line must still be off-screen, not sitting on the track.");
        }

        [Test]
        public void FinishLineSlidesInMonotonically()
        {
            const float entryX = 12f;
            float previous = float.MaxValue;
            for (float distance = 70f; distance <= 100f; distance += 2.5f)
            {
                float x = SprintTrackLayout.FinishLineX(SprintUiLayout.FinishReveal01(distance), entryX);
                Assert.That(x, Is.LessThan(previous), $"The line must keep approaching at {distance} m.");
                previous = x;
            }
        }

        [Test]
        public void FinishLineComesToRestWhereTheRunnerFinishes()
        {
            Assert.That(SprintTrackLayout.FinishLineX(1f, 12f), Is.EqualTo(SprintTrackLayout.FinishX).Within(.0001f));
            Assert.That(SprintTrackLayout.TrackStartX + SprintTrackLayout.TrackLength,
                Is.EqualTo(SprintTrackLayout.FinishX).Within(.0001f),
                "A runner at full distance must stand on the line.");
            // The narrowest supported view (16:10 at ortho 5.4) is 8.64 units either side of centre.
            Assert.That(SprintTrackLayout.FinishX + SprintTrackLayout.FinishWidth * .5f, Is.LessThan(8.64f),
                "The line must be on screen at 16:10.");
            Assert.That(SprintTrackLayout.FinishTopY, Is.GreaterThan(SprintTrackLayout.FinishBottomY));
        }

        [TestCaseSource(nameof(LandscapeSafeAreas))]
        public void NoTwoElementsOverlapDuringTheStartState(Rect safe)
        {
            SprintUiLayout.NamedRect[] rects = SprintUiLayout.StartStateRects(safe);
            for (int i = 0; i < rects.Length; i++)
            {
                for (int j = i + 1; j < rects.Length; j++)
                {
                    Assert.That(rects[i].Rect.Overlaps(rects[j].Rect), Is.False,
                        $"{rects[i].Name} overlaps {rects[j].Name} at {safe.width}x{safe.height}");
                }
            }
        }

        [TestCaseSource(nameof(LandscapeSafeAreas))]
        public void NoTwoElementsOverlapDuringTheRace(Rect safe)
        {
            SprintUiLayout.NamedRect[] rects = SprintUiLayout.RaceRects(safe);
            for (int i = 0; i < rects.Length; i++)
            {
                for (int j = i + 1; j < rects.Length; j++)
                {
                    Assert.That(rects[i].Rect.Overlaps(rects[j].Rect), Is.False,
                        $"{rects[i].Name} overlaps {rects[j].Name} at {safe.width}x{safe.height}");
                }
            }
        }

        [TestCaseSource(nameof(LandscapeSafeAreas))]
        public void EveryElementStaysInsideTheSafeAreaWithEdgePadding(Rect safe)
        {
            float padY = safe.height * .01f;
            foreach (SprintUiLayout.NamedRect named in SprintUiLayout.StartStateRects(safe))
            {
                float padX = named.Name == "LeftControl" || named.Name == "RightControl"
                    ? safe.height * .0185f : safe.width * .02f;
                Assert.That(named.Rect.xMin, Is.GreaterThanOrEqualTo(safe.xMin + padX - .01f), $"{named.Name} left");
                Assert.That(named.Rect.xMax, Is.LessThanOrEqualTo(safe.xMax - padX + .01f), $"{named.Name} right");
                Assert.That(named.Rect.yMin, Is.GreaterThanOrEqualTo(safe.yMin + padY - .01f), $"{named.Name} bottom");
                Assert.That(named.Rect.yMax, Is.LessThanOrEqualTo(safe.yMax - padY + .01f), $"{named.Name} top");
            }
        }

        [TestCaseSource(nameof(LandscapeSafeAreas))]
        public void ControlsAreSymmetricEqualSizedAndNeverCrossTheMidpoint(Rect safe)
        {
            Rect left = SprintUiLayout.ControlRect(safe, true);
            Rect right = SprintUiLayout.ControlRect(safe, false);
            Assert.That(left.width, Is.EqualTo(right.width).Within(.01f));
            Assert.That(left.height, Is.EqualTo(right.height).Within(.01f));
            Assert.That(left.yMin, Is.EqualTo(right.yMin).Within(.01f));
            Assert.That(left.xMin - safe.xMin, Is.EqualTo(safe.xMax - right.xMax).Within(.01f));
            Assert.That(left.xMax, Is.LessThan(safe.center.x));
            Assert.That(right.xMin, Is.GreaterThan(safe.center.x));
        }

        [TestCaseSource(nameof(LandscapeSafeAreas))]
        public void ElementSizesDependOnHeightOnly(Rect safe)
        {
            var reference = new Rect(0f, 0f, 1920f, safe.height);
            Assert.That(SprintUiLayout.ControlRect(safe, true).size,
                Is.EqualTo(SprintUiLayout.ControlRect(reference, true).size), "ControlRect");
            Assert.That(SprintUiLayout.ControlRect(safe, false).size,
                Is.EqualTo(SprintUiLayout.ControlRect(reference, false).size), "ControlRect right");
            Assert.That(SprintUiLayout.ScoreboardRect(safe).size,
                Is.EqualTo(SprintUiLayout.ScoreboardRect(reference).size), "ScoreboardRect");
            Assert.That(SprintUiLayout.PauseRect(safe).size,
                Is.EqualTo(SprintUiLayout.PauseRect(reference).size), "PauseRect");
            Assert.That(SprintUiLayout.ModeChipRect(safe).size,
                Is.EqualTo(SprintUiLayout.ModeChipRect(reference).size), "ModeChipRect");
            Assert.That(SprintUiLayout.CountdownRect(safe).size,
                Is.EqualTo(SprintUiLayout.CountdownRect(reference).size), "CountdownRect");
            Assert.That(SprintUiLayout.InstructionRect(safe).size,
                Is.EqualTo(SprintUiLayout.InstructionRect(reference).size), "InstructionRect");
            Assert.That(SprintUiLayout.ProgressRailRect(safe).height,
                Is.EqualTo(SprintUiLayout.ProgressRailRect(reference).height), "rail thickness");
        }

        [Test]
        public void ProgressRailIsTheOneElementThatSpansWidth()
        {
            // Deliberate exception to height-only sizing: the rail maps 0-100 m onto the
            // screen's left-right axis, the same axis the runner moves along.
            Rect narrow = SprintUiLayout.ProgressRailRect(new Rect(0f, 0f, 1728f, 1080f));
            Rect wide = SprintUiLayout.ProgressRailRect(new Rect(0f, 0f, 2400f, 1080f));
            Assert.That(wide.width, Is.GreaterThan(narrow.width),
                "the rail must stretch with the screen, unlike every other element");
            Assert.That(wide.height, Is.EqualTo(narrow.height).Within(.01f),
                "but its thickness stays height-derived");
        }

        [Test]
        public void PauseIsSquareAndScoreboardSitsAboveTheTopLane()
        {
            var safe = new Rect(0f, 0f, 1920f, 1080f);
            Rect pause = SprintUiLayout.PauseRect(safe);
            Assert.That(pause.width, Is.EqualTo(pause.height).Within(.01f));
            Assert.That(SprintUiLayout.ScoreboardRect(safe).yMin,
                Is.GreaterThan(safe.yMin + safe.height * .76f), "scoreboard must clear the top lane");
        }

        [TestCaseSource(nameof(LandscapeSafeAreas))]
        public void ScoreboardFitsItsWidestRow(Rect safe)
        {
            // Worst case row 1 is "100 / 100 m" at Title size beside the rank pill.
            // Budget in canvas units, measured against the panel's inner width.
            const float distanceGlyphs = 11f;          // "100 / 100 m"
            const float titleAdvance = MinigameUiTheme.Title * .55f;   // conservative advance per glyph
            const float rankPill = MinigameUiTheme.Headline * 2.4f;
            const float padding = MinigameUiTheme.SpaceLg;
            const float gap = MinigameUiTheme.SpaceMd;

            float required = distanceGlyphs * titleAdvance + gap + rankPill + padding;
            Assert.That(SprintUiLayout.ScoreboardRect(safe).width, Is.GreaterThanOrEqualTo(required),
                $"scoreboard is too narrow for its widest row at {safe.width}x{safe.height}");
        }

        [Test]
        public void RaceRectsAreTheStartStateRectsWithoutCountdownAndInstruction()
        {
            var safe = new Rect(0f, 0f, 1920f, 1080f);
            var raceNames = new List<string>();
            foreach (SprintUiLayout.NamedRect named in SprintUiLayout.RaceRects(safe))
                raceNames.Add(named.Name);
            Assert.That(raceNames, Does.Not.Contain("Countdown"));
            Assert.That(raceNames, Does.Not.Contain("Instruction"));
            Assert.That(raceNames, Has.Count.EqualTo(6));
            Assert.That(SprintUiLayout.StartStateRects(safe), Has.Length.EqualTo(8));
        }
    }
}
