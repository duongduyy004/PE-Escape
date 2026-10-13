using KMA.Gameplay;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public sealed class MapNodeView : MonoBehaviour
    {
        [SerializeField] SubjectId subjectId;
        [SerializeField] string displayName;
        [SerializeField] bool comingSoon;
        [SerializeField] ScriptableObject subjectConfigAsset;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text detailLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text actionLabel;
        [SerializeField] Button button;
        [SerializeField] Image cardImage;
        [SerializeField] Image iconPlate;
        [SerializeField] Outline cardOutline;
        [SerializeField] GameObject detailVisibilityRoot;
        [SerializeField] Image badgeImage;
        [SerializeField] Image[] starImages = new Image[0];
        [SerializeField] GameObject lockIcon;
        [SerializeField] GameObject doneMark;
        [SerializeField] GameObject currentTag;
        [SerializeField] Image selectionRing;
        [SerializeField] RectTransform badgeRect;
        [SerializeField] RectTransform labelGroup;
        [SerializeField] Image glyphImage;
        [SerializeField] float labelBaseY;

        [SerializeField] Color subjectColor = Color.white;

        static Color ReadyBorder => UITheme.Shared.Accent;
        static Color CompleteBorder => UITheme.Shared.MapCompleteBorder;
        static Color LockedBorder => UITheme.Shared.MapLockedBorder;
        static Color LockedCard => UITheme.Shared.MapLockedCard;
        static Color LockedIcon => UITheme.Shared.MapLockedIcon;

        public SubjectId SubjectId => subjectId;
        public string DisplayName => displayName;
        public bool IsComingSoon => comingSoon;
        public bool HasSubjectConfigAsset => subjectConfigAsset != null;
        public ScriptableObject SubjectConfigAsset => subjectConfigAsset;
        public string DetailText => detailLabel == null ? string.Empty : detailLabel.text;
        public bool IsInteractable => button != null && button.interactable;
        public int Stars { get; private set; }
        public Rank BestRank { get; private set; }
        public int Lives { get; private set; }
        public bool IsCompleted => completed;
        public bool IsLocked { get; private set; }
        public bool IsCurrent { get; private set; }
        public bool IsSelected { get; private set; }
        public string StatusText => statusLabel == null ? string.Empty : statusLabel.text;

        bool completed;
        float baseStatusSize;

        public void Configure(SubjectId id, string name, bool isComingSoon, SubjectRecord record, int lives)
        {
            ConfigureState(id, name, isComingSoon, record, lives, !isComingSoon);
        }

        void ConfigureState(SubjectId id, string name, bool isComingSoon, SubjectRecord record,
            int lives, bool unlocked)
        {
            subjectId = id;
            displayName = name;
            comingSoon = isComingSoon;
            BestRank = record == null ? Rank.F : record.BestRank;
            completed = record != null && record.Passed;
            Stars = completed ? ScoreUtil.ToStars(BestRank) : 0;
            Lives = lives;
            if (titleLabel != null)
                titleLabel.text = VietText.Fix(displayName);
            RenderAvailability(unlocked && !isComingSoon,
                isComingSoon ? "ĐANG PHÁT TRIỂN" : "CHƯA MỞ KHÓA");
        }

        public void Configure(ScriptableObject configAsset, SubjectRecord record, int lives)
        {
            if (configAsset == null)
                throw new System.ArgumentNullException(nameof(configAsset));
            subjectConfigAsset = configAsset;
            var type = configAsset.GetType();
            subjectId = (SubjectId)type.GetField("subjectId").GetValue(configAsset);
            displayName = (string)type.GetField("displayName").GetValue(configAsset);
            comingSoon = (bool)type.GetField("comingSoon").GetValue(configAsset);
            var unlocked = (bool)type.GetField("unlocked").GetValue(configAsset);
            ConfigureState(subjectId, displayName, comingSoon, record, lives, unlocked);
        }

        public void ConfigureJourneyState(SubjectId subject, string title, bool unlocked, bool passed,
            SubjectRecord record)
        {
            subjectId = subject;
            displayName = title;
            comingSoon = false;
            BestRank = record == null ? Rank.F : record.BestRank;
            completed = passed;
            Stars = completed ? ScoreUtil.ToStars(BestRank) : 0;
            if (titleLabel != null) titleLabel.text = VietText.Fix(title);
            bool needsExam = !unlocked && subject == SubjectId.Chess;
            RenderAvailability(unlocked, unlocked ? completed ? "HOÀN THÀNH" : "SẴN SÀNG"
                : needsExam ? "Đạt Bóng đá để mở" : "CHƯA MỞ KHÓA");
            // The longer locked copy of the final exam stop needs a smaller size to fit beside the stars.
            if (statusLabel != null)
            {
                if (baseStatusSize <= 0f) baseStatusSize = statusLabel.fontSizeMax;
                statusLabel.fontSizeMax = needsExam ? baseStatusSize * .8f : baseStatusSize;
            }
        }

        public void Bind(Button target, TMP_Text title, TMP_Text detail, GameObject detailRoot = null)
        {
            button = target;
            titleLabel = title;
            detailLabel = detail;
            detailVisibilityRoot = detailRoot != null ? detailRoot : detail == null ? null : detail.gameObject;
        }

        public void BindStatusLabel(TMP_Text status) => statusLabel = status;

        public void BindPresentation(TMP_Text status, TMP_Text action, Image background, Image sportIcon,
            Outline outline, Color accent)
        {
            statusLabel = status;
            actionLabel = action;
            cardImage = background;
            iconPlate = sportIcon;
            cardOutline = outline;
            subjectColor = accent;
        }

        public void BindJourneyStop(Image badge, Image[] stars, GameObject lockPictogram,
            GameObject completedMark, GameObject currentMarker, Image ring,
            RectTransform badgeTransform, RectTransform labels, Image glyph = null)
        {
            badgeImage = badge;
            starImages = stars ?? new Image[0];
            lockIcon = lockPictogram;
            doneMark = completedMark;
            currentTag = currentMarker;
            selectionRing = ring;
            badgeRect = badgeTransform;
            labelGroup = labels;
            glyphImage = glyph;
            labelBaseY = labels != null ? labels.anchoredPosition.y : 0f;
            SetJourneyMarkers(false, false);
        }

        public void SetJourneyMarkers(bool current, bool selected)
        {
            IsCurrent = current;
            IsSelected = selected;
            if (currentTag != null) currentTag.SetActive(current);
            if (selectionRing != null) selectionRing.gameObject.SetActive(selected || current);
            if (badgeRect != null)
                badgeRect.localScale = Vector3.one * (current ? UITheme.Shared.LessonJourney.stopCurrentScale : 1f);
            // The ring grows with the badge, otherwise the enlarged badge would hide it (and its glow).
            if (selectionRing != null && badgeRect != null)
                selectionRing.rectTransform.localScale = badgeRect.localScale;
            if (labelGroup != null)
            {
                // Names sit 22px under a normal badge and 26px + the extra radius under the current one.
                float badge = UITheme.Shared.LessonJourney.stopBadgeSize;
                float extra = current ? badge * (UITheme.Shared.LessonJourney.stopCurrentScale - 1f) * .5f : 0f;
                labelGroup.anchoredPosition = new Vector2(labelGroup.anchoredPosition.x, labelBaseY - extra);
            }
            if (selectionRing != null) selectionRing.color = ShellTheme.Gold;
        }

        void Update()
        {
            if (!IsCurrent || selectionRing == null) return;
            float pulse = (Mathf.Sin(Time.unscaledTime * UITheme.Shared.LessonJourney.glowSpeed) + 1f) * .5f;
            Color gold = ShellTheme.Gold;
            selectionRing.color = new Color(gold.r, gold.g, gold.b, Mathf.Lerp(.55f, 1f, pulse));
        }

        public void SetAvailability(bool selectable, string unavailableLabel)
        {
            RenderAvailability(selectable && !comingSoon, unavailableLabel);
        }

        void RenderAvailability(bool selectable, string unavailableLabel)
        {
            if (button != null)
                button.interactable = selectable;
            if (detailLabel != null)
            {
                detailLabel.text = VietText.Fix(selectable
                    ? completed ? $"HẠNG {BestRank}  {Stars} SAO" : "SẴN SÀNG"
                    : unavailableLabel);
                if (detailVisibilityRoot != null)
                    detailVisibilityRoot.SetActive(selectable && completed);
            }
            if (statusLabel != null)
                statusLabel.text = VietText.Fix(selectable
                    ? completed ? "HOÀN THÀNH" : "SẴN SÀNG"
                    : unavailableLabel);
            if (actionLabel != null)
                actionLabel.text = VietText.Fix(selectable ? "THI" : string.Empty);
            ApplyVisualState(completed, !selectable);
        }

        void ApplyVisualState(bool completed, bool locked)
        {
            if (cardImage != null)
                cardImage.color = locked ? LockedCard : UITheme.Shared.Card;
            if (iconPlate != null)
                iconPlate.color = locked ? LockedIcon : subjectColor;
            if (cardOutline != null)
                cardOutline.effectColor = locked ? LockedBorder : completed ? CompleteBorder : ReadyBorder;
            if (statusLabel != null)
                statusLabel.color = locked
                    ? UITheme.Shared.MapLockedText
                    : completed ? CompleteBorder : UITheme.Shared.MapReadyText;
            IsLocked = locked;
            if (lockIcon != null) lockIcon.SetActive(locked);
            if (doneMark != null) doneMark.SetActive(completed && !locked);
            if (badgeImage != null) badgeImage.color = locked ? LockedCard : subjectColor;
            if (glyphImage != null) glyphImage.color = locked ? UITheme.Shared.MapLockedText : ShellTheme.Ink;
            if (badgeImage != null && statusLabel != null)
                statusLabel.color = locked ? UITheme.Shared.MapHint
                    : completed ? ShellTheme.Gold : ShellTheme.GoldLight;
            for (int i = 0; i < starImages.Length; i++)
                if (starImages[i] != null)
                    starImages[i].color = i < Stars && !locked
                        ? ShellTheme.Gold : UITheme.Shared.MapLockedIcon;
        }
    }
}
