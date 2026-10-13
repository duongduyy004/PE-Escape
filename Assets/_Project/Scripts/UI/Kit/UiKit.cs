using KMA.Gameplay.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    /// Builds and restyles the shared minigame widgets. The kit owns look and press feedback;
    /// callers own position and size. Runs at runtime and in editor configurators alike.
    public static class UiKit
    {
        static UiKitAssets Assets => UiKitAssets.Load();

        /// GetComponent-or-AddComponent that is safe with Unity null (the ?? operator is not).
        public static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        public static T GetOrAdd<T>(Component target) where T : Component => GetOrAdd<T>(target.gameObject);

        public static RectTransform Rect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        public static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.zero);

        public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// Pins the shared pause button to the safe area's top-right corner with the kit margin.
        public static void PlacePause(RectTransform pause) =>
            Place(pause, Vector2.one, Vector2.one, new Vector2(-MinigameUiTheme.SpaceMd, -MinigameUiTheme.SpaceMd),
                Vector2.one * MinigameUiTheme.ButtonHeight);

        /// Gives an Image a baked rounded-rect sprite and scales its corners to the radius.
        public static void SetRadius(Image image, float radius)
        {
            Sprite sprite = Assets.RoundRectFor(radius);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = radius > 0f ? sprite.border.x / radius : 1f;
        }

        public static Image Shape(Transform parent, string name, float radius, Color color)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            SetRadius(image, radius < 0f ? MinigameUiTheme.RadiusPanel : radius);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Disc(Transform parent, string name, bool ring, Color color)
        {
            RectTransform rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ring ? Assets.Ring : Assets.Circle;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static void AddShadow(Graphic graphic)
        {
            foreach (Outline outline in graphic.GetComponents<Outline>())
                DestroyObject(outline);
            Shadow shadow = null;
            foreach (Shadow existing in graphic.GetComponents<Shadow>())
                if (!(existing is Outline))
                    shadow = existing;
            shadow ??= graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = MinigameUiTheme.ShadowColor;
            shadow.effectDistance = MinigameUiTheme.ShadowOffset;
        }

        public static Image Panel(Transform parent, string name, float radius = -1f,
            float alpha = MinigameUiTheme.SurfaceOpaque, bool shadow = true)
        {
            Image image = Shape(parent, name, radius < 0f ? MinigameUiTheme.RadiusPanel : radius, MinigameUiTheme.WithAlpha(MinigameUiTheme.Surface, alpha));
            if (shadow)
                AddShadow(image);
            return image;
        }

        /// Restyles an existing Image as a kit panel (prefab styler, tutorial card).
        public static void StylePanel(Image image, float radius = -1f,
            float alpha = MinigameUiTheme.SurfaceOpaque)
        {
            SetRadius(image, radius < 0f ? MinigameUiTheme.RadiusPanel : radius);
            image.color = MinigameUiTheme.WithAlpha(MinigameUiTheme.Surface, alpha);
            AddShadow(image);
        }

        public static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool outline = false)
        {
            var label = Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            StyleLabel(label, size, color, outline);
            label.alignment = alignment;
            label.text = VietText.Fix(text);
            VietTypography.Apply(label);
            return label;
        }

        public static void StyleLabel(TMP_Text label, float size, Color color, bool outline = false)
        {
            label.font = Assets.Font;
            label.fontSharedMaterial = outline ? Assets.OutlineMaterial : Assets.Font.material;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = false;
            label.fontSize = Mathf.Max(size, MinigameUiTheme.MinimumFontSize);
            label.color = color;
            label.raycastTarget = false;
            VietTypography.Apply(label);
        }

        /// Returns the TMP label under root. A scene-authored legacy Text is replaced by a stretched
        /// kit label that keeps its caption, so buttons never keep the Arial fallback.
        public static TMP_Text EnsureTmpLabel(Transform root, float size, Color color)
        {
            TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                Text legacy = root.GetComponentInChildren<Text>(true);
                string caption = legacy != null ? legacy.text : string.Empty;
                if (legacy != null)
                    DestroyObject(legacy.gameObject);
                label = Label(root, "Label", caption, size, color);
                Stretch(label.rectTransform);
            }
            StyleLabel(label, size, color);
            return label;
        }

        /// Lets a label shrink to fit its rect, never below the minimum font size.
        public static void FitLabel(TMP_Text label, float maxSize)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = MinigameUiTheme.MinimumFontSize;
            label.fontSizeMax = Mathf.Max(maxSize, MinigameUiTheme.MinimumFontSize);
        }

        public static ChipHandle Chip(Transform parent, string name, string text)
        {
            Image background = Panel(parent, name, MinigameUiTheme.RadiusPanel, MinigameUiTheme.SurfaceSoft);
            TMP_Text label = Label(background.transform, "Label", text, MinigameUiTheme.Body, MinigameUiTheme.TextPrimary);
            Stretch(label.rectTransform, new Vector2(MinigameUiTheme.SpaceMd, MinigameUiTheme.SpaceXs),
                new Vector2(-MinigameUiTheme.SpaceMd, -MinigameUiTheme.SpaceXs));
            FitLabel(label, MinigameUiTheme.Body);
            return new ChipHandle(background, label);
        }

        public static ButtonHandle Button(Transform parent, string name, string text, ButtonVariant variant)
        {
            RectTransform rect = Rect(parent, name);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, MinigameUiTheme.ButtonHeight);
            rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            TMP_Text label = Label(rect, "Label", text, MinigameUiTheme.Body, MinigameUiTheme.TextPrimary);
            Stretch(label.rectTransform, new Vector2(MinigameUiTheme.SpaceSm, 0f), new Vector2(-MinigameUiTheme.SpaceSm, 0f));
            return StyleButton(button, variant);
        }

        /// Restyles any uGUI Button, kit-built or prefab-authored, as a kit button. Idempotent.
        public static ButtonHandle StyleButton(UnityEngine.UI.Button button, ButtonVariant variant, string text = null)
        {
            GameObject root = button.gameObject;
            RemoveLegacyButtonParts(root.transform);

            Image face = GetOrAdd<Image>(root);
            SetRadius(face, MinigameUiTheme.RadiusControl);
            face.raycastTarget = true;
            AddShadow(face);
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;

            Transform existingFill = root.transform.Find("Fill");
            Image fill = existingFill != null
                ? existingFill.GetComponent<Image>()
                : Shape(root.transform, "Fill", MinigameUiTheme.RadiusControl - MinigameUiTheme.BorderWidth, MinigameUiTheme.Surface);
            SetRadius(fill, Mathf.Max(0f, MinigameUiTheme.RadiusControl - MinigameUiTheme.BorderWidth));
            fill.rectTransform.SetAsFirstSibling();
            Stretch(fill.rectTransform, Vector2.one * MinigameUiTheme.BorderWidth, -Vector2.one * MinigameUiTheme.BorderWidth);

            TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);
            if (label == null && text != null)
            {
                label = Label(root.transform, "Label", text, MinigameUiTheme.Body, MinigameUiTheme.TextPrimary);
                Stretch(label.rectTransform, new Vector2(MinigameUiTheme.SpaceSm, 0f), new Vector2(-MinigameUiTheme.SpaceSm, 0f));
            }
            if (label != null)
            {
                StyleLabel(label, MinigameUiTheme.Body, MinigameUiTheme.TextPrimary);
                FitLabel(label, MinigameUiTheme.Body);
                label.alignment = TextAlignmentOptions.Center;
                if (text != null)
                    label.text = VietText.Fix(text);
            }

            var feedback = GetOrAdd<KitPressFeedback>(root);
            feedback.Configure(face, (RectTransform)root.transform);
            feedback.BindLabel(label);
            var handle = new ButtonHandle(button, face, fill, label, feedback);
            ApplyVariant(handle, variant);
            return handle;
        }

        public static void ApplyVariant(ButtonHandle handle, ButtonVariant variant)
        {
            Color faceColor = variant == ButtonVariant.Danger ? MinigameUiTheme.Energy : MinigameUiTheme.Accent;
            bool disabled = handle.Button != null && !handle.Button.interactable;
            if (handle.Face != null && !disabled)
                handle.Face.color = faceColor;
            if (handle.Feedback != null)
                handle.Feedback.SetRestColor(faceColor);
            if (handle.Fill != null)
                handle.Fill.gameObject.SetActive(variant == ButtonVariant.Secondary);
            if (handle.Label != null)
            {
                Color labelColor = variant == ButtonVariant.Secondary ? MinigameUiTheme.TextPrimary : MinigameUiTheme.Surface;
                if (handle.Feedback != null)
                {
                    handle.Feedback.BindLabel(handle.Label);
                    handle.Feedback.SetLabelRestColor(labelColor);
                }
                else
                    handle.Label.color = labelColor;
                VietTypography.Apply(handle.Label, variant == ButtonVariant.Secondary
                    ? VietFontRole.ButtonSecondary : VietFontRole.ButtonPrimary);
            }
        }

        /// The parts StyleButton added, for callers that switch a button's variant later.
        public static ButtonHandle ButtonParts(UnityEngine.UI.Button button)
        {
            Transform fill = button.transform.Find("Fill");
            return new ButtonHandle(button, button.GetComponent<Image>(), fill != null ? fill.GetComponent<Image>() : null,
                button.GetComponentInChildren<TMP_Text>(true), button.GetComponent<KitPressFeedback>());
        }

        /// Draws a round action button centred in a larger hit area; the hit area owns the feedback.
        public static RoundButtonHandle RoundButton(RectTransform hitArea, string text,
            float size = MinigameUiTheme.RoundButton)
        {
            RectTransform root = Rect(hitArea, "RoundButton");
            Place(root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.one * size);
            float drop = MinigameUiTheme.ShadowOffset.y * 2f;
            // The disc is drawn in the navy outline token so it stays on the palette; the uGUI Shadow effect keeps ShadowColor.
            Image shadow = Disc(root, "Shadow", false,
                MinigameUiTheme.WithAlpha(MinigameUiTheme.TextOutline, MinigameUiTheme.ShadowColor.a));
            Stretch(shadow.rectTransform, new Vector2(0f, drop), new Vector2(0f, drop));
            Image rim = Disc(root, "Rim", true, MinigameUiTheme.TextPrimary);
            Stretch(rim.rectTransform);
            float inset = size * 3f / 64f;
            Image face = Disc(root, "Face", false, MinigameUiTheme.Accent);
            Stretch(face.rectTransform, Vector2.one * inset, -Vector2.one * inset);
            TMP_Text label = Label(face.transform, "Label", text, MinigameUiTheme.Headline, MinigameUiTheme.Surface);
            VietTypography.Apply(label, VietFontRole.ButtonPrimary);
            Stretch(label.rectTransform);
            FitLabel(label, MinigameUiTheme.Headline);

            var feedback = GetOrAdd<KitPressFeedback>(hitArea);
            feedback.Configure(face, root);
            return new RoundButtonHandle(root, shadow, rim, face, label, feedback);
        }

        public static ControlPlateHandle ControlPlate(RectTransform visual, string arrow, string text)
        {
            Image border = Shape(visual, "Border", MinigameUiTheme.RadiusControl, KitControlState.Border(ControlState.Rest));
            Stretch(border.rectTransform);
            AddShadow(border);
            Image background = Shape(visual, "Background", MinigameUiTheme.RadiusControl - MinigameUiTheme.BorderWidth,
                KitControlState.Fill(ControlState.Rest));
            Stretch(background.rectTransform, Vector2.one * MinigameUiTheme.BorderWidth, -Vector2.one * MinigameUiTheme.BorderWidth);
            TMP_Text arrowLabel = Label(visual, "Arrow", arrow, MinigameUiTheme.BodyLarge * 1.6f, MinigameUiTheme.TextPrimary);
            Anchor(arrowLabel.rectTransform, new Vector2(.1f, .44f), new Vector2(.9f, .88f));
            TMP_Text label = Label(visual, "Label", text, MinigameUiTheme.BodyLarge, MinigameUiTheme.TextPrimary);
            VietTypography.Apply(label, VietFontRole.ButtonSecondary);
            FitLabel(label, MinigameUiTheme.BodyLarge);
            Anchor(label.rectTransform, new Vector2(.1f, .12f), new Vector2(.9f, .46f));
            return new ControlPlateHandle(visual, border, background, arrowLabel, label);
        }

        public static KitBar Bar(Transform parent, string name, bool pip = false, bool label = false)
        {
            float radius = MinigameUiTheme.BarHeight * .5f;
            Image track = Shape(parent, name, radius, MinigameUiTheme.Track);
            track.rectTransform.sizeDelta = new Vector2(track.rectTransform.sizeDelta.x, MinigameUiTheme.BarHeight);
            Image fill = Shape(track.transform, "Fill", radius, MinigameUiTheme.Accent);
            fill.rectTransform.pivot = new Vector2(0f, .5f);
            Anchor(fill.rectTransform, Vector2.zero, new Vector2(0f, 1f));

            Image pipImage = null;
            if (pip)
            {
                pipImage = Shape(track.transform, "Pip", 4f, MinigameUiTheme.Player);
                pipImage.rectTransform.anchorMin = new Vector2(0f, -.35f);
                pipImage.rectTransform.anchorMax = new Vector2(0f, 1.35f);
                pipImage.rectTransform.pivot = new Vector2(.5f, .5f);
                pipImage.rectTransform.sizeDelta = new Vector2(MinigameUiTheme.BarHeight * .45f, 0f);
                pipImage.rectTransform.anchoredPosition = Vector2.zero;
            }

            TMP_Text text = null;
            if (label)
            {
                text = Label(track.transform, "Label", "0%", MinigameUiTheme.Caption, MinigameUiTheme.TextPrimary,
                    TextAlignmentOptions.Center, outline: true);
                VietTypography.Apply(text, VietFontRole.Hud);
                Stretch(text.rectTransform);
            }

            var bar = track.gameObject.AddComponent<KitBar>();
            bar.Configure(track, fill, pipImage, text);
            bar.SetValue(0f);
            return bar;
        }

        public static SliderHandle Slider(Transform parent, string name)
        {
            RectTransform root = Rect(parent, name);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            Image track = Shape(root, "Track", MinigameUiTheme.BarHeight * .25f, MinigameUiTheme.Track);
            Anchor(track.rectTransform, new Vector2(0f, .40f), new Vector2(1f, .60f));

            RectTransform area = Rect(root, "HandleSlideArea");
            area.anchorMin = new Vector2(0f, .5f);
            area.anchorMax = new Vector2(1f, .5f);
            area.sizeDelta = new Vector2(-MinigameUiTheme.SliderKnob, MinigameUiTheme.SliderKnob);
            Image knob = Disc(area, "Handle", false, MinigameUiTheme.Accent);
            knob.rectTransform.sizeDelta = new Vector2(MinigameUiTheme.SliderKnob, MinigameUiTheme.SliderKnob);
            Image ring = Disc(knob.transform, "Ring", true, MinigameUiTheme.Surface);
            Stretch(ring.rectTransform);

            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.transition = Selectable.Transition.None;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            return new SliderHandle(slider, track, knob);
        }

        public static JoystickHandle Joystick(RectTransform area)
        {
            Image stickBase = Disc(area, "JoystickBase", false, KitControlState.Fill(ControlState.Rest));
            Place(stickBase.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                Vector2.one * MinigameUiTheme.JoystickBase);
            Image rim = Disc(stickBase.transform, "JoystickRim", true, KitControlState.Border(ControlState.Rest));
            Stretch(rim.rectTransform);
            Image knob = Disc(area, "JoystickKnob", false, MinigameUiTheme.WithAlpha(MinigameUiTheme.TextPrimary, .9f));
            Place(knob.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                Vector2.one * MinigameUiTheme.JoystickKnob);
            return new JoystickHandle(stickBase, rim, knob);
        }

        /// Turns a rect into the shared pause button: rounded Surface square with two bars.
        /// Idempotent, and removes any legacy text label.
        public static void StylePauseButton(RectTransform root)
        {
            foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
                DestroyObject(legacy.gameObject);

            Image face = GetOrAdd<Image>(root);
            SetRadius(face, MinigameUiTheme.RadiusPause);
            face.color = MinigameUiTheme.WithAlpha(MinigameUiTheme.Surface, MinigameUiTheme.SurfaceOpaque);
            face.raycastTarget = true;
            AddShadow(face);

            var button = GetOrAdd<UnityEngine.UI.Button>(root);
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;

            PauseBar(root, "BarLeft", .28f, .44f);
            PauseBar(root, "BarRight", .56f, .72f);

            var feedback = GetOrAdd<KitPressFeedback>(root);
            feedback.Configure(face, root);
        }

        public static TMP_Text Countdown(Transform parent, string name) =>
            Label(parent, name, string.Empty, MinigameUiTheme.Display, MinigameUiTheme.Accent,
                TextAlignmentOptions.Center, outline: true);

        public static void StyleCountdown(TMP_Text label) =>
            StyleLabel(label, MinigameUiTheme.Display, MinigameUiTheme.Accent, outline: true);

        static void PauseBar(RectTransform root, string name, float minX, float maxX)
        {
            if (root.Find(name) != null)
                return;
            Image bar = Shape(root, name, 2f, MinigameUiTheme.TextPrimary);
            Anchor(bar.rectTransform, new Vector2(minX, .26f), new Vector2(maxX, .74f));
        }

        static void RemoveLegacyButtonParts(Transform root)
        {
            Transform shadow = root.Find("Shadow");
            if (shadow != null)
                DestroyObject(shadow.gameObject);

            Transform visual = root.Find("Visual");
            if (visual != null)
            {
                Image visualImage = visual.GetComponent<Image>();
                if (visualImage != null)
                    DestroyObject(visualImage);
                foreach (Shadow effect in visual.GetComponents<Shadow>())
                    DestroyObject(effect);
                Stretch((RectTransform)visual);
            }
        }

        static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
