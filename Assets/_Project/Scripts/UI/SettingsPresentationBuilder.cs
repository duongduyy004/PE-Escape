using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public static class SettingsPresentationBuilder
    {
        const TextAlignmentOptions Left = TextAlignmentOptions.MidlineLeft;
        const TextAlignmentOptions Right = TextAlignmentOptions.MidlineRight;

        public static void Build(SettingsScreen screen)
        {
            if (screen.transform.Find("SettingsPanel") != null)
                return;
            var root = screen.GetComponent<RectTransform>() ?? screen.gameObject.AddComponent<RectTransform>();
            Stretch(root, Vector2.zero, Vector2.one);
            var backdrop = screen.gameObject.AddComponent<Image>();
            backdrop.color = UITheme.Shared.Scrim;

            var panel = Rect(root, "SettingsPanel", new Vector2(.22f, .14f), new Vector2(.78f, .86f));
            ShellKit.StyleCard(panel);
            ShellTitleHandle title = ShellKit.Title(panel, "Title", "CÀI ĐẶT", MinigameUiTheme.Title);
            Stretch(title.Root, new Vector2(.08f, .82f), new Vector2(.92f, .96f));
            var music = Volume(panel, "MusicSlider", "ÂM LƯỢNG NHẠC", .58f, out TMP_Text musicValue);
            var sfx = Volume(panel, "SfxSlider", "ÂM LƯỢNG HIỆU ỨNG", .36f, out TMP_Text sfxValue);

            Label(panel, "VibrationLabel", "RUNG", new Vector2(.09f, .23f), new Vector2(.6f, .33f), Left);
            ShellToggleHandle vibration = ShellKit.Toggle(panel, "VibrationToggle");
            Stretch(vibration.Toggle.GetComponent<RectTransform>(), new Vector2(.67f, .23f), new Vector2(.91f, .33f));

            ShellButtonHandle back = ShellKit.Button(panel, "BackButton", "QUAY LẠI", ShellButtonVariant.Secondary);
            Stretch((RectTransform)back.Button.transform, new Vector2(.26f, .075f), new Vector2(.74f, .18f));
            back.Button.onClick.AddListener(screen.Back);
            TMP_Text hint = Label(panel, "Hint", "Thay đổi được lưu tự động", new Vector2(.08f, .015f),
                new Vector2(.92f, .07f), TextAlignmentOptions.Center);
            hint.color = ShellTheme.BodyText;
            screen.BindControls(music, sfx, vibration.Toggle, musicValue, sfxValue, vibration.Value);
        }

        static Slider Volume(Transform parent, string name, string title, float bottom, out TMP_Text value)
        {
            var row = Rect(parent, name + "Row", new Vector2(.09f, bottom), new Vector2(.91f, bottom + .2f));
            Label(row, "Title", title, new Vector2(0f, .55f), new Vector2(.78f, 1f), Left);
            value = Label(row, "Value", "100%", new Vector2(.78f, .55f), Vector2.one, Right);
            ShellSliderHandle slider = ShellKit.Slider(row, name);
            Stretch(slider.Slider.GetComponent<RectTransform>(), new Vector2(0f, .08f), new Vector2(1f, .52f));
            return slider.Slider;
        }

        static TMP_Text Label(Transform parent, string name, string text, Vector2 min, Vector2 max,
            TextAlignmentOptions alignment)
        {
            TMP_Text label = ShellKit.Label(parent, name, text, MinigameUiTheme.Caption, ShellTheme.Ink, VietFontRole.Hud, alignment);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(label.rectTransform, min, max);
            return label;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect, min, max);
            return rect;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
