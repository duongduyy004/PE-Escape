using System;
using System.Collections.Generic;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    /// Visual-novel style journey dialogue: two actor slots (player always right), a typewriter
    /// text box with emoji sprites, optional reaction stickers, tap anywhere to continue.
    public sealed class JourneyDialoguePresenter : MonoBehaviour
    {
        public static readonly Color ListenerTint = new Color(.4f, .4f, .4f, 1f);

        const string EmojiResource = "Journey/JourneyEmoji";
        const float ActorHeight = 734f, ActorBottom = 260f, ActorX = 630f, ActorOffX = 1250f;
        const float SlideSeconds = .25f, HopSeconds = .3f, StickerSeconds = .25f;
        const float BoxHeight = 313f, BoxMargin = 67f, BoxBottom = 38f;
        static readonly Color StickerRed = new Color32(0xE2, 0x55, 0x3D, 0xFF);
        static readonly Color DotIdle = new Color(1f, 1f, 1f, .3f);

        readonly struct DialogueRequest
        {
            public readonly string NodeId, SeenKey;
            public DialogueRequest(string nodeId, string seenKey) { NodeId = nodeId; SeenKey = seenKey; }
        }

        sealed class ActorSlot
        {
            public Image Image;
            public float HomeX, OffX, Slide, Hop = -1f;
            public bool Speaking;
            public JourneyCharacter Character, Pending;
            public Sprite PendingSprite;
        }

        JourneyDialogueLibrary library;
        Func<string, bool> persistSeen;
        readonly Queue<DialogueRequest> pending = new Queue<DialogueRequest>();
        readonly DialogueTypewriter typewriter = new DialogueTypewriter();
        readonly List<Image> dots = new List<Image>();
        readonly ActorSlot left = new ActorSlot { HomeX = -ActorX, OffX = -ActorOffX };
        readonly ActorSlot right = new ActorSlot { HomeX = ActorX, OffX = ActorOffX };
        RectTransform overlay, nameTagRect, dotsRoot, stickerRect;
        Image backgroundImage, nameTagImage;
        TMP_Text nameTag, body, errorText, hint, stickerText;
        IReadOnlyList<JourneyDialogueLine> activeLines;
        string activeSeenKey;
        int lineIndex;
        float stickerAge = -1f;
        Action closed;

        public bool IsShowing => overlay != null && overlay.gameObject.activeSelf;
        public int CurrentLineIndex => lineIndex;
        public bool IsLineFullyShown => typewriter.IsDone;

        public void Configure(JourneyDialogueLibrary dialogueLibrary, Func<string, bool> saveSeen,
            Sprite sharedBackground = null)
        {
            library = dialogueLibrary;
            persistSeen = saveSeen;
            EnsureView();
            if (sharedBackground != null) backgroundImage.sprite = sharedBackground;
        }

        public void Show(string nodeId, Action onClosed) => Show(nodeId, nodeId, onClosed);

        public void ShowJourney(GameSession session, GameManager manager, Sprite sharedBackground = null)
        {
            if (session == null || manager == null) return;
            Configure(JourneyDialogueLibrary.LoadDefault(), key => manager.TryMarkJourneyDialogueSeen(key, out _),
                sharedBackground);
            pending.Clear();
            JourneyProgress journey = session.Journey;
            void Add(string node, string key = null)
            {
                string seenKey = key ?? node;
                if (!journey.IsDialogueSeen(seenKey)) pending.Enqueue(new DialogueRequest(node, seenKey));
            }

            Add("opening");
            if (journey.CourseComplete)
            {
                Add("course_complete");
            }
            else
            {
                switch (journey.CheckpointChallengeId)
                {
                    case "sprint_learn": Add("sprint_intro"); break;
                    case "sprint_exam": Add("sprint_exam"); break;
                    case "volleyball_learn": Add("sprint_pass"); Add("volleyball_intro"); break;
                    case "volleyball_exam": Add("volleyball_exam"); break;
                    case "soccer_learn": Add("volleyball_pass"); Add("soccer_intro"); break;
                    case "soccer_exam": Add("soccer_exam"); break;
                    case "chess_final": Add("soccer_pass"); Add("chess_intro"); break;
                }
            }
            ShowNext();
        }

        /// Tap handler: reveal the rest of the line, then move to the next line, then close.
        public void Advance()
        {
            if (!IsShowing) return;
            errorText.text = string.Empty;
            if (!typewriter.IsDone)
            {
                typewriter.Complete();
                body.maxVisibleCharacters = typewriter.VisibleCharacters;
                UpdateHint();
                return;
            }
            if (lineIndex + 1 < activeLines.Count)
            {
                lineIndex++;
                RenderLine();
                return;
            }
            FinishNode();
        }

        void Show(string nodeId, string seenKey, Action onClosed)
        {
            if (library == null) library = JourneyDialogueLibrary.LoadDefault();
            if (library == null) throw new InvalidOperationException("Journey dialogue Resources asset is missing.");
            EnsureView();
            JourneyDialogueNode node = library.Get(nodeId);
            activeLines = node.Lines;
            activeSeenKey = seenKey;
            closed = onClosed;
            lineIndex = 0;
            errorText.text = string.Empty;
            overlay.gameObject.SetActive(true);
            ResetActors();
            BuildDots(activeLines.Count);
            RenderLine();
        }

        void Update()
        {
            if (!IsShowing) return;
            float dt = Time.unscaledDeltaTime;
            if (!typewriter.IsDone)
            {
                typewriter.Tick(dt);
                body.maxVisibleCharacters = typewriter.VisibleCharacters;
                if (typewriter.IsDone) UpdateHint();
            }
            AnimateSlot(left, dt);
            AnimateSlot(right, dt);
            if (stickerAge >= 0f)
            {
                stickerAge += dt;
                float t = Mathf.Clamp01(stickerAge / StickerSeconds);
                float scale = t < .6f ? Mathf.Lerp(0f, 1.15f, t / .6f) : Mathf.Lerp(1.15f, 1f, (t - .6f) / .4f);
                stickerRect.localScale = new Vector3(scale, scale, 1f);
                if (t >= 1f) stickerAge = -1f;
            }
        }

        void ResetActors()
        {
            JourneyCharacter player = library.Player;
            right.Character = player;
            right.Pending = null;
            right.Image.sprite = player?.GetPose(DialoguePose.Idle);
            right.Slide = 0f;
            right.Hop = -1f;
            left.Character = null;
            left.Pending = null;
            left.Image.sprite = null;
            left.Slide = 0f;
            left.Hop = -1f;
            foreach (JourneyDialogueLine line in activeLines)
            {
                JourneyCharacter character = library.GetCharacter(line.CharacterId);
                if (character.IsPlayer) continue;
                left.Character = character;
                left.Image.sprite = character.GetPose(DialoguePose.Idle);
                break;
            }
        }

        void RenderLine()
        {
            JourneyDialogueLine line = activeLines[lineIndex];
            JourneyCharacter speaker = library.GetCharacter(line.CharacterId);
            Sprite sprite = speaker.GetPose(line.Pose);
            bool onRight = speaker.IsPlayer;
            ActorSlot speaking = onRight ? right : left;
            if (onRight) right.Image.sprite = sprite;
            else PlaceLeft(speaker, sprite);
            right.Speaking = onRight;
            left.Speaking = !onRight;
            speaking.Hop = 0f;
            ApplySlot(left);
            ApplySlot(right);

            nameTag.text = VietText.Fix(speaker.DisplayName);
            nameTagImage.color = speaker.TagColor;
            nameTagRect.anchorMin = nameTagRect.anchorMax = new Vector2(onRight ? 1f : 0f, 1f);
            nameTagRect.pivot = new Vector2(onRight ? 1f : 0f, .5f);
            nameTagRect.anchoredPosition = new Vector2(onRight ? -36f : 36f, 0f);
            nameTagRect.localEulerAngles = new Vector3(0f, 0f, onRight ? 3f : -3f);

            body.text = VietText.Fix(DialogueEmoji.Expand(line.Text));
            body.maxVisibleCharacters = 0;
            body.ForceMeshUpdate();
            typewriter.Begin(body.textInfo.characterCount);
            body.maxVisibleCharacters = typewriter.VisibleCharacters;

            bool hasSticker = !string.IsNullOrWhiteSpace(line.Sticker);
            stickerRect.gameObject.SetActive(hasSticker);
            stickerAge = hasSticker ? 0f : -1f;
            if (hasSticker)
            {
                stickerText.text = VietText.Fix(line.Sticker);
                stickerRect.anchoredPosition = new Vector2(speaking.HomeX + (onRight ? -170f : 170f),
                    ActorBottom + ActorHeight * .82f);
                stickerRect.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(-8f, 8f));
                stickerRect.localScale = Vector3.zero;
            }

            for (int i = 0; i < dots.Count; i++) dots[i].color = i <= lineIndex ? HomeMenuStyle.Gold : DotIdle;
            UpdateHint();
        }

        void PlaceLeft(JourneyCharacter character, Sprite sprite)
        {
            if (left.Character == null || left.Character == character)
            {
                left.Character = character;
                left.Image.sprite = sprite;
                left.Pending = null;
                left.PendingSprite = null;
                return;
            }
            left.Pending = character;
            left.PendingSprite = sprite;
        }

        void AnimateSlot(ActorSlot slot, float dt)
        {
            float step = dt / (SlideSeconds * .5f);
            if (slot.Pending != null)
            {
                slot.Slide = Mathf.MoveTowards(slot.Slide, 0f, step);
                if (slot.Slide <= 0f)
                {
                    slot.Character = slot.Pending;
                    slot.Image.sprite = slot.PendingSprite;
                    slot.Pending = null;
                    slot.PendingSprite = null;
                }
            }
            else
            {
                slot.Slide = Mathf.MoveTowards(slot.Slide, 1f, step);
            }
            if (slot.Hop >= 0f)
            {
                slot.Hop += dt;
                if (slot.Hop >= HopSeconds) slot.Hop = -1f;
            }
            ApplySlot(slot);
        }

        void ApplySlot(ActorSlot slot)
        {
            float hop = slot.Hop >= 0f ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(slot.Hop / HopSeconds)) * ActorHeight * .06f : 0f;
            float sink = slot.Speaking ? 0f : -ActorHeight * .04f;
            float eased = 1f - (1f - slot.Slide) * (1f - slot.Slide);
            slot.Image.rectTransform.anchoredPosition =
                new Vector2(Mathf.Lerp(slot.OffX, slot.HomeX, eased), ActorBottom + hop + sink);
            slot.Image.color = slot.Speaking ? Color.white : ListenerTint;
            slot.Image.enabled = slot.Character != null && slot.Image.sprite != null;
        }

        void UpdateHint()
        {
            string text = !typewriter.IsDone ? "chạm để hiện hết"
                : lineIndex + 1 >= activeLines.Count
                    ? SceneRouter.Instance != null && !string.IsNullOrEmpty(SceneRouter.Instance.PendingJourneyChallengeId)
                        ? "TIẾP TỤC »" : "ĐÓNG »"
                    : "TIẾP »";
            hint.text = VietText.Fix(text);
        }

        void FinishNode()
        {
            errorText.text = string.Empty;
            if (persistSeen == null || !persistSeen(activeSeenKey))
            {
                errorText.text = VietText.Fix("Không lưu được mốc hội thoại. Chạm để thử lại.");
                return;
            }
            overlay.gameObject.SetActive(false);
            stickerRect.gameObject.SetActive(false);
            Action callback = closed;
            closed = null;
            callback?.Invoke();
        }

        void ShowNext()
        {
            if (pending.Count == 0) return;
            DialogueRequest request = pending.Dequeue();
            Show(request.NodeId, request.SeenKey, ShowNext);
        }

        void BuildDots(int count)
        {
            foreach (Image dot in dots)
                if (dot != null) Destroy(dot.gameObject);
            dots.Clear();
            for (int i = 0; i < count; i++)
            {
                Image dot = UiKit.Disc(dotsRoot, "Dot" + i, false, DotIdle);
                dot.rectTransform.sizeDelta = new Vector2(16f, 16f);
                LayoutElement element = dot.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = element.preferredHeight = 16f;
                dots.Add(dot);
            }
        }

        void EnsureView()
        {
            if (overlay != null) return;
            overlay = UiKit.Rect(transform, "JourneyDialogueOverlay");
            UiKit.Stretch(overlay);
            overlay.gameObject.AddComponent<CanvasGroup>();

            var backgroundObject = new GameObject("SharedMenuBackground", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
            backgroundObject.transform.SetParent(overlay, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(.5f, .5f);
            backgroundRect.sizeDelta = Vector2.zero;
            backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.raycastTarget = false;
            AspectRatioFitter backgroundFit = backgroundObject.GetComponent<AspectRatioFitter>();
            backgroundFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backgroundFit.aspectRatio = 1928f / 816f;

            Image dimmer = UiKit.Rect(overlay, "BackgroundDimmer").gameObject.AddComponent<Image>();
            UiKit.Stretch(dimmer.rectTransform);
            dimmer.color = MinigameUiTheme.WithAlpha(HomeMenuStyle.Navy, .36f);
            dimmer.raycastTarget = false;

            left.Image = MakeActor("LeftActor", false);
            right.Image = MakeActor("RightActor", true);

            stickerRect = UiKit.Rect(overlay, "Sticker");
            UiKit.Place(stickerRect, new Vector2(.5f, 0f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(340f, 84f));
            Image stickerCard = stickerRect.gameObject.AddComponent<Image>();
            UiKit.SetRadius(stickerCard, 18f);
            stickerCard.color = Color.white;
            stickerCard.raycastTarget = false;
            Outline stickerOutline = stickerRect.gameObject.AddComponent<Outline>();
            stickerOutline.effectColor = HomeMenuStyle.Navy;
            stickerOutline.effectDistance = new Vector2(3f, -3f);
            stickerText = MakeText(stickerRect, "Label", 40f, StickerRed, TextAlignmentOptions.Center);
            UiKit.Stretch(stickerText.rectTransform);
            stickerText.fontStyle = FontStyles.Bold;
            stickerRect.gameObject.SetActive(false);

            Image box = UiKit.Shape(overlay, "DialogueBox", 28f, MinigameUiTheme.WithAlpha(HomeMenuStyle.Navy, .93f));
            RectTransform boxRect = box.rectTransform;
            boxRect.anchorMin = Vector2.zero;
            boxRect.anchorMax = new Vector2(1f, 0f);
            boxRect.pivot = new Vector2(.5f, 0f);
            boxRect.offsetMin = new Vector2(BoxMargin, BoxBottom);
            boxRect.offsetMax = new Vector2(-BoxMargin, BoxBottom + BoxHeight);
            Outline boxOutline = box.gameObject.AddComponent<Outline>();
            boxOutline.effectColor = HomeMenuStyle.Gold;
            boxOutline.effectDistance = new Vector2(3f, -3f);

            nameTagImage = UiKit.Shape(boxRect, "NameTag", 26f, HomeMenuStyle.Gold);
            nameTagRect = nameTagImage.rectTransform;
            nameTagRect.sizeDelta = new Vector2(320f, 56f);
            Outline tagOutline = nameTagImage.gameObject.AddComponent<Outline>();
            tagOutline.effectColor = Color.white;
            tagOutline.effectDistance = new Vector2(2f, -2f);
            nameTag = MakeText(nameTagRect, "Name", 28f, HomeMenuStyle.Navy, TextAlignmentOptions.Center);
            UiKit.Stretch(nameTag.rectTransform);
            nameTag.fontStyle = FontStyles.Bold;

            body = MakeText(boxRect, "Dialogue", 34f, HomeMenuStyle.White, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(body.rectTransform, new Vector2(56f, 58f), new Vector2(-56f, -44f));
            body.spriteAsset = Resources.Load<TMP_SpriteAsset>(EmojiResource);

            // Bottom row (between the progress dots and the hint) so it never sits under the name tag,
            // which hangs on the top edge on either side.
            errorText = MakeText(boxRect, "SaveError", 22f, HomeMenuStyle.GoldLight, TextAlignmentOptions.Left);
            UiKit.Place(errorText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(260f, 12f), new Vector2(640f, 34f));
            hint = MakeText(boxRect, "Hint", 22f, HomeMenuStyle.Gold, TextAlignmentOptions.Right);
            UiKit.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 12f), new Vector2(360f, 34f));
            dotsRoot = UiKit.Rect(boxRect, "ProgressDots");
            UiKit.Place(dotsRoot, Vector2.zero, Vector2.zero, new Vector2(40f, 20f), new Vector2(200f, 18f));
            HorizontalLayoutGroup dotLayout = dotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotLayout.spacing = 10f;
            dotLayout.childAlignment = TextAnchor.MiddleLeft;
            dotLayout.childControlWidth = dotLayout.childControlHeight = false;
            dotLayout.childForceExpandWidth = dotLayout.childForceExpandHeight = false;

            RectTransform tapRect = UiKit.Rect(overlay, "TapArea");
            UiKit.Stretch(tapRect);
            Image tapImage = tapRect.gameObject.AddComponent<Image>();
            tapImage.color = new Color(0f, 0f, 0f, 0f);
            tapImage.raycastTarget = true;
            Button tap = tapRect.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Advance);

            MakeSkip().onClick.AddListener(FinishNode);
            overlay.gameObject.SetActive(false);
        }

        Image MakeActor(string name, bool flipped)
        {
            RectTransform rect = UiKit.Rect(overlay, name);
            UiKit.Place(rect, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero,
                new Vector2(ActorHeight * .8f, ActorHeight));
            if (flipped) rect.localScale = new Vector3(-1f, 1f, 1f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        Button MakeSkip()
        {
            ButtonHandle handle = UiKit.Button(overlay, "BỎ QUA", "BỎ QUA »", ButtonVariant.Secondary);
            UiKit.Place((RectTransform)handle.Button.transform, Vector2.one, Vector2.one, new Vector2(-58f, -43f),
                new Vector2(240f, MinigameUiTheme.ButtonHeight));
            return handle.Button;
        }

        static TMP_Text MakeText(Transform parent, string name, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            TMP_Text text = UiKit.Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            UiKit.StyleLabel(text, fontSize, color);
            VietTypography.Apply(text);
            return text;
        }
    }
}
