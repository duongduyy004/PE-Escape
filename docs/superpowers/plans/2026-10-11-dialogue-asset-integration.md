# Dialogue and Asset Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nối đầy đủ lời thoại/biểu cảm được duyệt vào hành trình và các scene, đồng thời tăng tốc kim Bật cóc lên 0,7 giây mỗi chiều.

**Architecture:** Nội dung tạo bằng builder; bộ điều phối thuần quản lý thời gian, ưu tiên và giới hạn lặp. Một host mỗi scene nối adapter sự kiện với view HUD, bàn cờ hoặc kết quả; cổng bắt đầu phối hợp các điều kiện độc lập. Giữ rules, input ownership và tiến trình đang chơi.

**Tech Stack:** Unity 6000.3.23f1, C#, ScriptableObject, uGUI/TMP, UiKit/UITheme, NUnit EditMode/PlayMode, workflow authoring và screenshot hiện có.

**Spec:** `docs/superpowers/specs/2026-10-11-dialogue-asset-integration-design.md`, người dùng duyệt bằng “okey”. Nguồn chữ: `Chay-tron-the-chat-loi-thoai-bien-tap.md`.

## Global Constraints

- Chạy giữ 12 nhịp; luyện 150 m/20 giây; thi 150 m/15 giây. Builder phải tạo lại đúng 20/15.
- Bóng chuyền giữ Learn 3 lần đỡ, Practice 5 điểm/120 giây, Exam 10 điểm/không đồng hồ. Bóng đá giữ Learn 3 bàn/không thủ môn, Practice 2 bàn/6 lượt, Exam 3 bàn/đủ 5 lượt; thủ môn Normal.
- Bật cóc giữ 60 m/60 giây, safeZone 0,4, quãng bật 1,5/0,25 m, jumpSeconds 0,6, recoverSeconds 2,5; chỉ sweepSeconds đổi 0,9 → 0,7.
- Giữ ID/save/routing/luật/timing resolve; cờ sai lần 3 mới trượt, node supplementary vẫn dự phòng. Không thêm frame vận động mới.
- SV năm 1 = MaleAdventurer; Anh Tuấn = MalePerson; thủ môn = StudentKeeper. Giảng viên ghi đạt bằng scoreWrite, không Clap/Congratulate.
- Một bong bóng, không raycast, đúng speaker; cooldown phản ứng 10 giây, tối đa 2 lần/nhóm/lượt; pause dừng thời gian thoại/cooldown; reset khi retry/unload.
- Ngã Bật cóc: “Ui da!” 0,7 giây → giảng viên 1,8 giây trong hồi phục hiện có. Giữ resolve cờ 0,9 giây, summary Celebration giây 8.
- Câu mở bài không tiêu tốn giờ chơi; thao tác điều khiển đang có phải tiếp tục hoạt động trong thoại gameplay. Direct/FreePlay không có challenge thì không tự giả định Learn/Practice: chỉ dùng câu phù hợp cấu hình rules hiện tại; bỏ câu yêu cầu một level cụ thể nếu không có context đó.
- Lệnh shell dùng `rtk`; không stage file người dùng hoặc generated churn ngoài nhiệm vụ. Giữ `RunnerBreathing.cs.meta` đang untracked ngoài commit.

## Review Focus

1. Guide đóng và intro yêu cầu bắt đầu cùng frame: countdown chỉ mở một lần sau câu đầu bài — test Task 5.
2. Landed gọi trước khi rules cập nhật Finished: cú bật cuối không chen goodjump vào finish — test Task 10.
3. Lưu kết quả thất bại rồi RetrySave: không phát lại đoạn kết/trừ lượt hoặc thay lỗi kỹ thuật bằng lời nhân vật — test Task 11.
4. Scene unload/rebind trong câu đầu bài: callback cũ không mở gate scene mới, không còn subscriber/hold — test Tasks 4–5.
5. Thiếu icon/sprite hoặc key chưa đăng ký: builder báo đúng lỗi; runtime bỏ yêu cầu hỏng và game vẫn chơi được — test Tasks 3–4/9.

## File structure and contracts

Tạo folder `Assets/_Project/Scripts/UI/Dialogue/`, thuộc assembly UI hiện có, namespace `KMA.Gameplay.UI`. Không thêm tham chiếu từ common assembly sang UI. Các adapter đặt cùng folder môn, không để host tham chiếu controller từng môn.

| File mới | Trách nhiệm / contract |
|---|---|
| `UI/Dialogue/GameplayDialogueContent.cs` | Các kiểu serializable `DialogueSpeaker { Student, Teacher }`, `DialoguePriority { Success, Technical, Error, Result }`, `GameplayDialogueLine`, `GameplayDialogueEvent`. Line có Speaker, Text, Sprite[] Poses, PoseStepSeconds, DurationSeconds, Sprite Icon. Event có Id, ReactionGroup, Priority, CooldownSeconds, MaximumPlays và Lines. MaximumPlays=0 nghĩa không giới hạn cho phản hồi bắt buộc. |
| `UI/Dialogue/GameplayDialogueLibrary.cs` | `SetContent(IEnumerable<GameplayDialogueEvent>)`, `Validate(out string error)`, `TryGet(string id, out GameplayDialogueEvent value)`, `Resolve(string id, IReadOnlyDictionary<string,string> tokens)` trả bản sao đã nội suy, không sửa asset. `LoadDefault()` từ Resources/Journey/GameplayDialogues. |
| `UI/Dialogue/GameplayDialogueScheduler.cs` | `bool Enqueue(GameplayDialogueEvent value, Func<bool> valid = null, float budgetSeconds = float.PositiveInfinity)`, `Tick(float dt, bool paused)`, `Clear()`, `ResetAttempt()`, `bool IsIdle`, `GameplayDialogueLine Current`, `event Action<GameplayDialogueLine> LineChanged` (null = hide). Clock nội bộ chỉ tăng khi !paused. |
| `UI/Dialogue/IGameplayDialogueView.cs` | `void Show(GameplayDialogueLine line)`, `void Hide()`. View tự tra tên speaker từ JourneyDialogueLibrary đã cấu hình. |
| `UI/Dialogue/GameplayDialogueView.cs` | HUD view; `Configure(JourneyDialogueLibrary cast, RectTransform safeArea)`; dùng sprite reference, speaker/tag, chuỗi pose và icon. Không viết sprite gameplay. |
| `UI/Dialogue/GameplayDialogueHost.cs` | `Configure(GameplayDialogueLibrary library, IGameplayDialogueView view)`, `SetView(IGameplayDialogueView view)`, `ResetAttempt()`, `bool Emit(string id, Func<bool> valid = null, float budgetSeconds = float.PositiveInfinity, IReadOnlyDictionary<string,string> tokens = null)`, `BeginResult(string preferredEventId = null)`, `string PreferredResultEventId`, `Clear()`, `bool IsIdle`. BeginResult idempotent trong lượt, chỉ xóa gameplay một lần và giữ câu kết đã phát; adapters đăng ký event kết riêng qua preferredEventId để ResultPanel không chọn thêm câu khác. Host tick scheduler và dọn callback lúc destroy; library thiếu/id hỏng thì log một lần và trả false. |
| `Gameplay/Common/MinigameStartGate.cs` | `IDisposable Hold(string owner)`, `RequestStart()`, `bool CanStart`, `event Action Ready`, `Reset()`. Mỗi token chỉ release một lần; Ready chỉ phát một lần/lượt khi đã request và hết hold. |
| `UI/Dialogue/GameplayOpeningFlow.cs` | `Configure(MinigameBase game, GameplayDialogueHost host, string eventId, IReadOnlyDictionary<string,string> tokens)`, `GuideClosed()`, `ResetAttempt()`. Acquire hold trước Start; chỉ Emit sau guide đóng/không tự mở; release khi scheduler idle hoặc nội dung hỏng. |
| `Editor/GameplayDialogueContentBuilder.cs` | `Build()` tạo/validate Resources/Journey/GameplayDialogues.asset, gồm 55 event bong bóng từ mục 4/7 nguồn chữ. Illegal/data_error là toast; Celebration giữ câu trên timeline riêng. |
| `Editor/DialoguePresentationAuthoring.cs` | `Configure(Transform root, DialogueHudSlot slot)` tạo/wire đúng một HUD view + host, giữ object/reference bền trong scene. `DialogueHudSlot` chọn Sprint/Volleyball/Football/Frog/Result/GameOver; không dùng magic geometry ngoài theme. |

Các đường dẫn rút gọn trong bảng này bắt đầu từ `Assets/_Project/Scripts/` trừ `Editor/` bắt đầu từ `Assets/`. Các file trong danh sách nhiệm vụ bên dưới đều dùng đường dẫn đầy đủ. Unity tạo/import `.meta` cùng file mới, không tạo GUID thủ công.

## Verification conventions

- Trước thi hành: dùng skill worktrees, kiểm tra branch/worktree và Unity đang mở bằng `rtk proxy pgrep -ax Unity`; không xóa lock đang hoạt động hoặc mở batch Editor thứ hai vào cùng project. Repo hiện ở nhánh `docs/dialogue-asset-integration`.
- Snapshot serialized asset/scene liên quan vào `/tmp` trước builder/test. Test regeneration phục hồi bytes/scene trong finally theo `StudentJourneyContentBuilderTests` hiện có. Chỉ phục hồi churn chứng minh do lần chạy tạo ra, không ghi đè thay đổi người dùng.
- Mỗi task code đi qua red → implement → green. Dùng `rtk proxy bash tools/run-unity-tests.sh <platform> <filter> <name>`; cần XML `result=Passed`, `total>0`, `failed=0`. Wrapper đã parse XML; thiếu XML hoặc chỉ exit Unity=0 không phải pass.
- Ký hiệu test trong các bước là assertion cần đưa vào test method có tên được chỉ định; fixture tự tạo fake view/content/game và dispose trong TearDown. Không thêm API runtime chỉ để expose private implementation; kiểm tra hành vi bằng scheduler/view/clock/input.
- Commit từng task: stage đúng Files và `.meta` mới của task, kiểm tra `rtk git diff --cached --name-only` và `rtk git diff --cached --check`, rồi commit với message ghi ở task. Không merge/push tự động.

---

### Task 1: Tăng tốc Bật cóc, giữ cân bằng còn lại

**Files:** Modify `Assets/_Project/Scripts/Gameplay/FrogJump/FrogJumpTuning.cs`, `Assets/_Project/ScriptableObjects/FrogJump/FrogJumpBalance.asset`; Test `Assets/Tests/EditMode/Gameplay/FrogJump/FrogJumpRulesTests.cs`, `Assets/Tests/EditMode/EditorTools/FrogJumpSceneTests.cs`.

**Interfaces:** Consumes `FrogJumpRules.Tick(float)`, `Needle01`, `Stop()`. Produces tuning default/asset sweepSeconds=0.7; public API không đổi.

- [ ] **1. Red tests:** `FasterDefaultSweepsEachEdgeInPointSevenSeconds`: Tick 0.35 → Needle01=0.5; thêm 0.35 → 1; thêm 0.35 → 0.5; thêm 0.35 → 0, tolerance 0.001. `FasterNeedleKeepsFallRecovery`: Stop ở 0 → Fallen; Tick 2.49 vẫn Fallen; thêm 0.01 → Aiming; Distance=0. Asset test load tuning xác nhận cùng chu kỳ qua rules, không chỉ so hằng.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh EditMode KMA.Tests.Gameplay.FrogJump.FrogJumpRulesTests dialogue-t1-red`; test sweep mới phải fail trên 0.9.
- [ ] **3. Implement:** đổi đúng sweepSeconds default + serialized thành 0.7. Những test cũ dùng 0.45 cố định để dừng giữa thanh đổi thành `tuning.sweepSeconds / 2f`; test limit đặt `timeLimitSeconds = tuning.sweepSeconds/2f + tuning.jumpSeconds`. Không thay expected thắng/thua của test cân bằng để ép pass.
- [ ] **4. Green:** chạy filter `KMA.Tests.Gameplay.FrogJump` và `KMA.Tests.EditorTools.FrogJumpSceneTests`, tên `dialogue-t1-green`/`dialogue-t1-assets`. Xác nhận center/edge jump distances, 60 m finish và timeout vẫn đúng.
- [ ] **5. Commit:** `feat(frog): speed up the aiming needle without changing jump rules`.

### Task 2: Đồng nhất cast, 13 node, mục tiêu và hướng dẫn

**Files:** Modify `Assets/Editor/StudentJourneyContentBuilder.cs`, `Assets/_Project/Resources/Journey/JourneyDialogues.asset`, `Assets/_Project/ScriptableObjects/Journey/*.asset`, `Assets/_Project/Scripts/Gameplay/Sprint/SprintGuide.cs`, `Assets/_Project/Scripts/Gameplay/Volleyball/VolleyballGuide.cs`, `Assets/_Project/Scripts/Gameplay/Football/FootballGuide.cs`, `Assets/_Project/Scripts/Gameplay/Chess/ChessGuide.cs`, `Assets/_Project/Scripts/Gameplay/FrogJump/FrogJumpGuide.cs`, `Assets/_Project/Scripts/UI/MinigameGuidePages.cs`.
**Tests:** Modify `Assets/Tests/EditMode/EditorTools/StudentJourneyContentBuilderTests.cs`, `Assets/Tests/EditMode/Progression/JourneyDialogueDataTests.cs`, `Assets/Tests/EditMode/Gameplay/Running/SprintGuideTests.cs`, `Assets/Tests/EditMode/Gameplay/Volleyball/VolleyballGuideTests.cs`, `Assets/Tests/EditMode/Gameplay/Ball/FootballGuideTests.cs`, `Assets/Tests/EditMode/Gameplay/Chess/ChessGuideTests.cs`, `Assets/Tests/EditMode/Gameplay/FrogJump/FrogJumpGuideTests.cs`.

**Interfaces:** Consumes JourneyDialogueLibrary/guide Build signatures hiện có. Produces cùng API và ID, cast/pose/node theo spec §§2–3, mục tiêu nội suy theo cấu hình.

- [ ] **1. Red tests:** `RegenerationPreservesCurrentSprintTimings`: BuildChallenges twice; practice.TimeLimit=20, exam=15, distance=150. `EditedCastMatchesGameplayHero`: tan_thu.Idle sprite name MaleAdventurer_fear; anh_khoa_tren.Idle MalePerson_idle; co.Cheer BossPE_scoreWrite. Assert 13 nodes, 35 lines, đúng pose từng dòng bảng spec, tên mới, không emoji/sticker cũ. Guide tests assert Practice=20, Exam=15, Frog=60 và lỗi cờ lần 3; guide có số tùy biến không còn số cũ.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh EditMode KMA.Tests.EditorTools.StudentJourneyContentBuilderTests dialogue-t2-red`.
- [ ] **3. Implement:** sửa source trước, giữ ChallengeSpec số khác, đổi cast/mapping/node/text từ nguồn và các số được duyệt. `BuildAll()` tạo lại catalog/dialogues; không tự kích hoạt supplementary. Sửa các guide constant và vẫn dùng Number/TimeWords từ giá trị thật.
- [ ] **4. Green:** chạy filters `KMA.Tests.EditorTools.StudentJourneyContentBuilderTests`, `KMA.Tests.Gameplay.Progression.JourneyDialogueDataTests`, từng guide fixture trong Files; XML name `dialogue-t2-<fixture>`. Review diff generated assets chỉ gồm thay đổi được duyệt, không seed lại 30/22.
- [ ] **5. Commit:** `feat(story): align the edited cast dialogue and lesson instructions`.

### Task 3: Nội dung gameplay, icon và bộ điều phối thuần

**Files:** Create `Assets/_Project/Scripts/UI/Dialogue/GameplayDialogueContent.cs`, `Assets/_Project/Scripts/UI/Dialogue/GameplayDialogueLibrary.cs`, `Assets/_Project/Scripts/UI/Dialogue/GameplayDialogueScheduler.cs`, `Assets/Editor/GameplayDialogueContentBuilder.cs`, `Assets/_Project/Resources/Journey/GameplayDialogues.asset`, `Assets/_Project/Art/UI/Dialogue/PointingHandRight.png`; Create tests `Assets/Tests/EditMode/Presentation/GameplayDialogueSchedulerTests.cs`, `Assets/Tests/EditMode/EditorTools/GameplayDialogueContentBuilderTests.cs`.

**Interfaces:** Produces Content/Library/Scheduler contracts của bảng; builder dùng CharacterArt.Load và cấu hình cast Task 2. Các adapter sau gọi event ID, không tự giữ copy câu.

- [ ] **1. Red tests:** `SharedReactionGroupHonoursTenSecondsAndTwoPlays`: frog.fall.first ở t=0 được phát; repeat t=9.99 bị từ chối; t=10 được phát; t=20 bị từ chối; ResetAttempt cho phát lại. `ResultPreemptsAndDropsPendingGameplay`, `InvalidContextDropsSecondSpeaker`, `PauseFreezesDurationAndCooldown`, `BudgetFitsFallDialogue`, `MandatoryChessFeedbackIsNotThrottled`. Builder assert 55 event bong bóng, đúng speaker/poses spec §7/8, numeric tokens và no duplicate ID; thiếu icon/sprite thì Validate=false với path/event rõ ràng.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.GameplayDialogueSchedulerTests dialogue-t3-red`.
- [ ] **3. Implement:** scheduler cấp thứ tự ổn định theo priority, không giữ hàng đợi vô hạn; preempt thấp hơn, revalidate trước mỗi line, ngân sách event tính cả hai line. Dùng nhóm phản ứng chung/cap2 cho lỗi lặp, một lần cho start/lowtime/done và cap0 cho hint bắt buộc. Nội suy template bản sao; token chưa resolve là lỗi cấu hình. Dùng skill imagegen tạo icon bitmap trong suốt 96×96 như spec, import Sprite/UI, kiểm tra trực tiếp; builder load reference thật. Bổ sung `StudentJourneyContentBuilder.BuildAll()` gọi Build sau khi icon có sẵn.
- [ ] **4. Green:** filters scheduler và `KMA.Tests.EditorTools.GameplayDialogueContentBuilderTests`, XML `dialogue-t3-scheduler`/`dialogue-t3-library`. Assert chạy Build hai lần không nhân event; icon alpha/tỉ lệ/reference đúng; thư viện không chứa toast như lời nhân vật.
- [ ] **5. Commit:** `feat(dialogue): add event content priorities cooldowns and pointing icon`.

### Task 4: View HUD, host và cleanup scene

**Files:** Create `Assets/_Project/Scripts/UI/Dialogue/IGameplayDialogueView.cs`, `Assets/_Project/Scripts/UI/Dialogue/GameplayDialogueView.cs`, `Assets/_Project/Scripts/UI/Dialogue/GameplayDialogueHost.cs`, `Assets/Editor/DialoguePresentationAuthoring.cs`, `Assets/Tests/PlayMode/Presentation/GameplayDialogueViewTests.cs`, `Assets/Tests/PlayMode/Presentation/GameplayDialogueHostTests.cs`; Modify `Assets/_Project/Scripts/UI/UITheme.cs`, `Assets/_Project/Settings/UI/UITheme.asset` only if current theme lacks dialogue dimensions.

**Interfaces:** Consumes Task 3, produces view/host/authoring contracts. Host exists only in its scene; scheduler ownership remains host, view swappable for result/chess.

- [ ] **1. Red tests:** `TwoSpeakersShareOneNonBlockingBubble`: student then teacher updates name/sprite; all Graphics.raycastTarget=false; only one visible root. `DestroyCancelsQueuedDialogue`: destroy host, instantiate next scene fixture, advance time → no old line. `MissingLibraryReturnsFalseWithoutDisablingGame`: LogAssert expected diagnostic, Emit=false; game input remains enabled. `ReauthorKeepsOnePersistentView` rebuild twice/open scene again, no duplicate or transient Sprite reference.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh PlayMode KMA.Tests.Presentation.GameplayDialogueHostTests dialogue-t4-red`.
- [ ] **3. Implement:** view uses Journey cast names, full-body images, VietText.Fix, explicit icon Image. Host advances scheduler with pause-aware time, Show(null) maps Hide, SetView hides previous before showing current. Paused/guide state freezes time; final result view remains readable using UI time unless actual pause menu/guide is open. Store styling in existing shared theme, use safe-area layout and scene-specific reserved slots; no collider/extra NPC.
- [ ] **4. Green:** host/view filters, XML `dialogue-t4-host`/`dialogue-t4-view`. Real pointer to underlying control passes through bubble; state-driven sprite on field unchanged while portrait changes. Destroy/disable/rebind has no subscriptions/coroutines left.
- [ ] **5. Commit:** `feat(ui): add speaker-aware gameplay dialogue views and scene host`.

### Task 5: Gate trước bài phối hợp guide, intro và thoại

**Files:** Create `Assets/_Project/Scripts/Gameplay/Common/MinigameStartGate.cs`, `Assets/_Project/Scripts/UI/Dialogue/GameplayOpeningFlow.cs`, `Assets/Tests/EditMode/Gameplay/Common/MinigameStartGateTests.cs`, `Assets/Tests/PlayMode/Presentation/GameplayOpeningFlowTests.cs`; Modify `Assets/_Project/Scripts/Gameplay/Common/MinigameBase.cs`, `Assets/_Project/Scripts/UI/PhaseOverlay.cs`, `Assets/_Project/Scripts/UI/MinigameGuideHost.cs`, `Assets/_Project/Scripts/Gameplay/Sprint/SprintStartPresentation.cs`, `Assets/_Project/Scripts/Gameplay/Football/FootballController.cs`.

**Interfaces:** Gate contract từ bảng; thêm `MinigameBase.AcquireStartHold(string owner) : IDisposable`, `RequestStart() : void`, `StartPreparationReady : bool`. Giữ `SetTutorialGate(bool)` compatibility nhưng release legacy owner không ghi đè hold khác. `MinigameGuideHost` thêm `event Action<GuideMode> Closed`; opening flow subscribe once và xử lý cả guide không auto-open.

- [ ] **1. Red tests:** `GuideAndIntroSameFrameStartOnceAfterDialogue`: hai hold guide/dialogue, request twice; dispose guide chưa Ready; dispose dialogue Ready count=1; lifecycle clock=0 suốt thoại. `DoubleDisposeAndResetIgnoreOldHold`, `NoGuideStillPlaysOpeningOnce`, `MissingOpeningReleasesHold`, `UnloadDoesNotOpenNextScene`. Sprint presentation test countdownElapsed không tăng trước StartPreparationReady; cờ vẫn giữ intro riêng.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh EditMode KMA.Tests.Gameplay.Common.MinigameStartGateTests dialogue-t5-red`.
- [ ] **3. Implement:** common gate thuần/refcount tokens, epoch reset vô hiệu token cũ. Acquire opening hold khi configure/Awake trước Start; guide holds được release sau đóng; request start giữ ý định nếu hold còn. PhaseOverlay dùng request, không force mở. Sprint không advance countdown riêng cho đến gate ready; Football BeginMatch giữ request một lần rồi mở khi ready. Không dùng UI assembly trong common; không delay Machine.Begin của cờ bằng câu extra.
- [ ] **4. Green:** Gate EditMode + OpeningFlow PlayMode, cùng regressions `KMA.Tests.Presentation.SprintPresentationGateTests`, `KMA.Tests.Presentation.MinigameGuideSceneTests`; XML `dialogue-t5-<fixture>`. Countdown/còi chỉ một luồng, pause/resume trong opening không bỏ qua hold.
- [ ] **5. Commit:** `fix(start): wait for guides intros and opening dialogue before timing play`.

### Task 6: Nối thoại chạy vào nhịp thật

**Files:** Create `Assets/_Project/Scripts/Gameplay/Sprint/SprintDialogueAdapter.cs`, `Assets/Tests/PlayMode/Gameplay/Running/SprintDialogueTests.cs`; Modify `Assets/_Project/Scripts/Gameplay/Sprint/SprintController.cs`, `Assets/Editor/DemoSprintArtConfigurator.cs`, `Assets/Editor/SprintSceneConfigurator.cs`, `Assets/_Project/Scenes/MG_Sprint.unity` và scene chạy demo nếu configurator hiện có thực sự sửa nó.

**Interfaces:** Adapter `Configure(SprintController controller, GameplayDialogueHost host, GameplayOpeningFlow opening)`. Controller thêm `event Action<bool> CadenceTapped` (true = đúng), `ChallengeDefinition CurrentChallenge` đọc asset đang bound. Adapter dùng TargetTime/TargetDistance/TargetCount/Phase/ResultDecided và reference Animator của player; chỉ nhận Stumble khi Animator thật chuyển vào state Stumble.

- [ ] **1. Red tests:** `LearnWrongResetsStreakWithoutFallDialogue`, `PracticeBreakDoesNotReplaceRunnerSprite`, `ExamStumbleRequiresPresentedReaction`, `FiveSecondWarningFiresOnce`, `LearnAndPracticeDoneRequireFinalPass`. Assert sp.practice.start chứa 20 và sticker/story thi 15, race time vẫn 0 trước opening kết thúc; failed results không phát done.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh PlayMode KMA.Tests.Gameplay.Running.SprintDialogueTests dialogue-t6-red`.
- [ ] **3. Implement:** controller phát notification sau quyết định tap, không thay SprintRules. Adapter chọn event theo CurrentChallenge.Kind và giữ one-shot threshold; done trên outcome cuối đăng ký bằng host.BeginResult(doneId) rồi Emit(doneId), không lúc mới về đích còn chờ rivals. Adapter quan sát cạnh chuyển state Stumble của Animator player đang render, không dùng lỗi bấm làm bằng chứng đã vấp; nếu animation không đi vào Stumble thì không phát sp.exam.stumble. Không thêm animation transition/recovery/lỗi Learn. Configurator author HUD và reference, giữ input và animation ownership; thêm `SprintSceneConfigurator.ConfigureDialogue()` để author/wire riêng mà không rebuild sân.
- [ ] **4. Green:** SprintDialogueTests và namespace `KMA.Tests.Gameplay.Running`, XML `dialogue-t6-speech`/`dialogue-t6-running`; kiểm tra một lượt đúng, lỗi và hết giờ, field sprite vẫn thay theo animation như trước.
- [ ] **5. Commit:** `feat(sprint): trigger edited dialogue from cadence and race events`.

### Task 7: Nối thoại bóng chuyền theo pha bóng và Dive thật

**Files:** Create `Assets/_Project/Scripts/Gameplay/Volleyball/VolleyballDialogueAdapter.cs`, `Assets/Tests/PlayMode/Gameplay/Volleyball/VolleyballDialogueTests.cs`; Modify `Assets/_Project/Scripts/Gameplay/Volleyball/VolleyballController.cs`, `Assets/_Project/Scripts/Gameplay/Volleyball/VolleyAthleteView.cs`, `Assets/Editor/VolleyballSceneConfigurator.cs`, `Assets/_Project/Scenes/MG_Volleyball.unity`.

**Interfaces:** Adapter `Configure(VolleyballController controller, GameplayDialogueHost host, GameplayOpeningFlow opening)`; consumes Match.PlayerActed/PointScored/Completed, playerView và `VolleyballController.CurrentChallenge : ChallengeDefinition` đọc asset đang bound. Thêm `VolleyAthleteView.ActionPresented : event Action<AthleteAction>` phát sau Render thật đổi action; adapter theo Dive completion thay vì tạo Dive qua điểm thua.

- [ ] **1. Red tests:** `LostPointDoesNotInventDive`, `ActualDiveAndRecoveryProduceOneStandUpLine`, `EarlyLateAdviceWaitsForAvailableGap`, `NinePointsTriggersOneMatchpointWhileNotOver`, `ThreeReceivesCompleteLearnWithoutConsecutiveGate`. Learn/practice/exam emit đúng ID; joystick và ĐÁNH/NHẢY thao tác được trong bubble.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh PlayMode KMA.Tests.Gameplay.Volleyball.VolleyballDialogueTests dialogue-t7-red`.
- [ ] **3. Implement:** adapter giữ ngữ cảnh rally để badtime/lostpoint hết hiệu lực khi pha mới bắt đầu; dùng action trình diễn actual Dive và trở lại sau tiếp đất, không ngã giả. Subscribe/unsubscribe cặp; done từ challenge completion đăng ký host.BeginResult(doneId) trước Emit(doneId), portrait không tác động FramesFor/animation. Author HUD slot tránh joystick/nút và vị trí bóng rơi.
- [ ] **4. Green:** VolleyballDialogueTests + `KMA.Tests.Gameplay.Volleyball`, XML `dialogue-t7-speech`/`dialogue-t7-volley`; Exam không được thêm giới hạn 120 giây.
- [ ] **5. Commit:** `feat(volleyball): attach dialogue to points timing and real dives`.

### Task 8: Nối thoại bóng đá theo outcome và lượt sút

**Files:** Create `Assets/_Project/Scripts/Gameplay/Football/FootballDialogueAdapter.cs`, `Assets/Tests/PlayMode/Gameplay/Football/FootballDialogueTests.cs`; Modify `Assets/_Project/Scripts/Gameplay/Football/FootballController.cs`, `Assets/Editor/FootballSceneConfigurator.cs`, `Assets/_Project/Scenes/MG_Football.unity`.

**Interfaces:** Adapter `Configure(FootballController controller, GameplayDialogueHost host, GameplayOpeningFlow opening)`; consumes Rules.State/Kicks/LastOutcome/Goals, `FootballController.CurrentChallenge : ChallengeDefinition` đọc asset đang bound và result signal. Controller thêm `event Action<FootballState> StatePresented` tại một điểm phát sau state change, không sửa luật outcome. Dedupe mỗi flight/kick.

- [ ] **1. Red tests:** `SavedLineRequiresSavedOutcome`, `MissNeverChangesKickerToFall`, `FifthKickLineRunsOnlyWhileAiming`, `GoalsDoNotAnnounceExamPassBeforeFiveKicks`, `PracticeDoneRequiresRulesMatchResult`, `FailedFinalUsesImpossibleWithoutDuplicateFailureSpeech`. Field keeper vẫn StudentKeeper, Learn không bật thủ môn.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh PlayMode KMA.Tests.Gameplay.Football.FootballDialogueTests dialogue-t8-red`.
- [ ] **3. Implement:** gửi notification sau Tick khi state đổi, kể cả nhánh trực tiếp MatchResult; outcome ID theo trạng thái thật, không mỗi frame. lastkick Kicks=4 + Aiming và chưa kết thúc; goal chỉ nếu trận còn chơi; practice final chỉ lúc rules chốt. Event kết riêng done/impossible đăng ký host.BeginResult(eventId) trước Emit(eventId). Không thêm celebration animation/khóa thao tác/sửa FootballRules. Author HUD ngoài lực và preview.
- [ ] **4. Green:** FootballDialogueTests + `KMA.Tests.Gameplay.Football`, XML `dialogue-t8-speech`/`dialogue-t8-football`; kiểm tra goal/saved/out/post và trận đủ 5 lượt.
- [ ] **5. Commit:** `feat(football): show contextual dialogue for shots and final outcomes`.

### Task 9: Cờ vua có speaker đúng, pose đầy đủ và gợi ý động

**Files:** Create `Assets/_Project/Scripts/Gameplay/Chess/ChessDialogueAdapter.cs`, `Assets/Tests/PlayMode/Gameplay/Chess/ChessDialogueTests.cs`; Modify `Assets/_Project/Scripts/Gameplay/Chess/ChessCastView.cs`, `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs`, `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalHud.cs`, `Assets/_Project/Scripts/Gameplay/Chess/Core/PuzzleHints.cs`, `Assets/_Project/Resources/Chess/Puzzles/m2_001.json`, `Assets/Editor/ChessFinalSceneConfigurator.cs`, `Assets/_Project/Scenes/MG_ChessFinal.unity`; Test `Assets/Tests/EditMode/Gameplay/Chess/ChessFinalStateMachineTests.cs`, `Assets/Tests/EditMode/Gameplay/Chess/ShippedPuzzleTests.cs`.

**Interfaces:** ChessCastView implements IGameplayDialogueView; giữ SetStudent/SetTeacher/PlayTeacher cho cast ngoài bubble. Adapter `Configure(ChessFinalController controller, GameplayDialogueHost host, ChessCastView view)`; `PuzzleHints.For` giữ signature, nội suy nội dung theo current node.

- [ ] **1. Red tests:** `StudentSpeechUsesStudentAnchorAndTeacherSpeechUsesTeacherAnchor`; `AllCalledPoseKeysExist`; `SecondMistakeAllowsCorrectionThirdFails`; `HintsFollowQueenE6H6ThenH6G7`; `IllegalAndDataErrorKeepPosesAndDoNotWhistle`; `OpeningOccursOnceBeforeBeginExtraOnlyForM2001`; `ResolutionKeepsPointNineSecondsAndDropsReliefAfterResultOpens`. Mistake1 ends strictLook, mistake2 angry, solved happy/scoreWrite, no congratulate.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh PlayMode KMA.Tests.Gameplay.Chess.ChessDialogueTests dialogue-t9-red`.
- [ ] **3. Implement:** add full key mappings spec §8 vào builder/serialized arrays, đổi label tên HUD. Say existing route qua host/speaker, bỏ timer cũ tranh ownership; pose sequence chỉ một nguồn và không thêm còi. Thay text controller/intro/toast; gợi ý đọc node hiện tại, invalidates hint đang chờ khi quân giảng viên đã đi xong. Câu extra không sửa clock; preserve chessMove only rook logic và resolve delay. Solved/timeout/failed dùng host.BeginResult(eventId), rồi Emit, để result panel tiếp nhận đúng đoạn kết.
- [ ] **4. Green:** ChessDialogueTests, `KMA.Tests.Gameplay.Chess`, `KMA.Tests.EditorTools.ChessCelebrationBackdropTests`, XML `dialogue-t9-<fixture>`; chơi thật sai 1,2 rồi đúng và riêng sai 3; hint2/3 ở cả hai node.
- [ ] **5. Commit:** `feat(chess): add correct speakers poses and node-aware edited hints`.

### Task 10: Thoại Bật cóc trong thời gian hồi phục có sẵn

**Files:** Create `Assets/_Project/Scripts/Gameplay/FrogJump/FrogJumpDialogueAdapter.cs`, `Assets/Tests/PlayMode/Presentation/FrogJumpDialogueTests.cs`; Modify `Assets/_Project/Scripts/Gameplay/FrogJump/FrogJumpController.cs`, `Assets/Editor/FrogJumpSceneConfigurator.cs`, `Assets/_Project/Scenes/MG_FrogJump.unity`, `Assets/Tests/PlayMode/Presentation/KMA.Gameplay.UI.PlayMode.Tests.asmdef` (thêm FrogJump reference nếu thiếu).

**Interfaces:** Adapter `Configure(FrogJumpController controller, GameplayDialogueHost host, GameplayOpeningFlow opening)`; consumes Rules.Landed/State/Falls/TimeRemaining and session PendingFrogJump.SavesLife. Controller thêm `event Action<FrogJumpState> StatePresented` sau TickPlay cập nhật state. Không thay FrogJumpView pose API.

- [ ] **1. Red tests:** `FirstFallShowsStudentForPointSevenThenTeacherForOnePointEight`: đúng speaker, hurt/command/icon; sprite sân fallSitRight, recovery đúng 2.5, không tăng timer. `RepeatFallsShareCooldownAndCap`; `FinishOnFinalLandingDoesNotQueueGoodJump`: Landed cuối trước Finished không tạo nhận xét thành công phụ. `TenSecondWarningDropsWhileFallen`; `ChargedBranchNeverClaimsLifeSaved`; `OpeningReadsSixtyMetres`.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh PlayMode KMA.Tests.Presentation.FrogJumpDialogueTests dialogue-t10-red`.
- [ ] **3. Implement:** Landed(0) gửi ngã ngay; Landed(>0) giữ candidate goodjump đến sau tick/state notification để loại cú cán đích. Finishing đăng ký host.BeginResult(finishOrTimeoutId) trước Emit; lowtime edge một lần. Nội suy track/time/recover từ tuning, chọn save/charged từ PendingFrogJump. Dùng ngân sách còn lại của Fallen; repeat ưu tiên lệnh giảng viên nếu hai câu dài không đọc kịp. Đổi feedback NGÃ/CHÍNH XÁC theo source, giữ Haptics/audio.
- [ ] **4. Green:** FrogJumpDialogueTests + `KMA.Tests.Gameplay.Progression.FrogJumpRoutingTests`, XML `dialogue-t10-speech`/`dialogue-t10-routing`; kiểm tra tap trước/sau recovery và kim 0.7 thật trong scene.
- [ ] **5. Commit:** `feat(frog): add timed fall dialogue and contextual penalty reactions`.

### Task 11: Kết quả, Game Over và Tổng kết không phát trùng

**Files:** Create `Assets/_Project/Scripts/UI/Dialogue/ResultDialoguePolicy.cs`, `Assets/Tests/EditMode/Presentation/ResultDialoguePolicyTests.cs`, `Assets/Tests/PlayMode/Presentation/ResultDialogueTests.cs`; Modify `Assets/_Project/Scripts/UI/ResultPanel.cs`, `Assets/_Project/Scripts/UI/GameOverPresentationBuilder.cs`, `Assets/_Project/Scripts/UI/GameOverScreen.cs`, `Assets/_Project/Scripts/Gameplay/Celebration/CelebrationSceneController.cs`, `Assets/_Project/Scripts/Progression/Journey/CelebrationSummary.cs`, `Assets/Editor/CelebrationSceneConfigurator.cs`, `Assets/_Project/Scenes/Celebration.unity`; Test `Assets/Tests/PlayMode/Gameplay/Celebration/CelebrationSceneTests.cs`, `Assets/Tests/PlayMode/Progression/JourneyRoutingTests.cs`.

**Interfaces:** `ResultDialoguePolicy.Select(bool pass, ChallengeKind kind, string specificEventId, JourneyCommitOutcome? outcome, string saveError) : string` returns ID/null; specificEventId lấy host.PreferredResultEventId. Consumes host.BeginResult/SetView and persisted outcome; ResultPanel owns final dialogue view, host keeps one scheduler. GameOver creates scene host/view only if none exists.

- [ ] **1. Red tests:** `SpecificCompletionSuppressesGenericPass`; `FirstAndRepeatFailureFollowCommittedOutcome`; `RetrySaveDoesNotReplaySpeechOrChangeAttemptId`; `SaveErrorStaysSystemText`; `ZeroAttemptsKeepsMapAction`; `ChargedPenaltyDoesNotChargeAgain`; `GameOverSpeaksOnce`; `TeacherTimelineStrictHoldWriteWithThreeFrames`. Celebration speech at 5 seconds, summary at 8; action handlers unchanged.
- [ ] **2. Run red:** `rtk proxy bash tools/run-unity-tests.sh EditMode KMA.Tests.Presentation.ResultDialoguePolicyTests dialogue-t11-red`.
- [ ] **3. Implement:** ResultDecided clears gameplay queue; final panel selects/hands off one chosen concluding event. Failure selection waits for outcome determining SavesLife; saveError keeps technical status with no actor replacement. Dynamic penalty warning time from tuning, remaining/max from session. Rewrite labels exactly source §§5–6; GameOver bubble outside button area. Configurator uses teacher strictLook/scoreHold/scoreWrite and hero idle/happy/happy, preserves arrays of 3 and actions.
- [ ] **4. Green:** policy + ResultDialogueTests + CelebrationSceneTests + JourneyRoutingTests, XML `dialogue-t11-<fixture>`. Verify retry save with same attempt and no double decrement; node sprint/volley/soccer_pass does not replay generic result dialogue.
- [ ] **5. Commit:** `feat(results): unify edited penalty feedback and restrained course summary`.

### Task 12: Regeneration, toàn hành trình và QA trực quan

**Files:** Create `Assets/Tests/PlayMode/Presentation/DialogueJourneyAcceptanceTests.cs`, `docs/qa/dialogue-asset-integration.md`; Modify `Assets/Editor/PlayModeScreenshotQaStates.cs`, `Assets/Editor/PlayModeScreenshot.cs` chỉ thêm các QA state cần chụp thoại thật; Test `Assets/Tests/EditMode/EditorTools/GameplayDialogueContentBuilderTests.cs`.

**Interfaces:** Consumes toàn bộ task trước; QA states dùng public gameplay/host hooks, không ghi fake text/sprite trực tiếp để chụp. `GameplayDialogueContentBuilder.Build()` và existing scene builder methods giữ stable entry points.

- [ ] **1. Red acceptance:** `DirectAndJourneyEntryKeepOneDialogueHost`, `RetryAndSceneChangeDiscardOldLines`, `RealPointerInputWorksBehindDialogue`, `RegenerationPreservesEditedContentAndSpeed`. Compare semantic serialized references before/after hai lần BuildAll + five scene configurators; map13/content55/current configs survive; every called key/icon wired. Kiểm tra source tooling không phụ thuộc file Markdown ở runtime.
- [ ] **2. Run acceptance before final fixes:** `rtk proxy bash tools/run-unity-tests.sh PlayMode KMA.Tests.Presentation.DialogueJourneyAcceptanceTests dialogue-t12-red`; fix nếu đỏ bởi integration/authoring gaps. Không tạo test giả đỏ nếu đã đầy đủ ở Tasks 1–11.
- [ ] **3. Author and capture:** tạo lại content bằng `StudentJourneyContentBuilder.BuildAll()`; scene qua `SprintSceneConfigurator.ConfigureDialogue()`, `VolleyballSceneConfigurator.BuildScene()`, `FootballSceneConfigurator.BuildScene()`, `ChessFinalSceneConfigurator.BuildScene()`, `FrogJumpSceneConfigurator.BuildScene()`, `CelebrationSceneConfigurator.BuildScene()` trong Editor hiện có hoặc batch khi project đóng. QA states: frog-first-fall, frog-repeat-fall, frog-charged-result, chess-student-speech, chess-second-mistake, sprint-lowtime, volley-lostpoint, football-lastkick, gameover-comment, celebration-teacher. Dùng request.json/done.json của PlayModeScreenshot, unique id, gameViewSize 1920x1080/1440x1080/2400x1080; inspect PNG bằng view_image. Thao tác pointer thật mỗi môn chứng minh bubble không chặn input.
- [ ] **4. Full green:** `rtk proxy bash tools/run-unity-tests.sh EditMode '' dialogue-full-edit`; sau XML hợp lệ chạy `rtk proxy bash tools/run-unity-tests.sh PlayMode '' dialogue-full-play`. Đọc failed cases nếu baseline đỏ, báo riêng và sửa failure do thay đổi; không coi baseline đỏ là task hoàn thành. `rtk git diff --check`, review all serialized diffs; ghi QA với XML paths, screenshots và trạng thái device/build thực tế. Không chạy/export Android chỉ để thay thế tương tác Editor; nếu chưa làm ghi rõ chưa kiểm chứng.
- [ ] **5. Commit:** `test(qa): verify dialogue regeneration input and full journey presentation`.

## Execution handoff

Kế hoạch có 12 task, nhiều giao diện nối tiếp và cùng sửa controller/builder. Khuyến nghị **Native**: triển khai tuần tự trong phiên này theo executing-plans, sau đó reviewer độc lập kiểm tra toàn nhánh; giúp tránh nhiều context giao diện giữa task. **Subagent-driven** là lựa chọn nếu người dùng muốn kiểm duyệt độc lập từng task và chấp nhận chi phí bổ sung. Chưa thực thi code cho đến khi người dùng duyệt kế hoạch và chọn phương thức.

Trước khi bắt đầu, đưa file lời thoại người dùng vào nhánh triển khai một cách rõ ràng để đặc tả/plan/source đi cùng bản giao việc; không gộp file RunnerBreathing.cs.meta không liên quan. Sau khi kiểm chứng, báo riêng commit và trạng thái push; kế hoạch này không bao gồm merge/push.
