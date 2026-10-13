using System.Linq;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class ShellKitTests
    {
        GameObject root;

        [SetUp] public void SetUp() => root = new GameObject("ShellRoot", typeof(RectTransform));
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        static int Count(Transform parent, string name) => parent.Cast<Transform>().Count(t => t.name == name);

        [Test]
        public void CardDrawsShadowBorderAndFaceInOrderWithoutEffectComponents()
        {
            ShellCardHandle card = ShellKit.Card(root.transform, "Card");
            Assert.That(card.Shadow.transform.GetSiblingIndex(), Is.EqualTo(0));
            Assert.That(card.Border.transform.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(card.Face.transform.GetSiblingIndex(), Is.EqualTo(2));
            Assert.That(card.Shadow.color, Is.EqualTo(ShellTheme.Ink));
            Assert.That(card.Border.color, Is.EqualTo(ShellTheme.Ink));
            Assert.That(card.Face.color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(card.Shadow.rectTransform.offsetMin, Is.EqualTo(ShellTheme.CardShadow));
            Assert.That(card.Shadow.rectTransform.offsetMax, Is.EqualTo(ShellTheme.CardShadow));
            Assert.That(card.Face.rectTransform.offsetMin, Is.EqualTo(Vector2.one * ShellTheme.CardBorder));
            Assert.That(card.Face.rectTransform.offsetMax, Is.EqualTo(-Vector2.one * ShellTheme.CardBorder));
            Assert.That(card.Root.GetComponentsInChildren<Shadow>(true), Is.Empty);
            foreach (Image layer in new[] { card.Shadow, card.Border, card.Face })
            {
                Assert.That(layer.raycastTarget, Is.False, layer.name);
                Assert.That(layer.GetComponent<LayoutElement>().ignoreLayout, Is.True, layer.name);
                Assert.That(UiKitAssets.Load().AllSprites(), Does.Contain(layer.sprite), layer.name);
            }
        }

        [Test]
        public void StyleCardIsIdempotentAndUndecorates()
        {
            ShellCardHandle card = ShellKit.Card(root.transform, "Card", decorated: true);
            ShellKit.StyleCard(card.Root, decorated: true);
            Assert.That(Count(card.Root, ShellKit.ShadowName), Is.EqualTo(1));
            Assert.That(Count(card.Root, ShellKit.FaceName), Is.EqualTo(1));
            Assert.That(card.Root.Cast<Transform>().Count(t => t.name.StartsWith(ShellKit.AccentPrefix)), Is.EqualTo(2));
            ShellKit.StyleCard(card.Root, decorated: false);
            Assert.That(card.Root.Cast<Transform>().Count(t => t.name.StartsWith(ShellKit.AccentPrefix)), Is.Zero);
        }

        [Test]
        public void AccentSquareFaceKeepsAOnePixelCornerNotTheSpriteBorder()
        {
            ShellCardHandle card = ShellKit.Card(root.transform, "Card", decorated: true);
            Transform accent = card.Root.Cast<Transform>().First(t => t.name.StartsWith(ShellKit.AccentPrefix));
            Image face = accent.Find(ShellKit.FaceName).GetComponent<Image>();
            Assert.That(face.pixelsPerUnitMultiplier, Is.EqualTo(face.sprite.border.x).Within(0.001f));
        }

        [Test]
        public void ContentAddedAfterTheCardDrawsAboveItsLayers()
        {
            ShellCardHandle card = ShellKit.Card(root.transform, "Card");
            var content = new GameObject("Content", typeof(RectTransform)).transform;
            content.SetParent(card.Root, false);
            ShellKit.StyleCard(card.Root);
            Assert.That(content.GetSiblingIndex(), Is.GreaterThan(card.Face.transform.GetSiblingIndex()));
        }

        [Test]
        public void TileHasNoDropAndAWhiteFace()
        {
            ShellCardHandle tile = ShellKit.Tile(root.transform, "Stat");
            Assert.That(tile.Shadow.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(tile.Face.color, Is.EqualTo(ShellTheme.White));
            Assert.That(tile.Face.rectTransform.offsetMin, Is.EqualTo(Vector2.one * ShellTheme.ControlBorder));
        }

        [Test]
        public void ChipLabelReadsOnItsFaceAndRespectsTheFontFloor()
        {
            ShellChipHandle chip = ShellKit.Chip(root.transform, "Lives", "Lượt thi: 5/5");
            Assert.That(MinigameUiTheme.ContrastRatio(chip.Label.color, chip.Card.Face.color), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(chip.Label.fontSizeMin, Is.GreaterThanOrEqualTo(MinigameUiTheme.MinimumFontSize));
            Assert.That(chip.Label.text, Is.EqualTo("Lượt thi: 5/5"));
            Assert.That(chip.Card.Shadow.rectTransform.offsetMin, Is.EqualTo(ShellTheme.ControlShadow));
        }

        [Test]
        public void BadgeUsesCircleLayersAndKeepsChildrenOnTop()
        {
            var badge = UiKit.Rect(root.transform, "Badge");
            var glyph = UiKit.Rect(badge, "IconGlyph");
            ShellCardHandle handle = ShellKit.Badge(badge, UITheme.Shared.LessonJourney.sprint);
            ShellKit.Badge(badge, UITheme.Shared.LessonJourney.sprint);
            Assert.That(Count(badge, ShellKit.FaceName), Is.EqualTo(1));
            Assert.That(handle.Face.sprite, Is.SameAs(UiKitAssets.Load().Circle));
            Assert.That(handle.Face.color, Is.EqualTo(UITheme.Shared.LessonJourney.sprint));
            Assert.That(glyph.GetSiblingIndex(), Is.GreaterThan(handle.Face.transform.GetSiblingIndex()));
        }

        [TestCase(ShellButtonVariant.Primary)]
        [TestCase(ShellButtonVariant.Secondary)]
        [TestCase(ShellButtonVariant.Danger)]
        public void ButtonVariantsKeepReadableTextOnTheirFace(ShellButtonVariant variant)
        {
            ShellButtonHandle handle = ShellKit.Button(root.transform, "Play", "CHƠI", variant);
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellKit.FaceColor(variant)));
            Assert.That(MinigameUiTheme.ContrastRatio(handle.Label.color, handle.Card.Face.color), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(handle.Label.transform.parent, Is.SameAs(handle.Body));
            Assert.That(handle.Label.fontSizeMin, Is.GreaterThanOrEqualTo(MinigameUiTheme.MinimumFontSize));
            Assert.That(((RectTransform)handle.Button.transform).sizeDelta.y, Is.EqualTo(ShellTheme.ControlHeight));
            Assert.That(handle.Button.transition, Is.EqualTo(Selectable.Transition.None));
            Assert.That(handle.Button.targetGraphic, Is.SameAs(handle.Card.Face));
            Assert.That(handle.Card.Shadow.transform.parent, Is.SameAs(handle.Button.transform), "the shadow does not move");
            Assert.That(handle.Card.Border.transform.parent, Is.SameAs(handle.Body));
        }

        [Test]
        public void StyleButtonIsIdempotentAndKeepsListeners()
        {
            var legacy = new GameObject("RETRYButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            legacy.transform.SetParent(root.transform, false);
            legacy.AddComponent<KitPressFeedback>();
            new GameObject("Face", typeof(RectTransform), typeof(Image)).transform.SetParent(legacy.transform, false);
            var oldLabel = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            oldLabel.transform.SetParent(legacy.transform, false);
            var button = legacy.GetComponent<Button>();
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);

            ShellKit.StyleButton(button, ShellButtonVariant.Primary, "CHƠI LẠI");
            ShellButtonHandle handle = ShellKit.StyleButton(button, ShellButtonVariant.Primary, "CHƠI LẠI");
            button.onClick.Invoke();

            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(legacy.GetComponent<Outline>(), Is.Null);
            Assert.That(legacy.GetComponent<KitPressFeedback>(), Is.Null);
            Assert.That(legacy.transform.Find("Face"), Is.Null);
            Assert.That(Count(legacy.transform, ShellKit.BodyName), Is.EqualTo(1));
            Assert.That(Count(legacy.transform, ShellKit.ShadowName), Is.EqualTo(1));
            Assert.That(Count(handle.Body, ShellKit.FaceName), Is.EqualTo(1));
            Assert.That(legacy.GetComponentsInChildren<TMP_Text>(true), Has.Length.EqualTo(1));
            Assert.That(handle.Label.text, Is.EqualTo("CHƠI LẠI"));
            Assert.That(legacy.GetComponents<ShellPressFeedback>(), Has.Length.EqualTo(1));
            Image hit = legacy.GetComponent<Image>();
            Assert.That(hit.color.a, Is.LessThan(.01f));
            Assert.That(hit.raycastTarget, Is.True);
        }

        [Test]
        public void IconButtonIsASquareSecondaryWithTheGlyph()
        {
            ShellButtonHandle back = ShellKit.IconButton(root.transform, "BackButton", "‹");
            Assert.That(((RectTransform)back.Button.transform).sizeDelta, Is.EqualTo(Vector2.one * ShellTheme.IconButtonSize));
            Assert.That(back.Card.Face.color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(back.Label.text, Is.EqualTo("‹"));
        }

        [Test]
        public void TitleDrawsAGoldDropBehindInkOrWarningText()
        {
            ShellTitleHandle title = ShellKit.Title(root.transform, "TitleMain", "CHƯA QUA HỌC PHẦN", 84f, warning: true);
            Assert.That(title.Text.color, Is.EqualTo(ShellTheme.TitleRed));
            Assert.That(title.Drop.color, Is.EqualTo(ShellTheme.Gold));
            Assert.That(title.Drop.transform.GetSiblingIndex(), Is.LessThan(title.Text.transform.GetSiblingIndex()));
            Assert.That(title.Drop.rectTransform.offsetMin, Is.EqualTo(ShellTheme.TitleDrop));
            Assert.That(title.Text.enableVertexGradient, Is.False);
            Assert.That(title.Text.font, Is.SameAs(VietTypography.Library.title));
            ShellKit.SetTitleText(title.Root, "HẾT LƯỢT");
            Assert.That(title.Text.text, Is.EqualTo("HẾT LƯỢT"));
            Assert.That(title.Drop.text, Is.EqualTo("HẾT LƯỢT"));
        }

        [Test]
        public void ToggleAndSliderUseShellLayersAndDriveTheirGraphics()
        {
            ShellToggleHandle toggle = ShellKit.Toggle(root.transform, "VibrationToggle");
            Assert.That(toggle.Toggle.graphic, Is.SameAs(toggle.Check));
            Assert.That(toggle.Check.color, Is.EqualTo(ShellTheme.Gold));
            Assert.That(toggle.Track.Face.color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(MinigameUiTheme.ContrastRatio(toggle.Value.color, ShellTheme.Gold), Is.GreaterThanOrEqualTo(4.5f));

            ShellSliderHandle slider = ShellKit.Slider(root.transform, "MusicSlider");
            Assert.That(slider.Slider.fillRect, Is.SameAs(slider.Fill.rectTransform));
            Assert.That(slider.Slider.handleRect, Is.SameAs(slider.Knob.rectTransform));
            Assert.That(slider.Knob.color, Is.EqualTo(ShellTheme.Ink));
            slider.Slider.value = .25f;
            Assert.That(slider.Fill.rectTransform.anchorMax.x, Is.EqualTo(.25f).Within(.001f));
        }
    }
}
