# Đồng bộ style panel và button toàn game: họ "Shell" + "Kit"

**Ngày:** 2026-10-13

**Trạng thái:** Thiết kế đã được người dùng duyệt từng phần trong hội thoại; đặc tả viết chờ duyệt, sau đó lập kế hoạch triển khai.

**Nguồn yêu cầu:** “Kiểm tra các style của phần UI (panel, button) của các màn hình game và đề xuất sửa lại cho đồng bộ style toàn game.”

## 1. Mục tiêu và quyết định đã thống nhất

Mọi màn ngoài minigame dùng chung một ngôn ngữ hình ảnh lấy từ key art menu (“Shell”); minigame giữ họ “Kit”. Hai họ dùng chung token gốc trong `UITheme`, mỗi họ có factory và audit riêng để style không trôi lại.

| Câu hỏi | Quyết định |
|---|---|
| Ngôn ngữ hình ảnh | **Hướng A:** chính thức hóa style “menu card” (kem, viền navy, bóng navy cứng) cho mọi màn ngoài minigame; minigame giữ Kit |
| Biến thể Shell | **A — giống key art:** viền 6, bóng navy cứng chéo, bo vừa (chọn trên visual companion) |
| Kiến trúc | **Hướng 1:** `ShellKit` song song `UiKit`, token chung trong `UITheme` |
| Audit | Mở rộng cho các màn ngoài minigame (`ShellStyleAudit`) và các minigame chưa được phủ |
| Splash | Giữ thang chữ riêng (màn trình diễn), sàn chữ 24 |
| Hiệu ứng nhấn Shell | Mặt nút trượt xuống vị trí bóng, không co |
| Nút ĐÁNH/NHẢY Volleyball | Chuyển sang `UiKit.RoundButton` |
| Thay đổi fix/Duong chưa commit | Đã commit trước khi bắt đầu: `f933c86`, `742ae68` |

Thay thế mục 4 “Button — hai họ” của `2026-10-06-campus-ui-unification-design.md`: họ “Xiên” không còn được dùng (Menu đã chuyển sang key art), Shell thế chỗ.

### Giả định

- Không đổi gameplay, layout màn hay luồng điều hướng.
- Art key art của Menu/GameOver (`MenuKeyArtPanel.png`, `MenuKeyArtBackdrop.png`) giữ nguyên làm chuẩn tham chiếu.
- Tỉ lệ cần đạt: 16:9 và 20:9 landscape, như các spec trước.

## 2. Hiện trạng (kết quả điều tra)

**Hệ thống token**

- `UITheme` có palette gốc, `MenuStyle`, `LessonJourneyStyle`, `SplashStyle`, `DialogueStyle`, `MotionStyle` và 13 màu `map*`. `MinigameUiTheme` proxy sang `UITheme` cho Kit và giữ thang chữ/spacing/kích thước dạng hằng số.
- Trùng/xung đột: hai press scale (`Motion` .94, `Menu` .96); ba bộ disabled (`UITheme.disabled*`, `Menu.disabled*`, alpha trong `KitControlState`, cộng `.58` cứng trong `KitPressFeedback.cs:131`); hai màu success (`success`, `resultSuccess`); viền 4/3/2 ở nhiều chỗ; bốn thang chữ (Kit, LessonJourney 54/42/39, Menu 23/22/18, Splash 26/22/20); `spacing`, `muted`, `border` không được dùng ở production; gold nhạt/đậm viết lại trong `VietTypography.cs:53-54`.
- `HomePresentationBuilder` tự sinh `DiscSprite`/`SlantSprite` ngoài `UiKitAssets`.

**Màn hình**

- **Menu:** panel key art vẽ sẵn + vùng bấm trong suốt. `HomeMenuButton` và `HomePresentationBuilder.StyleButton` không còn ai gọi; `MainMenuScreen.cs:27-30` vẫn lặp qua chúng. Màu hover/nhấn trong `HomeKeyArtButton.cs:12-14` và màu disabled `HomePresentationBuilder.cs:88` viết cứng. Card xác nhận New Game (`S5ShellSceneController.cs:234-248`) là Image vuông + Outline `(5,-5)`, chữ 22.
- **GameOver:** card kem tự dựng với hằng số riêng (`GameOverPresentationBuilder.cs:19-34`): Cream/ButtonGold/TitleRed, viền 6, bóng `(8,-8)`, nút bo 14 bóng `(5,-5)`, chữ 26.
- **Settings:** panel và toggle/slider là Image vuông không sprite (`SettingsPresentationBuilder.cs:31-79`), nút Back là Kit; chữ hint 20.
- **Map header:** `MenuBlock` + `RoundedRectSprite` riêng, bóng `(6,-6)` (`MapPresentationBuilder.cs:232-366`), nhiều Outline và màu cứng (L173, 210-212, 318, 418, 434), chữ 20–26.
- **Map stops:** `BrutalButton` (`MapStopBuilder.cs:27`, `MapNodeView.cs:192`), Outline `(3,-3)`/`(6,-6)` (`MapStopBuilder.cs:41-65,125`), tag 190×38, chữ 20/24.
- **Popup bài học:** nút đóng Outline vàng (`JourneyLessonPresentation.cs:93-95`), CHƠI tự dựng (L273-278).
- **Hộp thoại Journey:** navy + 3 Outline, `StickerRed` cứng (`JourneyDialoguePresenter.cs:22,425-449`), chữ 22.
- **SceneTransitionOverlay:** `CardShadow (6,-6)` (L17, 263).
- **Minigame:** HUD/Pause/Result/Guide, Sprint, Football, Chess, Celebration dùng Kit. Volleyball ĐÁNH/NHẢY là `BuildTransparentIconButton` (`VolleyballSceneConfigurator.cs:450-474`), timer chữ 36 (L418); `GameplayDialogueView.cs:27-28` chữ 28 viết cứng.
- **Audit:** `MinigameStyleAudit` chỉ phủ prefab HUD/PhaseOverlay/ResultPanel, HUD Football/Volleyball và Sprint runtime. Chess, FrogJump, Celebration, Menu, Map, Settings, GameOver, Splash không được phủ.

## 3. Token

### 3.1 `UITheme.ShellStyle` (mới)

| Token | Giá trị | Ghi chú |
|---|---|---|
| `cream` | `#FBF4E4` | Mặt card/nút phụ (lấy từ key art) |
| `ink` | `#0B2A4A` | Viền, bóng, chữ chính; thay `Menu.navy` |
| `buttonGold` | `#F9C82B` | Mặt nút Primary |
| `titleRed` | `#C62828` | Chỉ tiêu đề cảnh báo (vd “HẾT LƯỢT”) |
| `accentRed` | = `UITheme.primary` `#FF595E` | Ô vuông trang trí, nút Danger |
| `bodyText` | `#33475E` | Chữ phụ trên nền kem |
| `disabledFace` / `disabledText` | `#E6E1D6` / `#8A94A3` | Disabled Shell: rõ là khóa, chữ vẫn đọc được |
| `cardBorder` / `controlBorder` | 6 / 4 | |
| `cardShadow` / `controlShadow` | `(8,-8)` / `(5,-5)` | Bóng `ink` đặc |
| `cardRadius` / `controlRadius` | 20 / 12 | |
| `pressedShift` | = `controlShadow` | Khi nhấn mặt nút trượt tới vị trí bóng |
| `titleDrop` | `(3,-3)` màu `buttonGold` | Bóng vàng tiêu đề như key art |
| `map` (nhóm con) | các màu `map*` hiện có | Chuyển nguyên giá trị từ `UITheme` vào đây |

`ShellStyle.Palette()` trả danh sách màu hợp lệ cho audit: các màu trên + `UITheme.Scrim` + màu thể thao của `LessonJourneyStyle` (nội dung điểm dừng).

### 3.2 Gộp token chung

- **Thang chữ chung** (Kit và Shell): Display 160 / Title 54 / Headline 48 / BodyLarge 40 / Body 32 / Caption 28; sàn 28 mọi màn trừ Splash. Thang chuyển từ hằng số `MinigameUiTheme` sang `UITheme.TypeScale` (serialized); `MinigameUiTheme` proxy lại để không đổi call site Kit. `LessonJourneyStyle.*Size`, `DialogueStyle.nameSize/textSize`, cỡ chữ Menu quy về thang này (54→Title, 42→BodyLarge, 39→BodyLarge 40, 22→Caption).
- **Splash:** giữ `SplashStyle` riêng; `statusSize` và `footerFontSize` nâng lên tối thiểu 24.
- **Press:** chỉ còn `Motion.pressScale` (Kit) và `Motion.pressRestore` (Kit + Shell). Xóa `Menu.pressScale`.
- **Disabled Kit:** `KitPressFeedback` dùng `UITheme.DisabledSurface/DisabledText`, bỏ alpha `.58` cứng.
- **Xóa:** `MenuStyle` (toàn bộ; `navy`/`gold`/`red` chuyển sang `ShellStyle`, `goldLight`/`goldDark` chuyển vào `ShellStyle` cho gradient tiêu đề và `VietTypography.cs:53-54` đọc từ đó), `success`, `muted`, `border`, `spacing` (Space* giữ trong `MinigameUiTheme`). `UITheme.asset` được cập nhật tương ứng; test đọc các field đã xóa được sửa.
- Kit giữ hình dạng và bóng mềm `(0,-4)`.

## 4. `ShellKit`

Thư mục `Assets/_Project/Scripts/UI/Shell/`.

**Cấu trúc thị giác:** mọi phần tử là 3 lớp Image dùng sprite bo góc bake sẵn — `Shadow` (`ink`, lệch theo token) → `Border` (`ink`) → `Face` (inset bằng độ dày viền). Không dùng component `Outline`/`Shadow`. `UiKitAssets` bake thêm `RoundRect12`; `RoundRect20` đã có. `SlantSprite`/`DiscSprite` chuyển từ `HomePresentationBuilder` sang `UiKitAssets` (bake qua `UiShapeRaster`).

| API | Dùng cho |
|---|---|
| `ShellKit.Card(parent, name, decorated = false)` / `StyleCard` | Panel GameOver, Settings, popup bài học, card xác nhận, card chuyển cảnh, hộp thoại Journey. `decorated` thêm 2 ô vuông `accentRed` viền `ink` |
| `ShellKit.Button(parent, name, text, ShellButton variant)` / `StyleButton(button, variant, text = null)` | Mọi nút ngoài minigame; cao mặc định 84. Primary: mặt `buttonGold`, chữ `ink`. Secondary: mặt `cream`, chữ `ink`. Danger: mặt `accentRed`, chữ `cream` |
| `ShellKit.IconButton(parent, name, glyph)` | “‹” Map, “✕” đóng popup; vuông 88 |
| `ShellKit.Toggle(parent, name)` / `ShellKit.Slider(parent, name)` | Settings: track bo tròn viền `ink`, bật = `buttonGold`, knob `ink` |
| `ShellKit.Chip(parent, name, text)` | Ô mạng Map, nhãn trạng thái, tag điểm dừng |
| `ShellKit.Badge(parent, name, ShellBadgeState state, Color sport)` | Điểm dừng Map: Locked / Ready / Current / Complete, màu từ `ShellStyle.map` + màu môn |
| `ShellKit.Title(parent, name, text, bool warning = false)` / `ShellKit.Label(parent, name, text, TypeRole role)` | Tiêu đề Saira Black màu `ink` (hoặc `titleRed` khi `warning`) có `titleDrop`; chữ thường Be Vietnam Pro theo thang chung |
| `ShellPressFeedback` (MonoBehaviour) | Nhấn: `Face` + `Border` dịch `pressedShift`, bóng giữ nguyên; thả: trở lại trong `Motion.pressRestore` (unscaled). Disabled: đổi sang `disabledFace`/`disabledText`, bỏ dịch |

**Quy ước**

- Mọi hàm idempotent (gọi lại không nhân đôi lớp), như `UiKit.StyleButton`, để authoring `Rebuild` an toàn.
- `Style*` không đụng `onClick`, `interactable` hay `RectTransform` đã author.
- `ShellKit` dùng `UiKitAssets` và `VietTypography`; không tham chiếu `MinigameUiTheme`. Minigame không gọi `ShellKit`; màn Shell không gọi `UiKit.Button`/`UiKit.Panel`.
- Hộp thoại Journey dùng Shell; hộp thoại trong minigame (`GameplayDialogueView`) giữ Kit. Cùng layout, khác họ theo màn chứa nó.

## 5. Chuyển đổi từng màn

| Màn | Thay đổi |
|---|---|
| Menu | Giữ key art + vùng bấm. `HomeKeyArtButton` lấy màu hover/nhấn từ token (`ShellStyle.ink` alpha), `HomePresentationBuilder.cs:88` dùng `disabledFace`. Card xác nhận New Game (`S5ShellSceneController.cs:234-272`) → `ShellKit.Card` + Button Primary “CHƠI MỚI” / Secondary “HỦY” |
| GameOver | `GameOverPresentationBuilder` bỏ hằng số riêng, dùng `ShellKit.Card(decorated: true)`, `Title(warning: true)`, `Button`; chữ 26 → Caption 28 |
| Settings | Panel → `ShellKit.Card`; toggle/slider → `ShellKit.Toggle`/`Slider`; Back → `ShellKit.Button(Secondary)`; hint → Caption 28 |
| Map header | `MenuBlock`, `RoundedRectSprite`, `BlockShadow` → `ShellKit.Card`/`Chip`/`IconButton("‹")`; bỏ Outline cứng; chữ theo thang chung |
| Map stops | `MapStopBuilder`/`MapNodeView` → `ShellKit.Badge` + `Chip` (tag); bỏ `BrutalButton` và Outline. Tag tăng kích thước nếu chữ 28 tràn (chốt bằng ảnh) |
| Popup bài học / danh sách | Card → `ShellKit.Card`; đóng → `IconButton("✕")`; CHƠI → `ShellKit.Button(Primary)`; nút chọn chặng → `Badge` |
| Hộp thoại Journey | Hộp → `ShellKit.Card`; “BỎ QUA” → `ShellKit.Button(Secondary)`; bỏ 3 Outline, `StickerRed` |
| SceneTransitionOverlay | Card → `ShellKit.Card`; bỏ `CardShadow` |
| Splash | Giữ thiết kế; dùng `SlantSprite`/`DiscSprite` từ `UiKitAssets`; status/footer ≥ 24 |
| Volleyball | ĐÁNH/NHẢY → `UiKit.RoundButton` thay `BuildTransparentIconButton`; giữ vùng chạm 400/300; timer 36 → BodyLarge 40 |
| Gameplay dialogue | `GameplayDialogueView.cs:27-28` dùng `MinigameUiTheme.Caption` |
| Nội dung | Sửa “đối thủ .” → “đối thủ.” trong `StudentJourneyContentBuilder.cs` và `JourneyDialogues.asset` |

**Dọn dẹp:** xóa `HomeMenuButton`, `HomePresentationBuilder.StyleButton` (và helper chỉ nó dùng), `BrutalButton`, vòng lặp `HomeMenuButton` trong `MainMenuScreen.cs:27-30`, nhánh hủy `BrutalButton` trong `UiKit.StyleButton` (`UiKit.cs:444-446`).

## 6. Audit

### 6.1 `ShellStyleAudit`

`Scripts/UI/Shell/ShellStyleAudit.cs`, cùng dạng `MinigameStyleAudit.Audit(GameObject root) → List<string>`. Helper chung (`PathOf`, kiểm font) tách sang `Scripts/UI/Kit/StyleAuditShared.cs` để hai audit dùng chung.

| Luật | Shell |
|---|---|
| Sprite | Chỉ sprite thuộc `UiKitAssets` (RoundRect*, Circle, Ring, Slant, Disc) |
| Màu Image/chữ | Thuộc `ShellStyle.Palette()` (so RGB, như Kit) |
| Font | Một trong 4 font Việt của `VietFontLibrary` |
| Cỡ chữ | ≥ Caption 28 (Splash: ≥ 24, qua tham số `minimumFontSize`) |
| Component cấm | `Outline`, `Shadow`, `BrutalButton`, `HomeMenuButton`, `UnityEngine.UI.Text` |
| Miễn trừ | Image dùng sprite key art (`MenuKeyArtPanel`, `MenuKeyArtBackdrop`, `HomeIllustration`), Image alpha < .01 (vùng bấm), tên `Icon` và con của nó, artwork của dialogue (`IsArtwork`) |

### 6.2 Mở rộng `MinigameStyleAudit`

- Thêm luật cấm `Outline`/`Shadow` component, miễn cho label của `Bar` và `Countdown` (cần viền chữ để đọc trên nền gameplay).
- Phủ thêm: MG_ChessFinal, MG_FrogJump, Celebration, `PausePanel`, guide panel.

### 6.3 Test

- `ShellStyleConsistencyTests` (EditMode): chạy `ShellStyleAudit` trên scene Menu (kể cả card xác nhận dựng runtime), Map (header, stops, popup bài học mở sẵn), Settings, GameOver; Splash với sàn 24.
- `StyleFamilyBoundaryTests` (EditMode): builder minigame không gọi `ShellKit`, builder Shell không gọi `UiKit.Button`/`UiKit.Panel` (kiểm qua audit: Shell root không được chứa sprite/màu Kit-only và ngược lại).
- Mở rộng `MinigameStyleConsistencyTests` theo 6.2.

## 7. Kiểm chứng

- **Duyệt ảnh hai bước:** (1) làm `ShellKit` + chuyển GameOver, chụp và gửi người dùng duyệt; (2) sau khi duyệt mới chuyển các màn còn lại.
- **Ảnh trước/sau** qua `tools/qa-screenshot.sh` (skill `testing-unity-ui-with-screenshots`) ở 16:9 và 20:9: Menu (+ card xác nhận), Settings, Map (+ popup bài học, hộp thoại Journey), GameOver, Splash, chuyển cảnh, Volleyball (ĐÁNH/NHẢY). Ảnh chọn lọc vào `docs/qa/images/`, báo cáo `docs/qa/shell-ui-style.md`.
- **EditMode:** `ShellKit` (idempotent, đúng 3 lớp và thứ tự, lấy giá trị từ token, disabled); `ShellPressFeedback` (dịch khi nhấn, trở lại, không dịch khi disabled); test audit ở mục 6.3.
- **PlayMode:** bấm các nút Menu, Map, Settings, GameOver vẫn gọi đúng `onClick` sau khi đổi style; Volleyball ĐÁNH/NHẢY vẫn nhận input.
- Chạy lại toàn bộ EditMode và PlayMode hiện có.

## 8. Ngoài phạm vi

Art key art Menu/GameOver; hình dạng Kit trong minigame; gameplay, độ khó và layout màn; nội dung chữ (trừ lỗi dấu chấm ở mục 5); build Android.

## 9. Rủi ro và lưu ý

- GameOver, Menu, MG_Volleyball được dựng bởi authoring/configurator; thay đổi phải nằm trong builder/configurator rồi `Rebuild`, không chỉnh tay scene.
- Sàn chữ 28 có thể tràn ở Map (tag 190×38, ô mạng); tăng kích thước tag theo ảnh.
- Xóa `BrutalButton`, `HomeMenuButton`, `MenuStyle` làm hỏng test đang tham chiếu; sửa trong cùng đợt.
- Đổi `UITheme` (thêm `ShellStyle`, `TypeScale`, xóa field) cần cập nhật `UITheme.asset`; Unity bỏ field không còn trong code khi serialize lại — kiểm `docs/ui-theme.md` và cập nhật mô tả token.
