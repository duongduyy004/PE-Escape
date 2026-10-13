using System.Reflection;
using System.Collections;
using System.Linq;
using UnityEditor;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class UIComponentTests
    {
        [Test]
        public void SafeAreaFitterMapsLandscapeInsetsToBothHorizontalEdges()
        {
            var root = new GameObject("safe-area-fitter", typeof(RectTransform));
            try
            {
                var rectTransform = root.GetComponent<RectTransform>();
                // Offsets only read as insets on a stretched rect; on the default coincident
                // anchors they are sizeDelta, and the fitter deliberately leaves those alone.
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;

                var fitter = root.AddComponent<SafeAreaFitter>();
                var offsets = fitter.CalculateOffsets(
                    new Rect(100f, 0f, 1720f, 1080f),
                    new Vector2(1920f, 1080f));

                Assert.That(offsets.left, Is.EqualTo(100f).Within(.01f));
                Assert.That(offsets.right, Is.EqualTo(100f).Within(.01f));

                fitter.Apply(new Rect(100f, 0f, 1720f, 1080f), new Vector2Int(1920, 1080));
                Assert.That(rectTransform.offsetMin.x, Is.EqualTo(100f).Within(.01f));
                Assert.That(rectTransform.offsetMax.x, Is.EqualTo(-100f).Within(.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MapPresentationFollowsTheShellStyle()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, CompleteSprintJourney());
                var problems = ShellStyleAudit.Audit(root.transform.Find("S5MapPresentation/Content/Header").gameObject);
                problems.AddRange(ShellStyleAudit.Audit(root.transform.Find("S5MapPresentation/Content/SelectionGrid").gameObject));
                Assert.That(problems, Is.Empty, string.Join("\n", problems));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapStopTagFitsAtTheFontFloor()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                TMP_Text tag = screen.Nodes[0].transform.Find("CurrentTag").GetComponentInChildren<TMP_Text>(true);
                Assert.That(tag.fontSizeMin, Is.GreaterThanOrEqualTo(MinigameUiTheme.MinimumFontSize));
                tag.ForceMeshUpdate();
                Assert.That(tag.isTextOverflowing, Is.False, "ĐANG Ở ĐÂY must fit its tag");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SharedFontAssetsExposeSourceAndAtlasTextures()
        {
            foreach (var assetName in new[] { "Baloo2-ExtraBold", "Nunito-Bold" })
            {
                var path = $"Assets/_Project/Fonts/{assetName}.asset";
                var font = AssetDatabase.LoadMainAssetAtPath(path);
                Assert.That(font, Is.Not.Null, path);

                var source = font.GetType().GetProperty("sourceFontFile")?.GetValue(font);
                Assert.That(source, Is.Not.Null, $"{assetName} source font");

                var atlases = font.GetType().GetProperty("atlasTextures")?.GetValue(font) as IEnumerable;
                Assert.That(atlases, Is.Not.Null, $"{assetName} atlas textures");
                var atlasCount = 0;
                foreach (var atlas in atlases)
                {
                    atlasCount++;
                    Assert.That(atlas, Is.Not.Null, $"{assetName} atlas {atlasCount}");
                }
                Assert.That(atlasCount, Is.GreaterThan(0), $"{assetName} atlas count");
            }
        }

        [Test]
        public void HeartBarClampsToFiveSlotsAndRendersFilledAndEmptyStates()
        {
            var root = new GameObject("heart-bar");
            try
            {
                var heartBar = root.AddComponent<HeartBar>();
                var slots = new Image[5];
                for (var index = 0; index < slots.Length; index++)
                {
                    var slot = new GameObject($"heart-{index}", typeof(RectTransform));
                    slot.transform.SetParent(root.transform, false);
                    slots[index] = slot.AddComponent<Image>();
                }
                SetPrivateField(heartBar, "slots", slots);

                heartBar.SetHearts(3);

                Assert.That(heartBar.CurrentHearts, Is.EqualTo(3));
                Assert.That(slots[0].color, Is.EqualTo(heartBar.FilledColor));
                Assert.That(slots[2].color, Is.EqualTo(heartBar.FilledColor));
                Assert.That(slots[3].color, Is.EqualTo(heartBar.EmptyColor));

                heartBar.SetHearts(99);
                Assert.That(heartBar.CurrentHearts, Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MapPresentationPlacesThreeOrderedSubjectsOnJourneyPath()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());

                var grid = root.transform.Find("S5MapPresentation/Content/SelectionGrid")
                    .GetComponent<GridLayoutGroup>();
                Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
                Assert.That(grid.constraintCount, Is.EqualTo(3));
                Assert.That(grid.GetComponentsInChildren<MapNodeView>(true), Has.Length.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<MapNodeView>(true), Has.Length.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<MapNodeView>(true).Select(node => node.SubjectId),
                    Is.EqualTo(new[] { SubjectId.Sprint, SubjectId.Volleyball, SubjectId.Football, SubjectId.Chess }));
                Assert.That(grid.cellSize.y, Is.GreaterThanOrEqualTo(220f));
                Assert.That(root.transform.Find("S5MapPresentation/Content/ProgressSection"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MapHeaderMakesNavigationTitleAndLivesReadableAtLandscapeScale()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());

                Transform header = root.transform.Find("S5MapPresentation/Content/Header");
                Assert.That(header.GetComponent<Image>(), Is.Null, "The header floats its blocks on the scenery.");
                Assert.That(header.Find("Divider"), Is.Null);

                LayoutElement back = header.Find("BackButton").GetComponent<LayoutElement>();
                Assert.That(back.preferredWidth, Is.GreaterThanOrEqualTo(56f));
                Assert.That(back.preferredHeight, Is.GreaterThanOrEqualTo(56f));

                TMP_Text title = header.Find("Heading/TitleContainer/Title/Text").GetComponent<TMP_Text>();
                Assert.That(title.fontSize, Is.InRange(36, 44));
                Assert.That(title.text, Is.EqualTo("CHỌN MÔN THI"));
                Assert.That(title.font, Is.SameAs(VietTypography.Library.title));
                Assert.That(title.extraPadding, Is.True);
                Assert.That(header.Find("Heading/SubtitleContainer"), Is.Null);

                Transform livesPanel = header.Find("LivesPanel");
                Assert.That(livesPanel, Is.Not.Null);
                Assert.That(livesPanel.Find(ShellKit.FaceName).GetComponent<Image>(), Is.Not.Null);
                TMP_Text lives = livesPanel.Find("LivesLabelContainer/LivesLabel").GetComponent<TMP_Text>();
                Assert.That(lives.text, Is.EqualTo("Lượt thi: 5/5"));
                Assert.That(lives.fontSize, Is.GreaterThanOrEqualTo(24));
                foreach (Image heart in livesPanel.GetComponentInChildren<HeartBar>(true)
                             .GetComponentsInChildren<Image>(true))
                {
                    Assert.That(heart.sprite, Is.Not.Null, heart.name);
                    Assert.That(heart.preserveAspect, Is.True, heart.name);
                    Assert.That(heart.GetComponent<LayoutElement>().preferredWidth,
                        Is.GreaterThanOrEqualTo(40f), heart.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Same language as the menu panel: cream face, thick navy border, hard navy drop shadow,
        // navy italic title over a gold offset copy and a red square accent.
        [Test]
        public void MapHeaderUsesTheMenuPanelStyle()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                Transform header = root.transform.Find("S5MapPresentation/Content/Header");
                Color cream = ShellTheme.Cream;

                foreach (string block in new[] { "BackButton", "Heading", "LivesPanel" })
                {
                    Transform rect = header.Find(block);
                    Transform layers = block == "BackButton" ? rect.Find(ShellKit.BodyName) : rect;
                    Assert.That(layers.Find(ShellKit.BorderName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Ink), block);
                    Assert.That(layers.Find(ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Cream), block);
                    Assert.That(rect.GetComponents<Shadow>(), Is.Empty, block + " draws its drop as a layer");
                    Assert.That(rect.Find(ShellKit.ShadowName), Is.Not.Null, block);
                }
                TMP_Text title = header.Find("Heading/TitleContainer/Title/Text").GetComponent<TMP_Text>();
                Assert.That(title.color, Is.EqualTo(ShellTheme.Ink));
                TMP_Text titleShadow = header.Find("Heading/TitleContainer/Title/Drop").GetComponent<TMP_Text>();
                Assert.That(titleShadow.text, Is.EqualTo(title.text));
                Assert.That(titleShadow.color, Is.EqualTo(ShellTheme.Gold));
                Assert.That(header.Find("Heading").Cast<Transform>().Any(t => t.name.StartsWith(ShellKit.AccentPrefix)), Is.True);
                TMP_Text arrow = header.Find("BackButton").GetComponentInChildren<TMP_Text>(true);
                Assert.That(arrow.color, Is.EqualTo(ShellTheme.Ink));
                TMP_Text lives = header.Find("LivesPanel/LivesLabelContainer/LivesLabel").GetComponent<TMP_Text>();
                Assert.That(ContrastRatio(lives.color, cream), Is.GreaterThanOrEqualTo(4.5f));

                HeartBar hearts = screen.Hearts;
                hearts.SetHearts(2);
                Image empty = hearts.GetComponentsInChildren<Image>(true)[4];
                Assert.That(ContrastRatio(Opaque(empty.color, cream), cream), Is.GreaterThanOrEqualTo(1.5f),
                    "An empty heart must still show on the cream face.");
                hearts.SetCountdown(System.TimeSpan.FromMinutes(5), false);
                Assert.That(hearts.CountdownText, Is.EqualTo("5:00"));
                TMP_Text timer = header.Find("LivesPanel/LivesLabelContainer/LifeTimer").GetComponent<TMP_Text>();
                Assert.That(timer.gameObject.activeSelf, Is.True);
                Assert.That(ContrastRatio(timer.color, cream), Is.GreaterThanOrEqualTo(4.5f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Color Opaque(Color color, Color under) =>
            new Color(Mathf.Lerp(under.r, color.r, color.a), Mathf.Lerp(under.g, color.g, color.a),
                Mathf.Lerp(under.b, color.b, color.a), 1f);

        [Test]
        public void JourneyLessonLabelsMeetContrastOnTheirButtonSurface()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());

                Transform panel = root.transform.Find("S5MapPresentation/Content/JourneyLessons");
                Color panelSurface = panel.GetComponent<Image>().color;
                Assert.That(ContrastRatio(panel.Find("Lesson1/StageTitle").GetComponent<TMP_Text>().color,
                    panelSurface), Is.GreaterThanOrEqualTo(4.5f), "Stage names must read on the popup surface.");
                Color detail = panel.Find("DetailCard").GetComponent<Image>().color;
                Assert.That(ContrastRatio(panel.Find("DetailCard/Objective").GetComponent<TMP_Text>().color, detail),
                    Is.GreaterThanOrEqualTo(4.5f), "The objective must read on the detail card.");
                Assert.That(ContrastRatio(panel.Find("DetailCard/PlayButton/Label").GetComponent<TMP_Text>().color,
                    panel.Find("DetailCard/PlayButton").GetComponent<Image>().color), Is.GreaterThanOrEqualTo(4.5f),
                    "The play label must read on its button.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LessonJourneyShowsCompletionCountAndExplainsTheNextLockedStage()
        {
            var root = new GameObject("lesson-journey", typeof(RectTransform));
            try
            {
                var session = new GameSession();
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, session);
                Transform panel = screen.LessonList.transform;
                var progress = panel.Find("CourseProgress")?.GetComponent<TMP_Text>();
                Assert.That(progress, Is.Not.Null, "The chapter must show its lesson completion count.");
                Assert.That(progress.text, Is.EqualTo("0/3 bài hoàn thành"));
                TMP_Text status = panel.Find("DetailCard/Status").GetComponent<TMP_Text>();
                Button play = panel.Find("DetailCard/PlayButton").GetComponent<Button>();
                panel.Find("Lesson2").GetComponent<Button>().onClick.Invoke();
                Assert.That(status.text, Does.Contain("HỌC"), "A locked practice must explain which stage opens it.");
                Assert.That(play.interactable, Is.False);

                var definition = session.Journey.Catalog.Get("sprint_learn");
                Assert.That(session.TryStartChallenge(definition.Id, ChallengeAttemptMode.Journey,
                    definition.Difficulty, out ChallengeAttemptContext attempt), Is.True);
                session.SubmitChallengeResult(new ChallengeAttemptResult(attempt, true,
                    new ChallengeMetrics(completedTargets: definition.TargetCount)));
                screen.RefreshJourney(session);

                Assert.That(progress.text, Is.EqualTo("1/3 bài hoàn thành"));
                panel.Find("Lesson1").GetComponent<Button>().onClick.Invoke();
                Assert.That(play.interactable, Is.True, "A finished lesson can be reviewed.");
                panel.Find("Lesson2").GetComponent<Button>().onClick.Invoke();
                Assert.That(play.interactable, Is.True, "The next lesson can be played.");
                panel.Find("Lesson3").GetComponent<Button>().onClick.Invoke();
                Assert.That(play.interactable, Is.False);
                Assert.That(status.text, Does.Contain("LUYỆN"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LessonJourneyKeepsThreeReadableStagesInSequence(
            [Values(1440f, 1920f, 2400f)] float width, [Values(0, 1, 2)] int chapter)
        {
            const float height = 1080f;
            var root = new GameObject("lesson-layout", typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
                var screen = root.AddComponent<MapScreen>();
                var session = new GameSession();
                foreach (ChallengeDefinition definition in session.Journey.Catalog.Ordered.Take(chapter * 3))
                {
                    Assert.That(session.TryStartChallenge(definition.Id, ChallengeAttemptMode.Journey,
                        definition.Difficulty, out ChallengeAttemptContext attempt), Is.True);
                    session.SubmitChallengeResult(new ChallengeAttemptResult(attempt, true,
                        new ChallengeMetrics(completedTargets: definition.TargetCount),
                        ChallengeDefinition.IsScored(definition.Kind) ? new MinigameResult(true, 8f, Rank.A) : null));
                }
                MapPresentationBuilder.Build(screen, session);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                RectTransform panel = (RectTransform)screen.LessonList.transform;
                Rect previous = default;
                for (int i = 0; i < 3; i++)
                {
                    RectTransform card = (RectTransform)panel.Find($"Lesson{i + 1}");
                    Vector3[] corners = new Vector3[4];
                    card.GetWorldCorners(corners);
                    var bounds = new Rect(corners[0], corners[2] - corners[0]);
                    Assert.That(card.rect.width, Is.GreaterThan(width * .15f));
                    Assert.That(card.rect.height, Is.GreaterThan(height * .13f),
                        "Each stage needs room for its disc and name.");
                    if (i > 0) Assert.That(bounds.xMin, Is.GreaterThan(previous.xMax));
                    previous = bounds;
                    AssertLabelsFit(card);
                }
                for (int i = 0; i < 3; i++)
                {
                    panel.Find($"Lesson{i + 1}").GetComponent<Button>().onClick.Invoke();
                    AssertLabelsFit(panel.Find("DetailCard"));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void AssertLabelsFit(Transform root)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                label.ForceMeshUpdate(true);
                Assert.That(label.textBounds.size.y,
                    Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f), label.name);
                Assert.That(label.isTextTruncated, Is.False, label.name + " must show its full text.");
            }
        }

        static float ContrastRatio(Color foreground, Color background)
        {
            float first = RelativeLuminance(foreground);
            float second = RelativeLuminance(background);
            return (Mathf.Max(first, second) + .05f) / (Mathf.Min(first, second) + .05f);
        }

        [Test]
        public void CompletedCourseSummaryLeavesLessonCardsVisibleAndClickable()
        {
            var root = new GameObject("completed-lesson-journey", typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(1440f, 1080f);
                var session = new GameSession();
                foreach (ChallengeDefinition definition in session.Journey.Catalog.Ordered)
                {
                    Assert.That(session.TryStartChallenge(definition.Id, ChallengeAttemptMode.Journey,
                        definition.Difficulty, out ChallengeAttemptContext attempt), Is.True);
                    session.SubmitChallengeResult(new ChallengeAttemptResult(attempt, true,
                        new ChallengeMetrics(completedTargets: definition.TargetCount),
                        ChallengeDefinition.IsScored(definition.Kind) ? new MinigameResult(true, 8f, Rank.A) : null));
                }
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, session);
                var summary = (RectTransform)screen.CourseSummary.transform;
                var panel = (RectTransform)screen.LessonList.transform;
                Assert.That(summary.GetComponent<Image>().raycastTarget, Is.False);
                screen.SelectSubject(SubjectId.Sprint);
                Transform scrim = panel.parent.Find("LessonScrim");
                Assert.That(panel.GetSiblingIndex(), Is.GreaterThan(scrim.GetSiblingIndex()));
                Assert.That(scrim.GetSiblingIndex(), Is.GreaterThan(summary.GetSiblingIndex()),
                    "The modal lessons must draw above the completed-course summary.");
                foreach (TMP_Text label in summary.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate(true);
                    Assert.That(label.preferredHeight,
                        Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f));
                    Assert.That(label.preferredWidth,
                        Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1f));
                }
                Assert.That(screen.LessonList.GetComponentsInChildren<Button>()
                    .All(button => button.interactable), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static float RelativeLuminance(Color color)
        {
            static float Linear(float value) => value <= .04045f
                ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
            return .2126f * Linear(color.r) + .7152f * Linear(color.g) + .0722f * Linear(color.b);
        }

        [Test]
        public void MapCardsCommunicateCompletedAndLockedStatesWithoutColorAlone()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var session = CompleteSprintJourney();
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, session);
                MapNodeView completed = screen.Nodes.Single(node => node.SubjectId == SubjectId.Sprint);
                MapNodeView football = screen.Nodes.Single(node => node.SubjectId == SubjectId.Football);
                Assert.That(completed.StatusText, Is.EqualTo("HOÀN THÀNH"));
                Assert.That(completed.transform.Find("Badge/DoneMark").gameObject.activeSelf, Is.True);
                Assert.That(football.StatusText, Is.EqualTo("CHƯA MỞ KHÓA"));
                Assert.That(football.transform.Find("Badge/LockIcon").gameObject.activeSelf, Is.True);
                Assert.That(completed.GetComponent<ShellPressFeedback>(), Is.Not.Null);
                Assert.That(football.GetComponent<Button>().interactable, Is.False);
                var iconSprites = screen.Nodes.Select(node => node.transform.Find("Badge/IconGlyph")
                    .GetComponent<Image>().sprite).ToArray();
                Assert.That(iconSprites.Distinct().Count(), Is.EqualTo(4));
                foreach (MapNodeView node in screen.Nodes)
                    Assert.That(node.transform.Find("LabelGroup/TitleContainer/Title").GetComponent<TMP_Text>().fontSize,
                        Is.GreaterThanOrEqualTo(30), node.name);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FutureSubjectsAreLockedUntilPreviousExamPasses()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());

                Assert.That(screen.Nodes[0].IsInteractable, Is.True);
                foreach (MapNodeView future in screen.Nodes.Where(node => node.SubjectId != SubjectId.Sprint))
                {
                    Assert.That(future.IsInteractable, Is.False, future.DisplayName);
                    Assert.That(future.StatusText, Is.EqualTo(future.SubjectId == SubjectId.Chess
                        ? "Đạt Bóng đá để mở" : "CHƯA MỞ KHÓA"));
                }

            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MapBadgesUseCircleSurfacesAndControlledDepth()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                foreach (MapNodeView node in screen.Nodes)
                {
                    Transform badge = node.transform.Find("Badge");
                    Assert.That(badge.Find(ShellKit.FaceName).GetComponent<Image>().sprite, Is.Not.Null, node.name);
                    RectTransform shadow = (RectTransform)badge.Find(ShellKit.ShadowName);
                    Assert.That(shadow, Is.Not.Null, node.name);
                    Assert.That(Mathf.Abs(shadow.offsetMin.y), Is.LessThanOrEqualTo(8f), node.name);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapLayoutPreservesCardTitleLivesAndUpcomingLabelSpace()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());

                Transform sprint = root.transform.Find(
                    "S5MapPresentation/Content/SelectionGrid/SprintNode");
                var titleBox = (RectTransform)sprint.Find("LabelGroup/TitleContainer");
                Assert.That(-titleBox.offsetMin.y, Is.GreaterThanOrEqualTo(40f));

                LayoutElement livesLabel = root.transform.Find(
                        "S5MapPresentation/Content/Header/LivesPanel/LivesLabelContainer")
                    .GetComponent<LayoutElement>();
                Assert.That(livesLabel.preferredHeight, Is.GreaterThanOrEqualTo(36f));
                Assert.That(livesLabel.preferredWidth, Is.GreaterThanOrEqualTo(150f));
                Assert.That(livesLabel.GetComponentInChildren<TMP_Text>().enableWordWrapping, Is.False);

                Transform lessons = root.transform.Find("S5MapPresentation/Content/JourneyLessons");
                Assert.That(lessons, Is.Not.Null);
                Assert.That(lessons.Find("Lesson1").GetComponent<LayoutElement>().preferredHeight,
                    Is.GreaterThanOrEqualTo(30f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LockPictogramsAppearOnlyOnLockedStops()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                Assert.That(screen.Nodes[0].transform.Find("Badge/LockIcon").gameObject.activeSelf, Is.False);
                Assert.That(screen.Nodes[1].transform.Find("Badge/LockIcon").gameObject.activeSelf, Is.True);
                Assert.That(screen.Nodes[2].transform.Find("Badge/LockIcon").gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapSecondaryTextRemainsReadableOnCompactLandscapeScreens()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());

                Transform sprint = root.transform.Find(
                    "S5MapPresentation/Content/SelectionGrid/SprintNode");
                Assert.That(sprint.Find("LabelGroup/MetaPill/StatusContainer/Status").GetComponent<TMP_Text>().fontSize,
                    Is.GreaterThanOrEqualTo(24));
                Assert.That(sprint.Find("LabelGroup/TitleContainer/Title").GetComponent<TMP_Text>().fontSize,
                    Is.GreaterThanOrEqualTo(36));

                foreach (TMP_Text line in root.transform.Find("S5MapPresentation/Content/JourneyLessons")
                             .GetComponentsInChildren<TMP_Text>(true))
                    Assert.That(line.fontSize, Is.GreaterThanOrEqualTo(24), line.name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LockedLessonCardsStayReadable()
        {
            var root = new GameObject("locked-cards", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                Transform panel = screen.LessonList.transform;
                Transform locked = panel.Find("Lesson3");
                Assert.That(ContrastRatio(locked.Find("StageTitle").GetComponent<TMP_Text>().color,
                    panel.GetComponent<Image>().color), Is.GreaterThanOrEqualTo(4.5f));
                locked.GetComponent<Button>().onClick.Invoke();
                Color detail = panel.Find("DetailCard").GetComponent<Image>().color;
                Assert.That(ContrastRatio(panel.Find("DetailCard/Status").GetComponent<TMP_Text>().color, detail),
                    Is.GreaterThanOrEqualTo(4.5f));
                Assert.That(ContrastRatio(panel.Find("DetailCard/Objective").GetComponent<TMP_Text>().color, detail),
                    Is.GreaterThanOrEqualTo(4.5f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapNodeJourneyStop_ShowsLockStarsAndMarkersFromState()
        {
            var node = new GameObject("Stop", typeof(RectTransform)).AddComponent<MapNodeView>();
            try
            {
                var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image))
                    .GetComponent<Image>();
                badge.transform.SetParent(node.transform, false);
                var stars = new Image[3];
                for (int i = 0; i < 3; i++)
                {
                    stars[i] = new GameObject("Star" + i, typeof(RectTransform), typeof(Image))
                        .GetComponent<Image>();
                    stars[i].transform.SetParent(node.transform, false);
                }
                var lockIcon = new GameObject("LockIcon", typeof(RectTransform));
                var doneMark = new GameObject("DoneMark", typeof(RectTransform));
                var tag = new GameObject("CurrentTag", typeof(RectTransform));
                var ring = new GameObject("SelectionRing", typeof(RectTransform), typeof(Image))
                    .GetComponent<Image>();
                var labelGroup = new GameObject("LabelGroup", typeof(RectTransform)).GetComponent<RectTransform>();
                foreach (GameObject part in new[] { lockIcon, doneMark, tag, ring.gameObject, labelGroup.gameObject })
                    part.transform.SetParent(node.transform, false);
                node.BindJourneyStop(badge, stars, lockIcon, doneMark, tag, ring,
                    badge.rectTransform, labelGroup);
                var status = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI))
                    .GetComponent<TextMeshProUGUI>();
                status.transform.SetParent(node.transform, false);
                node.BindStatusLabel(status);

                node.ConfigureJourneyState(SubjectId.Volleyball, "Bóng chuyền", false, false, null);
                Assert.That(node.IsLocked, Is.True);
                Assert.That(lockIcon.activeSelf, Is.True);
                Assert.That(doneMark.activeSelf, Is.False);
                Assert.That(node.StatusText, Is.EqualTo("CHƯA MỞ KHÓA"));
                Assert.That(stars.Count(star => star.color == ShellTheme.Gold), Is.Zero);

                var record = new SubjectRecord();
                record.Accept(new MinigameResult(true, 0f, Rank.C));
                node.ConfigureJourneyState(SubjectId.Volleyball, "Bóng chuyền", true, true, record);
                Assert.That(node.IsLocked, Is.False);
                Assert.That(node.IsCompleted, Is.True);
                Assert.That(lockIcon.activeSelf, Is.False);
                Assert.That(doneMark.activeSelf, Is.True);
                Assert.That(node.StatusText, Is.EqualTo("HOÀN THÀNH"));
                int filled = stars.Count(star => star.color == ShellTheme.Gold);
                Assert.That(filled, Is.EqualTo(node.Stars), "Filled stars must equal Stars, not always 3.");
                Assert.That(node.Stars, Is.LessThan(3));

                node.SetJourneyMarkers(true, true);
                Assert.That(node.IsCurrent, Is.True);
                Assert.That(tag.activeSelf, Is.True);
                Assert.That(ring.gameObject.activeSelf, Is.True);
                Assert.That(ring.color.a, Is.EqualTo(1f).Within(.001f));
                Assert.That(badge.rectTransform.localScale.x,
                    Is.EqualTo(UITheme.Shared.LessonJourney.stopCurrentScale).Within(.001f));

                node.SetJourneyMarkers(false, false);
                Assert.That(tag.activeSelf, Is.False);
                Assert.That(ring.gameObject.activeSelf, Is.False);
                Assert.That(badge.rectTransform.localScale.x, Is.EqualTo(1f).Within(.001f));
            }
            finally { Object.DestroyImmediate(node.gameObject); }
        }

        [Test]
        public void MapStops_FreshGameShowsOneReadyStopAndTwoLockedStopsEachWithStarsAndStatus()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());

                foreach (MapNodeView node in screen.Nodes)
                {
                    Assert.That(node.transform.Find("Badge/IconGlyph").GetComponent<Image>().sprite, Is.Not.Null, node.name);
                    Assert.That(node.transform.Find("Badge/OrderBadge"), Is.Not.Null, node.name);
                    Assert.That(node.transform.Find("LabelGroup/TitleContainer/Title").GetComponent<TMP_Text>().text,
                        Is.EqualTo(node.DisplayName), node.name);
                    Transform pill = node.transform.Find("LabelGroup/MetaPill");
                    Assert.That(pill, Is.Not.Null, node.name + " must always show stars and status");
                    Assert.That(pill.Find("Stars").childCount, Is.EqualTo(3), node.name);
                    Assert.That(pill.Find("StatusContainer/Status").GetComponent<TMP_Text>().text, Is.Not.Empty, node.name);
                }

                MapNodeView sprint = screen.Nodes[0], volley = screen.Nodes[1], football = screen.Nodes[2];
                Assert.That(sprint.IsLocked, Is.False);
                Assert.That(sprint.StatusText, Is.EqualTo("SẴN SÀNG"));
                Assert.That(sprint.transform.Find("Badge/LockIcon").gameObject.activeSelf, Is.False);
                foreach (MapNodeView locked in new[] { volley, football })
                {
                    Assert.That(locked.IsLocked, Is.True, locked.name);
                    Assert.That(locked.StatusText, Is.EqualTo("CHƯA MỞ KHÓA"), locked.name);
                    Assert.That(locked.transform.Find("Badge/LockIcon").gameObject.activeSelf, Is.True, locked.name);
                    Assert.That(locked.GetComponent<Button>().interactable, Is.False, locked.name);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapStops_UseCircularBadgesAndRuntimeSprites()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                var stars = new System.Collections.Generic.HashSet<Sprite>();
                foreach (MapNodeView node in screen.Nodes)
                {
                    Image badge = node.transform.Find("Badge/" + ShellKit.FaceName).GetComponent<Image>();
                    Assert.That(badge.sprite, Is.SameAs(KMA.UI.Kit.UiKitAssets.Load().Circle), node.name);
                    Assert.That(badge.type, Is.EqualTo(Image.Type.Simple), node.name);
                    Assert.That(node.transform.Find("Badge/" + ShellKit.ShadowName), Is.Not.Null, node.name);
                    foreach (Image star in node.transform.Find("LabelGroup/MetaPill/Stars").GetComponentsInChildren<Image>())
                    {
                        Assert.That(star.sprite, Is.Not.Null, node.name);
                        stars.Add(star.sprite);
                    }
                    Assert.That(((RectTransform)node.transform).sizeDelta,
                        Is.EqualTo(UITheme.Shared.LessonJourney.stopSize), node.name);
                }
                Assert.That(stars, Has.Count.EqualTo(1), "All stars share one runtime sprite.");
                Assert.That(screen.Nodes[3].transform.Find("Badge/FinishFlag"), Is.Not.Null);
                Assert.That(screen.Nodes[0].transform.Find("Badge/FinishFlag"), Is.Null);
                Assert.That(screen.Nodes[1].transform.Find("Badge/FinishFlag"), Is.Null);
                Assert.That(screen.Nodes[2].transform.Find("Badge/FinishFlag"), Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapStops_PassedSubjectShowsDoneMarkAndRankStars()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, CompleteSprintJourney());
                MapNodeView sprint = screen.Nodes.Single(node => node.SubjectId == SubjectId.Sprint);
                Assert.That(sprint.IsCompleted, Is.True);
                Assert.That(sprint.StatusText, Is.EqualTo("HOÀN THÀNH"));
                Assert.That(sprint.transform.Find("Badge/DoneMark").gameObject.activeSelf, Is.True);
                int filled = sprint.transform.Find("LabelGroup/MetaPill/Stars").GetComponentsInChildren<Image>()
                    .Count(star => star.color == ShellTheme.Gold);
                Assert.That(filled, Is.EqualTo(sprint.Stars));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapMarkers_FreshGameMarksSprintCurrentAndSelected()
        {
            var root = new GameObject("markers", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                Assert.That(screen.Nodes.Select(node => node.IsCurrent), Is.EqualTo(new[] { true, false, false, false }));
                Assert.That(screen.Nodes.Select(node => node.IsSelected), Is.EqualTo(new[] { true, false, false, false }));
                Assert.That(screen.Nodes[0].transform.Find("CurrentTag").gameObject.activeSelf, Is.True);
                Assert.That(screen.Nodes[1].transform.Find("CurrentTag").gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapMarkers_AfterSprintCourseTheCurrentStopMovesToVolleyballAndSelectionFollowsClicks()
        {
            var root = new GameObject("markers-2", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, CompleteSprintJourney());
                MapNodeView sprint = screen.Nodes[0], volley = screen.Nodes[1], football = screen.Nodes[2];
                Assert.That(volley.IsCurrent, Is.True);
                Assert.That(sprint.IsCurrent, Is.False);
                Assert.That(football.IsLocked, Is.True);

                screen.SelectSubject(SubjectId.Sprint);
                Assert.That(sprint.IsSelected, Is.True);
                Assert.That(volley.IsSelected, Is.False);
                Assert.That(volley.IsCurrent, Is.True, "Selecting an older stop must not move the current marker.");

                screen.SelectSubject(SubjectId.Football);   // locked: selection must not change
                Assert.That(football.IsSelected, Is.False);
                Assert.That(sprint.IsSelected, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapMarkers_CompletedCourseHasNoCurrentStop()
        {
            var root = new GameObject("markers-3", typeof(RectTransform));
            try
            {
                var session = new GameSession();
                foreach (ChallengeDefinition definition in session.Journey.Catalog.Ordered)
                    Complete(session, definition.Id);
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, session);
                Assert.That(screen.Nodes.Any(node => node.IsCurrent), Is.False);
                Assert.That(root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "CurrentTag").All(t => !t.gameObject.activeSelf), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LessonPanelHeadingIsOnlyTheSubjectName()
        {
            var root = new GameObject("heading", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                Assert.That(screen.LessonList.transform.Find("CourseTitle").GetComponent<TMP_Text>().text,
                    Is.EqualTo("Chạy nước rút"));
                Assert.That(screen.LessonList.SelectedSubject, Is.EqualTo(SubjectId.Sprint));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapNodeJourneyStop_KeepsLabelOffsetBelowTheBadge()
        {
            var node = new GameObject("Stop", typeof(RectTransform)).AddComponent<MapNodeView>();
            try
            {
                var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                badge.transform.SetParent(node.transform, false);
                var labelGroup = new GameObject("LabelGroup", typeof(RectTransform)).GetComponent<RectTransform>();
                labelGroup.SetParent(node.transform, false);
                labelGroup.anchoredPosition = new Vector2(0f, -270f);
                node.BindJourneyStop(badge, new Image[0], null, null, null, null, badge.rectTransform, labelGroup);

                node.SetJourneyMarkers(true, true);
                Assert.That(labelGroup.anchoredPosition.y, Is.LessThan(-270f),
                    "The current badge is larger, so its labels move further down.");
                node.SetJourneyMarkers(false, false);
                Assert.That(labelGroup.anchoredPosition.y, Is.EqualTo(-270f).Within(.001f),
                    "Markers must offset the authored label position, never replace it.");
            }
            finally { Object.DestroyImmediate(node.gameObject); }
        }

        [Test]
        public void MapStops_StatusTextAndIconsAreReadableOnTheirSurfaces()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                // Sprint completed, Volleyball ready, Football locked: every status colour is exercised.
                MapPresentationBuilder.Build(screen, CompleteSprintJourney());
                foreach (MapNodeView node in screen.Nodes)
                {
                    Color pill = ShellTheme.Ink;
                    Color status = node.transform.Find("LabelGroup/MetaPill/StatusContainer/Status")
                        .GetComponent<TMP_Text>().color;
                    Assert.That(ContrastRatio(status, pill), Is.GreaterThanOrEqualTo(4.5f),
                        node.name + " status text on the navy pill");
                    Color badge = node.transform.Find("Badge/" + ShellKit.FaceName).GetComponent<Image>().color;
                    Color glyph = node.transform.Find("Badge/IconGlyph").GetComponent<Image>().color;
                    Assert.That(ContrastRatio(glyph, badge), Is.GreaterThanOrEqualTo(2f),
                        node.name + " sport icon must stand out from its badge");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapStops_StarsAndStatusDoNotShareSpaceAndTheFlagClearsTheCurrentTag()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                foreach (MapNodeView node in screen.Nodes)
                {
                    var stars = (RectTransform)node.transform.Find("LabelGroup/MetaPill/Stars");
                    var status = (RectTransform)node.transform.Find("LabelGroup/MetaPill/StatusContainer");
                    float pillWidth = ((RectTransform)node.transform.Find("LabelGroup/MetaPill")).sizeDelta.x;
                    float starsRight = stars.anchorMin.x * pillWidth + stars.offsetMin.x + 3 * 26f + 2 * 2f;
                    float statusLeft = status.anchorMin.x * pillWidth + status.offsetMin.x;
                    Assert.That(starsRight, Is.LessThanOrEqualTo(statusLeft), node.name);
                }
                var flag = (RectTransform)screen.Nodes[3].transform.Find("Badge/FinishFlag");
                Assert.That(flag.anchorMin.y, Is.LessThanOrEqualTo(.8f),
                    "The flag must sit below the badge top so the current-stop tag never covers it.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapStops_StatusTextFitsBesideTheStars()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());   // Volleyball/Football show the longest status
                foreach (MapNodeView node in screen.Nodes)
                {
                    TMP_Text status = node.transform.Find("LabelGroup/MetaPill/StatusContainer/Status")
                        .GetComponent<TMP_Text>();
                    status.ForceMeshUpdate(true);
                    Assert.That(status.preferredWidth, Is.LessThanOrEqualTo(status.rectTransform.rect.width),
                        node.name + " status text must not spill over the stars.");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapStops_GoldRingIsVisibleAroundTheEnlargedCurrentBadge()
        {
            var root = new GameObject("ring", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                MapNodeView current = screen.Nodes[0];
                Rect badge = WorldRect((RectTransform)current.transform.Find("Badge"));
                Rect ring = WorldRect((RectTransform)current.transform.Find("SelectionRing"));
                Assert.That(current.IsCurrent, Is.True);
                Assert.That(ring.width, Is.GreaterThan(badge.width + 10f),
                    "The ring must stay visible outside the scaled current badge.");
                Assert.That(ring.Contains(badge.min) && ring.Contains(badge.max), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void JourneyRoad_RebuildsAStalePathTrackInsteadOfThrowing()
        {
            var root = new GameObject("stale", typeof(RectTransform));
            var gridObject = new GameObject("Grid", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                var grid = (RectTransform)gridObject.transform;
                grid.sizeDelta = new Vector2(1920f, 565f);
                // The pre-journey scene had a childless PathTrack1 (a single Image).
                var stale = new GameObject("PathTrack1", typeof(RectTransform), typeof(Image));
                stale.transform.SetParent(grid, false);
                var layout = gridObject.AddComponent<MapJourneyPathLayout>();
                Assert.DoesNotThrow(() => layout.Configure(screen.Nodes));
                Assert.That(grid.Find("PathTrack1").Cast<Transform>().Count(c => c.name.StartsWith("Outline")),
                    Is.EqualTo(36));
                Assert.That(grid.Find("PathTrack2"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(gridObject); }
        }

        static (MapScreen screen, RectTransform grid) BuildMapAt(GameObject root, Vector2 size, GameSession session)
        {
            ((RectTransform)root.transform).sizeDelta = size;
            var screen = root.AddComponent<MapScreen>();
            MapPresentationBuilder.Build(screen, session);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
            var grid = (RectTransform)root.transform.Find("S5MapPresentation/Content/SelectionGrid");
            grid.GetComponent<MapJourneyPathLayout>().Refresh();
            return (screen, grid);
        }

        static UITheme.LessonJourneyStyle Style() => UITheme.Shared.LessonJourney;

        static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0], corners[2] - corners[0]);
        }

        [Test]
        public void JourneyRoad_PassesThroughBadgeCentresAndIsBuiltOnce([Values(1280f, 1920f, 2400f)] float width)
        {
            var root = new GameObject("road", typeof(RectTransform));
            try
            {
                var (screen, grid) = BuildMapAt(root, new Vector2(width, 1080f), new GameSession());
                MapPresentationBuilder.Build(screen, new GameSession());   // second build must not duplicate
                var layout = grid.GetComponent<MapJourneyPathLayout>();
                Assert.That(grid.Cast<Transform>().Count(child => child.name.StartsWith("PathTrack")), Is.EqualTo(3));
                Assert.That(grid.Find("PathTrack1").childCount, Is.GreaterThan(30));
                Vector2 first = layout.StopCenter(0), second = layout.StopCenter(1), third = layout.StopCenter(2),
                    fourth = layout.StopCenter(3);
                Assert.That(second.y, Is.GreaterThan(first.y), "Even stops sit higher than odd stops.");
                Assert.That(third.y, Is.EqualTo(first.y).Within(.5f));
                Assert.That(fourth.y, Is.EqualTo(second.y).Within(.5f));
                Assert.That(first.x, Is.LessThan(second.x));
                Assert.That(second.x, Is.LessThan(third.x));
                Assert.That(third.x, Is.LessThan(fourth.x));
                Image firstOutline = grid.Find("PathTrack1/Outline1").GetComponent<Image>();
                Assert.That(Vector2.Distance(firstOutline.rectTransform.anchoredPosition, first),
                    Is.LessThan(Style().stopBadgeSize), "Road starts under the first badge.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void JourneyStops_StayInsideMapZoneWithTheLessonsClosed(
            [Values(1280f, 1440f, 1728f, 1920f, 2400f)] float width)
        {
            var root = new GameObject("zone", typeof(RectTransform));
            try
            {
                var (screen, grid) = BuildMapAt(root, new Vector2(width, 1080f), new GameSession());
                Assert.That(screen.LessonList.IsOpen, Is.False, "The map opens on the roadmap alone.");
                Rect gridRect = WorldRect(grid);
                Assert.That(gridRect.height, Is.GreaterThan(1080f * .65f), "The roadmap leaves room for the header and course summary.");
                foreach (MapNodeView node in screen.Nodes)
                {
                    Rect stop = WorldRect((RectTransform)node.transform);
                    Assert.That(stop.xMin, Is.GreaterThanOrEqualTo(gridRect.xMin - 1f), node.name);
                    Assert.That(stop.xMax, Is.LessThanOrEqualTo(gridRect.xMax + 1f), node.name);
                    Assert.That(stop.yMin, Is.GreaterThanOrEqualTo(gridRect.yMin - 1f), node.name);
                    Assert.That(stop.yMax, Is.LessThanOrEqualTo(gridRect.yMax + 1f), node.name);
                }
                if (width >= 1728f)
                    Assert.That(screen.Nodes[0].transform.localScale.x, Is.GreaterThan(1f),
                        "Stops grow when the map has room, so they stay legible on phones.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(1280f, 720f)]
        [TestCase(1920f, 1080f)]
        [TestCase(2340f, 1080f)]
        [TestCase(2400f, 1080f)]
        public void LessonPopupAndDetailCardStayInsideTheMapContentAcrossPhoneRatios(float width, float height)
        {
            var root = new GameObject("popup-geometry", typeof(RectTransform));
            try
            {
                var (screen, _) = BuildMapAt(root, new Vector2(width, height), new GameSession());
                RectTransform panel = (RectTransform)screen.LessonList.transform;
                RectTransform content = (RectTransform)panel.parent;
                RectTransform detail = (RectTransform)panel.Find("DetailCard");
                RectTransform close = (RectTransform)panel.Find("CloseButton");
                Rect panelWorld = WorldRect(panel);
                Rect contentWorld = WorldRect(content);
                Rect detailWorld = WorldRect(detail);
                Assert.That(panelWorld.xMin, Is.GreaterThanOrEqualTo(contentWorld.xMin - 1f));
                Assert.That(panelWorld.xMax, Is.LessThanOrEqualTo(contentWorld.xMax + 1f));
                Assert.That(panelWorld.yMin, Is.GreaterThanOrEqualTo(contentWorld.yMin - 1f));
                Assert.That(panelWorld.yMax, Is.LessThanOrEqualTo(contentWorld.yMax + 1f));
                Assert.That(detailWorld.xMin, Is.GreaterThan(panelWorld.xMin));
                Assert.That(detailWorld.xMax, Is.LessThan(panelWorld.xMax));
                Assert.That(detailWorld.yMin, Is.GreaterThan(panelWorld.yMin));
                Assert.That(close.rect.width, Is.GreaterThan(0f));
                Assert.That(close.rect.height, Is.GreaterThan(0f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TappingAnUnlockedStopOpensTheLessonPopupAndItCloses()
        {
            var root = new GameObject("popup", typeof(RectTransform));
            try
            {
                var (screen, _) = BuildMapAt(root, new Vector2(1920f, 1080f), new GameSession());
                JourneyLessonList lessons = screen.LessonList;
                Transform scrim = lessons.transform.parent.Find("LessonScrim");
                Assert.That(scrim, Is.Not.Null);
                Assert.That(scrim.gameObject.activeSelf, Is.False);

                screen.Nodes[1].GetComponent<Button>().onClick.Invoke();   // Volleyball is still locked
                Assert.That(lessons.IsOpen, Is.False, "A locked stop does not open the lessons.");

                screen.Nodes[0].GetComponent<Button>().onClick.Invoke();
                Assert.That(lessons.IsOpen, Is.True);
                Assert.That(scrim.gameObject.activeSelf, Is.True);
                Assert.That(lessons.SelectedSubject, Is.EqualTo(SubjectId.Sprint));
                Assert.That(lessons.transform.GetSiblingIndex(), Is.EqualTo(lessons.transform.parent.childCount - 1),
                    "The popup draws above the map and the course summary.");

                lessons.transform.Find("CloseButton").GetComponent<Button>().onClick.Invoke();
                Assert.That(lessons.IsOpen, Is.False);
                Assert.That(scrim.gameObject.activeSelf, Is.False);

                screen.SelectSubject(SubjectId.Sprint);
                scrim.GetComponent<Button>().onClick.Invoke();
                Assert.That(lessons.IsOpen, Is.False, "Tapping outside the popup closes it.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void JourneyRoad_FillsOnlyThePassedSegments()
        {
            var root = new GameObject("progress", typeof(RectTransform));
            var doneRoot = new GameObject("progress-done", typeof(RectTransform));
            try
            {
                var (_, grid) = BuildMapAt(root, new Vector2(1920f, 1080f), new GameSession());
                Assert.That(grid.Find("PathTrack1/Fill1").gameObject.activeSelf, Is.False,
                    "Fresh game: no gold segment, and no stray dot at the start.");
                Assert.That(grid.Find("PathTrack1/Dot1").gameObject.activeSelf, Is.True);

                var (_, doneGrid) = BuildMapAt(doneRoot, new Vector2(1920f, 1080f), CompleteSprintJourney());
                Assert.That(doneGrid.Find("PathTrack1/Fill1").gameObject.activeSelf, Is.True);
                Assert.That(doneGrid.Find("PathTrack1/Dot1").gameObject.activeSelf, Is.False);
                Assert.That(doneGrid.Find("PathTrack2/Fill1").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(doneRoot);
            }
        }

        [Test]
        public void MapNodeAvailabilityTransitionsKeepInteractionAndVisualStateSynchronized()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                MapNodeView node = screen.Nodes.Single(candidate =>
                    candidate.SubjectId == SubjectId.Sprint);
                Button button = node.GetComponent<Button>();
                ShellPressFeedback feedback = node.GetComponent<ShellPressFeedback>();
                TMP_Text status = node.transform.Find("LabelGroup/MetaPill/StatusContainer/Status").GetComponent<TMP_Text>();

                node.SetAvailability(false, "TẠM KHÓA");

                Assert.That(button.interactable, Is.False);
                feedback.Press();
                Assert.That(feedback.IsPressed, Is.False, "a locked stop does not press");
                Assert.That(status.text, Is.EqualTo("TẠM KHÓA"));

                node.SetAvailability(true, "TẠM KHÓA");

                Assert.That(button.interactable, Is.True);
                Assert.That(feedback.enabled, Is.True);
                Assert.That(status.text, Is.EqualTo("SẴN SÀNG"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MapPaletteUsesBlueForSprintAndGoldForCompletion()
        {
            var root = new GameObject("map", typeof(RectTransform));
            try
            {
                var session = CompleteSprintJourney();
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, session);
                MapNodeView sprint = screen.Nodes.Single(node => node.SubjectId == SubjectId.Sprint);
                Color sprintAccent = sprint.transform.Find("Badge/" + ShellKit.FaceName).GetComponent<Image>().color;
                Assert.That(sprintAccent.b, Is.GreaterThan(sprintAccent.r),
                    "Subject accents should not use warning red.");
                Assert.That(sprint.transform.Find("Badge/DoneMark").GetComponent<Image>().color,
                    Is.EqualTo(ShellTheme.Gold));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapAndLessonCardsUseImportedIconSprites()
        {
            var root = new GameObject("icon-map", typeof(RectTransform));
            try
            {
                var screen = root.AddComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                foreach (MapNodeView node in screen.Nodes)
                {
                    Sprite sprite = node.transform.Find("Badge/IconGlyph").GetComponent<Image>().sprite;
                    Assert.That(sprite, Is.EqualTo(Resources.Load<Sprite>("Icons/SportIcon_" + node.SubjectId)),
                        node.name);
                }

                string[] stages = { "StageIcon_Learn", "StageIcon_Practice", "StageIcon_Exam" };
                for (int i = 0; i < stages.Length; i++)
                {
                    Sprite expected = Resources.Load<Sprite>("Icons/" + stages[i]);
                    Assert.That(expected, Is.Not.Null, stages[i]);
                    Assert.That(screen.LessonList.transform.Find($"Lesson{i + 1}/StageIcon/Glyph")
                        .GetComponent<Image>().sprite, Is.EqualTo(expected), stages[i]);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MapBackdropFillsTheCanvasWhileMapControlsStayUnderTheSafeArea()
        {
            var canvasObject = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            var safeArea = new GameObject("SafeAreaRoot", typeof(RectTransform));
            safeArea.transform.SetParent(canvasObject.transform, false);
            var safeRect = safeArea.GetComponent<RectTransform>();
            safeRect.anchorMin = new Vector2(.08f, .06f);
            safeRect.anchorMax = new Vector2(.92f, .94f);
            var mapObject = new GameObject("MapScreen", typeof(RectTransform), typeof(MapScreen));
            mapObject.transform.SetParent(safeArea.transform, false);
            try
            {
                var screen = mapObject.GetComponent<MapScreen>();
                MapPresentationBuilder.Build(screen, new GameSession());
                Transform mapRoot = mapObject.transform.Find("S5MapPresentation");
                Transform backdrop = canvasObject.transform.Find("S5MapBackdrop");
                Assert.That(backdrop, Is.Not.Null, "The backdrop belongs to the full canvas, outside SafeAreaRoot.");
                Assert.That(backdrop.parent, Is.EqualTo(canvasObject.transform));
                Assert.That(((RectTransform)backdrop).anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(((RectTransform)backdrop).anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(mapRoot, Is.Not.Null);
                Assert.That(mapRoot.IsChildOf(safeArea.transform), Is.True,
                    "Map controls remain inside the fitted safe area.");
                Assert.That(mapRoot.GetComponentInChildren<Button>(true), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        static GameSession CompleteSprintJourney()
        {
            var session = new GameSession();
            Complete(session, "sprint_learn");
            Complete(session, "sprint_practice");
            Complete(session, "sprint_exam");
            return session;
        }

        static void Complete(GameSession session, string id)
        {
            ChallengeDefinition definition = session.Journey.Catalog.Get(id);
            Assert.That(session.TryStartChallenge(id, ChallengeAttemptMode.Journey, definition.Difficulty,
                out ChallengeAttemptContext context), Is.True);
            session.SubmitChallengeResult(new ChallengeAttemptResult(context, true,
                new ChallengeMetrics(completedTargets: definition.TargetCount),
                ChallengeDefinition.IsScored(definition.Kind) ? new MinigameResult(true, 8f, Rank.A) : null));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
