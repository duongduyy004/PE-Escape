using KMA.UI.Kit;
using TMPro;
using KMA.Gameplay.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay
{
    public static class SprintFestivalPresentation
    {
        const string SuccessTitle = "HOÀN THÀNH!";
        const string FailureTitle = "THẤT BẠI";
        // The world-space BẠN plate is a sliced kit sprite drawn at this scale.
        const float MarkerSpriteScale = .25f;

        public static void Build()
        {
            if (GameObject.Find("SprintBroadcastChrome") != null)
                return;

            Canvas canvas = GameObject.Find("S2_HUD_Minigame")?.GetComponent<Canvas>()
                ?? Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            Transform safeArea = canvas.transform.Find("SafeAreaRoot");
            if (safeArea == null)
                return;
            PrepareSafeArea(safeArea);

            GameObject oldChrome = GameObject.Find("SprintFestivalChrome");
            if (oldChrome != null)
                Object.Destroy(oldChrome);

            DisableSharedMetrics(safeArea);

            RectTransform root = UiKit.Rect(safeArea, "SprintBroadcastChrome");
            UiKit.Stretch(root);
            root.SetAsLastSibling();

            RectTransform safeRect = (RectTransform)safeArea;
            Rect safe = SafeRect(safeRect);

            var chromeLayout = root.gameObject.AddComponent<SprintChromeLayout>();
            chromeLayout.Bind(safeRect);

            // Progress rail: the kit bar re-derives its corner radius whenever its height changes.
            KitBar rail = UiKit.Bar(root, "ProgressRail", pip: true);
            rail.Fill.name = "RailFill";
            rail.Pip.name = "PlayerPip";
            var railRect = (RectTransform)rail.transform;
            ApplyRect(railRect, safe, SprintUiLayout.ProgressRailRect(safe));
            chromeLayout.Register(railRect, SprintUiLayout.ProgressRailRect);
            RectTransform pip = rail.Pip.rectTransform;
            pip.sizeDelta = new Vector2(safe.height * .014f, 0f);
            // The pip's width comes from safe.height; if the Canvas has not laid out yet
            // (safe.height == 0) it would stay invisible, so re-derive it when the safe area changes.
            chromeLayout.Register(liveSafe => pip.sizeDelta = new Vector2(liveSafe.height * .014f, pip.sizeDelta.y));

            // Scoreboard
            Image scoreboard = UiKit.Panel(root, "Scoreboard");
            ApplyRect(scoreboard.rectTransform, safe, SprintUiLayout.ScoreboardRect(safe));
            chromeLayout.Register(scoreboard.rectTransform, SprintUiLayout.ScoreboardRect);

            TMP_Text distance = Metric(scoreboard.transform, "Distance", MinigameUiTheme.Title,
                MinigameUiTheme.TextPrimary, new Vector2(.05f, .46f), new Vector2(.62f, .92f));
            distance.alignment = TextAlignmentOptions.Left;
            distance.text = VietText.Fix($"0 / {SprintRules.RaceDistance:0} m");

            Image rankBadge = UiKit.Shape(scoreboard.transform, "RankBadge", MinigameUiTheme.RadiusPanel,
                MinigameUiTheme.WithAlpha(MinigameUiTheme.Accent, .22f));
            UiKit.Anchor(rankBadge.rectTransform, new Vector2(.66f, .46f), new Vector2(.95f, .92f));
            TMP_Text rank = UiKit.Label(rankBadge.transform, "RankLabel", "VỊ TRÍ 1", MinigameUiTheme.Headline,
                MinigameUiTheme.Accent);
            UiKit.Stretch(rank.rectTransform, new Vector2(MinigameUiTheme.SpaceSm, 0f), new Vector2(-MinigameUiTheme.SpaceSm, 0f));
            // "VỊ TRÍ 4" is much wider than the old "4th": shrink rather than overflow the chip.
            UiKit.FitLabel(rank, MinigameUiTheme.Headline);
            rank.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_Text combo = Metric(scoreboard.transform, "Combo", MinigameUiTheme.Body,
                MinigameUiTheme.Energy, new Vector2(.05f, .10f), new Vector2(.62f, .42f));
            combo.alignment = TextAlignmentOptions.Left;
            combo.text = VietText.Fix("CHUỖI ×0");

            TMP_Text timer = Metric(scoreboard.transform, "RaceTimer", MinigameUiTheme.Headline,
                MinigameUiTheme.TextPrimary, new Vector2(.66f, .06f), new Vector2(.95f, .42f));
            timer.alignment = TextAlignmentOptions.Center;
            timer.text = VietText.Fix($"{SprintRules.DefaultTimeLimit:0}s");

            // Mode chip
            ChipHandle mode = UiKit.Chip(root, "ModeLabel", $"CHẠY NƯỚC RÚT · {SprintRules.RaceDistance:0}M");
            ApplyRect(mode.Background.rectTransform, safe, SprintUiLayout.ModeChipRect(safe));
            chromeLayout.Register(mode.Background.rectTransform, SprintUiLayout.ModeChipRect);

            EnsurePause(root, safe, chromeLayout);
            EnsureStartPresentation(root, safe);
            EnsureControls(root, safe);
            EnsurePlayerIdentity(root);
            EnsureFinishLine(root);
            EnsureResultPresentation();
        }

        public static void ConfigureChallenge(ChallengeDefinition definition)
        {
            if (definition == null) return;
            Transform chrome = GameObject.Find("SprintBroadcastChrome")?.transform;
            if (chrome == null) return;
            TMP_Text mode = chrome.Find("ModeLabel/Label")?.GetComponent<TMP_Text>();
            TMP_Text instruction = chrome.Find("StartPresentation/InstructionPlate/InstructionLabel")
                ?.GetComponent<TMP_Text>();
            bool learn = definition.Kind == ChallengeKind.Learn;
            if (mode != null)
            {
                string text = learn ? $"SPRINT · HỌC {definition.TimeLimit:0}S · CHUỖI {definition.TargetCount}"
                    : definition.Kind == ChallengeKind.Practice
                        ? $"SPRINT · LUYỆN {definition.Distance:0}M / {definition.TimeLimit:0}S"
                        : $"SPRINT · THI {definition.Distance:0}M / {definition.TimeLimit:0}S";
                mode.text = VietText.Fix(text);
            }
            if (instruction != null)
                instruction.text = VietText.Fix(learn
                    ? $"TẬP LUÂN PHIÊN HAI CHÂN TRONG {definition.TimeLimit:0} GIÂY"
                    : $"CHẠY {definition.Distance:0} M TRONG {definition.TimeLimit:0} GIÂY");
            var finishLine = chrome.GetComponent<SprintFinishLinePresenter>();
            if (finishLine != null)
            {
                finishLine.enabled = !learn;
                if (learn && finishLine.FinishRoot != null) finishLine.FinishRoot.SetActive(false);
            }
        }

        /// Positions a RectTransform from an absolute rect expressed in the safe area's own space.
        static void ApplyRect(RectTransform rect, Rect safe, Rect target)
        {
            rect.anchorMin = new Vector2(
                Mathf.InverseLerp(safe.xMin, safe.xMax, target.xMin),
                Mathf.InverseLerp(safe.yMin, safe.yMax, target.yMin));
            rect.anchorMax = new Vector2(
                Mathf.InverseLerp(safe.xMin, safe.xMax, target.xMax),
                Mathf.InverseLerp(safe.yMin, safe.yMax, target.yMax));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// Reachable from SprintChromeLayout, which lives outside this static class.
        internal static void ApplyRectPublic(RectTransform rect, Rect safe, Rect target) =>
            ApplyRect(rect, safe, target);

        static Rect SafeRect(RectTransform safeArea) =>
            new Rect(0f, 0f, safeArea.rect.width, safeArea.rect.height);

        /// The finish line is drawn on the track in world space, beside the runners, so where it rests
        /// is exactly where they finish whatever the screen aspect.
        static void EnsureFinishLine(RectTransform root)
        {
            const string name = "SprintFinishLine";
            const int squareCount = 10;
            const int sortingOrder = 5;   // over the track backdrop, under the runners (11+)

            GameObject finish = GameObject.Find(name);
            if (finish == null)
            {
                finish = new GameObject(name);
                var texture = new Texture2D(1, squareCount) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int i = 0; i < squareCount; i++)
                    texture.SetPixel(0, i, i % 2 == 0 ? Color.black : Color.white);
                texture.Apply();

                // One pixel per unit: the sprite is 1 x squareCount units, then scaled onto the lanes.
                var renderer = finish.AddComponent<SpriteRenderer>();
                renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, squareCount), new Vector2(.5f, 0f), 1f);
                renderer.sortingOrder = sortingOrder;
            }

            float height = SprintTrackLayout.FinishTopY - SprintTrackLayout.FinishBottomY;
            finish.transform.position = new Vector3(SprintTrackLayout.FinishX, SprintTrackLayout.FinishBottomY, 0f);
            finish.transform.localScale = new Vector3(SprintTrackLayout.FinishWidth, height / squareCount, 1f);

            var presenter = root.GetComponent<SprintFinishLinePresenter>()
                ?? root.gameObject.AddComponent<SprintFinishLinePresenter>();
            presenter.Configure(Object.FindFirstObjectByType<SprintController>(), finish);
        }

        static void EnsureResultPresentation()
        {
            KMA.Gameplay.UI.ResultPanel panel =
                Object.FindFirstObjectByType<KMA.Gameplay.UI.ResultPanel>(FindObjectsInactive.Include);
            if (panel != null)
                panel.SetTitles(SuccessTitle, FailureTitle);
        }

        static void EnsureStartPresentation(RectTransform parent, Rect safe)
        {
            RectTransform root = UiKit.Rect(parent, "StartPresentation");
            UiKit.Stretch(root);
            root.SetAsLastSibling();

            TMP_Text countdown = UiKit.Countdown(root, "CountdownLabel");
            if (safe.width > 0f && safe.height > 0f)
                ApplyRect(countdown.rectTransform, safe, SprintUiLayout.CountdownRect(safe));

            Image plate = UiKit.Panel(root, "InstructionPlate", MinigameUiTheme.RadiusPanel, MinigameUiTheme.SurfaceSoft);
            RectTransform instructionRoot = plate.rectTransform;
            if (safe.width > 0f && safe.height > 0f)
                ApplyRect(instructionRoot, safe, SprintUiLayout.InstructionRect(safe));

            TMP_Text instruction = UiKit.Label(instructionRoot, "InstructionLabel",
                SprintStartPresentation.InstructionCopy, MinigameUiTheme.BodyLarge, MinigameUiTheme.TextPrimary);
            UiKit.Stretch(instruction.rectTransform, new Vector2(MinigameUiTheme.SpaceMd, MinigameUiTheme.SpaceXs),
                new Vector2(-MinigameUiTheme.SpaceMd, -MinigameUiTheme.SpaceXs));

            SprintStartPresentation presenter = Object.FindFirstObjectByType<SprintStartPresentation>();
            if (presenter == null)
                presenter = root.gameObject.AddComponent<SprintStartPresentation>();
            presenter.Configure(countdown.gameObject, countdown, instructionRoot.gameObject, instruction);
            presenter.Bind(Object.FindFirstObjectByType<SprintController>());

            var chromeLayout = parent.GetComponent<SprintChromeLayout>();
            if (chromeLayout != null)
            {
                chromeLayout.Register(countdown.rectTransform, SprintUiLayout.CountdownRect);
                chromeLayout.Register(instructionRoot, SprintUiLayout.InstructionRect);
            }
        }

        static void PrepareSafeArea(Transform safeArea)
        {
            safeArea.gameObject.SetActive(true);

            // SafeAreaRoot is the only rect in the HUD that stretches (0,0)-(1,1), so it is the only
            // one where a safe-area offset reads as an inset rather than a resize. It owns the inset;
            // the Canvas root must not carry a fitter at all.
            var fitter = safeArea.GetComponent<KMA.Gameplay.UI.SafeAreaFitter>()
                ?? safeArea.gameObject.AddComponent<KMA.Gameplay.UI.SafeAreaFitter>();
            fitter.enabled = true;
            fitter.Apply(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }

        static void EnsureControls(RectTransform root, Rect safe)
        {
            KMA.Input.ScreenTapArea leftTap = FindTapArea("LeftTap");
            KMA.Input.ScreenTapArea rightTap = FindTapArea("RightTap");
            if (leftTap == null || rightTap == null)
                return;

            ControlPlateHandle left = BuildControl(leftTap, safe, true);
            ControlPlateHandle right = BuildControl(rightTap, safe, false);

            var presenter = root.GetComponent<SprintControlPresenter>()
                ?? root.gameObject.AddComponent<SprintControlPresenter>();
            presenter.Configure(Object.FindFirstObjectByType<SprintController>(),
                left.Visual, right.Visual, left.Background, right.Background, left.Border, right.Border);
            presenter.BindPressFeedback(leftTap, rightTap);

            var chromeLayout = root.GetComponent<SprintChromeLayout>();
            if (chromeLayout != null)
            {
                chromeLayout.Register(leftTap.GetComponent<RectTransform>(), safeRect => SprintUiLayout.ControlRect(safeRect, true));
                chromeLayout.Register(rightTap.GetComponent<RectTransform>(), safeRect => SprintUiLayout.ControlRect(safeRect, false));
            }
        }

        static ControlPlateHandle BuildControl(KMA.Input.ScreenTapArea tapArea, Rect safe, bool left)
        {
            var tapRect = tapArea.GetComponent<RectTransform>();
            // A degenerate safe rect (batchmode's headless canvas, or the first frame before
            // Canvas layout runs) would collapse InverseLerp to a zero-size anchor pin. Skip and
            // keep the authored tap-area rect; SprintChromeLayout re-applies once safe is valid.
            if (safe.width > 0f && safe.height > 0f)
                ApplyRect(tapRect, safe, SprintUiLayout.ControlRect(safe, left));

            Image tapImage = tapArea.GetComponent<Image>() ?? tapArea.gameObject.AddComponent<Image>();
            tapImage.color = new Color(1f, 1f, 1f, 0f);
            tapImage.raycastTarget = true;

            Transform existing = tapRect.Find("Visual");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            // The hit area still owns all input; the larger plate and shoe icon share its press pulse.
            RectTransform visual = UiKit.Rect(tapRect, "Visual");
            Rect visualRect = SprintUiLayout.ControlVisualRect01(left);
            UiKit.Anchor(visual, new Vector2(visualRect.xMin, visualRect.yMin), new Vector2(visualRect.xMax, visualRect.yMax));
            ControlPlateHandle plate = UiKit.ControlPlate(visual, string.Empty, string.Empty);
            plate.Arrow.gameObject.SetActive(false);
            plate.Label.gameObject.SetActive(false);

            RectTransform iconRoot = UiKit.Rect(visual, "FootIcon");
            UiKit.Anchor(iconRoot, new Vector2(.31f, .16f), new Vector2(.69f, .84f));
            Image icon = iconRoot.gameObject.AddComponent<Image>();
            icon.sprite = left ? SprintFootIcons.Left : SprintFootIcons.Right;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
            // The authored shoe faces right; mirror it so the left control's toe points left.
            if (left && SprintFootIcons.Shoe != null)
                iconRoot.localScale = new Vector3(-1f, 1f, 1f);
            return plate;
        }

        static void DisableSharedMetrics(Transform safeArea)
        {
            Transform canvasRoot = safeArea.parent != null ? safeArea.parent : safeArea;
            string[] obsolete =
            {
                "Timer", "Phase", "Score", "Status", "Progress", "Stamina", "HeartBar", "SprintMetrics"
            };

            for (int i = 0; i < obsolete.Length; i++)
            {
                Transform found = FindDescendant(canvasRoot, obsolete[i]);
                if (found != null)
                    found.gameObject.SetActive(false);
            }
        }

        static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        static TMP_Text Metric(Transform parent, string name, float fontSize, Color color, Vector2 min, Vector2 max)
        {
            TMP_Text text = UiKit.Label(parent, name, string.Empty, fontSize, color);
            UiKit.Anchor(text.rectTransform, min, max);
            return text;
        }

        static void EnsurePlayerIdentity(Transform chrome)
        {
            Transform chromeMarker = chrome.Find("PlayerMarker");
            if (chromeMarker != null)
                Object.Destroy(chromeMarker.gameObject);

            Transform player = GameObject.Find("Player")?.transform;
            if (player == null)
                return;

            Transform presentation = player.GetComponentInChildren<RunnerVisualPresenter>(true)?.transform ?? player;

            Transform stale = presentation.Find("PlayerLabel");
            if (stale != null)
                Object.DestroyImmediate(stale.gameObject);
            Transform existing = presentation.Find("PlayerMarker");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var marker = new GameObject("PlayerMarker").transform;
            marker.SetParent(presentation, false);
            marker.localPosition = new Vector3(SprintPlayerMarkerPlacement.SideOffset,
                SprintPlayerMarkerPlacement.MarkerHeight, 0f);

            Sprite plateSprite = UiKitAssets.Load().RoundRect20;
            var plate = new GameObject("Plate", typeof(SpriteRenderer)).transform;
            plate.SetParent(marker, false);
            plate.localScale = new Vector3(MarkerSpriteScale, MarkerSpriteScale, 1f);
            var plateRenderer = plate.GetComponent<SpriteRenderer>();
            plateRenderer.sprite = plateSprite;
            plateRenderer.drawMode = SpriteDrawMode.Sliced;
            // 1.3 x 0.34 world units: the plate has to cover the BẠN label, not sit behind it.
            plateRenderer.size = new Vector2(1.296f, .342f) / MarkerSpriteScale;
            plateRenderer.color = MinigameUiTheme.WithAlpha(MinigameUiTheme.Surface, MinigameUiTheme.SurfaceOpaque);
            plateRenderer.sortingOrder = 19;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(marker, false);
            var label = labelObject.AddComponent<TextMeshPro>();
            label.text = VietText.Fix("BẠN");
            label.fontSize = 48 * .055f;
            label.rectTransform.sizeDelta = new Vector2(1.296f, .342f);
            label.alignment = TextAlignmentOptions.Center;
            label.color = MinigameUiTheme.Player;
            VietTypography.Apply(label, VietFontRole.Hud);
            var labelRenderer = labelObject.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
                label.sortingOrder = 20;

            var chevron = new GameObject("Chevron", typeof(SpriteRenderer)).transform;
            chevron.SetParent(marker, false);
            chevron.localPosition = new Vector3(-SprintPlayerMarkerPlacement.ChevronOffset, 0f, 0f);
            chevron.localScale = new Vector3(.22f, .22f, 1f);
            chevron.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var chevronRenderer = chevron.GetComponent<SpriteRenderer>();
            chevronRenderer.sprite = plateSprite;
            chevronRenderer.drawMode = SpriteDrawMode.Sliced;
            chevronRenderer.size = new Vector2(.06f, .06f);
            chevronRenderer.color = MinigameUiTheme.Player;
            chevronRenderer.sortingOrder = 20;

            var placement = presentation.GetComponent<SprintPlayerMarkerPlacement>()
                ?? presentation.gameObject.AddComponent<SprintPlayerMarkerPlacement>();
            placement.Bind(marker);
        }

        static void EnsurePause(RectTransform parent, Rect safe, SprintChromeLayout chromeLayout)
        {
            if (Object.FindFirstObjectByType<KMA.Gameplay.UI.PausePanel>() != null)
                return;

            RectTransform button = UiKit.Rect(parent, "PausePanel");
            UiKit.StylePauseButton(button);
            if (safe.width > 0f && safe.height > 0f)
            {
                ApplyRect(button, safe, SprintUiLayout.PauseRect(safe));
            }
            else
            {
                // A degenerate first-frame safe rect (batchmode's headless canvas) collapses the
                // usual spread-anchor math to (0,0); pin to the top-right corner instead.
                // SprintChromeLayout re-applies the real anchors once safe becomes valid.
                UiKit.Place(button, Vector2.one, Vector2.one, new Vector2(-38f, -65f), new Vector2(96f, 96f));
            }
            chromeLayout.Register(button, SprintUiLayout.PauseRect);
            button.gameObject.AddComponent<KMA.Gameplay.UI.PausePanel>();
        }

        static KMA.Input.ScreenTapArea FindTapArea(string name)
        {
            GameObject target = GameObject.Find(name);
            return target == null ? null : target.GetComponent<KMA.Input.ScreenTapArea>();
        }
    }

    /// <summary>
    /// Keeps the PLAYER marker beside the runner instead of above them.
    ///
    /// The four painted lanes sit 1.48 units apart and a runner sprite is 1.28 tall, so only
    /// ~0.2 units separate the player's head from the next lane's feet — an overhead plate lands
    /// on the lane 1 rival's body. The marker rides alongside the runner inside their own lane.
    ///
    /// The track spans the whole viewport at 16:9, so a fixed side would push the marker off
    /// screen at the starting line or at the tape; it takes whichever side has room instead.
    /// </summary>
    public sealed class SprintPlayerMarkerPlacement : MonoBehaviour
    {
        public const float SideOffset = 1.05f;
        public const float MarkerHeight = .42f;
        public const float ChevronOffset = .52f;

        [SerializeField] Transform marker;
        [SerializeField] Transform chevron;

        public Transform Marker => marker;
        public float SideSign => marker == null ? 0f : Mathf.Sign(marker.localPosition.x);

        public void Bind(Transform markerTransform)
        {
            marker = markerTransform;
            chevron = markerTransform == null ? null : markerTransform.Find("Chevron");
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (marker == null)
                return;

            float runnerX = marker.parent == null ? 0f : marker.parent.position.x;
            float offset = runnerX > 0f ? -SideOffset : SideOffset;
            marker.localPosition = new Vector3(offset, MarkerHeight, 0f);

            if (chevron == null)
                chevron = marker.Find("Chevron");
            if (chevron != null)
            {
                // The chevron always sits on the runner's side of the plate.
                var local = chevron.localPosition;
                chevron.localPosition = new Vector3(-Mathf.Sign(offset) * ChevronOffset, local.y, local.z);
            }
        }
    }
}
