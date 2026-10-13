# Shell UI Style Family Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every screen outside the minigames (Menu confirm card, Settings, Map, lesson popup, journey dialogue, scene transition, GameOver, Splash) is drawn with one named "Shell" style taken from the menu key art — cream face, 6 px navy border, hard navy offset shadow — built by a shared `ShellKit` and guarded by a `ShellStyleAudit`; minigames keep the `UiKit` family, and both families read one token source in `UITheme`.

**Architecture:**
- Tokens: a new serialized `UITheme.ShellStyle` group plus a static `ShellTheme` facade (same pattern as `MinigameUiTheme`). `MenuStyle`/`HomeMenuStyle` are retired once every caller has moved.
- `ShellKit` (static, next to `UiKit` in `Scripts/UI/Kit/`) draws every Shell element as layered Images using the baked `UiKitAssets` sprites: `ShellShadow` → `ShellBorder` → `ShellFace`. Buttons put border, face and label under a `Body` child that `ShellPressFeedback` slides onto the shadow when pressed. No `Outline`/`Shadow` components.
- `ShellStyleAudit` (Shell rules) and `MinigameStyleAudit` (Kit rules) share helpers in `StyleAuditShared`; a `StyleExempt` marker component tells both audits to skip content art (icons, portraits, badges, illustrations).
- Scenes change only through builders + `KMA/Presentation/Author Shell Scenes` (`ShellSceneAuthoring.Apply`) and the minigame configurators.

**Tech Stack:** Unity 6000.3.23f1, C# (asmdef-split), uGUI + TextMeshPro, NUnit EditMode/PlayMode via `tools/run-unity-tests.sh`, `tools/qa-screenshot.sh` (visual QA).

**Spec:** `docs/superpowers/specs/2026-10-13-shell-ui-style-design.md`

## Adjustments to the spec (found while reading the code)

Executors follow the plan where it differs from the spec:

1. **No `RoundRect12` bake.** `UiKit.SetRadius` already scales the baked 24 px sprite to any radius with `pixelsPerUnitMultiplier`; radius 12 works without a new sprite.
2. **Type scale stays as `const` in `MinigameUiTheme`.** Moving it to serialized fields would break every `const`/default-parameter use; `ShellKit` reads the same constants, so there is still one scale.
3. **`map*` colours stay where they are** in `UITheme`; `ShellTheme.Palette()` lists them. Moving them is churn with no visible effect.
4. **`success`, `muted`, `mutedForeground`, `border`, `spacing` are not deleted.** `success` and `mutedForeground` are used (lesson popup, FrogJump power bar, HUD); deleting the rest is out of scope.
5. **`titleAngle` and the slant angle are still needed** (Splash title, Splash/transition loading bars). They move from `MenuStyle` into `ShellStyle` instead of being deleted.
6. **`SlantSprite` stays a runtime generator** (it is rasterised per target size); it moves from `HomePresentationBuilder` to a new `ShellArt` class with the shared badge. Splash and the transition bar are audited with relaxed options (sprites unchecked).
7. **Namespaces:** Shell code lives in `Assets/_Project/Scripts/UI/Kit/` under `namespace KMA.UI.Kit`, not a new `Scripts/UI/Shell/` folder, because `Scripts/Shell/` is already the `KMA.Gameplay.Shell` assembly and a second "Shell" namespace would be confusing.
8. **Volleyball ĐÁNH/NHẢY** keep their icon glyphs (an existing test pins "controls use icons without face labels"): they get the Kit round-button shape (`UiKit.RoundIconButton`, a label-less `RoundButton`) with the glyphs drawn on its face.
9. **Shell button labels use `VietFontRole.Hud`** (Barlow, plain material). The `ButtonPrimary/Secondary` materials carry an outline meant for the dark Kit buttons.
10. **Danger buttons use `ink` text on `accentRed`** (contrast ≈ 5:1); cream on `#FF595E` is ≈ 3:1.

## Global Constraints

- No gameplay change: rules, timings, input, scene flow and navigation stay identical. Every existing `onClick` keeps working after restyling (`StyleButton` never touches listeners).
- Shell tokens (verbatim): `cream #FBF4E4`, `ink #0B2A4A`, `gold #F9C82B`, `goldLight (255,224,102)`, `goldDark (255,180,0)`, `titleRed #C62828`, `accentRed = UITheme.primary #FF595E`, `bodyText #33475E`, `disabledFace #E6E1D6`, `disabledText #7F8998`, `cardBorder 6`, `controlBorder 4`, `cardRadius 20`, `controlRadius 12`, `cardShadow (8,-8)`, `controlShadow (5,-5)`, `pressedShift = controlShadow`, `titleDrop (3,-3)`, `titleAngle -8`, `slantAngle 12.094757`, control height `84`, icon button `88`.
- Type scale (one for Kit and Shell): Display 160 / Title 54 / Headline 48 / BodyLarge 40 / Body 32 / Caption 28; minimum 28 everywhere except Splash (minimum 24).
- Minigames never call `ShellKit`; Shell screens never call `UiKit.Button`/`UiKit.Panel`/`UiKit.StyleButton`. (`UiKit.Rect/Stretch/Place/Anchor/SetRadius/Shape/Disc/GetOrAdd/FitLabel` are shared geometry helpers and allowed everywhere.)
- No `Outline` or `Shadow` component on any Shell element. TMP `outlineWidth` on text over scenery (map stop names) is allowed.
- UI copy is Vietnamese; every TMP string goes through `VietText.Fix`.
- Scenes are only changed by running `"$UNITY" -batchmode -projectPath . -executeMethod KMA.EditorTools.ShellSceneAuthoring.Apply -quit -logFile Builds/<name>.log` (Bootstrap, Menu, Map, GameOver) or a minigame configurator (`KMA.EditorTools.VolleyballSceneConfigurator.BuildScene`); never hand-edit `.unity` YAML. Editing scalar values in `Assets/_Project/Settings/UI/UITheme.asset` is allowed.
- EditMode tests that open/build scenes re-serialize scenes, prefabs and TMP font assets. After each test run, `git status` and `git checkout -- <path>` every scene/prefab/font file the current task did not intend to change; never commit `Assets/Fonts/TMP/*.asset` churn.
- Commit directly to `master`. Commit messages carry **no** `Co-Authored-By` trailer (user memory rule). Always `git add` explicit paths.
- Unity must be closed for batch runs (check `tasklist //FI "IMAGENAME eq Unity.exe"`; never kill an Editor you did not start).
  - Tests: `tools/run-unity-tests.sh <EditMode|PlayMode> <filter> <name>`, then read `Builds/TestResults/<name>.xml` (Unity can exit 0 on failures). `<filter>` is a test class or namespace-qualified name, e.g. `KMA.Tests.Presentation.ShellKitTests`.
  - Editor methods: `UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe"`, then the `-executeMethod` line above; check with `grep -E "error|Exception" Builds/<name>.log`.
  - Screenshots: a GUI Editor (not batchmode) must be running, see `.claude/skills/testing-unity-ui-with-screenshots/SKILL.md`; then `tools/qa-screenshot.sh <out.png> <scene.unity> <waitSeconds> ...`. Close it before the next batch run.

## Review Focus

1. **A disabled Shell button must stay readable and must not slide.** Continue on a fresh save, the lesson PLAY button on a locked stage ("KHÓA"): face `disabledFace`, label `disabledText`, no press shift. Pinned in Task 3 (`DisabledButtonPaintsDisabledTokensAndIgnoresPresses`).
2. **Rebuilding a scene twice** (authoring is re-run often, `MapPresentationBuilder.Build` runs again at runtime on a baked scene) must not stack layers or accents. Pinned in Task 2 (`StyleCardIsIdempotentAndUndecorates`), Task 3 (`StyleButtonIsIdempotentAndKeepsListeners`) and Task 13 (`ShellSceneAuthoring.Validate` object-count check).
3. **Raising the 28 px floor can overflow small boxes** (map stop tag, lives timer, dialogue hint). Text must shrink-to-fit, never overflow its rect at 16:9 and 20:9. Pinned in Task 9 (`MapStopTagFitsAtTheFontFloor`), Task 13 (`ShellScreensHaveNoTextBelowTheFloor`) and Task 15 (20:9 captures).
4. **A scene baked with the old style** (Map with `BrutalButton`, GameOver with the old cream card) must be re-authored, otherwise it carries missing scripts or stale layers. Pinned in Task 9 (Map re-authored, `MapSceneHasNoMissingScripts`) and Task 6 (`AnOldCreamCardIsReplacedAndKeepsItsButtons`).
5. **Content art must not be "fixed" into tokens** (sport icons, hearts, stars, portraits, the sport badge). Every such image carries `StyleExempt`. Pinned in Task 4 (`ExemptSubtreesAreSkipped`) and by the scene audits in Task 13 passing without recolouring icons.

---

## File Structure

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/UI/UITheme.cs` (modify) | New `ShellStyle` group; later remove `MenuStyle` |
| `Assets/_Project/Scripts/UI/Kit/ShellTheme.cs` (new) | Static facade over `UITheme.Shared.Shell` + `Palette()` |
| `Assets/_Project/Scripts/UI/Kit/StyleExempt.cs` (new) | Marker: audits skip this subtree |
| `Assets/_Project/Scripts/UI/Kit/ShellHandles.cs` (new) | `ShellButtonVariant` + handle structs |
| `Assets/_Project/Scripts/UI/Kit/ShellKit.cs` (new) | Factories/restylers for every Shell element |
| `Assets/_Project/Scripts/UI/Kit/ShellPressFeedback.cs` (new) | Press slide + disabled paint for Shell buttons |
| `Assets/_Project/Scripts/UI/Kit/StyleAuditShared.cs` (new) | Path, palette, text and exemption helpers |
| `Assets/_Project/Scripts/UI/Kit/ShellStyleAudit.cs` (new) | Shell rules |
| `Assets/_Project/Scripts/UI/Kit/MinigameStyleAudit.cs` (modify) | Uses shared helpers, honours `StyleExempt`, bans `Outline`/`Shadow` outside `Bar`/`Countdown` |
| `Assets/_Project/Scripts/UI/ShellArt.cs` (new) | Sport badge + slanted bar sprite, moved from `HomePresentationBuilder` |
| `Assets/_Project/Scripts/UI/GameOverPresentationBuilder.cs` | Card, titles, stats, buttons via `ShellKit` |
| `Assets/_Project/Scripts/UI/HomePresentationBuilder.cs`, `HomeKeyArtButton.cs`, `MainMenuScreen.cs` | Tokens; Xiên family removed |
| `Assets/_Project/Scripts/Shell/S5ShellSceneController.cs` | New-game confirm card via `ShellKit` |
| `Assets/_Project/Scripts/UI/SettingsPresentationBuilder.cs` | Card, slider, toggle, back via `ShellKit` |
| `Assets/_Project/Scripts/UI/MapPresentationBuilder.cs`, `MapStopBuilder.cs`, `MapNodeView.cs`, `MapJourneyPathLayout.cs` | Header, summary, stops via `ShellKit`; `BrutalButton` gone |
| `Assets/_Project/Scripts/UI/JourneyLessonPresentation.cs`, `JourneyLessonList.cs` | Popup on a cream card |
| `Assets/_Project/Scripts/UI/JourneyDialoguePresenter.cs` | Dialogue box, name tag, sticker via `ShellKit` |
| `Assets/_Project/Scripts/UI/SceneTransitionOverlay.cs`, `SplashPresentationView.cs` | Tokens, `ShellKit.Card`, `ShellArt` |
| `Assets/_Project/Scripts/UI/Kit/UiKit.cs` | `RoundIconButton`; drop `BrutalButton` branch |
| `Assets/Editor/VolleyballSceneConfigurator.cs` | ĐÁNH/NHẢY on `UiKit.RoundIconButton` |
| `Assets/Editor/ShellSceneAuthoring.cs` | GameOver authored with `Rebuild` |
| Deleted: `BrutalButton.cs`, `HomeMenuButton.cs`, `HomeMenuStyle.cs` (+ `.meta`) | |
| Tests (new): `Assets/Tests/EditMode/Presentation/ShellThemeTests.cs`, `ShellKitTests.cs`, `ShellPressFeedbackTests.cs`, `ShellStyleAuditTests.cs`; `Assets/Tests/EditMode/EditorTools/ShellStyleConsistencyTests.cs` | |

---

### Task 1: Shell tokens and the `StyleExempt` marker

**Files:**
- Modify: `Assets/_Project/Scripts/UI/UITheme.cs` (add `ShellStyle` after `MenuStyle`, field + accessor after `menu`)
- Create: `Assets/_Project/Scripts/UI/Kit/ShellTheme.cs`
- Create: `Assets/_Project/Scripts/UI/Kit/StyleExempt.cs`
- Test: `Assets/Tests/EditMode/Presentation/ShellThemeTests.cs`

**Interfaces:**
- Produces: `UITheme.ShellStyle` (public fields below), `UITheme.Shell`; `ShellTheme.{Cream, Ink, Gold, GoldLight, GoldDark, TitleRed, AccentRed, BodyText, DisabledFace, DisabledText, White}` (`Color`), `ShellTheme.{CardBorder, ControlBorder, CardRadius, ControlRadius, TitleAngle, SlantAngle}` (`float`), `ShellTheme.{CardShadow, ControlShadow, PressedShift, TitleDrop}` (`Vector2`), consts `ShellTheme.ControlHeight = 84f`, `IconButtonSize = 88f`, `BadgeSize = 132f`, `AccentSquare = 30f`; `ShellTheme.Palette()` → `Color[]`; `StyleExempt` (MonoBehaviour).

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Tests/EditMode/Presentation/ShellThemeTests.cs
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    public sealed class ShellThemeTests
    {
        [Test]
        public void ShellTokensMatchTheKeyArt()
        {
            Assert.That((Color32)ShellTheme.Cream, Is.EqualTo(new Color32(0xFB, 0xF4, 0xE4, 0xFF)));
            Assert.That((Color32)ShellTheme.Ink, Is.EqualTo(new Color32(0x0B, 0x2A, 0x4A, 0xFF)));
            Assert.That((Color32)ShellTheme.Gold, Is.EqualTo(new Color32(0xF9, 0xC8, 0x2B, 0xFF)));
            Assert.That(ShellTheme.AccentRed, Is.EqualTo(UITheme.Shared.Primary));
            Assert.That(ShellTheme.CardBorder, Is.EqualTo(6f));
            Assert.That(ShellTheme.ControlBorder, Is.EqualTo(4f));
            Assert.That(ShellTheme.CardRadius, Is.EqualTo(20f));
            Assert.That(ShellTheme.ControlRadius, Is.EqualTo(12f));
            Assert.That(ShellTheme.CardShadow, Is.EqualTo(new Vector2(8f, -8f)));
            Assert.That(ShellTheme.ControlShadow, Is.EqualTo(new Vector2(5f, -5f)));
            Assert.That(ShellTheme.PressedShift, Is.EqualTo(ShellTheme.ControlShadow));
        }

        [Test]
        public void ShellTextColoursReadOnTheirFaces()
        {
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.Ink, ShellTheme.Cream), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.Ink, ShellTheme.Gold), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.Ink, ShellTheme.AccentRed), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.BodyText, ShellTheme.Cream), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.TitleRed, ShellTheme.White), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(MinigameUiTheme.ContrastRatio(ShellTheme.DisabledText, ShellTheme.DisabledFace), Is.GreaterThanOrEqualTo(2.5f),
                "disabled text stays legible, just quieter");
        }

        [Test]
        public void PaletteHoldsEveryShellToken()
        {
            Color[] palette = ShellTheme.Palette();
            foreach (Color token in new[] { ShellTheme.Cream, ShellTheme.Ink, ShellTheme.Gold, ShellTheme.TitleRed,
                         ShellTheme.AccentRed, ShellTheme.BodyText, ShellTheme.DisabledFace, ShellTheme.DisabledText,
                         ShellTheme.White, UITheme.Shared.Scrim, UITheme.Shared.LessonJourney.sprint })
                Assert.That(System.Array.Exists(palette, c => MinigameUiTheme.RgbEquals(c, token)), Is.True, token.ToString());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.ShellThemeTests shell-theme`
Expected: compilation error (`ShellTheme` does not exist) → no `<test-run>` passed in `Builds/TestResults/shell-theme.xml`.

- [ ] **Step 3: Add `ShellStyle` to `UITheme`**

In `UITheme.cs`, after the `MenuStyle` class, add:

```csharp
        /// The look of every screen outside the minigames, taken from the menu key art:
        /// cream faces in a navy border over a hard navy offset shadow.
        [Serializable]
        public sealed class ShellStyle
        {
            public Color cream = new Color32(0xFB, 0xF4, 0xE4, 0xFF);
            public Color ink = new Color32(0x0B, 0x2A, 0x4A, 0xFF);
            public Color gold = new Color32(0xF9, 0xC8, 0x2B, 0xFF);
            public Color goldLight = new Color32(255, 224, 102, 255);
            public Color goldDark = new Color32(255, 180, 0, 255);
            public Color titleRed = new Color32(0xC6, 0x28, 0x28, 0xFF);
            public Color bodyText = new Color32(0x33, 0x47, 0x5E, 0xFF);
            public Color disabledFace = new Color32(0xE6, 0xE1, 0xD6, 0xFF);
            public Color disabledText = new Color32(0x8A, 0x94, 0xA3, 0xFF);
            [Min(0f)] public float cardBorder = 6f;
            [Min(0f)] public float controlBorder = 4f;
            [Min(0f)] public float cardRadius = 20f;
            [Min(0f)] public float controlRadius = 12f;
            public Vector2 cardShadow = new Vector2(8f, -8f);
            public Vector2 controlShadow = new Vector2(5f, -5f);
            public Vector2 titleDrop = new Vector2(3f, -3f);
            [Range(-45f, 45f)] public float titleAngle = -8f;
            [Range(0f, 40f)] public float slantAngle = 12.094757f;
        }
```

After `[SerializeField] private MenuStyle menu = new MenuStyle();` add:

```csharp
        [Header("Shell screens (everything outside the minigames)")]
        [SerializeField] private ShellStyle shell = new ShellStyle();
        public ShellStyle Shell => shell;
```

- [ ] **Step 4: Create `ShellTheme` and `StyleExempt`**

```csharp
// Assets/_Project/Scripts/UI/Kit/ShellTheme.cs
using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.UI.Kit
{
    /// Design tokens for every screen outside the minigames. Reads the shared ScriptableObject; owns no scene state.
    public static class ShellTheme
    {
        static UITheme.ShellStyle Style => UITheme.Shared.Shell;

        public static Color Cream => Style.cream;
        public static Color Ink => Style.ink;
        public static Color Gold => Style.gold;
        public static Color GoldLight => Style.goldLight;
        public static Color GoldDark => Style.goldDark;
        public static Color TitleRed => Style.titleRed;
        public static Color AccentRed => UITheme.Shared.Primary;
        public static Color BodyText => Style.bodyText;
        public static Color DisabledFace => Style.disabledFace;
        public static Color DisabledText => Style.disabledText;
        public static Color White => UITheme.Shared.Card;

        public static float CardBorder => Style.cardBorder;
        public static float ControlBorder => Style.controlBorder;
        public static float CardRadius => Style.cardRadius;
        public static float ControlRadius => Style.controlRadius;
        public static Vector2 CardShadow => Style.cardShadow;
        public static Vector2 ControlShadow => Style.controlShadow;
        public static Vector2 PressedShift => Style.controlShadow;
        public static Vector2 TitleDrop => Style.titleDrop;
        public static float TitleAngle => Style.titleAngle;
        public static float SlantAngle => Style.slantAngle;

        public const float ControlHeight = 84f;
        public const float IconButtonSize = 88f;
        public const float BadgeSize = 132f;
        public const float AccentSquare = 30f;

        /// Every colour a Shell screen may draw with, compared without alpha.
        public static Color[] Palette()
        {
            UITheme theme = UITheme.Shared;
            UITheme.LessonJourneyStyle journey = theme.LessonJourney;
            return new[]
            {
                Cream, Ink, Gold, GoldLight, GoldDark, TitleRed, AccentRed, BodyText, DisabledFace, DisabledText, White,
                theme.Scrim, theme.Accent, theme.Success, theme.MutedForeground, theme.MapHint, theme.MapLivesSurface,
                theme.MapLockedBorder, theme.MapReadyText, theme.MapActionText, theme.MapLockIcon, theme.MapButtonHighlight,
                theme.MapCompleteBorder, theme.MapLockedCard, theme.MapLockedIcon, theme.MapLockedText,
                journey.sprint, journey.volleyball, journey.football, journey.chess,
            };
        }
    }
}
```

```csharp
// Assets/_Project/Scripts/UI/Kit/StyleExempt.cs
using UnityEngine;

namespace KMA.UI.Kit
{
    /// Marks content art (sport icons, hearts, stars, portraits, badges, illustrations) that the style
    /// audits skip, together with its children. Panels and buttons never carry it.
    [DisallowMultipleComponent]
    public sealed class StyleExempt : MonoBehaviour { }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.ShellThemeTests shell-theme`
Expected: 3 passed, 0 failed in `Builds/TestResults/shell-theme.xml`.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/UITheme.cs Assets/_Project/Scripts/UI/Kit/ShellTheme.cs Assets/_Project/Scripts/UI/Kit/ShellTheme.cs.meta Assets/_Project/Scripts/UI/Kit/StyleExempt.cs Assets/_Project/Scripts/UI/Kit/StyleExempt.cs.meta Assets/Tests/EditMode/Presentation/ShellThemeTests.cs Assets/Tests/EditMode/Presentation/ShellThemeTests.cs.meta
git commit -m "feat(ui): add Shell style tokens and the style-audit exemption marker"
```

(Unity writes the `.meta` files on import during the test run; if one is missing, open-and-close the project in batch mode once: the test run already does this.)

---

### Task 2: `ShellKit` surfaces — card, tile, chip, badge

**Files:**
- Create: `Assets/_Project/Scripts/UI/Kit/ShellHandles.cs`
- Create: `Assets/_Project/Scripts/UI/Kit/ShellKit.cs`
- Test: `Assets/Tests/EditMode/Presentation/ShellKitTests.cs`

**Interfaces:**
- Consumes: `ShellTheme` (Task 1), `UiKit.Rect/Stretch/Shape/SetRadius/Disc/GetOrAdd/FitLabel`, `UiKitAssets.Load().Circle`.
- Produces:
  - `public enum ShellButtonVariant { Primary, Secondary, Danger }`
  - `public readonly struct ShellCardHandle { RectTransform Root; Image Shadow; Image Border; Image Face; }` (ctor in that order)
  - `public readonly struct ShellChipHandle { ShellCardHandle Card; TMP_Text Label; }`
  - `ShellKit.ShadowName = "ShellShadow"`, `BorderName = "ShellBorder"`, `FaceName = "ShellFace"`, `BodyName = "Body"`, `AccentPrefix = "ShellAccent"`
  - `ShellCardHandle ShellKit.StyleSurface(RectTransform root, float radius, float border, Vector2 shadow, Color face)`
  - `ShellCardHandle ShellKit.Card(Transform parent, string name, bool decorated = false)`
  - `ShellCardHandle ShellKit.StyleCard(RectTransform root, bool decorated = false)`
  - `ShellCardHandle ShellKit.Tile(Transform parent, string name, Color? face = null)` / `StyleTile(RectTransform root, Color? face = null)` — control radius/border, no shadow, white face by default
  - `ShellChipHandle ShellKit.Chip(Transform parent, string name, string text, Color? face = null)`
  - `ShellCardHandle ShellKit.Badge(RectTransform root, Color face)` — circle layers on an existing rect

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/Presentation/ShellKitTests.cs
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.ShellKitTests shell-kit`
Expected: compilation error (`ShellKit` does not exist).

- [ ] **Step 3: Create the handles**

```csharp
// Assets/_Project/Scripts/UI/Kit/ShellHandles.cs
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
```

`ShellButtonHandle` references `ShellPressFeedback` (Task 3). To keep this task compiling, create the class now as an empty shell and fill it in Task 3:

```csharp
// Assets/_Project/Scripts/UI/Kit/ShellPressFeedback.cs  (placeholder body, completed in Task 3)
using UnityEngine;

namespace KMA.UI.Kit
{
    public sealed class ShellPressFeedback : MonoBehaviour { }
}
```

- [ ] **Step 4: Create `ShellKit` with the surface factories**

```csharp
// Assets/_Project/Scripts/UI/Kit/ShellKit.cs
using KMA.Gameplay.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    /// Builds and restyles the shared widgets of every screen outside the minigames, in the menu key art
    /// style: a cream face in a navy border over a hard navy offset shadow. Owns look and press
    /// feedback; callers own position and size. Every call is idempotent.
    public static class ShellKit
    {
        public const string ShadowName = "ShellShadow";
        public const string BorderName = "ShellBorder";
        public const string FaceName = "ShellFace";
        public const string BodyName = "Body";
        public const string AccentPrefix = "ShellAccent";

        /// Layers shadow, border and face as the first three children of root, keeping any content above them.
        public static ShellCardHandle StyleSurface(RectTransform root, float radius, float border, Vector2 shadow, Color face)
        {
            Image shadowImage = Layer(root, ShadowName, 0, radius, ShellTheme.Ink);
            UiKit.Stretch(shadowImage.rectTransform, shadow, shadow);
            Image borderImage = Layer(root, BorderName, 1, radius, ShellTheme.Ink);
            UiKit.Stretch(borderImage.rectTransform);
            Image faceImage = Layer(root, FaceName, 2, Mathf.Max(0f, radius - border), face);
            UiKit.Stretch(faceImage.rectTransform, Vector2.one * border, -Vector2.one * border);
            return new ShellCardHandle(root, shadowImage, borderImage, faceImage);
        }

        public static ShellCardHandle Card(Transform parent, string name, bool decorated = false) =>
            StyleCard(UiKit.Rect(parent, name), decorated);

        public static ShellCardHandle StyleCard(RectTransform root, bool decorated = false)
        {
            ShellCardHandle card = StyleSurface(root, ShellTheme.CardRadius, ShellTheme.CardBorder, ShellTheme.CardShadow,
                ShellTheme.Cream);
            Decorate(root, decorated);
            return card;
        }

        public static ShellCardHandle Tile(Transform parent, string name, Color? face = null) =>
            StyleTile(UiKit.Rect(parent, name), face);

        public static ShellCardHandle StyleTile(RectTransform root, Color? face = null) =>
            StyleSurface(root, ShellTheme.ControlRadius, ShellTheme.ControlBorder, Vector2.zero, face ?? ShellTheme.White);

        public static ShellChipHandle Chip(Transform parent, string name, string text, Color? face = null)
        {
            RectTransform rect = UiKit.Rect(parent, name);
            ShellCardHandle card = StyleSurface(rect, ShellTheme.ControlRadius, ShellTheme.ControlBorder,
                ShellTheme.ControlShadow, face ?? ShellTheme.Cream);
            TMP_Text label = Label(rect, "Label", text, MinigameUiTheme.Body, ShellTheme.Ink, VietFontRole.Hud);
            UiKit.Stretch(label.rectTransform,
                new Vector2(MinigameUiTheme.SpaceSm + ShellTheme.ControlBorder, ShellTheme.ControlBorder),
                new Vector2(-MinigameUiTheme.SpaceSm - ShellTheme.ControlBorder, -ShellTheme.ControlBorder));
            return new ShellChipHandle(card, label);
        }

        /// Circle layers for a round badge (map stops): drop disc, navy rim and a coloured face.
        public static ShellCardHandle Badge(RectTransform root, Color face)
        {
            Image shadow = Circle(root, ShadowName, 0, ShellTheme.Ink);
            UiKit.Stretch(shadow.rectTransform, ShellTheme.ControlShadow, ShellTheme.ControlShadow);
            Image border = Circle(root, BorderName, 1, ShellTheme.Ink);
            UiKit.Stretch(border.rectTransform);
            Image faceImage = Circle(root, FaceName, 2, face);
            UiKit.Stretch(faceImage.rectTransform, Vector2.one * ShellTheme.CardBorder, -Vector2.one * ShellTheme.CardBorder);
            return new ShellCardHandle(root, shadow, border, faceImage);
        }

        /// A bold label on the shared type scale, never below the font floor.
        public static TMP_Text Label(Transform parent, string name, string text, float size, Color color,
            VietFontRole role = VietFontRole.BodyBold, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = UiKit.Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = VietText.Fix(text);
            label.fontSize = Mathf.Max(size, MinigameUiTheme.MinimumFontSize);
            label.alignment = alignment;
            label.raycastTarget = false;
            VietTypography.Apply(label, role);
            label.enableVertexGradient = false;
            label.color = color;
            UiKit.FitLabel(label, label.fontSize);
            return label;
        }

        static void Decorate(RectTransform root, bool decorated)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name.StartsWith(AccentPrefix))
                    Destroy(child.gameObject);
            }
            if (!decorated) return;
            AccentSquare(root, AccentPrefix + "Top", new Vector2(0f, 1f), new Vector2(ShellTheme.AccentSquare * 2f, 0f));
            AccentSquare(root, AccentPrefix + "Bottom", new Vector2(1f, 0f), new Vector2(-ShellTheme.AccentSquare * 2f, 0f));
        }

        static void AccentSquare(RectTransform root, string name, Vector2 anchor, Vector2 position)
        {
            RectTransform square = UiKit.Rect(root, name);
            UiKit.Place(square, anchor, new Vector2(.5f, .5f), position, Vector2.one * ShellTheme.AccentSquare);
            UiKit.GetOrAdd<LayoutElement>(square.gameObject).ignoreLayout = true;
            StyleSurface(square, 3f, ShellTheme.ControlBorder * .75f, Vector2.zero, ShellTheme.AccentRed);
            square.SetSiblingIndex(3);
        }

        static Image Layer(RectTransform root, string name, int index, float radius, Color color)
        {
            Transform existing = root.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : UiKit.Shape(root, name, radius, color);
            UiKit.SetRadius(image, radius);
            return Finish(image, index, color);
        }

        static Image Circle(RectTransform root, string name, int index, Color color)
        {
            Transform existing = root.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : UiKit.Disc(root, name, false, color);
            image.sprite = UiKitAssets.Load().Circle;
            image.type = Image.Type.Simple;
            return Finish(image, index, color);
        }

        static Image Finish(Image image, int index, Color color)
        {
            image.color = color;
            image.raycastTarget = false;
            UiKit.GetOrAdd<LayoutElement>(image.gameObject).ignoreLayout = true;
            image.transform.SetSiblingIndex(index);
            return image;
        }

        static void Destroy(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
```

Note: when `Destroy` runs in Play Mode, the old accent is only removed at the end of the frame; the new accents are named the same. In Play Mode, rename the old one first (`child.name = "Stale"`) before destroying, so `Find`-based callers never see it:

```csharp
                if (child.name.StartsWith(AccentPrefix))
                {
                    child.name = "Stale";
                    Destroy(child.gameObject);
                }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.ShellKitTests shell-kit`
Expected: 6 passed, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/Kit/ShellHandles.cs Assets/_Project/Scripts/UI/Kit/ShellHandles.cs.meta Assets/_Project/Scripts/UI/Kit/ShellKit.cs Assets/_Project/Scripts/UI/Kit/ShellKit.cs.meta Assets/_Project/Scripts/UI/Kit/ShellPressFeedback.cs Assets/_Project/Scripts/UI/Kit/ShellPressFeedback.cs.meta Assets/Tests/EditMode/Presentation/ShellKitTests.cs Assets/Tests/EditMode/Presentation/ShellKitTests.cs.meta
git commit -m "feat(ui): add ShellKit cards, tiles, chips and badges"
```

---

### Task 3: `ShellKit` buttons, titles, toggle, slider and `ShellPressFeedback`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/Kit/ShellKit.cs` (append)
- Modify: `Assets/_Project/Scripts/UI/Kit/ShellPressFeedback.cs` (replace placeholder)
- Test: `Assets/Tests/EditMode/Presentation/ShellKitTests.cs` (append), `Assets/Tests/EditMode/Presentation/ShellPressFeedbackTests.cs` (new)

**Interfaces:**
- Consumes: Task 2 surface API.
- Produces:
  - `ShellButtonHandle ShellKit.Button(Transform parent, string name, string text, ShellButtonVariant variant)`
  - `ShellButtonHandle ShellKit.StyleButton(UnityEngine.UI.Button button, ShellButtonVariant variant, string text = null)` — label ends up at `<button>/Body/<label>`; legacy `Fill`, `Face`, `Icon`, `HoverArrow`, `DisabledShade` children and `KitPressFeedback`/`HomeKeyArtButton`/`Outline`/`Shadow` components are removed; the root `Image` becomes a clear hit area.
  - `ShellButtonHandle ShellKit.IconButton(Transform parent, string name, string glyph)` — square `IconButtonSize`, Secondary, Symbol font at `Headline`
  - `Color ShellKit.FaceColor(ShellButtonVariant)`, `Color ShellKit.LabelColor(ShellButtonVariant)`
  - `ShellTitleHandle ShellKit.Title(Transform parent, string name, string text, float size, bool warning = false)` — children `Drop` (gold, offset `TitleDrop`) and `Text` (ink, or `titleRed` when `warning`)
  - `void ShellKit.SetTitleText(Transform titleRoot, string text)`
  - `ShellToggleHandle ShellKit.Toggle(Transform parent, string name)`, `ShellSliderHandle ShellKit.Slider(Transform parent, string name)`
  - `ShellPressFeedback.Configure(RectTransform body, Graphic face, TMP_Text label, Color faceRest, Color labelRest)`, `Press()`, `Release()`, `Tick(float)`, `bool IsPressed`, `Vector2 Offset`

- [ ] **Step 1: Write the failing tests**

Append to `ShellKitTests`:

```csharp
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
```

```csharp
// Assets/Tests/EditMode/Presentation/ShellPressFeedbackTests.cs
using KMA.UI.Kit;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    public sealed class ShellPressFeedbackTests
    {
        GameObject root;

        [SetUp] public void SetUp() => root = new GameObject("PressRoot", typeof(RectTransform));
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void PressSlidesTheBodyOntoTheShadowAndReleaseEasesBack()
        {
            ShellButtonHandle handle = ShellKit.Button(root.transform, "Play", "CHƠI", ShellButtonVariant.Primary);
            handle.Feedback.Press();
            Assert.That(handle.Feedback.IsPressed, Is.True);
            Assert.That(handle.Feedback.Offset, Is.EqualTo(ShellTheme.PressedShift));
            handle.Feedback.Tick(1f);
            Assert.That(handle.Feedback.Offset, Is.EqualTo(ShellTheme.PressedShift), "held presses stay down");

            handle.Feedback.Release();
            handle.Feedback.Tick(MinigameUiTheme.PressRestoreSeconds * .5f);
            Assert.That(handle.Feedback.Offset.magnitude, Is.GreaterThan(0f).And.LessThan(ShellTheme.PressedShift.magnitude));
            handle.Feedback.Tick(MinigameUiTheme.PressRestoreSeconds);
            Assert.That(handle.Feedback.Offset, Is.EqualTo(Vector2.zero));
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellTheme.Gold));
        }

        [Test]
        public void DisabledButtonPaintsDisabledTokensAndIgnoresPresses()
        {
            ShellButtonHandle handle = ShellKit.Button(root.transform, "Continue", "TIẾP TỤC", ShellButtonVariant.Secondary);
            handle.Button.interactable = false;
            handle.Feedback.Tick(0f);
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellTheme.DisabledFace));
            Assert.That(handle.Label.color, Is.EqualTo(ShellTheme.DisabledText));
            handle.Feedback.Press();
            Assert.That(handle.Feedback.IsPressed, Is.False);
            Assert.That(handle.Feedback.Offset, Is.EqualTo(Vector2.zero));

            handle.Button.interactable = true;
            handle.Feedback.Tick(0f);
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(handle.Label.color, Is.EqualTo(ShellTheme.Ink));
        }

        [Test]
        public void PressSlidesFromAnOffCentreRestPosition()
        {
            var badge = UiKit.Rect(root.transform, "Badge");
            badge.anchoredPosition = new Vector2(0f, -120f);
            var feedback = root.AddComponent<ShellPressFeedback>();
            feedback.Configure(badge, null, null, Color.white, Color.white);
            Assert.That(badge.anchoredPosition, Is.EqualTo(new Vector2(0f, -120f)), "configuring must not move the badge");
            feedback.Press();
            Assert.That(badge.anchoredPosition, Is.EqualTo(new Vector2(0f, -120f) + ShellTheme.PressedShift));
            feedback.Release();
            feedback.Tick(MinigameUiTheme.PressRestoreSeconds);
            Assert.That(badge.anchoredPosition, Is.EqualTo(new Vector2(0f, -120f)));
        }

        [Test]
        public void ADisabledButtonIsPaintedDisabledFromItsFirstFrame()
        {
            var go = new GameObject("Locked", typeof(RectTransform), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(root.transform, false);
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.interactable = false;
            ShellButtonHandle handle = ShellKit.StyleButton(button, ShellButtonVariant.Primary, "KHÓA");
            Assert.That(handle.Card.Face.color, Is.EqualTo(ShellTheme.DisabledFace));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation.ShellKitTests;KMA.Tests.Presentation.ShellPressFeedbackTests" shell-controls`
Expected: compilation errors (`ShellKit.Button` etc. missing).

- [ ] **Step 3: Implement `ShellPressFeedback`**

```csharp
// Assets/_Project/Scripts/UI/Kit/ShellPressFeedback.cs
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    /// Shell press response: while held, the button body (border, face, label) slides onto its shadow;
    /// after release it eases back over PressRestoreSeconds. A non-interactable Selectable on the same
    /// object paints the Shell disabled tokens and ignores presses. Owns no input.
    public sealed class ShellPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform body;
        [SerializeField] Graphic face;
        [SerializeField] TMP_Text label;
        [SerializeField] Color faceRest = Color.white;
        [SerializeField] Color labelRest = Color.black;
        // Where the body sits at rest; the press slides it from here (map stop badges are not centred).
        [SerializeField] Vector2 restPosition;
        Selectable selectable;
        bool pressed;
        bool painted;
        bool wasDisabled;
        float restoreRemaining;

        public bool IsPressed => pressed;
        public Vector2 Offset => body == null ? Vector2.zero : body.anchoredPosition - restPosition;

        public void Configure(RectTransform bodyRect, Graphic faceGraphic, TMP_Text buttonLabel, Color faceColor, Color labelColor)
        {
            if (body != null && body == bodyRect)
                body.anchoredPosition = restPosition;   // undo a press in progress before re-reading the rest
            body = bodyRect;
            restPosition = body != null ? body.anchoredPosition : Vector2.zero;
            face = faceGraphic;
            label = buttonLabel;
            faceRest = faceColor;
            labelRest = labelColor;
            selectable = GetComponent<Selectable>();
            pressed = false;
            restoreRemaining = 0f;
            SetShift(0f);
            wasDisabled = IsDisabled;
            Paint(wasDisabled);
            painted = true;
        }

        public void OnPointerDown(PointerEventData eventData) => Press();
        public void OnPointerUp(PointerEventData eventData) => Release();

        public void Press()
        {
            if (IsDisabled) return;
            pressed = true;
            restoreRemaining = 0f;
            SetShift(1f);
        }

        public void Release()
        {
            if (!pressed) return;
            pressed = false;
            restoreRemaining = MinigameUiTheme.PressRestoreSeconds;
        }

        public void Tick(float deltaTime)
        {
            bool disabled = IsDisabled;
            if (!painted || disabled != wasDisabled)
            {
                wasDisabled = disabled;
                painted = true;
                if (disabled)
                {
                    pressed = false;
                    restoreRemaining = 0f;
                    SetShift(0f);
                }
                Paint(disabled);
            }
            if (pressed || restoreRemaining <= 0f) return;
            restoreRemaining = Mathf.Max(0f, restoreRemaining - deltaTime);
            SetShift(restoreRemaining / MinigameUiTheme.PressRestoreSeconds);
        }

        void Update() => Tick(Time.unscaledDeltaTime);

        void OnDisable()
        {
            pressed = false;
            restoreRemaining = 0f;
            SetShift(0f);
        }

        bool IsDisabled
        {
            get
            {
                if (selectable == null) selectable = GetComponent<Selectable>();
                return selectable != null && !selectable.interactable;
            }
        }

        void Paint(bool disabled)
        {
            if (face != null) face.color = disabled ? ShellTheme.DisabledFace : faceRest;
            if (label != null) label.color = disabled ? ShellTheme.DisabledText : labelRest;
        }

        void SetShift(float amount)
        {
            if (body != null) body.anchoredPosition = restPosition + ShellTheme.PressedShift * amount;
        }
    }
}
```

- [ ] **Step 4: Append the control factories to `ShellKit`**

```csharp
        public static Color FaceColor(ShellButtonVariant variant) => variant switch
        {
            ShellButtonVariant.Primary => ShellTheme.Gold,
            ShellButtonVariant.Danger => ShellTheme.AccentRed,
            _ => ShellTheme.Cream,
        };

        public static Color LabelColor(ShellButtonVariant variant) => ShellTheme.Ink;

        public static ShellButtonHandle Button(Transform parent, string name, string text, ShellButtonVariant variant)
        {
            RectTransform rect = UiKit.Rect(parent, name);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, ShellTheme.ControlHeight);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            return StyleButton(button, variant, text);
        }

        public static ShellButtonHandle IconButton(Transform parent, string name, string glyph)
        {
            ShellButtonHandle handle = Button(parent, name, glyph, ShellButtonVariant.Secondary);
            ((RectTransform)handle.Button.transform).sizeDelta = Vector2.one * ShellTheme.IconButtonSize;
            VietTypography.Apply(handle.Label, VietFontRole.Symbol);
            handle.Label.fontSize = MinigameUiTheme.Headline;
            UiKit.FitLabel(handle.Label, MinigameUiTheme.Headline);
            handle.Label.color = LabelColor(ShellButtonVariant.Secondary);
            return handle;
        }

        /// Restyles any uGUI Button, kit-, prefab- or scene-authored, as a Shell button. Idempotent; keeps listeners.
        public static ShellButtonHandle StyleButton(UnityEngine.UI.Button button, ShellButtonVariant variant, string text = null)
        {
            var root = (RectTransform)button.transform;
            RemoveLegacyButtonParts(root);

            Image hit = UiKit.GetOrAdd<Image>(root.gameObject);
            hit.sprite = null;
            hit.color = Color.clear;
            hit.raycastTarget = true;

            Image shadow = Layer(root, ShadowName, 0, ShellTheme.ControlRadius, ShellTheme.Ink);
            UiKit.Stretch(shadow.rectTransform, ShellTheme.ControlShadow, ShellTheme.ControlShadow);
            Transform existingBody = root.Find(BodyName);
            RectTransform body = existingBody != null ? (RectTransform)existingBody : UiKit.Rect(root, BodyName);
            body.SetSiblingIndex(1);
            UiKit.Stretch(body);
            UiKit.GetOrAdd<LayoutElement>(body.gameObject).ignoreLayout = true;
            Image border = Layer(body, BorderName, 0, ShellTheme.ControlRadius, ShellTheme.Ink);
            UiKit.Stretch(border.rectTransform);
            Image face = Layer(body, FaceName, 1, Mathf.Max(0f, ShellTheme.ControlRadius - ShellTheme.ControlBorder),
                FaceColor(variant));
            UiKit.Stretch(face.rectTransform, Vector2.one * ShellTheme.ControlBorder, -Vector2.one * ShellTheme.ControlBorder);

            TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);
            if (label == null && (text != null || root.GetComponentInChildren<Text>(true) != null))
                label = UiKit.EnsureTmpLabel(root, MinigameUiTheme.BodyLarge, LabelColor(variant));
            if (label != null)
            {
                label.transform.SetParent(body, false);
                label.transform.SetAsLastSibling();
                UiKit.Stretch(label.rectTransform, new Vector2(MinigameUiTheme.SpaceSm, 0f), new Vector2(-MinigameUiTheme.SpaceSm, 0f));
                if (text != null) label.text = VietText.Fix(text);
                label.fontSize = MinigameUiTheme.BodyLarge;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                VietTypography.Apply(label, VietFontRole.Hud);
                UiKit.FitLabel(label, MinigameUiTheme.BodyLarge);
            }

            button.transition = Selectable.Transition.None;
            button.targetGraphic = face;
            face.raycastTarget = true;
            var feedback = UiKit.GetOrAdd<ShellPressFeedback>(root.gameObject);
            feedback.Configure(body, face, label, FaceColor(variant), LabelColor(variant));
            return new ShellButtonHandle(button, body, new ShellCardHandle(root, shadow, border, face), label, feedback);
        }

        public static ShellTitleHandle Title(Transform parent, string name, string text, float size, bool warning = false)
        {
            RectTransform root = UiKit.Rect(parent, name);
            TMP_Text drop = TitleText(root, "Drop", text, size, ShellTheme.Gold);
            UiKit.Stretch(drop.rectTransform, ShellTheme.TitleDrop, ShellTheme.TitleDrop);
            TMP_Text face = TitleText(root, "Text", text, size, warning ? ShellTheme.TitleRed : ShellTheme.Ink);
            UiKit.Stretch(face.rectTransform);
            return new ShellTitleHandle(root, face, drop);
        }

        public static void SetTitleText(Transform titleRoot, string text)
        {
            foreach (string part in new[] { "Drop", "Text" })
            {
                TMP_Text label = titleRoot.Find(part)?.GetComponent<TMP_Text>();
                if (label != null) label.text = VietText.Fix(text);
            }
        }

        public static ShellToggleHandle Toggle(Transform parent, string name)
        {
            RectTransform root = UiKit.Rect(parent, name);
            ShellCardHandle track = StyleSurface(root, ShellTheme.ControlRadius, ShellTheme.ControlBorder,
                ShellTheme.ControlShadow, ShellTheme.Cream);
            Image check = UiKit.Shape(track.Face.transform, "Checkmark",
                Mathf.Max(0f, ShellTheme.ControlRadius - ShellTheme.ControlBorder), ShellTheme.Gold);
            UiKit.Stretch(check.rectTransform);
            TMP_Text value = Label(root, "Value", "BẬT", MinigameUiTheme.Body, ShellTheme.Ink, VietFontRole.Hud);
            UiKit.Stretch(value.rectTransform);
            track.Face.raycastTarget = true;
            var toggle = root.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = track.Face;
            toggle.graphic = check;
            toggle.transition = Selectable.Transition.None;
            toggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.None;
            return new ShellToggleHandle(toggle, track, check, value);
        }

        public static ShellSliderHandle Slider(Transform parent, string name)
        {
            RectTransform root = UiKit.Rect(parent, name);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            RectTransform trackRect = UiKit.Rect(root, "Track");
            UiKit.Anchor(trackRect, new Vector2(0f, .3f), new Vector2(1f, .7f));
            ShellCardHandle track = StyleSurface(trackRect, ShellTheme.ControlRadius, ShellTheme.ControlBorder,
                ShellTheme.ControlShadow, ShellTheme.Cream);
            RectTransform fillArea = UiKit.Rect(track.Face.transform, "FillArea");
            UiKit.Stretch(fillArea);
            Image fill = UiKit.Shape(fillArea, "Fill", Mathf.Max(0f, ShellTheme.ControlRadius - ShellTheme.ControlBorder),
                ShellTheme.Gold);
            UiKit.Stretch(fill.rectTransform);

            RectTransform area = UiKit.Rect(root, "HandleSlideArea");
            area.anchorMin = new Vector2(0f, .5f);
            area.anchorMax = new Vector2(1f, .5f);
            area.sizeDelta = new Vector2(-MinigameUiTheme.SliderKnob, MinigameUiTheme.SliderKnob);
            Image knob = UiKit.Disc(area, "Handle", false, ShellTheme.Ink);
            knob.rectTransform.sizeDelta = Vector2.one * MinigameUiTheme.SliderKnob;
            Image ring = UiKit.Disc(knob.transform, "Ring", true, ShellTheme.Gold);
            UiKit.Stretch(ring.rectTransform);

            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.transition = Selectable.Transition.None;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return new ShellSliderHandle(slider, track, fill, knob);
        }

        static TMP_Text TitleText(RectTransform parent, string name, string text, float size, Color color)
        {
            TMP_Text label = Label(parent, name, text, size, color, VietFontRole.Title);
            label.fontStyle = FontStyles.Italic;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        static void RemoveLegacyButtonParts(RectTransform root)
        {
            foreach (string stale in new[] { "Fill", "Face", "Icon", "HoverArrow", "DisabledShade", "Shadow", "Visual" })
            {
                Transform old = root.Find(stale);
                if (old != null) Destroy(old.gameObject);
            }
            foreach (Shadow effect in root.GetComponents<Shadow>()) Destroy(effect);
            foreach (KitPressFeedback old in root.GetComponents<KitPressFeedback>()) Destroy(old);
            foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name == "HomeKeyArtButton") Destroy(behaviour);
        }
```

(`HomeKeyArtButton` lives in the same assembly, but referencing it by name keeps `ShellKit` free of a menu-specific type. A stale `Text` child is replaced by `UiKit.EnsureTmpLabel`, which already destroys it.)

When `Destroy` is deferred in Play Mode the stale `Face` child is still found by `Find` this frame; rename before destroying, as in Task 2:

```csharp
                if (old != null) { old.name = "Stale"; Destroy(old.gameObject); }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation.ShellKitTests;KMA.Tests.Presentation.ShellPressFeedbackTests" shell-controls`
Expected: all ShellKitTests (12) and ShellPressFeedbackTests (4) pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/Kit/ShellKit.cs Assets/_Project/Scripts/UI/Kit/ShellPressFeedback.cs Assets/Tests/EditMode/Presentation/ShellKitTests.cs Assets/Tests/EditMode/Presentation/ShellPressFeedbackTests.cs Assets/Tests/EditMode/Presentation/ShellPressFeedbackTests.cs.meta
git commit -m "feat(ui): add Shell buttons, titles, toggle, slider and press feedback"
```

---

### Task 4: Style audits — shared helpers, `ShellStyleAudit`, stricter `MinigameStyleAudit`

**Files:**
- Create: `Assets/_Project/Scripts/UI/Kit/StyleAuditShared.cs`
- Create: `Assets/_Project/Scripts/UI/Kit/ShellStyleAudit.cs`
- Modify: `Assets/_Project/Scripts/UI/Kit/MinigameStyleAudit.cs`
- Test: `Assets/Tests/EditMode/Presentation/ShellStyleAuditTests.cs` (new); existing `Assets/Tests/EditMode/EditorTools/MinigameStyleConsistencyTests.cs` must stay green

**Interfaces:**
- Produces:
  - `public readonly struct ShellAuditOptions { float MinimumFontSize; bool CheckSprites; bool CheckColours; bool CheckEffects; static ShellAuditOptions Default; static ShellAuditOptions Splash; }` — `Default` = (28, true, true, true); `Splash` = (24, false, false, false)
  - `List<string> ShellStyleAudit.Audit(GameObject root)` and `Audit(GameObject root, ShellAuditOptions options)`
  - `StyleAuditShared.PathOf(Transform)`, `IsExempt(Component)`, `InPalette(Color, Color[])`, `CheckText(TMP_Text, float minimum, Color[] palette, bool checkColours, List<string> problems)`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/Presentation/ShellStyleAuditTests.cs
using System.Linq;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class ShellStyleAuditTests
    {
        GameObject root;

        [SetUp] public void SetUp() => root = new GameObject("Screen", typeof(RectTransform));
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void ShellKitOutputPassesTheAudit()
        {
            ShellCardHandle card = ShellKit.Card(root.transform, "Card", decorated: true);
            ShellKit.Title(card.Root, "Title", "CÀI ĐẶT", 54f);
            ShellKit.Button(card.Root, "Back", "QUAY LẠI", ShellButtonVariant.Secondary);
            ShellKit.Chip(card.Root, "Lives", "5/5");
            ShellKit.Toggle(card.Root, "Toggle");
            ShellKit.Slider(card.Root, "Slider");
            var problems = ShellStyleAudit.Audit(root);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void AuditCatchesOffStyleSpritesColoursSizesEffectsAndLegacyText()
        {
            var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            knob.transform.SetParent(root.transform, false);
            knob.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            knob.color = new Color32(255, 152, 0, 255);
            knob.gameObject.AddComponent<Outline>();
            var small = new GameObject("Small", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            small.transform.SetParent(root.transform, false);
            small.fontSize = 20f;
            new GameObject("Legacy", typeof(RectTransform), typeof(Text)).transform.SetParent(root.transform, false);

            var problems = ShellStyleAudit.Audit(root);
            Assert.That(problems.Any(p => p.Contains("Knob") && p.Contains("sprite")), Is.True, string.Join("\n", problems));
            Assert.That(problems.Any(p => p.Contains("Knob") && p.Contains("colour")), Is.True);
            Assert.That(problems.Any(p => p.Contains("Knob") && p.Contains("Outline")), Is.True);
            Assert.That(problems.Any(p => p.Contains("Small") && p.Contains("size")), Is.True);
            Assert.That(problems.Any(p => p.Contains("legacy")), Is.True);
        }

        [Test]
        public void ExemptSubtreesAreSkipped()
        {
            var icon = new GameObject("IconGlyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(root.transform, false);
            icon.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            icon.color = new Color32(255, 152, 0, 255);
            icon.gameObject.AddComponent<StyleExempt>();
            var clear = new GameObject("Hit", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            clear.transform.SetParent(root.transform, false);
            clear.color = Color.clear;
            Assert.That(ShellStyleAudit.Audit(root), Is.Empty);
        }

        [Test]
        public void SplashOptionsOnlyCheckFontsAndTheLowerFloor()
        {
            var text = new GameObject("Status", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(root.transform, false);
            KMA.Gameplay.UI.VietTypography.Apply(text, KMA.Gameplay.UI.VietFontRole.Hud);
            text.enableAutoSizing = false;
            text.fontSize = 24f;
            text.color = new Color32(255, 152, 0, 255);
            Assert.That(ShellStyleAudit.Audit(root, ShellAuditOptions.Splash), Is.Empty);
            text.fontSize = 20f;
            Assert.That(ShellStyleAudit.Audit(root, ShellAuditOptions.Splash).Any(p => p.Contains("size")), Is.True);
        }

        [Test]
        public void MinigameAuditNowRejectsOutlineOutsideBarsAndHonoursTheMarker()
        {
            Image panel = UiKit.Panel(root.transform, "Panel");
            panel.gameObject.AddComponent<Outline>();
            Assert.That(MinigameStyleAudit.Audit(root).Any(p => p.Contains("Outline")), Is.True);
            panel.gameObject.AddComponent<StyleExempt>();
            Assert.That(MinigameStyleAudit.Audit(root), Is.Empty);
        }
    }
}
```

Note: `UiKit.Panel` adds a soft `Shadow` component by design (Kit family); the Kit audit forbids only `Outline` (and `Shadow` used *as a border*, i.e. an `Outline`). Keep `Shadow` allowed in the Kit audit; forbid `Outline` everywhere in Kit except under `KitBar` or a Countdown label (named `Countdown`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.ShellStyleAuditTests shell-audit`
Expected: compilation errors (`ShellStyleAudit`, `ShellAuditOptions` missing).

- [ ] **Step 3: Create the shared helpers**

```csharp
// Assets/_Project/Scripts/UI/Kit/StyleAuditShared.cs
using System;
using System.Collections.Generic;
using KMA.Gameplay.UI;
using TMPro;
using UnityEngine;

namespace KMA.UI.Kit
{
    /// Helpers shared by the Kit (minigame) and Shell style audits.
    public static class StyleAuditShared
    {
        public static bool IsExempt(Component component) =>
            component.GetComponentInParent<StyleExempt>(true) != null;

        public static bool InPalette(Color color, Color[] palette) =>
            Array.Exists(palette, token => MinigameUiTheme.RgbEquals(token, color));

        public static void CheckText(TMP_Text text, float minimum, Color[] palette, bool checkColours, List<string> problems)
        {
            string path = PathOf(text.transform);
            var fonts = VietTypography.Library;
            if (fonts == null || !Array.Exists(new[] { fonts.title, fonts.buttonHud, fonts.regular, fonts.bold }, font => font == text.font))
                problems.Add($"{path}: font {(text.font == null ? "none" : text.font.name)} is not a KMA Vietnamese font");
            float smallest = text.enableAutoSizing ? text.fontSizeMin : text.fontSize;
            if (text is TextMeshProUGUI && smallest < minimum)
                problems.Add($"{path}: text size {smallest} is below {minimum}");
            if (checkColours && text.color.a >= .01f && !InPalette(text.color, palette))
                problems.Add($"{path}: text colour #{ColorUtility.ToHtmlStringRGBA(text.color)} is not a theme token");
        }

        public static string PathOf(Transform node)
        {
            string path = node.name;
            for (Transform current = node.parent; current != null; current = current.parent)
                path = current.name + "/" + path;
            return path;
        }
    }
}
```

- [ ] **Step 4: Create `ShellStyleAudit`**

```csharp
// Assets/_Project/Scripts/UI/Kit/ShellStyleAudit.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.UI.Kit
{
    public readonly struct ShellAuditOptions
    {
        public readonly float MinimumFontSize;
        public readonly bool CheckSprites;
        public readonly bool CheckColours;
        public readonly bool CheckEffects;

        public ShellAuditOptions(float minimumFontSize, bool checkSprites, bool checkColours, bool checkEffects)
        {
            MinimumFontSize = minimumFontSize;
            CheckSprites = checkSprites;
            CheckColours = checkColours;
            CheckEffects = checkEffects;
        }

        public static ShellAuditOptions Default => new ShellAuditOptions(MinigameUiTheme.MinimumFontSize, true, true, true);
        /// The splash is a show-piece: its own slanted art and type, only the font set and a 24 px floor.
        public static ShellAuditOptions Splash => new ShellAuditOptions(24f, false, false, false);
    }

    /// The rules every screen outside the minigames must follow, shared by EditMode and PlayMode guard tests.
    public static class ShellStyleAudit
    {
        public static List<string> Audit(GameObject root) => Audit(root, ShellAuditOptions.Default);

        public static List<string> Audit(GameObject root, ShellAuditOptions options)
        {
            var problems = new List<string>();
            var kitSprites = new HashSet<Sprite>(UiKitAssets.Load().AllSprites());
            Color[] palette = ShellTheme.Palette();

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.color.a < .01f || StyleAuditShared.IsExempt(image)) continue;
                string path = StyleAuditShared.PathOf(image.transform);
                if (options.CheckSprites && image.sprite != null && !kitSprites.Contains(image.sprite))
                    problems.Add($"{path}: sprite {image.sprite.name} is not a UI kit sprite");
                if (options.CheckColours && !StyleAuditShared.InPalette(image.color, palette))
                    problems.Add($"{path}: colour #{ColorUtility.ToHtmlStringRGBA(image.color)} is not a Shell token");
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (!StyleAuditShared.IsExempt(text) && text.color.a >= .01f)
                    StyleAuditShared.CheckText(text, options.MinimumFontSize, palette, options.CheckColours, problems);

            if (options.CheckEffects)
                foreach (Shadow effect in root.GetComponentsInChildren<Shadow>(true))
                    if (!StyleAuditShared.IsExempt(effect))
                        problems.Add($"{StyleAuditShared.PathOf(effect.transform)}: {effect.GetType().Name} effect; Shell draws borders and shadows as layers");

            foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
                problems.Add($"{StyleAuditShared.PathOf(legacy.transform)}: legacy UnityEngine.UI.Text");

            return problems;
        }
    }
}
```

- [ ] **Step 5: Refactor `MinigameStyleAudit` onto the helpers**

Replace the body of `MinigameStyleAudit.Audit` and `IsExempt`/`PathOf` with:

```csharp
        public static List<string> Audit(GameObject root)
        {
            var problems = new List<string>();
            UiKitAssets assets = UiKitAssets.Load();
            var kitSprites = new HashSet<Sprite>(assets.AllSprites());
            Color[] palette = MinigameUiTheme.Palette();

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (IsExempt(image))
                    continue;
                string path = StyleAuditShared.PathOf(image.transform);
                bool sprintFoot = image.name == "FootIcon" && image.sprite != null &&
                    (image.sprite.name == "SprintLeftFootIcon" || image.sprite.name == "SprintRightFootIcon" || image.sprite.name == "SprintShoe");
                bool spriteOk = image.sprite == null ? IsScrim(image.color) : kitSprites.Contains(image.sprite) || sprintFoot;
                if (!spriteOk)
                    problems.Add($"{path}: sprite {(image.sprite == null ? "none" : image.sprite.name)} is not a UI kit sprite");
                if (!StyleAuditShared.InPalette(image.color, palette))
                    problems.Add($"{path}: colour #{ColorUtility.ToHtmlStringRGBA(image.color)} is not a theme token");
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (!StyleAuditShared.IsExempt(text))
                    StyleAuditShared.CheckText(text, MinigameUiTheme.MinimumFontSize, palette, true, problems);

            foreach (Outline outline in root.GetComponentsInChildren<Outline>(true))
                if (!StyleAuditShared.IsExempt(outline) && outline.GetComponentInParent<KitBar>(true) == null &&
                    outline.name != "Countdown")
                    problems.Add($"{StyleAuditShared.PathOf(outline.transform)}: Outline effect; kit borders are layers");

            foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
                problems.Add($"{StyleAuditShared.PathOf(legacy.transform)}: legacy UnityEngine.UI.Text");

            return problems;
        }

        static bool IsExempt(Image image) =>
            StyleAuditShared.IsExempt(image) ||
            (image.GetComponentInParent<GameplayDialogueView>(true)?.IsArtwork(image) ?? false) || image.color.a < .01f ||
            image.name == "Icon" || HasAncestor(image.transform, "FinishLine");
```

Delete the old private `PathOf` and `IsToken` (keep `IsScrim`, `HasAncestor`). Remove the now-unused `using System;` only if nothing else needs it.

- [ ] **Step 6: Run the audit tests and the existing Kit consistency tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.ShellStyleAuditTests shell-audit`
Expected: 5 passed.
Run: `tools/run-unity-tests.sh EditMode KMA.Tests.EditorTools.MinigameStyleConsistencyTests kit-audit`
Expected: all passed. If the new Outline rule flags a prefab or HUD, fix the source (replace the `Outline` with a kit layer or `UiKit.AddShadow`), not the rule, unless the outline is on a `KitBar` label or Countdown. Restore any re-serialized scene/prefab/font file you did not change on purpose (`git status`).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/UI/Kit/StyleAuditShared.cs Assets/_Project/Scripts/UI/Kit/StyleAuditShared.cs.meta Assets/_Project/Scripts/UI/Kit/ShellStyleAudit.cs Assets/_Project/Scripts/UI/Kit/ShellStyleAudit.cs.meta Assets/_Project/Scripts/UI/Kit/MinigameStyleAudit.cs Assets/Tests/EditMode/Presentation/ShellStyleAuditTests.cs Assets/Tests/EditMode/Presentation/ShellStyleAuditTests.cs.meta
git commit -m "feat(ui): add the Shell style audit and share audit helpers with the kit"
```

---

### Task 5: Move the sport badge and slanted bar to `ShellArt`; retire the slanted (Xiên) button family

**Files:**
- Create: `Assets/_Project/Scripts/UI/ShellArt.cs`
- Modify: `Assets/_Project/Scripts/UI/HomePresentationBuilder.cs` (remove `StyleButton`, `Disc`, `DiscSprite`, `SlantSprite`, `CreateBadge`, `RestyleBadge`, `AddSchoolMark`, the `disc`/`SlantSprites` fields, the `HomeMenuButton` cleanup line in `HitArea`)
- Modify: `Assets/_Project/Scripts/UI/MainMenuScreen.cs:27-31` (delete the `HomeMenuButton` loop)
- Modify: `Assets/_Project/Scripts/UI/SplashPresentationView.cs` (calls → `ShellArt`)
- Modify: `Assets/_Project/Scripts/UI/SceneTransitionOverlay.cs` (calls → `ShellArt`)
- Modify: `Assets/_Project/Scripts/UI/GameOverPresentationBuilder.cs:224` (drop the `HomeMenuButton` cleanup line; Task 6 rewrites the rest)
- Modify: `Assets/Tests/EditMode/Presentation/GameOverPresentationTests.cs:77` (replace `HomeMenuButton` assertion)
- Delete: `Assets/_Project/Scripts/UI/HomeMenuButton.cs` and `.meta`
- Test: existing `KMA.Tests.Presentation` (Splash, SceneTransitionOverlay, GameOver tests) stay green; add `ShellArtTests` to `Assets/Tests/EditMode/Presentation/ShellKitTests.cs`

**Interfaces:**
- Produces: `RectTransform ShellArt.CreateBadge(RectTransform parent, string name)` (root carries `StyleExempt`), `void ShellArt.RestyleBadge(RectTransform root)`, `Sprite ShellArt.SlantSprite(bool outline, int width = 320, int height = 56, float? borderWidth = null)`.

- [ ] **Step 1: Write the failing test**

Append to `ShellKitTests`:

```csharp
        [Test]
        public void SportBadgeIsExemptContentArtOnKitCircles()
        {
            RectTransform badge = ShellArt.CreateBadge((RectTransform)root.transform, "SportBadge");
            Assert.That(badge.GetComponent<StyleExempt>(), Is.Not.Null);
            Assert.That(badge.Find("WhiteRim").GetComponent<Image>().sprite, Is.SameAs(UiKitAssets.Load().Circle));
            Assert.That(badge.Find("NavyFace").GetComponent<Image>().color, Is.EqualTo(ShellTheme.Ink));
        }
```

- [ ] **Step 2: Run to verify it fails**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.ShellKitTests shell-art`
Expected: compilation error (`ShellArt` missing).

- [ ] **Step 3: Create `ShellArt`**

Move `CreateBadge`, `RestyleBadge`, `AddSchoolMark` and `SlantSprite` out of `HomePresentationBuilder` into:

```csharp
// Assets/_Project/Scripts/UI/ShellArt.cs
using System;
using System.Collections.Generic;
using KMA.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    /// Decorative art shared by the splash, the menu and the scene transition: the sport badge
    /// and the slanted loading-bar sprite. Content art, so the badge is exempt from the style audits.
    public static class ShellArt
    {
        static readonly Dictionary<(float, float, int, int, bool), Sprite> SlantSprites =
            new Dictionary<(float, float, int, int, bool), Sprite>();

        public static RectTransform CreateBadge(RectTransform parent, string name)
        {
            var root = Rect(parent, name);
            root.sizeDelta = Vector2.one * ShellTheme.BadgeSize;
            root.gameObject.AddComponent<StyleExempt>();
            Disc(root, "BadgeShadow", new Vector2(3f, -4f), 132f, MinigameUiTheme.WithAlpha(ShellTheme.Ink, .35f));
            Disc(root, "WhiteRim", Vector2.zero, 132f, ShellTheme.White);
            Disc(root, "NavyFace", Vector2.zero, 124f, ShellTheme.Ink);
            Disc(root, "Track", new Vector2(0f, -19f), 78f, ShellTheme.AccentRed);
            var star = Rect(root, "Star");
            Place(star, new Vector2(0f, 31f), new Vector2(42f, 42f));
            var starImage = star.gameObject.AddComponent<Image>();
            starImage.sprite = HomeMenuIcons.Get(HomeMenuIcons.Shape.Star);
            starImage.color = ShellTheme.Gold;
            starImage.raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                var lane = Rect(root, "Lane" + i);
                Place(lane, new Vector2(-16f + i * 16f, -22f), new Vector2(3f, 44f));
                lane.localRotation = Quaternion.Euler(0f, 0f, -32f);
                var laneImage = lane.gameObject.AddComponent<Image>();
                laneImage.color = ShellTheme.White;
                laneImage.raycastTarget = false;
            }
            AddSchoolMark(root);
            root.gameObject.AddComponent<HomeBadgeShine>();
            return root;
        }

        public static void RestyleBadge(RectTransform root)
        {
            if (root == null) return;
            UiKit.GetOrAdd<StyleExempt>(root.gameObject);
            foreach (Transform child in root)
                if (child.name.StartsWith("Laurel", StringComparison.Ordinal)) child.gameObject.SetActive(false);
            if (root.Find("SchoolBuilding") == null) AddSchoolMark(root);
        }

        // Rasterize at the actual target dimensions so short loading tracks keep the
        // same slant angle and a uniform stroke instead of stretching a button texture.
        public static Sprite SlantSprite(bool outline, int width = 320, int height = 56, float? borderWidth = null)
        {
            float angle = ShellTheme.SlantAngle;
            float stroke = Mathf.Clamp(borderWidth ?? ShellTheme.ControlBorder * .5f, 0f, height * .5f - 1f);
            var key = (angle, stroke, width, height, outline);
            if (SlantSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            float lean = Mathf.Tan(angle * Mathf.Deg2Rad) * height;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float left = lean - y * lean / height;
                    float right = width - y * lean / height;
                    float outer = Mathf.Clamp01(x - left + 1f) * Mathf.Clamp01(right - x);
                    float inner = y >= stroke && y <= height - 1f - stroke && x >= left + stroke && x <= right - stroke ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, outer * (outline ? 1f - inner : 1f)));
                }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f));
            SlantSprites[key] = sprite;
            return sprite;
        }

        static void AddSchoolMark(RectTransform root)
        {
            var school = Rect(root, "SchoolBuilding");
            Place(school, new Vector2(0f, 1f), new Vector2(38f, 20f));
            Solid(school, ShellTheme.White);
            var roof = Rect(root, "SchoolRoof");
            Place(roof, new Vector2(0f, 13f), new Vector2(48f, 5f));
            Solid(roof, ShellTheme.Gold);
            var door = Rect(root, "SchoolDoor");
            Place(door, new Vector2(0f, -4f), new Vector2(7f, 11f));
            Solid(door, ShellTheme.Ink);
            for (int i = 0; i < 3; i++)
            {
                var window = Rect(root, "SchoolWindow" + i);
                Place(window, new Vector2((i - 1) * 11f, 3f), new Vector2(4f, 4f));
                Solid(window, ShellTheme.AccentRed);
            }
        }

        static void Solid(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void Disc(Transform parent, string name, Vector2 position, float size, Color color)
        {
            var rect = Rect(parent, name);
            Place(rect, position, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiKitAssets.Load().Circle;
            image.color = color;
            image.raycastTarget = false;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
```

The old slant stroke default was `UITheme.BorderWidth * .5f` = 2; `ShellTheme.ControlBorder * .5f` = 2 — identical. The badge's `Menu.red` `#E2553D` becomes `accentRed` `#FF595E` (spec: one red).

- [ ] **Step 4: Repoint callers and delete the Xiên family**

- `SplashPresentationView.cs`: `HomePresentationBuilder.RestyleBadge` → `ShellArt.RestyleBadge`; `HomePresentationBuilder.CreateBadge` → `ShellArt.CreateBadge`; `HomeMenuStyle.BadgeSize` → `ShellTheme.BadgeSize`; three `HomePresentationBuilder.SlantSprite` → `ShellArt.SlantSprite`.
- `SceneTransitionOverlay.cs`: `HomePresentationBuilder.CreateBadge` → `ShellArt.CreateBadge`; `HomeMenuStyle.BadgeSize` → `ShellTheme.BadgeSize`; two `HomePresentationBuilder.SlantSprite` → `ShellArt.SlantSprite`.
- `HomePresentationBuilder.cs`: delete everything from `// Splash and menu share the same badge sprites` to the end of `SlantSprite`, plus the fields `disc` and `SlantSprites`, the `using System.Collections.Generic;`, the `Disc`/`Place`/`Stretch` helpers if now unused (keep `Rect` and `Stretch`: `HitArea` uses them), and line 71 (`foreach (var old in button.GetComponents<HomeMenuButton>()) …`).
- `MainMenuScreen.cs`: delete lines 27-31 (the `foreach (HomeMenuButton visual …)` block).
- `GameOverPresentationBuilder.cs`: delete line 224 (`foreach (var old in button.GetComponents<HomeMenuButton>()) …`).
- `GameOverPresentationTests.cs:77`: replace `Assert.That(retry.GetComponent<HomeMenuButton>(), Is.Null);` with `Assert.That(retry.GetComponents<MonoBehaviour>().Any(b => b != null && b.GetType().Name == "HomeMenuButton"), Is.False);` (Task 6 replaces this test's assertions again).
- Delete `Assets/_Project/Scripts/UI/HomeMenuButton.cs` and `Assets/_Project/Scripts/UI/HomeMenuButton.cs.meta`.
- `grep -rn "HomeMenuButton\|HomePresentationBuilder\.\(CreateBadge\|RestyleBadge\|SlantSprite\|StyleButton\)" Assets` must return nothing (scene YAML references to the deleted script GUID are fixed by re-authoring in Task 13; check with `grep -l "<HomeMenuButton GUID from the deleted .meta>" Assets/_Project/Scenes/*.unity` before deleting the meta — if a scene references it, note the scene for Task 13).

- [ ] **Step 5: Run the presentation tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation presentation-art`
Expected: all passed (including `SceneTransitionOverlayTests`, `GameOverPresentationTests`, `SportBadgeIsExemptContentArtOnKitCircles`).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/ShellArt.cs Assets/_Project/Scripts/UI/ShellArt.cs.meta Assets/_Project/Scripts/UI/HomePresentationBuilder.cs Assets/_Project/Scripts/UI/MainMenuScreen.cs Assets/_Project/Scripts/UI/SplashPresentationView.cs Assets/_Project/Scripts/UI/SceneTransitionOverlay.cs Assets/_Project/Scripts/UI/GameOverPresentationBuilder.cs Assets/Tests/EditMode/Presentation/GameOverPresentationTests.cs Assets/Tests/EditMode/Presentation/ShellKitTests.cs
git rm Assets/_Project/Scripts/UI/HomeMenuButton.cs Assets/_Project/Scripts/UI/HomeMenuButton.cs.meta
git commit -m "refactor(ui): move the sport badge and slant sprite to ShellArt, drop slanted buttons"
```

---

### Task 6: GameOver on `ShellKit` — **human review gate**

**Files:**
- Modify: `Assets/_Project/Scripts/UI/GameOverPresentationBuilder.cs`
- Modify: `Assets/Editor/ShellSceneAuthoring.cs:44-49` (GameOver uses `Rebuild`)
- Modify: `Assets/Tests/EditMode/Presentation/GameOverPresentationTests.cs`
- Scene (via authoring): `Assets/_Project/Scenes/GameOver.unity`

**Interfaces:**
- Consumes: `ShellKit.Card/Tile/Title/SetTitleText/Label/StyleButton`, `ShellTheme`.
- Produces: GameOver hierarchy `GameOverLayout/MenuPanel/{ShellShadow,ShellBorder,ShellFace,ShellAccentTop,ShellAccentBottom}`, `GameOverLayout/TitleTop/{Drop,Text}`, `GameOverLayout/TitleMain/{Drop,Text}`, `GameOverLayout/Subtitle`, `GameOverLayout/Stat*/{ShellShadow,ShellBorder,ShellFace,Value,Caption}`, buttons `GameOverLayout/<name>/{ShellShadow,Body/{ShellBorder,ShellFace,Label}}`.

- [ ] **Step 1: Rewrite the style test (failing)**

Replace `LayoutUsesTheMenuCardStyle` and fix paths in other tests of `GameOverPresentationTests`:

```csharp
        // The card matches the Home key art through ShellKit: cream face, navy border and drop, gold primary action.
        [Test]
        public void LayoutUsesTheShellCard()
        {
            GameOverPresentationBuilder.Build(screen, new GameSession());

            var panel = root.transform.Find("GameOverLayout/MenuPanel");
            Assert.That(panel.Find(ShellKit.BorderName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Ink));
            Assert.That(panel.Find(ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(((RectTransform)panel.Find(ShellKit.ShadowName)).offsetMin, Is.EqualTo(ShellTheme.CardShadow));
            Assert.That(panel.Cast<Transform>().Count(t => t.name.StartsWith(ShellKit.AccentPrefix)), Is.EqualTo(2));
            var title = root.transform.Find("GameOverLayout/TitleMain/Text").GetComponent<TMP_Text>();
            Assert.That(title.color, Is.EqualTo(ShellTheme.TitleRed));
            Assert.That(title.enableVertexGradient, Is.False, "a gold gradient would tint the headline");
            Color Face(string name) => root.transform.Find($"GameOverLayout/{name}/{ShellKit.BodyName}/{ShellKit.FaceName}")
                .GetComponent<Image>().color;
            Assert.That(Face("RETRYButton"), Is.EqualTo(ShellTheme.Gold));
            Assert.That(Face("NEW GAMEButton"), Is.EqualTo(ShellTheme.Cream));
            Assert.That(Face("MAIN MENUButton"), Is.EqualTo(ShellTheme.Cream));
            var problems = ShellStyleAudit.Audit(root.transform.Find("GameOverLayout").gameObject);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void AnOldCreamCardIsReplacedAndKeepsItsButtons()
        {
            var old = new GameObject("GameOverLayout", typeof(RectTransform)).transform;
            old.SetParent(root.transform, false);
            new GameObject("MenuPanel", typeof(RectTransform), typeof(Image)).transform.SetParent(old, false);
            foreach (var button in root.GetComponentsInChildren<Button>(true).ToArray())
                button.transform.SetParent(old, false);

            GameOverPresentationBuilder.Build(screen, new GameSession());

            Assert.That(root.transform.Cast<Transform>().Count(t => t.name == "GameOverLayout"), Is.EqualTo(1));
            Assert.That(root.transform.Find($"GameOverLayout/MenuPanel/{ShellKit.FaceName}"), Is.Not.Null);
            Assert.That(root.transform.Find("GameOverLayout").GetComponentsInChildren<Button>(true).Length, Is.EqualTo(3));
        }
```

Also in this file:
- `ReopenedAuthoredSceneKeepsDialogueAndEditedHeadings` and `EditedHeadingsKeepTheExistingActions`: `"GameOverLayout/TitleMain"` → `"GameOverLayout/TitleMain/Text"`, `"GameOverLayout/TitleTop"` → `"GameOverLayout/TitleTop/Text"`.
- `ButtonsKeepTheirListenersAndAreCentered`: `retry.GetComponent<KitPressFeedback>()` → `retry.GetComponent<ShellPressFeedback>()`; replace the `HomeMenuButton` assertion with `Assert.That(retry.GetComponent<KitPressFeedback>(), Is.Null);`.
- `BackgroundShowsTheIllustrationAtFullBrightnessOverTheOpaqueVeil`: `HomeMenuStyle.Navy` → `ShellTheme.Ink`.

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.GameOverPresentationTests gameover`
Expected: `LayoutUsesTheShellCard`, `AnOldCreamCardIsReplacedAndKeepsItsButtons` and the path-updated tests FAIL.

- [ ] **Step 2: Rewrite the builder**

In `GameOverPresentationBuilder.cs`:
- Delete `Cream`, `ButtonGold`, `TitleRed`, `Border`, `DropOffset`, `PanelRadius`, `ButtonRadius` and the methods `Card`, `Accent`, `Title`.
- `ButtonSize` becomes `new Vector2(560f, ShellTheme.ControlHeight)`.
- Rebuild check in `Build` (old dark layout *or* old cream card without Shell layers):

```csharp
            if (existing != null && existing.Find(PanelName + "/" + ShellKit.FaceName) == null)
            {
                // A layout from an older style: keep the scene's buttons, rebuild the rest.
                DropLayout(screen);
                existing = null;
            }
```

- Veil colour: `veil.gameObject.AddComponent<Image>().color = ShellTheme.Ink;`
- Layout body (replaces lines 90-112):

```csharp
            ShellCardHandle panel = ShellKit.Card(layout, PanelName, decorated: true);
            At(panel.Root, PanelCenter, PanelSize);

            ShellTitleHandle top = ShellKit.Title(layout, "TitleTop", "HẾT LƯỢT THI", MinigameUiTheme.Headline);
            At(top.Root, new Vector2(0f, 375f), new Vector2(620f, 60f));
            ShellTitleHandle main = ShellKit.Title(layout, "TitleMain", "CHƯA QUA HỌC PHẦN", 84f, warning: true);
            At(main.Root, new Vector2(0f, 285f), new Vector2(700f, 110f));

            var subtitle = ShellKit.Label(layout, "Subtitle", Subtitle, MinigameUiTheme.Caption, ShellTheme.BodyText,
                VietFontRole.Body);
            subtitle.textWrappingMode = TextWrappingModes.Normal;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            At(subtitle.rectTransform, new Vector2(0f, 180f), new Vector2(660f, 90f));

            Stat(layout, "StatPassed", $"{stats.SubjectsPassed}/{stats.SubjectsTotal}", "MÔN ĐÃ QUA", -250f);
            Stat(layout, "StatFailed", stats.FailedVisits.ToString(), "LẦN CHƯA ĐẠT", 0f);
            Stat(layout, "StatScore", stats.TotalScore.ToString(), "TỔNG ĐIỂM", 250f);

            Action(screen, layout, "RETRYButton", "CHƠI LẠI", -105f, ShellButtonVariant.Primary);
            Action(screen, layout, "NEW GAMEButton", "CHƠI MỚI", -210f, ShellButtonVariant.Secondary);
            Action(screen, layout, "MAIN MENUButton", "MENU CHÍNH", -315f, ShellButtonVariant.Secondary);
```

- `RefreshEditedContent`:

```csharp
            ShellKit.SetTitleText(layout.Find("TitleTop"), "HẾT LƯỢT THI");
            ShellKit.SetTitleText(layout.Find("TitleMain"), "CHƯA QUA HỌC PHẦN");
            layout.Find("Subtitle").GetComponent<TMP_Text>().text = VietText.Fix(Subtitle);
            layout.Find("StatFailed/Caption").GetComponent<TMP_Text>().text = VietText.Fix("LẦN CHƯA ĐẠT");
```

- `Stat`:

```csharp
        static void Stat(RectTransform parent, string name, string value, string caption, float x)
        {
            ShellCardHandle card = ShellKit.Tile(parent, name);
            At(card.Root, new Vector2(x, 45f), new Vector2(240f, 130f));
            var number = ShellKit.Label(card.Root, "Value", value, MinigameUiTheme.Title, ShellTheme.Ink, VietFontRole.Hud);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(number.rectTransform, new Vector2(.05f, .42f), new Vector2(.95f, .95f));
            var text = ShellKit.Label(card.Root, "Caption", caption, MinigameUiTheme.Caption, ShellTheme.TitleRed, VietFontRole.Hud);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(text.rectTransform, new Vector2(.03f, .07f), new Vector2(.97f, .45f));
        }
```

- `Action`:

```csharp
        static void Action(GameOverScreen screen, RectTransform parent, string name, string caption,
            float y, ShellButtonVariant variant)
        {
            var button = screen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => string.Equals(candidate.name, name, StringComparison.Ordinal));
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            At(rect, new Vector2(0f, y), ButtonSize);
            ShellKit.StyleButton(button, variant, caption);
        }
```

- Add `using KMA.UI.Kit;` if missing (it is present). Remove now-unused `Stretch` helper only if nothing uses it (`ApplyBackground` uses `Rect` and `UiKit.GetOrAdd`; `Stretch` is used by the veil).

- [ ] **Step 3: Author GameOver with `Rebuild`**

In `ShellSceneAuthoring.Apply`, replace the GameOver block's `GameOverPresentationBuilder.Build(screen, null);` with `GameOverPresentationBuilder.Rebuild(screen, null);`.

- [ ] **Step 4: Run tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.GameOverPresentationTests gameover`
Expected: all passed.

- [ ] **Step 5: Re-author the scenes and validate**

Run: `"$UNITY" -batchmode -projectPath . -executeMethod KMA.EditorTools.ShellSceneAuthoring.Apply -quit -logFile Builds/author-gameover.log` then `grep -E "error|Exception" Builds/author-gameover.log` → nothing. Keep only `Assets/_Project/Scenes/GameOver.unity` from this run for now: `git checkout -- Assets/_Project/Scenes/Bootstrap.unity Assets/_Project/Scenes/Menu.unity Assets/_Project/Scenes/Map.unity` and restore font churn.

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.EditorTools.GameOverSceneTests gameover-scene`
Expected: all passed (update any `TitleMain`/`TitleTop` or `HomeMenuStyle` reference in that file the same way as Step 1).

- [ ] **Step 6: Screenshots — STOP for human review**

With a GUI Editor running (see the screenshot skill), capture:
`tools/qa-screenshot.sh Builds/Screenshots/shell/gameover-16x9.png Assets/_Project/Scenes/GameOver.unity 2` and the 20:9 variant (`--size 2400x1080` or the skill's aspect option).
Show both images to the user and **wait for approval** of the Shell look before starting Task 7. If the user asks for token changes, change them in `UITheme.ShellStyle` defaults (and `UITheme.asset` if the asset carries a `shell:` block), re-run Steps 4-6.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/UI/GameOverPresentationBuilder.cs Assets/Editor/ShellSceneAuthoring.cs Assets/Tests/EditMode/Presentation/GameOverPresentationTests.cs Assets/Tests/EditMode/EditorTools/GameOverSceneTests.cs Assets/_Project/Scenes/GameOver.unity
git commit -m "feat(ui): build the GameOver card with ShellKit"
```

---

### Task 7: Menu — key-art hit areas on tokens, new-game confirm card on `ShellKit`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HomeKeyArtButton.cs:12-14,30`
- Modify: `Assets/_Project/Scripts/UI/HomePresentationBuilder.cs:85-93`
- Modify: `Assets/_Project/Scripts/Shell/S5ShellSceneController.cs:223-285`
- Test: `Assets/Tests/EditMode/EditorTools/ShellStyleConsistencyTests.cs` (new; later tasks append to it)

**Interfaces:**
- Consumes: `ShellKit.Card/Title/Label/Button`, `ShellStyleAudit`.
- Produces: `NewGameConfirmation/ConfirmationCard/{ShellShadow,ShellBorder,ShellFace,Title,Message,XÁC NHẬNButton,QUAY LẠIButton}`; the test file `ShellStyleConsistencyTests` with a `[TearDown]` that releases the scene.

- [ ] **Step 1: Write the failing EditMode test**

The EditorTools test assembly does not reference `KMA.Gameplay.Shell`, so the controller is reached by type name and its private `EnsureNewGameConfirmation` by reflection (as `ShellSceneAuthoring.Validate` does for `Awake`):

```csharp
// Assets/Tests/EditMode/EditorTools/ShellStyleConsistencyTests.cs
#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMA.Tests.EditorTools
{
    public sealed class ShellStyleConsistencyTests
    {
        [TearDown]
        public void ReleaseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void NewGameConfirmationFollowsTheShellStyle()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Menu.unity", OpenSceneMode.Single);
            MonoBehaviour controller = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .First(b => b != null && b.GetType().Name == "S5ShellSceneController");
            controller.GetType().GetMethod("EnsureNewGameConfirmation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);

            GameObject confirmation = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == "NewGameConfirmation").gameObject;
            Transform card = confirmation.transform.Find("ConfirmationCard");
            Assert.That(card.Find(ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(confirmation.GetComponent<Image>().color, Is.EqualTo(UITheme.Shared.Scrim));
            var problems = ShellStyleAudit.Audit(card.gameObject);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(card.GetComponentsInChildren<Button>(true).Select(b => b.name),
                Is.EquivalentTo(new[] { "XÁC NHẬNButton", "QUAY LẠIButton" }));
        }
    }
}
#endif
```

If `EnsureNewGameConfirmation` throws because `mainMenu` is only assigned in `Awake`, invoke `Awake` first the same way (`GetMethod("Awake", …)`), exactly as `ShellSceneAuthoring.Validate` does for GameOver.

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.EditorTools.ShellStyleConsistencyTests menu-confirm` — expected FAIL (no `ShellFace`). Restore `Menu.unity` afterwards if the run re-saved it (`git status`).

- [ ] **Step 2: Restyle the confirm card**

Replace `EnsureNewGameConfirmation`, `AddButton`, `AddLabel` in `S5ShellSceneController.cs`:

```csharp
        void EnsureNewGameConfirmation()
        {
            if (confirmationRoot != null)
                return;
            confirmationRoot = new GameObject("NewGameConfirmation", typeof(RectTransform), typeof(Image));
            confirmationRoot.transform.SetParent(mainMenu.transform, false);
            UiKit.Stretch(confirmationRoot.GetComponent<RectTransform>());
            confirmationRoot.GetComponent<Image>().color = UITheme.Shared.Scrim;

            ShellCardHandle card = ShellKit.Card(confirmationRoot.transform, "ConfirmationCard");
            card.Root.anchorMin = card.Root.anchorMax = new Vector2(.5f, .5f);
            card.Root.sizeDelta = new Vector2(660f, 400f);
            ShellTitleHandle title = ShellKit.Title(card.Root, "Title", "BẮT ĐẦU HÀNH TRÌNH MỚI?", MinigameUiTheme.BodyLarge);
            Place(title.Root, new Vector2(0f, 110f), new Vector2(580f, 70f));
            TMP_Text message = ShellKit.Label(card.Root, "Message",
                "Tiến độ hiện tại sẽ được thay thế.\nBạn có chắc muốn tiếp tục?", MinigameUiTheme.Caption,
                ShellTheme.BodyText, VietFontRole.Body);
            message.textWrappingMode = TextWrappingModes.Normal;
            Place(message.rectTransform, new Vector2(0f, 20f), new Vector2(580f, 100f));
            AddButton(card.Root, "XÁC NHẬN", new Vector2(-145f, -115f), mainMenu.ConfirmNewGame, ShellButtonVariant.Danger);
            AddButton(card.Root, "QUAY LẠI", new Vector2(145f, -115f), () =>
            {
                mainMenu.CancelNewGame();
                confirmationRoot.SetActive(false);
            }, ShellButtonVariant.Secondary);
            confirmationRoot.SetActive(false);
        }

        static Button AddButton(Transform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction action, ShellButtonVariant variant)
        {
            ShellButtonHandle handle = ShellKit.Button(parent, label + "Button", label, variant);
            Place((RectTransform)handle.Button.transform, position, new Vector2(260f, ShellTheme.ControlHeight));
            handle.Button.onClick.AddListener(action);
            return handle.Button;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
```

Add `using KMA.Gameplay.UI;` if `VietFontRole`/`UITheme` are not already in scope. Remove `ButtonVariant` usages in this file.

- [ ] **Step 3: Key-art button tints and disabled shade on tokens**

`HomeKeyArtButton.cs`:

```csharp
        static Color Idle => new Color(1f, 1f, 1f, 0f);
        static Color Hover => MinigameUiTheme.WithAlpha(ShellTheme.Gold, .28f);
        static Color Press => MinigameUiTheme.WithAlpha(ShellTheme.Ink, .22f);
```

and `colors.fadeDuration = UITheme.Shared.Motion.buttonFade;`. Add `using KMA.UI.Kit;`.

`HomePresentationBuilder.HitArea`: `shadeImage.color = MinigameUiTheme.WithAlpha(ShellTheme.DisabledFace, .6f);` and `UiKit.EnsureTmpLabel(rect, MinigameUiTheme.Caption, Color.clear);`.

- [ ] **Step 4: Run tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.EditorTools.ShellStyleConsistencyTests menu-confirm` → passed.
Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation presentation-menu` → all passed.
Run: `tools/run-unity-tests.sh PlayMode KMA.Tests.Progression.S5NewGameTests menu-play` → all passed (new-game flow still confirms and cancels).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Shell/S5ShellSceneController.cs Assets/_Project/Scripts/UI/HomeKeyArtButton.cs Assets/_Project/Scripts/UI/HomePresentationBuilder.cs Assets/Tests/EditMode/EditorTools/ShellStyleConsistencyTests.cs Assets/Tests/EditMode/EditorTools/ShellStyleConsistencyTests.cs.meta
git commit -m "feat(ui): put the new-game confirm card and menu hit areas on Shell tokens"
```

---

### Task 8: Settings on `ShellKit`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/SettingsPresentationBuilder.cs` (whole `Build`, `Volume`, `Label`)
- Test: `Assets/Tests/EditMode/Presentation/SettingsPresentationTests.cs` (new; if a settings test file already exists — `grep -rl "SettingsPresentationBuilder" Assets/Tests` — add the cases there instead)

**Interfaces:**
- Consumes: `ShellKit.StyleCard/Title/Label/Slider/Toggle/Button`.
- Produces: `SettingsPanel/{ShellShadow,ShellBorder,ShellFace,Title,MusicSliderRow,SfxSliderRow,VibrationLabel,VibrationToggle,BackButton,Hint}`; `SettingsScreen.BindControls` receives the same six controls as before.

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Tests/EditMode/Presentation/SettingsPresentationTests.cs
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class SettingsPresentationTests
    {
        GameObject root;

        [SetUp] public void SetUp() => root = new GameObject("settings", typeof(RectTransform));
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void SettingsPanelFollowsTheShellStyle()
        {
            var screen = root.AddComponent<SettingsScreen>();
            SettingsPresentationBuilder.Build(screen);
            Transform panel = root.transform.Find("SettingsPanel");
            Assert.That(panel.Find(ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Cream));
            Assert.That(root.GetComponent<Image>().color, Is.EqualTo(UITheme.Shared.Scrim));
            var problems = ShellStyleAudit.Audit(root);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void BackStillRoutesBack()
        {
            var screen = root.AddComponent<SettingsScreen>();
            SettingsPresentationBuilder.Build(screen);
            Button back = root.transform.Find("SettingsPanel/BackButton").GetComponent<Button>();
            Assert.That(back.onClick.GetPersistentEventCount() + CountRuntime(back), Is.GreaterThan(0));
            Assert.That(back.GetComponent<ShellPressFeedback>(), Is.Not.Null);
        }

        static int CountRuntime(Button button)
        {
            var field = typeof(UnityEngine.Events.UnityEventBase).GetField("m_Calls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var calls = field.GetValue(button.onClick);
            var runtime = calls.GetType().GetField("m_RuntimeCalls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(calls);
            return ((System.Collections.ICollection)runtime).Count;
        }
    }
}
```

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.SettingsPresentationTests settings` → FAIL.

- [ ] **Step 2: Rewrite the builder**

```csharp
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
```

Keep the existing `Rect` and `Stretch` helpers. Remove `Navy`, `Gold`, `TitleSize`, `LabelSize`, `HintSize`, `Center` and the `UiKit.StyleButton` call. Check `SettingsScreen` for any code that colours `vibrationValue` or the toggle graphic (`grep -n "color" Assets/_Project/Scripts/UI/SettingsScreen.cs`); if it paints with `Gold`/white, switch it to `ShellTheme.Ink`.

- [ ] **Step 3: Run tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation settings-all` → all passed (including any older settings tests; update their colour expectations to `ShellTheme` if they asserted the navy panel).

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/SettingsPresentationBuilder.cs Assets/Tests/EditMode/Presentation/SettingsPresentationTests.cs Assets/Tests/EditMode/Presentation/SettingsPresentationTests.cs.meta
git commit -m "feat(ui): build Settings with ShellKit"
```

---

### Task 9: Map header, summary and stops on `ShellKit`; remove `BrutalButton`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/MapPresentationBuilder.cs` (`Header`, `MenuBlock`→removed, `HeaderButton`, `EnsureCourseSummary`, `CreateFullBleedBackdrop` shade, delete dead `FutureRow`/`FutureChip`/`ButtonColors`/`UseRoundedSurface`/`RoundedRectSprite` once unused)
- Modify: `Assets/_Project/Scripts/UI/MapStopBuilder.cs`
- Modify: `Assets/_Project/Scripts/UI/MapNodeView.cs:172-235`
- Modify: `Assets/_Project/Scripts/UI/MapJourneyPathLayout.cs:130-135`
- Modify: `Assets/_Project/Scripts/UI/JourneyCourseSummary.cs` only if it colours its label (check `grep -n color`)
- Modify: `Assets/_Project/Scripts/UI/Kit/UiKit.cs:444-446` (drop the `BrutalButton` branch)
- Delete: `Assets/_Project/Scripts/UI/BrutalButton.cs` + `.meta`
- Modify tests: `Assets/Tests/EditMode/Presentation/UIComponentTests.cs`, `Assets/Tests/EditMode/Presentation/UiKitTests.cs:79-106`, `Assets/Tests/EditMode/EditorTools/MinigamePrefabStylerTests.cs:82`
- Scene (via authoring): `Assets/_Project/Scenes/Map.unity`

**Interfaces:**
- Consumes: `ShellKit.StyleCard/Chip/IconButton/Title/Badge/StyleButton`, `ShellPressFeedback`, `StyleExempt`.
- Produces (paths kept for `MapScreen`, `JourneyLessonList`, tests): `Content/Header/{BackButton, Heading/TitleContainer/Title (ShellTitle root: …/Title/Text), LivesPanel/{HeartBar, LivesLabelContainer/{LivesLabel, LifeTimer}}}`; stop `…Node/{CurrentTag, SelectionRing, Badge/{ShellShadow,ShellBorder,ShellFace,IconGlyph,OrderBadge,DoneMark,LockIcon,FinishFlag?}, LabelGroup/{TitleContainer/Title, MetaPill/{Stars, StatusContainer/Status}}}`; stop root carries `ShellPressFeedback` instead of `BrutalButton`.

- [ ] **Step 1: Update the Map tests to the Shell structure (failing)**

In `UIComponentTests.cs`:
- Delete `BrutalButtonReturnsToRestAfterPointerUp` (lines ~46-70) and any other test that only exercises `BrutalButton`.
- Header block test (lines ~205-240): replace the per-block assertions with

```csharp
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
                TMP_Text arrow = header.Find("BackButton").GetComponentInChildren<TMP_Text>(true);
                Assert.That(arrow.color, Is.EqualTo(ShellTheme.Ink));
```

  (delete the `Heading/Accent` red-square assertion: the Heading card is now `decorated` and its accents are `ShellAccent*`; assert `Assert.That(header.Find("Heading").Cast<Transform>().Any(t => t.name.StartsWith(ShellKit.AccentPrefix)), Is.True);`). Keep the lives/heart contrast assertions, with `cream` = `ShellTheme.Cream`.
- Header title test near line 173: path `Heading/TitleContainer/Title` → `Heading/TitleContainer/Title/Text`; font expectation `VietTypography.Library.bold` → `VietTypography.Library.title`; `fontSize` range stays `InRange(36, 44)` (Title is built at `BodyLarge` 40).
- `livesPanel.GetComponent<Image>()` → `livesPanel.Find(ShellKit.FaceName).GetComponent<Image>()`.
- Line ~450 `completed.GetComponent<BrutalButton>()` → `completed.GetComponent<ShellPressFeedback>()`.
- Lines ~1133-1140: `BrutalButton feedback = node.GetComponent<BrutalButton>();` → `ShellPressFeedback feedback = node.GetComponent<ShellPressFeedback>();`; replace `Assert.That(feedback.enabled, Is.False);` with `feedback.Press(); Assert.That(feedback.IsPressed, Is.False, "a locked stop does not press");`.
- `UITheme.Shared.Menu.gold` (lines ~637, 647, 748) → `ShellTheme.Gold`; `HomeMenuStyle.Navy` (line ~862) → `ShellTheme.Ink`; `HomeMenuStyle.Gold` (line ~1168) → `ShellTheme.Gold`.
- Line ~1164 `sprint.transform.Find("Badge").GetComponent<Image>()` → `sprint.transform.Find("Badge/" + ShellKit.FaceName).GetComponent<Image>()`.
- Add:

```csharp
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
```

In `UiKitTests.StyleButton_ReplacesBrutalistPartsAndIsIdempotent`: delete `legacy.AddComponent<BrutalButton>();` and `Assert.That(legacy.GetComponent<BrutalButton>(), Is.Null);`. In `MinigamePrefabStylerTests.cs:82` delete the `BrutalButton` assertion line.

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.UIComponentTests map` → FAIL (structure).

- [ ] **Step 2: Header, summary and backdrop shade**

In `MapPresentationBuilder`:
- `CreateFullBleedBackdrop`: `shade.color = MinigameUiTheme.WithAlpha(ShellTheme.Ink, .42f);`; add `UiKit.GetOrAdd<StyleExempt>(backgroundObject)` (illustration).
- Replace `MenuBlock(rect)` with `ShellKit.StyleCard(rect)` (Heading: `ShellKit.StyleCard(heading, decorated: true)`; LivesPanel: `ShellKit.StyleCard(livesPanel)`). Delete `MenuBlock`, `BlockStroke`, `BlockShadow`; keep the header layout padding for the drop: `layout.padding = new RectOffset(0, (int)ShellTheme.CardShadow.x, 0, (int)-ShellTheme.CardShadow.y);`.
- Title: replace the `LayoutLabel` + `TitleShadow` + `Accent` code with

```csharp
            RectTransform titleBox = Rect(heading, "TitleContainer");
            titleBox.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            ShellTitleHandle title = ShellKit.Title(titleBox, "Title", "CHỌN MÔN THI", MinigameUiTheme.BodyLarge);
            Stretch(title.Root, Vector2.zero, Vector2.zero);
            title.Text.alignment = title.Drop.alignment = TextAlignmentOptions.Left;
```

- `HeaderButton`:

```csharp
        static Button HeaderButton(Transform parent, string name, string label)
        {
            ShellButtonHandle handle = ShellKit.IconButton(parent, name, label);
            LayoutElement element = handle.Button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = BlockHeight;
            element.preferredHeight = BlockHeight;
            element.flexibleWidth = 0;
            return handle.Button;
        }
```

- Hearts: on the `HeartBar` object add `bar.gameObject.AddComponent<StyleExempt>();` and delete `slot.gameObject.AddComponent<Outline>().effectColor = navy;`.
- Lives label size `26` → `MinigameUiTheme.Caption`; `LifeTimer` size `20` → `MinigameUiTheme.Caption`; `livesLabelLayout.preferredHeight = 64`; `lives…preferredHeight = 32`; timer `preferredHeight = 30`; `Color navy` → `ShellTheme.Ink` throughout `Header`.
- `EnsureCourseSummary`: replace the Image/Outline setup with `ShellKit.StyleCard(rect)` (keep the root `Image` but set `color = Color.clear` so existing code that reads it still finds one), and the label colour `UITheme.Shared.TextPrimary` → `ShellTheme.Ink`.
- Delete unused `FutureRow`, `FutureChip`, `ButtonColors` (verify with `grep -rn "ButtonColors\|FutureRow" Assets` first; `MapNodeView` may use `ButtonColors` — if so, keep it but replace `new Color(1f,1f,1f,.58f)` with `MinigameUiTheme.WithAlpha(ShellTheme.White, MinigameUiTheme.SurfaceOpaque)`), and `UseRoundedSurface`/`RoundedRectSprite` once `SceneTransitionOverlay` (Task 12) no longer calls them — leave them until Task 12, then delete there.

- [ ] **Step 3: Stops**

In `MapStopBuilder.Build`:
- Replace `root.gameObject.AddComponent<BrutalButton>();` with nothing here; after the badge exists, add
  `var press = root.gameObject.AddComponent<ShellPressFeedback>(); press.Configure(badge, null, null, Color.white, Color.white);`
  (`face`/`label` null: the stop recolours its own badge per state; the feedback only slides the badge).
- CurrentTag: replace the tag Image/Outline/LayoutLabel block with

```csharp
            ShellChipHandle tag = ShellKit.Chip(root, "CurrentTag", "ĐANG Ở ĐÂY", ShellTheme.Gold);
            RectTransform tagRect = tag.Card.Root;
            Place(tagRect, new Vector2(.5f, 1f), new Vector2(240f, 48f), new Vector2(0f, -4f));
            tagRect.pivot = new Vector2(.5f, 1f);
            tag.Label.fontSize = MinigameUiTheme.Caption;
            UiKit.FitLabel(tag.Label, MinigameUiTheme.Caption);
```

  and pass `tagRect.gameObject` where `tag.gameObject` was passed to `BindJourneyStop`. Bump `stopTagHeight` default in `UITheme.LessonJourneyStyle` from `52f` to `56f` only if the screenshot in Task 15 shows the tag touching the ring.
- Badge: delete `badgeImage` creation, the `Outline border`, and the `Shadow shadow` lines; use `ShellCardHandle badgeLayers = ShellKit.Badge(badge, accent); Image badgeImage = badgeLayers.Face;`.
- `IconGlyph`, `CheckGlyph`, lock `Glyph`, `FinishFlag`, `Stars`: add `StyleExempt` to each; delete `flag.gameObject.AddComponent<Outline>()…`. Glyph colour `UITheme.Shared.Surface` → `ShellTheme.Ink`.
- Order label `24` → `MinigameUiTheme.Caption`; status `24` → `MinigameUiTheme.Caption`; title stays `36`.
- `node.BindPresentation(status, null, null, null, border, accent);` → `node.BindPresentation(status, null, null, null, null, accent);` (no Outline any more; `MapNodeView` null-checks `cardOutline`).
- `HomeMenuStyle.Navy` → `ShellTheme.Ink`, `HomeMenuStyle.Gold` → `ShellTheme.Gold`, `HomeMenuStyle.GoldLight` → `ShellTheme.GoldLight`; `MinigameUiTheme.WithAlpha(HomeMenuStyle.Navy, .8f)` → `MinigameUiTheme.WithAlpha(ShellTheme.Ink, .8f)`.

In `MapNodeView.cs`:
- `RenderAvailability`: delete the `BrutalButton feedback …` three lines (`ShellPressFeedback` reads `button.interactable` itself).
- `HomeMenuStyle.Gold` → `ShellTheme.Gold`; `HomeMenuStyle.GoldLight` → `ShellTheme.GoldLight`; `UITheme.Shared.Menu.gold` → `ShellTheme.Gold`; `glyphImage.color = locked ? … : UITheme.Shared.Surface` → `… : ShellTheme.Ink`. Add `using KMA.UI.Kit;`.

In `MapJourneyPathLayout.cs:130-135`: `HomeMenuStyle.Navy` → `ShellTheme.Ink`, `HomeMenuStyle.Gold` → `ShellTheme.Gold`, `HomeMenuStyle.White` → `ShellTheme.White`.

In `UiKit.RemoveLegacyButtonParts` delete the `BrutalButton` lines (444-446). Delete `BrutalButton.cs` and its `.meta`.

- [ ] **Step 4: Run tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation map` → all passed.
Run: `tools/run-unity-tests.sh EditMode KMA.Tests.EditorTools.MinigamePrefabStylerTests prefab` → all passed.
Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Presentation.JourneyMapTests;KMA.Tests.Presentation.JourneyMapChessTests;KMA.Tests.Presentation.FestivalUiExperienceTests;KMA.Tests.Progression.MapLessonPopupBackTests" map-play` → all passed.

- [ ] **Step 5: Re-author Map and check for missing scripts**

Run `ShellSceneAuthoring.Apply` (see Global Constraints). Keep `Assets/_Project/Scenes/Map.unity` (and GameOver if it changed only by re-save); `git checkout --` Bootstrap and Menu for now.
Then: `grep -c "m_Script: {fileID: 0}" Assets/_Project/Scenes/Map.unity` → `0`, and grep the deleted `BrutalButton.cs.meta` GUID (from `git show HEAD:Assets/_Project/Scripts/UI/BrutalButton.cs.meta`) in `Map.unity` → no match. Add an EditMode test (pins Review Focus 4):

```csharp
        [Test]
        public void MapSceneHasNoMissingScripts()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Map.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            int missing = scene.GetRootGameObjects().Sum(go => go.GetComponentsInChildren<Transform>(true)
                .Sum(t => UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            Assert.That(missing, Is.Zero);
        }
```

(append it to `ShellStyleConsistencyTests.cs`, created in Task 7).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/MapPresentationBuilder.cs Assets/_Project/Scripts/UI/MapStopBuilder.cs Assets/_Project/Scripts/UI/MapNodeView.cs Assets/_Project/Scripts/UI/MapJourneyPathLayout.cs Assets/_Project/Scripts/UI/Kit/UiKit.cs Assets/Tests/EditMode/Presentation/UIComponentTests.cs Assets/Tests/EditMode/Presentation/UiKitTests.cs Assets/Tests/EditMode/EditorTools/MinigamePrefabStylerTests.cs Assets/Tests/EditMode/EditorTools/ShellStyleConsistencyTests.cs Assets/_Project/Scenes/Map.unity
git rm Assets/_Project/Scripts/UI/BrutalButton.cs Assets/_Project/Scripts/UI/BrutalButton.cs.meta
git commit -m "feat(ui): move the map header and stops to ShellKit and retire BrutalButton"
```

(Add `JourneyCourseSummary.cs` if it changed.)

---

### Task 10: Lesson popup on a cream Shell card

**Files:**
- Modify: `Assets/_Project/Scripts/UI/JourneyLessonPresentation.cs` (`Create`, `EnsurePopup`, `CreateNode`, `CreateDetailCard`, `ActionButton`, `AddBorder`→deleted)
- Modify: `Assets/_Project/Scripts/UI/JourneyLessonList.cs:207-218,350`
- Modify tests: `Assets/Tests/EditMode/Presentation/UIComponentTests.cs` (popup contrast test ~262-285), `Assets/Tests/PlayMode/Progression/S5NewGameTests.cs` if it reads `PlayButton/Label`

**Interfaces:**
- Consumes: `ShellKit.StyleCard/StyleTile/IconButton/Button`, `StyleExempt`.
- Produces: `JourneyLessons/{ShellShadow,ShellBorder,ShellFace,ChapterAccent,CourseIcon,CourseTitle,ProgressStar,CourseProgress,LessonRoad*,Lesson*,DetailCard/{ShellShadow,ShellBorder,ShellFace,Objective,Status,PlayButton/Body/{ShellBorder,ShellFace,Label}},CloseButton/Body/…}`; root `Image` of `JourneyLessons` and `DetailCard` stays, `Color.clear`.

- [ ] **Step 1: Update the popup contrast test (failing)**

```csharp
                Transform panel = root.transform.Find("S5MapPresentation/Content/JourneyLessons");
                Color panelSurface = panel.Find(ShellKit.FaceName).GetComponent<Image>().color;
                Assert.That(panelSurface, Is.EqualTo(ShellTheme.Cream));
                Assert.That(ContrastRatio(panel.Find("Lesson1/StageTitle").GetComponent<TMP_Text>().color,
                    panelSurface), Is.GreaterThanOrEqualTo(4.5f), "Stage names must read on the popup surface.");
                Color detail = panel.Find("DetailCard/" + ShellKit.FaceName).GetComponent<Image>().color;
                Assert.That(ContrastRatio(panel.Find("DetailCard/Objective").GetComponent<TMP_Text>().color, detail),
                    Is.GreaterThanOrEqualTo(4.5f), "The objective must read on the detail card.");
                Transform play = panel.Find("DetailCard/PlayButton");
                Assert.That(ContrastRatio(play.GetComponentInChildren<TMP_Text>(true).color,
                    play.Find(ShellKit.BodyName + "/" + ShellKit.FaceName).GetComponent<Image>().color),
                    Is.GreaterThanOrEqualTo(4.5f), "The play label must read on its button.");
                var problems = ShellStyleAudit.Audit(panel.gameObject);
                Assert.That(problems, Is.Empty, string.Join("\n", problems));
```

Run the class → FAIL.

- [ ] **Step 2: Restyle the popup**

In `JourneyLessonPresentation`:
- `Create`: after `Image surface = Shape(parent, "JourneyLessons", Theme.Surface);` set `surface.color = Color.clear;` and call `ShellKit.StyleCard(panel);`. Delete `AddBorder(surface, …)`.
- `CourseIcon` sport glyphs: add `StyleExempt` to each `<Subject>Glyph`; colour `Theme.Surface` → `ShellTheme.Ink`.
- Heading `Theme.TextPrimary` → `ShellTheme.Ink`; progress `Theme.MapHint` → `ShellTheme.BodyText`; `ProgressStar`: add `StyleExempt`.
- `EnsurePopup`: scrim `MinigameUiTheme.WithAlpha(HomeMenuStyle.Navy, .62f)` → `MinigameUiTheme.WithAlpha(ShellTheme.Ink, .62f)`. Replace the close-button block (from `Image close = …` to the stroke loop) with

```csharp
            ShellButtonHandle close = ShellKit.IconButton(panel, "CloseButton", "✕");
            Place((RectTransform)close.Button.transform, Vector2.one, Vector2.one * ShellTheme.IconButtonSize);
            ((RectTransform)close.Button.transform).anchoredPosition = new Vector2(-32f, -32f);
```

  (if the Symbol font lacks "✕", the glyph renders as a box in the Task 15 screenshot; then use "×" — check `VietTypography.Library.title` with `TMP_FontAsset.HasCharacter('✕')` in a quick test.)
- `CreateNode`: `Glyph` → add `StyleExempt`, colour `ShellTheme.Ink`; `LockIcon` → `StyleExempt`; `ShortStroke`/`LongStroke` colour `Theme.Surface` → `ShellTheme.Ink`; `StageTitle` colour `Theme.TextPrimary` → `ShellTheme.Ink`. Replace the `ColorBlock` press tint with a Shell slide: delete the `colors` block, set `button.transition = Selectable.Transition.None;` and add `rect.gameObject.AddComponent<ShellPressFeedback>().Configure(disc.rectTransform, null, null, Color.white, Color.white);`.
- `CreateDetailCard`: `card.color = Color.clear; ShellKit.StyleTile(card.rectTransform);` (white face, no drop) and delete `AddBorder`. Objective colour `Theme.Surface` → `ShellTheme.Ink`; status `Theme.MutedForeground` stays (it is in the palette); objective/status sizes stay on `Style.objectiveSize`/`captionSize` (set to 40 in Task 13).
- `ActionButton`:

```csharp
        static Button ActionButton(Transform parent, string name, string text)
        {
            ShellButtonHandle handle = ShellKit.Button(parent, name, text, ShellButtonVariant.Primary);
            return handle.Button;
        }
```

- Delete `AddBorder`.

In `JourneyLessonList`:
- line 207 `node.Glyph.color = theme.Surface;` → `ShellTheme.Ink`;
- line 218 `node.Title.color = state.Unlocked ? theme.TextPrimary : theme.MapHint;` → `state.Unlocked ? ShellTheme.Ink : theme.MapLockedText;`
- line 350 `playButton.transform.Find("Label")?.GetComponent<TMP_Text>()` → `playButton.GetComponentInChildren<TMP_Text>(true)`.
- Add `using KMA.UI.Kit;` if missing.

- [ ] **Step 3: Run tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.UIComponentTests popup` → all passed.
Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Progression.S5NewGameTests;KMA.Tests.Progression.MapLessonPopupBackTests" popup-play` → all passed (fix any `PlayButton/Label` path the same way).

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/JourneyLessonPresentation.cs Assets/_Project/Scripts/UI/JourneyLessonList.cs Assets/Tests/EditMode/Presentation/UIComponentTests.cs Assets/Tests/PlayMode/Progression/S5NewGameTests.cs
git commit -m "feat(ui): put the lesson popup on a Shell card"
```

---

### Task 11: Journey dialogue on `ShellKit`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/JourneyDialoguePresenter.cs` (`StickerRed`, `Build` lines ~395-483, `MakeSkip`, dot colours line ~246)
- Test: `Assets/Tests/PlayMode/Presentation/JourneyNarrativeTests.cs`

**Interfaces:**
- Produces: `JourneyDialogueOverlay/{SharedMenuBackground (StyleExempt), BackgroundDimmer, LeftActor/RightActor (StyleExempt), Sticker (Shell surface), DialogueBox (Shell card)/{NameTag (Shell chip, gold), Dialogue, SaveError, Hint, ProgressDots}, TapArea, BỎ QUA (Shell button)}`.

- [ ] **Step 1: Write the failing test and fix the old skip assertion**

In `JourneyNarrativeTests.SkipCanRetryAfterSaveFailureAndPersistsBeforeClosing` replace

```csharp
            Assert.That(skip.GetComponent<KMA.UI.Kit.KitPressFeedback>(), Is.Not.Null);
            Assert.That(skip.GetComponent<Image>().sprite, Is.Not.Null);
```

with

```csharp
            Assert.That(skip.GetComponent<KMA.UI.Kit.ShellPressFeedback>(), Is.Not.Null);
            Assert.That(skip.transform.Find(KMA.UI.Kit.ShellKit.BodyName + "/" + KMA.UI.Kit.ShellKit.FaceName), Is.Not.Null);
```

and add (the file's `Open`, `Find<T>` helpers and `[UnityTearDown]` already exist):

```csharp
        [UnityTest]
        public IEnumerator JourneyDialogueFollowsTheShellStyle()
        {
            Open("opening");
            yield return null;
            Transform overlay = Find<RectTransform>("JourneyDialogueOverlay");
            Transform box = overlay.Find("DialogueBox");
            Assert.That(box.Find(KMA.UI.Kit.ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(KMA.UI.Kit.ShellTheme.Cream));
            Assert.That(box.Find("Dialogue").GetComponent<TMP_Text>().color, Is.EqualTo(KMA.UI.Kit.ShellTheme.Ink));
            var problems = KMA.UI.Kit.ShellStyleAudit.Audit(overlay.gameObject);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
```

Run: `tools/run-unity-tests.sh PlayMode KMA.Tests.Presentation.JourneyNarrativeTests journey-dialogue` → FAIL.

- [ ] **Step 2: Restyle**

- Delete `StickerRed`; sticker label colour → `ShellTheme.TitleRed`.
- `SharedMenuBackground`, `LeftActor`, `RightActor`: add `StyleExempt` (in `Build` and in `MakeActor`).
- Dimmer: `MinigameUiTheme.WithAlpha(ShellTheme.Ink, .36f)`.
- Sticker: replace the Image + `SetRadius(18f)` + `Outline` with `ShellKit.StyleSurface(stickerRect, ShellTheme.ControlRadius, ShellTheme.ControlBorder, ShellTheme.ControlShadow, ShellTheme.White);`.
- Dialogue box: replace `UiKit.Shape(overlay, "DialogueBox", 28f, …)` + `Outline` with

```csharp
            ShellCardHandle boxCard = ShellKit.Card(overlay, "DialogueBox");
            RectTransform boxRect = boxCard.Root;
```

  (keep the anchors/offset lines).
- Name tag: replace with

```csharp
            ShellChipHandle tagChip = ShellKit.Chip(boxRect, "NameTag", string.Empty, ShellTheme.Gold);
            nameTagRect = tagChip.Card.Root;
            nameTagImage = tagChip.Card.Face;
            nameTagRect.sizeDelta = new Vector2(320f, 60f);
            nameTag = tagChip.Label;
            nameTag.name = "Name";
```

  (`nameTagImage` is used later for colour per speaker — check usages; if it is recoloured per speaker, keep recolouring the Face.)
- Body: colour `ShellTheme.Ink`. SaveError: size `22f` → `MinigameUiTheme.Caption`, colour `ShellTheme.TitleRed`. Hint: size → `MinigameUiTheme.Caption`, colour `ShellTheme.BodyText`; widen its box to `new Vector2(420f, 40f)`.
- Progress dots line ~246: `HomeMenuStyle.Gold` → `ShellTheme.Gold`; `DotIdle` → `MinigameUiTheme.WithAlpha(ShellTheme.Ink, .25f)` (check its definition and make it a token-based colour).
- `MakeSkip`:

```csharp
        Button MakeSkip()
        {
            ShellButtonHandle handle = ShellKit.Button(overlay, "BỎ QUA", "BỎ QUA »", ShellButtonVariant.Secondary);
            UiKit.Place((RectTransform)handle.Button.transform, Vector2.one, Vector2.one, new Vector2(-58f, -43f),
                new Vector2(240f, ShellTheme.ControlHeight));
            return handle.Button;
        }
```

- `MakeText`: keep, it uses `UiKit.StyleLabel` + `VietTypography.Apply` (fonts are fine).
- Replace remaining `HomeMenuStyle.*` in the file with `ShellTheme.*` (`Navy`→`Ink`, `Gold`→`Gold`, `GoldLight`→`GoldLight`, `White`→`White`).

- [ ] **Step 3: Run tests**

Run: `tools/run-unity-tests.sh PlayMode KMA.Tests.Presentation.JourneyNarrativeTests journey-dialogue` → all passed.
Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation presentation-dialogue` → all passed.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/JourneyDialoguePresenter.cs Assets/Tests/PlayMode/Presentation/JourneyNarrativeTests.cs
git commit -m "feat(ui): draw the journey dialogue with ShellKit"
```

---

### Task 12: Scene transition and Splash on Shell tokens

**Files:**
- Modify: `Assets/_Project/Scripts/UI/SceneTransitionOverlay.cs` (`Build`, `CreateBar`)
- Modify: `Assets/_Project/Scripts/UI/SplashPresentationView.cs` (colours, angles, footer floor)
- Modify: `Assets/_Project/Scripts/UI/UITheme.cs` `SplashStyle` defaults (`statusSize 24`, `footerFontSize 24`) and `Assets/_Project/Settings/UI/UITheme.asset` lines `statusSize: 22` → `24`, `footerFontSize: 20` → `24`
- Modify: `Assets/_Project/Scripts/UI/MapPresentationBuilder.cs` (delete `UseRoundedSurface`/`RoundedRectSprite`)
- Modify tests: `Assets/Tests/EditMode/Presentation/SceneTransitionOverlayTests.cs:85-87`, the splash tests (`grep -rl SplashPresentationView Assets/Tests`)

- [ ] **Step 1: Update tests (failing)**

`SceneTransitionOverlayTests`:

```csharp
                Assert.That(panel.Find("Backdrop").GetComponent<Image>().color, Is.EqualTo(ShellTheme.Ink));
                Assert.That(panel.Find("Card/" + ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(ShellTheme.Cream));
                var problems = ShellStyleAudit.Audit(panel.Find("Card").gameObject);
                Assert.That(problems, Is.Empty, string.Join("\n", problems));
```

`Assets/Tests/PlayMode/Presentation/SplashLoadingFlowTests.cs` — add (uses the file's `Track` helper, as `SplashPresentation_FitsLandscapeRatiosAndRendersVietnameseGlyphs` does):

```csharp
        [Test]
        public void SplashTextKeepsTheSplashFloor()
        {
            var root = Track(new GameObject("SplashLoadingFlowTests.Floor", typeof(RectTransform), typeof(Canvas)));
            var art = Track(new GameObject("Illustration", typeof(RectTransform), typeof(Image)));
            art.transform.SetParent(root.transform, false);
            SplashPresentationView view = SplashPresentationView.Build(root.transform);
            var problems = KMA.UI.Kit.ShellStyleAudit.Audit(view.gameObject, KMA.UI.Kit.ShellAuditOptions.Splash);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
```

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.SceneTransitionOverlayTests transition` and `tools/run-unity-tests.sh PlayMode KMA.Tests.Presentation.SplashLoadingFlowTests splash` → FAIL.

- [ ] **Step 2: Transition overlay**

In `SceneTransitionOverlay.Build`:
- `Color navy = HomeMenuStyle.Navy;` → `Color navy = ShellTheme.Ink;`
- Track lines: rotation `UITheme.Shared.Menu.titleAngle` → `ShellTheme.TitleAngle`; colour `MinigameUiTheme.WithAlpha(ShellTheme.Gold, .55f)`.
- Card: replace the `border` Image, `UseRoundedSurface`, `Shadow` and `Fill` block with `ShellKit.StyleCard(card);`. Delete `CardStroke`, `CardShadow`.
- Badge stays `ShellArt.CreateBadge` (exempt). `HomeMenuStyle.BadgeSize` → `ShellTheme.BadgeSize` (done in Task 5).
- Title shadow colour `HomeMenuStyle.Gold` → `ShellTheme.Gold`.
- `CreateBar`: add `track.gameObject.AddComponent<StyleExempt>();` (slanted art); `HomeMenuStyle.Red` → `ShellTheme.AccentRed`; groove `Color.Lerp(UITheme.Shared.TextPrimary, navy, .15f)` → `ShellTheme.Cream` (a token; the lerp produced an off-palette colour).

Then delete `MapPresentationBuilder.UseRoundedSurface` and `RoundedRectSprite` (+ the `roundedRectSprite` field) — `grep -rn "UseRoundedSurface\|RoundedRectSprite" Assets` must be empty.

- [ ] **Step 3: Splash**

In `SplashPresentationView`:
- `UITheme.Shared.Menu.titleAngle` → `ShellTheme.TitleAngle` (two places); `UITheme.Shared.Menu.gold` → `ShellTheme.Gold`; `UITheme.Shared.Menu.navy` → `ShellTheme.Ink` (two places); `UITheme.Shared.Menu.red` → `ShellTheme.AccentRed`.
- `Footer`: `text.fontSizeMin = style.footerFontSize;` (was `* .6f`, which went below the floor).
- `SplashStyle`: `statusSize = 24f`, `footerFontSize = 24f`; same values in `UITheme.asset`.

- [ ] **Step 4: Run tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.Presentation transition-splash` → all passed.
Run: `tools/run-unity-tests.sh PlayMode KMA.Tests.Presentation.SplashLoadingFlowTests splash` → all passed.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/SceneTransitionOverlay.cs Assets/_Project/Scripts/UI/SplashPresentationView.cs Assets/_Project/Scripts/UI/UITheme.cs Assets/_Project/Settings/UI/UITheme.asset Assets/_Project/Scripts/UI/MapPresentationBuilder.cs Assets/Tests/EditMode/Presentation/SceneTransitionOverlayTests.cs Assets/Tests/PlayMode/Presentation/SplashLoadingFlowTests.cs
git commit -m "feat(ui): put the scene transition and splash on Shell tokens"
```

---

### Task 13: Retire `MenuStyle`/`HomeMenuStyle`, unify sizes, guard every Shell scene

**Files:**
- Modify: `Assets/_Project/Scripts/UI/UITheme.cs` (delete `MenuStyle`, the `menu` field and `Menu` accessor; `LessonJourneyStyle.bodySize = 40f`, `captionSize = 40f`, `objectiveSize = 40f`)
- Modify: `Assets/_Project/Settings/UI/UITheme.asset` (`bodySize: 42` → `40`, `captionSize: 39` → `40`, `objectiveSize: 39` → `40`; leave the stale `menu:` block — Unity drops it on next save)
- Modify: `Assets/_Project/Scripts/UI/VietTypography.cs:53-54` (gradient from `ShellTheme.GoldLight/GoldDark`)
- Modify: `Assets/_Project/Scripts/UI/HomeMenuResponsive.cs:21-22` (inline `MenuScale = 1.7f`, `PanelWidth = 560f` as private consts)
- Delete: `Assets/_Project/Scripts/UI/HomeMenuStyle.cs` + `.meta`
- Modify: `Assets/Editor/StudentJourneyContentBuilder.cs`, `Assets/_Project/Resources/Journey/JourneyDialogues.asset` (typo "đối thủ ." → "đối thủ.")
- Modify: `docs/ui-theme.md`
- Modify: `Assets/Tests/EditMode/EditorTools/ShellStyleConsistencyTests.cs` (scene audits)
- Scenes (via authoring): `Bootstrap.unity`, `Menu.unity`, `Map.unity`, `GameOver.unity`

- [ ] **Step 1: Write the scene audit tests (failing until scenes are re-authored)**

Append to `ShellStyleConsistencyTests`:

```csharp
        static GameObject Find(Scene scene, string name) =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == name).gameObject;

        [TestCase("Assets/_Project/Scenes/GameOver.unity", "GameOverLayout")]
        [TestCase("Assets/_Project/Scenes/Map.unity", "S5MapPresentation")]
        [TestCase("Assets/_Project/Scenes/Menu.unity", "HomeMenuLayout")]
        public void AuthoredShellScreensFollowTheShellStyle(string scenePath, string rootName)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var problems = ShellStyleAudit.Audit(Find(scene, rootName));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void BootstrapSplashFollowsTheSplashRules()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            var problems = ShellStyleAudit.Audit(Find(scene, "SplashLayout"), ShellAuditOptions.Splash);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void ShellScreensHaveNoTextBelowTheFloor()
        {
            foreach (string path in new[] { "GameOver", "Map", "Menu" })
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{path}.unity", OpenSceneMode.Single);
                foreach (TMP_Text text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
                {
                    if (text.GetComponentInParent<GameplayDialogueView>(true) != null || text.color.a < .01f) continue;
                    float smallest = text.enableAutoSizing ? text.fontSizeMin : text.fontSize;
                    Assert.That(smallest, Is.GreaterThanOrEqualTo(MinigameUiTheme.MinimumFontSize), $"{path}: {text.name}");
                }
            }
        }
```

(`HomeMenuLayout` holds the invisible hit areas — their labels are clear and skipped; the confirm card is runtime-only and covered by Task 7.)

- [ ] **Step 2: Remove the menu tokens**

- `grep -rn "HomeMenuStyle\|\.Menu\.\|MenuStyle" Assets --include=*.cs` — every remaining hit outside `UITheme.cs`/`HomeMenuStyle.cs` must be migrated now with the mapping `Navy→ShellTheme.Ink`, `Gold→ShellTheme.Gold`, `GoldLight→ShellTheme.GoldLight`, `GoldDark→ShellTheme.GoldDark`, `Red→ShellTheme.AccentRed`, `White→ShellTheme.White`, `BadgeSize→ShellTheme.BadgeSize`, `Menu.titleAngle→ShellTheme.TitleAngle`, `Menu.buttonSlantAngle→ShellTheme.SlantAngle`, `Menu.pressScale` (no caller left). `JourneyLessonPresentation`'s `Theme.Menu.goldDark` is gone after Task 10; check.
- Delete `MenuStyle`, `menu`, `Menu` from `UITheme.cs`; delete `HomeMenuStyle.cs` + `.meta`.
- `HomeMenuResponsive.cs`: replace `HomeMenuStyle.MenuScale` / `HomeMenuStyle.PanelWidth` with private consts `MenuScale = 1.7f`, `PanelWidth = 560f`.
- `VietTypography.cs`: `var top = (Color32)KMA.UI.Kit.ShellTheme.GoldLight; var bottom = (Color32)KMA.UI.Kit.ShellTheme.GoldDark;`.
- `LessonJourneyStyle`: `bodySize = 40f`, `captionSize = 40f`, `objectiveSize = 40f` (+ asset lines 91-93). In `JourneyLessonPresentation`, `status.fontSizeMin = Style.captionSize - 3f` → `Mathf.Max(MinigameUiTheme.MinimumFontSize, Style.captionSize - 8f)` and the play label `Style.captionSize - 2f` (gone after Task 10).
- Typo: in `StudentJourneyContentBuilder.cs` and `JourneyDialogues.asset` replace `"đối thủ . "` with `"đối thủ. "` (two strings in the builder, one in the asset).

- [ ] **Step 3: Re-author all Shell scenes and validate**

Run `ShellSceneAuthoring.Apply`, then `"$UNITY" -batchmode -projectPath . -executeMethod KMA.EditorTools.ShellSceneAuthoring.Validate -quit -logFile Builds/validate-shell.log` → `grep -E "error|Exception" Builds/validate-shell.log` empty (Validate checks no duplication on rebuild and no transient sprites). Keep the four scenes; restore font churn.

- [ ] **Step 4: Run tests**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests.EditorTools.ShellStyleConsistencyTests shell-scenes` → all passed. For each problem: replace the literal with the nearest Shell token in the builder; if the image is content art, add `StyleExempt` in the builder (never in the scene); re-author; re-run.
Run: `tools/run-unity-tests.sh EditMode KMA.Tests editmode-all` → all passed.

- [ ] **Step 5: Update `docs/ui-theme.md`**

Replace the bullet list with (Vietnamese, matching the file):

```markdown
- Hai họ style: **Shell** cho mọi màn ngoài minigame (Menu, Settings, Map, popup bài học, hộp thoại hành trình, màn chuyển cảnh, GameOver, Splash) và **Kit** cho minigame. Shell dựng bằng `ShellKit`, Kit bằng `UiKit`; không dùng chéo.
- Shell (`ShellStyle` trong asset): màu kem, navy (`ink`), vàng nút, đỏ tiêu đề, chữ phụ, disabled; viền card 6 / control 4; bo 20 / 12; bóng navy cứng (8,-8) / (5,-5); bóng vàng tiêu đề (3,-3); góc nghiêng tiêu đề và thanh tải. Khi nhấn, thân nút trượt xuống trùng bóng.
- Palette chung: Primary (= đỏ nhấn Shell), Accent, Surface, Text Primary, Scrim. Thang chữ chung 160/54/48/40/32/28, tối thiểu 28 (Splash 24).
- Corner Radius / Border Width / Shadow Offset gốc dùng cho Kit (nút 1.5 lần bán kính, nút pause 5/6, viền kit 3/4).
- Motion: thời gian phản hồi nút, fade, các bước hiện kết quả và chu kỳ ánh sáng badge. Animation dùng thời gian unscaled để hoạt động khi pause.
- `ShellStyleAudit` và `MinigameStyleAudit` kiểm tra mọi màn; ảnh nội dung (icon, chân dung, huy hiệu) gắn `StyleExempt`.
```

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/UITheme.cs Assets/_Project/Settings/UI/UITheme.asset Assets/_Project/Scripts/UI/VietTypography.cs Assets/_Project/Scripts/UI/HomeMenuResponsive.cs Assets/_Project/Scripts/UI/JourneyLessonPresentation.cs Assets/Editor/StudentJourneyContentBuilder.cs Assets/_Project/Resources/Journey/JourneyDialogues.asset docs/ui-theme.md Assets/Tests/EditMode/EditorTools/ShellStyleConsistencyTests.cs Assets/_Project/Scenes/Bootstrap.unity Assets/_Project/Scenes/Menu.unity Assets/_Project/Scenes/Map.unity Assets/_Project/Scenes/GameOver.unity
git rm Assets/_Project/Scripts/UI/HomeMenuStyle.cs Assets/_Project/Scripts/UI/HomeMenuStyle.cs.meta
git commit -m "refactor(ui): retire the menu tokens and guard every Shell scene with the audit"
```

---

### Task 14: Kit side — Volleyball round buttons, kit literals, wider minigame audit

**Files:**
- Modify: `Assets/_Project/Scripts/UI/Kit/UiKit.cs` (add `RoundIconButton`)
- Modify: `Assets/Editor/VolleyballSceneConfigurator.cs:378,391,418,450-474`
- Modify: `Assets/_Project/Scripts/UI/Dialogue/GameplayDialogueView.cs:27-28`
- Modify: `Assets/_Project/Scripts/UI/Kit/KitPressFeedback.cs:131`, `Assets/_Project/Scripts/UI/Kit/MinigameUiTheme.cs` (const)
- Modify: `Assets/Tests/EditMode/EditorTools/MinigameStyleConsistencyTests.cs`, `Assets/Tests/EditMode/Presentation/UiKitTests.cs`
- Scene (via configurator): `Assets/_Project/Scenes/MG_Volleyball.unity`

**Interfaces:**
- Produces: `RoundButtonHandle UiKit.RoundIconButton(RectTransform hitArea, float size = MinigameUiTheme.RoundButton)` — the `RoundButton` shape (shadow disc, rim, accent face, feedback on the hit area) with no label; `RoundButtonHandle.Label` is `null`. `MinigameUiTheme.DisabledGroupAlpha = .58f`.

- [ ] **Step 1: Write the failing tests**

`UiKitTests`:

```csharp
        [Test]
        public void RoundIconButtonHasTheRoundShapeAndNoLabel()
        {
            RectTransform hit = UiKit.Rect(root.transform, "Hit");
            RoundButtonHandle handle = UiKit.RoundIconButton(hit, 200f);
            Assert.That(hit.Find("RoundButton/Face").GetComponent<Image>().color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(hit.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
            Assert.That(hit.GetComponent<KitPressFeedback>(), Is.Not.Null);
            Assert.That(handle.Label, Is.Null);
        }
```

`MinigameStyleConsistencyTests`:

```csharp
        [TestCase(ChessFinalSceneConfigurator.ScenePath)]
        [TestCase(FrogJumpSceneConfigurator.ScenePath)]
        public void MoreMinigameHudsFollowTheKit(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject hud = scene.GetRootGameObjects().Single(go => go.name == "S2_HUD_Minigame");
            var problems = MinigameStyleAudit.Audit(hud);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void CelebrationCanvasesFollowTheKit()
        {
            var scene = EditorSceneManager.OpenScene(CelebrationSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var problems = scene.GetRootGameObjects().Where(go => go.GetComponent<Canvas>() != null)
                .SelectMany(MinigameStyleAudit.Audit).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void VolleyballActionButtonsUseTheRoundKitShape()
        {
            var scene = EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { "ActionButton", "JumpButton" })
            {
                Transform button = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .First(t => t.name == name);
                Assert.That(button.Find("RoundButton/Rim"), Is.Not.Null, name);
                Assert.That(button.Find("RoundButton/Face").GetComponent<UnityEngine.UI.Image>().color,
                    Is.EqualTo(MinigameUiTheme.Accent), name);
            }
        }
```

(`PausePanel` and the guide panels live inside `S2_HUD_Minigame` in every scene; the HUD audits cover them once Chess and FrogJump are added.)

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation.UiKitTests;KMA.Tests.EditorTools.MinigameStyleConsistencyTests" kit-wide` → FAIL.

- [ ] **Step 2: `UiKit.RoundIconButton`**

Refactor `RoundButton` so the disc layers live in a private helper and add the label-less variant:

```csharp
        public static RoundButtonHandle RoundButton(RectTransform hitArea, string text,
            float size = MinigameUiTheme.RoundButton)
        {
            RoundButtonHandle shape = RoundShape(hitArea, size);
            TMP_Text label = Label(shape.Face.transform, "Label", text, MinigameUiTheme.Headline, MinigameUiTheme.Surface);
            VietTypography.Apply(label, VietFontRole.ButtonPrimary);
            Stretch(label.rectTransform);
            FitLabel(label, MinigameUiTheme.Headline);
            return new RoundButtonHandle(shape.Root, shape.Shadow, shape.Rim, shape.Face, label, shape.Feedback);
        }

        /// The round action button without a caption, for controls drawn as icons (Volleyball ĐÁNH/NHẢY).
        public static RoundButtonHandle RoundIconButton(RectTransform hitArea, float size = MinigameUiTheme.RoundButton) =>
            RoundShape(hitArea, size);

        static RoundButtonHandle RoundShape(RectTransform hitArea, float size)
        {
            RectTransform root = Rect(hitArea, "RoundButton");
            Place(root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.one * size);
            float drop = MinigameUiTheme.ShadowOffset.y * 2f;
            Image shadow = Disc(root, "Shadow", false,
                MinigameUiTheme.WithAlpha(MinigameUiTheme.TextOutline, MinigameUiTheme.ShadowColor.a));
            Stretch(shadow.rectTransform, new Vector2(0f, drop), new Vector2(0f, drop));
            Image rim = Disc(root, "Rim", true, MinigameUiTheme.TextPrimary);
            Stretch(rim.rectTransform);
            float inset = size * 3f / 64f;
            Image face = Disc(root, "Face", false, MinigameUiTheme.Accent);
            Stretch(face.rectTransform, Vector2.one * inset, -Vector2.one * inset);
            var feedback = GetOrAdd<KitPressFeedback>(hitArea);
            feedback.Configure(face, root);
            return new RoundButtonHandle(root, shadow, rim, face, null, feedback);
        }
```

(Check `RoundButtonHandle`'s field names in `UiKitHandles.cs` and match them.)

- [ ] **Step 3: Volleyball**

Replace `BuildTransparentIconButton(buttonRect, false, …)` / `(jumpRect, true, …)` with `BuildRoundIconButton(buttonRect, false, …)` / `(jumpRect, true, …)`:

```csharp
        static void BuildRoundIconButton(RectTransform hitArea, bool jump, float size)
        {
            RoundButtonHandle handle = UiKit.RoundIconButton(hitArea, size);
            UiKitAssets assets = UiKitAssets.Load();
            if (jump)
                BuildJumpGlyph(handle.Root, assets.RoundRect24, size);
            else
                BuildHitGlyph(handle.Root, assets.RoundRect24, size);
            handle.Root.gameObject.AddComponent<CanvasGroup>();
        }
```

Delete `BuildTransparentIconButton`. In `BuildHitGlyph`/`BuildJumpGlyph`, glyph parts that were `MinigameUiTheme.TextPrimary` on the dark plate must now read on the gold face: change their colour to `MinigameUiTheme.Surface` (keep `TextOutline` edges). Timer: `timer.fontSize = 36f;` → `timer.fontSize = MinigameUiTheme.BodyLarge;`.

Rebuild: `"$UNITY" -batchmode -projectPath . -executeMethod KMA.EditorTools.VolleyballSceneConfigurator.BuildScene -quit -logFile Builds/volley.log`.

The existing `JumpButtonAndPlayerAimMarkerAreWired` asserts `RoundButton/JumpHead` exists, no TMP label, every icon part `raycastTarget == false`, `KitPressFeedback` present, and the disc inside the hit area — all must still pass.

- [ ] **Step 4: Kit literals**

- `GameplayDialogueView.cs:27-28`: `28` → `MinigameUiTheme.Caption` (both labels).
- `MinigameUiTheme`: add `public const float DisabledGroupAlpha = .58f;` next to `SurfaceDisabled`; `KitPressFeedback.cs:131`: `.58f` → `MinigameUiTheme.DisabledGroupAlpha`.

- [ ] **Step 5: Run tests and fix what the wider audit finds**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation;KMA.Tests.EditorTools" kit-wide` → all passed. For Chess/FrogJump/Celebration findings: board squares, pieces and character art get `StyleExempt` in their configurator/builder (content art); any HUD literal gets the nearest `MinigameUiTheme` token; re-run the configurator (`KMA.EditorTools.ChessFinalSceneConfigurator.BuildScene`, `FrogJumpSceneConfigurator.BuildScene`, `CelebrationSceneConfigurator.BuildScene` — check exact method names with `grep -n "public static void" Assets/Editor/<file>`) and re-run the tests.
Run: `tools/run-unity-tests.sh PlayMode KMA.Tests.Gameplay.Volleyball volley-play` → all passed.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/Kit/UiKit.cs Assets/Editor/VolleyballSceneConfigurator.cs Assets/_Project/Scripts/UI/Dialogue/GameplayDialogueView.cs Assets/_Project/Scripts/UI/Kit/KitPressFeedback.cs Assets/_Project/Scripts/UI/Kit/MinigameUiTheme.cs Assets/Tests/EditMode/EditorTools/MinigameStyleConsistencyTests.cs Assets/Tests/EditMode/Presentation/UiKitTests.cs Assets/_Project/Scenes/MG_Volleyball.unity
git commit -m "feat(ui): round kit buttons for volleyball and audit every minigame HUD"
```

(Add any configurator/scene files Step 5 changed.)

---

### Task 15: Visual QA, full test run, report

**Files:**
- Create: `docs/qa/shell-ui-style.md`, `docs/qa/images/shell-*.png` (selected captures)

- [ ] **Step 1: Full test run**

Run: `tools/run-unity-tests.sh EditMode KMA.Tests editmode-final` and `tools/run-unity-tests.sh PlayMode KMA.Tests playmode-final`. Expected: 0 failed in both XMLs. Record the totals.

- [ ] **Step 2: Captures (16:9 and 20:9)**

With a GUI Editor running, capture into `Builds/Screenshots/shell/`:
- Menu (idle) and Menu with the new-game confirm card open (Continue enabled save)
- Settings
- Map (fresh game), Map with the lesson popup open, Map with the journey dialogue
- Scene transition (trigger a load; or `SceneTransitionOverlay.Show()` from the screenshot tool's `--eval` if supported)
- GameOver
- Bootstrap splash
- MG_Volleyball (ĐÁNH/NHẢY)

Check each for: text overflowing its box (Review Focus 3), the stop tag clearing the ring, the close glyph rendering (not a box), the disabled Continue reading as locked, and no layer misalignment at 20:9. Fix in builders/tokens, re-author, re-capture.

- [ ] **Step 3: Report**

Write `docs/qa/shell-ui-style.md` with: date, what changed per screen (one line each), test totals from Step 1, a table of captures (screen × aspect → image path), and any known issue left. Copy one 16:9 capture per screen to `docs/qa/images/shell-<screen>.png`. For "before", reference the audit findings in section 2 of the spec rather than re-capturing old commits.

- [ ] **Step 4: Commit**

```bash
git add docs/qa/shell-ui-style.md docs/qa/images/shell-*.png
git commit -m "docs(qa): record the Shell UI style pass"
```
