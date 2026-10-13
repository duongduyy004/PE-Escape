using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    public enum ShellButtonVariant { Primary, Secondary, Danger }

    public readonly struct ShellCardHandle
    {
        public readonly RectTransform Root;
        public readonly Image Shadow;
        public readonly Image Border;
        public readonly Image Face;

        public ShellCardHandle(RectTransform root, Image shadow, Image border, Image face)
        {
            Root = root;
            Shadow = shadow;
            Border = border;
            Face = face;
        }
    }

    public readonly struct ShellChipHandle
    {
        public readonly ShellCardHandle Card;
        public readonly TMP_Text Label;

        public ShellChipHandle(ShellCardHandle card, TMP_Text label)
        {
            Card = card;
            Label = label;
        }
    }

    public readonly struct ShellButtonHandle
    {
        public readonly Button Button;
        public readonly RectTransform Body;
        public readonly ShellCardHandle Card;
        public readonly TMP_Text Label;
        public readonly ShellPressFeedback Feedback;

        public ShellButtonHandle(Button button, RectTransform body, ShellCardHandle card, TMP_Text label,
            ShellPressFeedback feedback)
        {
            Button = button;
            Body = body;
            Card = card;
            Label = label;
            Feedback = feedback;
        }
    }

    public readonly struct ShellTitleHandle
    {
        public readonly RectTransform Root;
        public readonly TMP_Text Text;
        public readonly TMP_Text Drop;

        public ShellTitleHandle(RectTransform root, TMP_Text text, TMP_Text drop)
        {
            Root = root;
            Text = text;
            Drop = drop;
        }
    }

    public readonly struct ShellToggleHandle
    {
        public readonly Toggle Toggle;
        public readonly ShellCardHandle Track;
        public readonly Image Check;
        public readonly TMP_Text Value;

        public ShellToggleHandle(Toggle toggle, ShellCardHandle track, Image check, TMP_Text value)
        {
            Toggle = toggle;
            Track = track;
            Check = check;
            Value = value;
        }
    }

    public readonly struct ShellSliderHandle
    {
        public readonly Slider Slider;
        public readonly ShellCardHandle Track;
        public readonly Image Fill;
        public readonly Image Knob;

        public ShellSliderHandle(Slider slider, ShellCardHandle track, Image fill, Image knob)
        {
            Slider = slider;
            Track = track;
            Fill = fill;
            Knob = knob;
        }
    }
}
