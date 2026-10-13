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
    }
}
