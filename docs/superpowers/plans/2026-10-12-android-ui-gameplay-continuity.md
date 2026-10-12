# Plan — Sửa UI Android, flow và animation

Ngày: 2026-10-12. Baseline local: `10b86bc`. Trạng thái: kế hoạch triển khai, chưa thực hiện thay đổi game.

Spec: [Android UI, luồng liên tục và animation](../specs/2026-10-12-android-ui-gameplay-continuity-design.md).

## 1. Thứ tự triển khai và đầu ra

Bộ task chi tiết: [36 task có dependency và tiêu chí Done](../tasks/2026-10-12-android-ui-gameplay-continuity/README.md). Các mục T0–T9 bên dưới là nhóm công việc, không còn là task nhỏ nhất để giao.

Phụ thuộc chính: T0 → T1; các nhánh T2/T3/T4/T5/T7/T8 đi theo dependency của task cụ thể. T6 policy/dialogue có thể làm trước, nhưng kiểm toàn Journey phải đợi T4/T5. T9 tích hợp đợi các nhánh tính năng. Không coi chuỗi T0–T9 là phụ thuộc bắt buộc tuần tự; file dùng chung cần một owner khi làm đồng thời. Mỗi task hoàn thành khi source, config/asset/scene và kiểm chứng thực đều nhất quán.

| Task | Yêu cầu | Đầu ra |
|---|---|---|
| T0 | Tất cả | Baseline code/save/screenshots có thể so sánh |
| T1 | R1, R7 | Full-bleed background, safe-area chuẩn và text roles |
| T2 | R1 | Map fit màn, popup Học/Luyện/Thi lớn 50% |
| T3 | R2 | Tên/logo thống nhất, menu control thật và icon Android |
| T4 | R3, R4 | Nút chân lớn/gần góc, nhân vật lớn, Học chạy 45 s |
| T5 | R5, R9 | Học bóng chuyền 3 drill, icon transparent, animation có contact/landing |
| T6 | R6 | Tiếp tục đến checkpoint sau lời dẫn, bảo vệ save/load |
| T7 | R8 | Bật cóc 30 s, vùng xanh 20%, nhảy 2,2–3,2 m |
| T8 | R10 | Landing thủ môn đúng trái/phải/gần/xa và Cheer đứng thẳng |
| T9 | R7 và regression | Summary không tràn, full journey/Android QA và APK |

## T0. Chốt baseline để sửa đúng nhánh hiện tại

1. Đọc hướng dẫn repo và RTK; kiểm HEAD/status. Workspace có nhiều file untracked từ trước; giữ nguyên, không xóa hoặc add chung ngoài phạm vi.
2. Tạo branch theo quy ước `codex/` nếu triển khai cần branch; không pull/reset lại code hoặc save người dùng chỉ để tạo baseline.
3. Capture Map/popup/Splash/Sprint Học/Celebration ở 2400 × 1080; ghi screen size, safe rect, canvas scale factor, camera viewport. Kiểm dải màu ảnh 2 là từ root UI, camera hay hệ thống Android; không sửa theo suy đoán.
4. Ghi hành vi Learn/Practice/Exam từ catalog assets và builders. Chụp video Học bóng chuyền ở lần Receive, các trạng thái thủ môn gần/xa để so animation sau sửa.
5. Giữ một save mới và bản sao save đã hoàn thành một phần để kiểm tương thích; sử dụng save test riêng.

Hoàn thành: có evidence baseline, chưa thay đổi luật hay tiến trình thật.

## T1. Sửa safe area và nền chung

**File chính:** `Assets/_Project/Scripts/UI/SafeAreaFitter.cs`, `MapPresentationBuilder.cs`, `CampusBackdropUi.cs`, `MinigameUIAssembler.cs`, `Assets/_Project/Scripts/UI/Kit/UiKit.cs`, `UITheme.cs`, `Assets/_Project/Settings/UI/UITheme.asset`; xem `ShellSceneAuthoring.cs`, `DialoguePresentationAuthoring.cs` và các root scene liên quan.

1. Chuyển safe-area fitting sang normalized anchors khi parent phủ màn; hỗ trợ chuyển screen corners sang local parent nếu dùng parent đặc biệt. Xử lý parent/null/zero screen và không mutate root canvas neo trùng về 0 × 0.
2. Theo dõi cả thay đổi `Screen.safeArea` lẫn resolution; cập nhật khi resume/system bars thay đổi mà root chưa báo đổi kích thước.
3. Tách backdrop/scrim ra ngoài safe content trên Map, dialogue và scene có background bị inset. Dùng cover crop giữ tỷ lệ ảnh. Loại inset áp dụng lặp trên cùng nhánh.
4. Thêm/sử dụng text roles: accent trên navy, navy trên light panel, warning/error có plate riêng. Đo tương phản màu composited.
5. Giữ Android fullscreen settings hiện có; chỉ sửa native window/camera viewport nếu evidence T0 chứng minh cần. Đồng bộ `ProjectConfigurator` nếu thay setting.

**Xác minh:** mở rộng `SafeAreaContractTests`, `CampusBackdropUiTests`, `UIThemeTests` cho full safe/inset bất đối xứng, scale 0,5/1/2 và safe rect thay đổi. PlayMode kiểm world corners nằm trong safe rect, backdrop phủ render surface. Kiểm Menu/Map/Sprint/Volley/Football/Chess/Frog/Celebration vì fitter dùng chung.

## T2. Menu chọn môn và popup lớn hơn

**File chính:** `MapJourneyPathLayout.cs`, `MapPresentationBuilder.cs`, `JourneyLessonPresentation.cs`, `JourneyLessonList.cs`, `UITheme.cs` và `UITheme.asset`, `Assets/Editor/ShellSceneAuthoring.cs`, `Assets/_Project/Scenes/Map.unity`.

1. Tính map zone và stop scale từ safe rect còn lại dưới header; tránh collision giữa tên/status và trạm kế cận ở 4:3.
2. Áp token node 186, badge 69, heading 54, body 42, caption/objective 39. Không chỉ đổi C# default khi asset đã serialize giá trị cũ.
3. Popup 75% safe height, 92–96% safe width; bố trí lại node row/detail/CTA/close, giữ selected-ring đúng node lớn.
4. Objective dài wrap; compact mode đặt CTA dưới text. Clamp popup vào safe bounds và không thu nhỏ font về cỡ cũ.
5. Author lại Map, reopen và kiểm Build runtime nhánh existing presentation vẫn nhận style mới, không giữ child có kích thước cũ.

**Xác minh:** `JourneyMapTests`, `JourneyMapChessTests`, `MapLessonPopupBackTests`; đo bounds và tỷ lệ icon/font trước/sau tại cùng resolution. Kiểm current/completed/locked nodes và objective dài của tất cả bài. Capture 16:9, 20:9, tablet.

## T3. Thay branding và menu vào game

**File chính:** `SplashPresentationView.cs`, `HomePresentationBuilder.cs`, `HomeKeyArtLayout.cs`, `HomeKeyArtButton.cs`, `HomeMenuIcons.cs`, `HomeMenuResponsive.cs`, `HomeMenuStyle.cs`, `ShellSceneAuthoring.cs`, `ProjectConfigurator.cs`, `ProjectSettings/ProjectSettings.asset`, art UI, `Bootstrap.unity`, `Menu.unity`.

1. Tạo logo/source theo brief trong spec: học sinh/giày chạy, motion trail, cartoon sân trường; export sprite transparent và adaptive/legacy launcher assets. Kiểm ở kích thước 48 dp.
2. Giữ backdrop art, tách title/nút/icon khỏi `MenuKeyArtPanel.png`. Dựng title TMP `CHẠY TRỐN` / `THỂ CHẤT` và bốn button thật bằng uGUI.
3. Thay cơ chế label trong suốt/hit area theo tọa độ painted art bằng responsive control layout. Bind lại các callback hiện có, states Continue disabled, new-game confirmation, settings/back/quit.
4. Splash dùng cùng logo/wordmark, plate tương phản; giữ loading bar async/minimum intro/skip behavior hiện tại.
5. ProductName và PlayerSettings dùng `Chạy trốn thể chất`; application ID giữ nguyên. Loại text/title branding cũ ở builder, saved scenes và key art, không sửa mọi occurrence KMA trong namespace/menu Editor/tên trường.
6. Author shell rồi reopen; cập nhật credits nếu có nguồn asset mới.

**Xác minh:** `SplashLoadingFlowTests`, `ScenePresentationContractTests`, `ShellButtonFamilyTests`, `S5NewGameTests`, `ProjectSettingsTests`; điều chỉnh các assertion cũ đang tìm TitleKMA/painted hitbox theo contract mới. Test nút thấy được thực sự bấm được, launcher hiển thị đúng tên/icon.

## T4. Sprint: controls, visual và timed Learn

**File chính:** `SprintUiLayout.cs`, `SprintChromeLayout.cs`, `SprintFestivalPresentation.cs`, `SprintControlPresenter.cs`, `SprintChallengeRules.cs`, `SprintController.cs`, `SprintHud.cs`, `SprintGuide.cs`, `Assets/Editor/DemoSprintArtConfigurator.cs`, `SprintSceneConfigurator.cs`, `StudentJourneyContentBuilder.cs`, `GameplayDialogueContentBuilder.cs`; `sprint_learn.asset`, journey dialogue resources, `MG_Sprint.unity`, runner prefabs/animations.

1. Tạo sprite FootLeft/FootRight theo theme. Tăng visual 1,3 lần và neo về góc ngoài thay vì center trong hit rect; safe inset 12–24 canvas unit. Giữ input bridge/tap detector và expected-side feedback.
2. Tăng visual player/rivals 1,25 lần ở parent không bị animator/breathing ghi đè; giữ lane-foot pivot và position model. Điều chỉnh label BẠN.
3. Đặt `sprint_learn.timeLimit=45` ở builder và asset. Theo dõi learn elapsed và best correct streak độc lập với race finish. Không resolve ở TargetCount=12 hay cự ly thi.
4. Pass khi elapsed đủ 45 và best streak ≥12; chưa đạt thì result trung tính/Tập lại, không penalty. Controller clamp dt khi chạm mốc; completion event một lần.
5. HUD/progress theo elapsed, đổi objective/guide/dialogue/mode chip. Cập nhật result learn để trình bày thời gian và chuỗi đạt được thay vì score thi hoặc chỉ con số 12.
6. Regenerate nội dung qua builder đúng phạm vi: preserve authored dialogue đã biên tập trong `Chay-tron-the-chat-loi-thoai-bien-tap.md` và resource hiện hành. Xem parser/regeneration trước khi chạy; không ghi đè toàn bộ thoại bằng default cũ.

**Xác minh:** `SprintChallengeTests`, `SprintChallengeRuntimeTests`, `SprintUiLayoutTests`, `RunnerVisualTests`, `SprintGuideTests`, dialogue tests. Các ca: đủ 12 sớm vẫn chơi đến 45; best streak đạt rồi sai cuối lượt; không bấm/chưa đủ ngưỡng; pause/resume; 45/60 config; visual không che player và không reset scale. Kiểm Practice/Exam còn đúng cự ly, timer, score và rival waits.

## T5. Volleyball: drills và animation dùng chung

**File chính:** `VolleyballChallengeRules.cs`, `VolleyballMatch.cs`, `VolleyballController.cs`, `VolleyballHud.cs`, `VolleyballGuide.cs`, `VolleyAthlete.cs`, `VolleyAthleteView.cs`, `SpriteFlipbook.cs`, `ActionButton.cs`, `JumpButton.cs`, `Assets/Editor/VolleyballSceneConfigurator.cs`, `StudentJourneyContentBuilder.cs`, challenge config/resources, `MG_Volleyball.unity`.

1. Tạo drill state/config Học: Receive 3, ServeHit 2, Jump/Smash 2; chọn server/feed pattern và reset rules cho từng drill. Guide/HUD/objective/result lấy từ cùng config.
2. Đếm action hợp lệ sau contact, loại None/Miss. Pending drill completion/reset được xử lý ở Tick sau animation/contact/follow-through, thay callback Receive reset ngay.
3. Drill cuối trì hoãn Finish đến khi thấy animation cuối. Không cộng thêm mục tiêu khi spam input trong thời gian pending.
4. Tách Serve/Smash, thêm phase/presentation action ID và frame durations. Wire Ready/Dig/Receive/Set/Windup/Contact/Follow/Land/Recover từ asset có sẵn như spec.
5. Đồng bộ contact với flight model, airborne với JumpHeight; không để Move/Idle ghi đè action đang trình diễn. Repeat cùng action phải replay được. Kiểm đối thủ và sorting quanh net.
6. Thay face vàng/chữ của hai action controls bằng icon có transparent plate/outline. Hỗ trợ cue và disabled, không làm alpha thay đổi mapping thao tác. Không chồng hai hit areas.
7. Rebuild/import scene qua configurator; đảm bảo serialized mảng clip mới được ghi ra scene, không chỉ tồn tại khi chạy isolated runtime.

**Xác minh:** `VolleyballChallengeTests`, `VolleyballChallengeRuntimeTests`, `VolleyballControllerTests`, `PresentationTests`, `SupplementPoseWiringTests`, `TouchControlTests`, `VolleyballInputBridgeTests`. PlayMode kiểm action sprite thật sau 1 frame và đến contact/land, không chỉ kiểm array chứa tên PNG. Lặp liên tiếp Receive/Smash; Học đủ 3 drill; Practice/Exam không chuyển sang drill. Quay video touch joystick+jump+hit, 30/60 fps.

## T6. Tiếp tục tự vào bài tiếp theo

**File chính:** `Assets/_Project/Scripts/Core/SceneRouter.Journey.cs`, `SceneRouter.FrogJump.cs`, `Assets/_Project/Scripts/Shell/S5ShellSceneController.cs`, `Assets/_Project/Scripts/UI/JourneyDialoguePresenter.cs`, `ResultPanel.cs`, `Assets/_Project/Scripts/Progression/Journey/ChallengeContracts.cs`, `JourneyProgress.cs`; continuation policy/intent class mới nếu cần.

1. Tách decision theo mode/pass/pending frog/final/courseComplete/save status thành policy có thể kiểm độc lập. Đích Journey dùng checkpoint sau commit.
2. Phân biệt action Tiếp tục và Về bản đồ trong contract/panel. Learn fail có Tập lại; Review/FreePlay có Chơi lại, không bị kéo vào Journey.
3. Thêm navigation intent tạm targetId + session/attempt identity. Result Tiếp tục và Chơi mới/Tiếp tục hành trình từ menu route qua Map bridge; Map chưa yêu cầu select node/CTA khi có intent hợp lệ. Mở Map để duyệt môn từ CTA phụ không tạo intent; pending frog được ưu tiên trước start bài mới.
4. Refactor `ShowJourney`: queue builder và callback queue complete, kể cả queue rỗng. Dòng cuối/Bỏ qua đi đến intent target sau lưu seen marker. Map bình thường vẫn có thể dừng và chọn môn.
5. Không gọi start hai lần do skip/double tap/queue completion. Consume intent khi start được chấp nhận; invalidate callback sau scene/session thay đổi.
6. Save fail không route; load fail rollback active attempt và có UI Thử lại/Về bản đồ ở nơi người chơi đang đứng. Callback từ result đã destroy không được dùng để hiện error.
7. Giữ `GameplayOpeningFlow`/guide/start holds, không để thời gian Play chạy khi cầu nối/lời dẫn chưa kết thúc. Giữ frog retry path đã có và các nhánh hết mạng.

**Xác minh:** `JourneyRoutingTests`, `StudentJourneyFlowTests`, `JourneyNarrativeTests`, `DialogueJourneyAcceptanceTests`, `GameplayOpeningFlowTests`, `FrogJumpRoutingTests`, `ChessFinalRoutingTests`, `JourneyCommitTests`, `JourneySaveTests`.

Ca bắt buộc: Học → Luyện → Thi cùng scene; Thi → môn sau; queue rỗng/đã xem; skip dialogue; save fail result; save fail seen marker; sync route reject và async load fail; double tap; restart sau commit; user chọn Về bản đồ; review/freeplay; fail learn/fail exam; frog pending; cờ vua lần đầu/replay.

## T7. Balance bật cóc 30 giây

**File chính:** `FrogJumpTuning.cs`, `FrogJumpBalanceConfig.cs`, `Assets/_Project/ScriptableObjects/FrogJump/FrogJumpBalance.asset`, `FrogJumpRules.cs`, `FrogJumpPowerBar.cs`, `FrogJumpView.cs`, `FrogJumpGuide.cs`, dialogue tokens/resources, `Assets/Editor/FrogJumpSceneConfigurator.cs`, `MG_FrogJump.unity`.

1. Áp track60/time30/safe0,2/min2,2/max3,2/sweep0,7/jump0,6/recover2,5 cho serialized config và fallback; loại giá trị cũ bị copy ở view/guide.
2. PowerBar draw segments dùng rule tuning, xanh đúng [0,4;0,6]. Đánh dấu tâm, phân biệt vùng ngoài bằng màu + viền; không báo hợp lệ ngoài green.
3. Guide/HUD/result/penalty dialogue lấy thời lượng từ balance source; thay cả câu biên tập hardcode 60 s khi có.
4. Tune view/camera và chuyển động nhảy cho cự ly lớn hơn, không ảnh hưởng ghi nhận distance ở Landed.
5. Chạy deterministic simulation center và safe-edge ở 30/60 fps, ghi thời gian/distance/jump count/falls. Biên toán học và sample input frame được kiểm riêng, không chọn frame ngoài green rồi gọi nó là ca không ngã.

**Xác minh:** `FrogJumpRulesTests`, `FrogJumpGuideTests`, `FrogJumpSceneTests`, `FrogJumpDialogueTests`, `FrogJumpPenaltyTests`, `FrogJumpRoutingTests`. Mục tiêu giải tích lý tưởng best ≈18,05 s, 19 jumps; safe first-window edge ≤30 s. Mẫu input 30/60 Hz phải chọn needle thật trong vùng hợp lệ, ghi số jump/mét thực (có thể khác 19/28); timeout, fall/recovery, pause, last jump chưa landing và commit penalty một lần. Nếu thực tế không đạt do lượng tử frame, tune min/max hoặc duration và cập nhật công thức trong QA; không giảm độ khó bằng mở rộng lại vùng xanh.

## T8. Landing thủ môn hợp lý

**File chính:** `FootballPoses.cs`, `FootballPresentation.cs`, `FootballSceneConfigurator.cs`; kiểm `FootballFlightSimulation.cs` và `FootballTuning.cs` để lấy dữ kiện. Assets `StudentKeeper`, `MG_Football.unity`.

1. Mở rộng pose config với ReachLeft/RightEmpty, DiveLeft/RightEmpty, Cheer; thêm state/timeline hậu outcome và chốt hướng/độ xa theo shot.
2. Trước va chạm, giữ logic/silhouette Ready đang xoay. Sau outcome, far → DiveEmpty, near → ReachEmpty, reset rotation 0 cho pose đã vẽ nghiêng. Saved chuyển Cheer sau hold ngắn, không chuyển ngay frame chạm.
3. Mỗi pose có ground/contact offset, scale đúng tỷ lệ art; Cheer đứng thẳng. Goal giữ pose landing rồi Ready, không Recover làm mặc định.
4. Ball renderer tiếp tục là nguồn bóng duy nhất; không dùng CatchBall/HoldBall sprite chứa bóng trong nhánh Saved đang bật quả bóng thật.
5. Reset timeline khi sang shot và khi reconfigure/retry; keeper-disabled ở Học không hiện pose lạc chỗ.
6. Author lại scene. Nếu mở rộng pose trước contact, thêm pose-aware silhouettes và test trước khi coi xong; không nhập nhánh mở rộng một cách nửa vời.

**Xác minh:** `FootballPosesTests`, `FootballPresentationTests`, `FootballSceneConfiguratorTests`, `KeeperSilhouetteTests`, `FootballRulesTests`, `FootballControllerTests`. Capture matrix gần/xa × trái/phải × Saved/Goal, center/high, shot kế tiếp, 3 difficulty. So contact path/outcome trước/sau để chứng minh sửa presentation không đổi luật bắt bóng.

## T9. Hoàn thiện readability, authoring và Android QA

**File chính:** `CelebrationSceneConfigurator.cs`, `CelebrationSceneController.cs`, result prefab/assembler, các HUD, `JourneyDialoguePresenter.cs`, `GameplayDialogueView.cs`, `MinigameGuidePanel.cs`, `SceneTransitionOverlay.cs`, `GameOverPresentationBuilder.cs`; QA mới đề xuất `docs/qa/android-ui-gameplay-continuity.md`.

1. Celebration header wrap hai dòng trong panel, rows dùng layout theo nội dung, đủ chỗ text cờ vua và buttons; panel responsive theo safe rect.
2. Rà mọi text vàng và label dài: Splash, Menu, Map, popup, guide, countdown, in-game feedback, result, dialogue, save/load errors, GameOver, Celebration. Mỗi chữ accent trên scenery có plate hoặc đổi text role theo nền.
3. Kiểm `TMP_Text.preferredWidth/Height` so với content rect, world corners và padding; phân biệt overflow thật với effect/shadow ngoài rect. Lấy câu dài nhất từ dữ liệu thực, không chỉ sample ngắn.
4. Chạy authoring đúng scene/content phạm vi sửa, bake sprite theo pipeline hiện tại, reopen và kiểm idempotence. Không giữ font atlas cache đổi ngoài phạm vi.
5. Chạy focused EditMode/PlayMode cho các task; sau khi tích hợp đủ, chạy full suites một lượt theo README/tool hiện có. Test XML phải có test chạy, zero failure và Passed; ghi rõ nếu môi trường không chạy được.
6. Build Android ARM64 bằng `tools/build-apk.cmd`; đóng Editor trước batch build theo repo. Dùng emulator x86_64 nếu cần nhưng không thay ARM64 device gate.
7. Chạy full hành trình mới và save cũ: Học/Luyện/Thi, failure/frog/retry, hết lượt, chess, summary. Dùng touch thật, landscape hai chiều, navigation modes và pause/background.
8. Ghi QA với commit, build hash/version, test counts, thiết bị/Android/safe rect, screenshot và video trước/sau. Không gọi đã nghiệm thu chỉ vì code có đủ tên asset.

## 2. Checklist bàn giao

- [ ] R1: Map không hở nền; popup icon/font ~150%, không overlap.
- [ ] R2: Chạy trốn thể chất thống nhất Splash/Menu/Android; logo đúng style, menu callback hoạt động.
- [ ] R3: nút chân visual ~130%, sát góc safe area, player/rivals lớn và chân không lệch lane.
- [ ] R4: Học đủ 45 s Play, có thể cấu hình 60; không mở khóa bằng idle, không phạt khi chưa đạt.
- [ ] R5: Học bóng chuyền có đỡ/phát/nhảy đánh thực, hai nút icon transparent đủ feedback.
- [ ] R6: Tiếp tục đến bài kế tiếp sau lời dẫn; fail không vượt checkpoint, có Về bản đồ và lỗi recover được.
- [ ] R7: chữ/menu/thông báo có nền tương phản; summary dài không tràn hoặc bị cắt mất nội dung.
- [ ] R8: frog30/safe20%, best/edge simulation và device playtest thắng được đúng giả định.
- [ ] R9: Volley Contact/Dig/Follow/Land hợp tình huống, repeat action không mất animation.
- [ ] R10: keeper landing đúng gần/xa/trái/phải, Saved Cheer đứng thẳng, không bóng trùng/pose cũ.
- [ ] Source builder, config, scene/prefab đồng bộ; author lại không làm mất sửa.
- [ ] Save cũ và tiến trình hiện có còn dùng được; asset `.meta`/credits đủ; QA và APK có bằng chứng.

## 3. Rủi ro triển khai cần xử lý trong từng task

| Rủi ro thực tế | Xử lý |
|---|---|
| SafeAreaFitter dùng chung, sửa một chỗ có thể lệch nhiều scene | T1 kiểm mọi root UI trước khi tiếp tục polish |
| Menu art chứa sẵn tên/icon/nút | T3 dựng lại control thật; không chỉ đổi label vô hình |
| Builder ghi đè config/scene mới bằng default cũ | Sửa source và serialized asset cùng task, kiểm regenerate/reopen |
| Dialogue mới nhất đã biên tập và có xử lý lỗi save | Chỉ cập nhật câu liên quan, preserve nội dung/cơ chế lỗi trong pipeline |
| Action model reset hoặc Move ghi đè animation | T5 delayed reset + phase/action ID; kiểm sprite hiển thị theo thời gian |
| Continue còn cần dialogue từ Map | T6 bridge intent + completion callback; không bỏ toàn bộ lời dẫn |
| Shape thủ môn và collider hiện gắn với Ready xoay | T8 chỉ đổi landing hậu outcome; mở rộng pre-contact phải có geometry/test |
| Chỉ đổi frog timer làm level không thắng được | T7 cập nhật mét/lần, mô phỏng center/edge và ghi evidence |

Không cần thêm framework UI/animation hay đổi save schema cho phần lớn task. Ưu tiên mở rộng những hệ thống đang có; nếu thêm field serialized cho drills/poses, giữ default/fallback tương thích và ghi rõ trong handoff.
