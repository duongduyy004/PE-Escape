# KMA - Task Split cho 4 Agent

Tài liệu handoff để 4 thành viên checkout các nhánh riêng, pull code mới nhất từ GitHub và triển khai song song trong một buổi tối khoảng 3-4 giờ.

## 1. Bối cảnh và phạm vi

Đây là project Unity game thể thao KMA. Các thay đổi cần triển khai trong buổi này:

1. **Sprint level 1**
   - Học: 50 giây.
   - Luyện: 45 giây.
   - Thi: 40 giây.
   - NPC có tốc độ khởi đầu ngẫu nhiên; cả 3 NPC đều có khả năng là người nhanh nhất.
   - Pace NPC dao động, có thể đuổi kịp/vượt người chơi.
   - NPC có burst/animation khi gần đích.
   - Người chơi ngừng bấm sau combo thì đứng im.
   - Bấm sai hoặc lệch nhịp thì ngã rồi recover.

2. **Volleyball và giáo viên thể chất**
   - Giáo viên lớn gần bằng nhân vật trên sân.
   - Ghi sổ khi ghi điểm.
   - Đánh trượt: chỉ tay rồi ghi sổ.
   - Thổi còi khi bắt đầu lượt phát bóng.
   - Joystick lớn hơn khoảng 20%, sát góc dưới trái nhưng vẫn an toàn với safe area.

3. **Football**
   - Keeper di chuyển ngang toàn vùng goal, dive ở góc xa hơn.
   - Mỗi lượt sút có cửa sổ 3 giây.
   - Đồng hồ bắt đầu khi giáo viên thổi còi và chạy xuyên suốt cả chỉnh góc lẫn nạp lực.
   - Giáo viên đứng ngoài vòng penalty.
   - Trước mỗi lượt sút: whistle.
   - Goal/miss: ghi sổ; miss: chỉ tay rồi ghi sổ.
   - Text feedback có nền, dễ đọc, fade-out và biến mất hoàn toàn.

4. **Guide/menu/typography**
   - Nhãn “Đạt” không che mặt giáo viên.
   - Title trong Settings và thông báo qua môn dễ đọc hơn, không quá đậm/nhòe.
   - Header và nút back ở menu chọn môn học lớn hơn, cân xứng với nút môn.

Ảnh tham chiếu được gửi trong task chỉ là tiêu chí về bố cục, vị trí, kích thước và độ tương phản; không có yêu cầu code riêng ngoài danh sách trên.

## 2. Trạng thái code hiện tại cần biết

- Sprint đã có các thành phần `RivalPaceProfile`, `RivalPaceRandomizer`, `RivalRunnerAI` và một phần logic pace dao động.
- `SprintRules` hiện đang phanh dần khi người chơi ngừng bấm; cần đổi sang đứng im theo yêu cầu mới.
- Tap sai hiện chủ yếu giảm impulse; chưa tạo trạng thái ngã hoàn chỉnh.
- `StudentJourneyContentBuilder` hiện vẫn dùng thời lượng Sprint cũ `45 / 20 / 15`.
- Football hiện có state:
  `Start -> Aiming -> Charging -> Kicking -> Flying -> ShotResult -> MatchResult`.
- `Aiming` là giai đoạn chỉnh hướng; `Charging` là lúc giữ nút để nạp lực; thả nút sẽ sút.
- Football hiện chưa có shot deadline 3 giây tính từ whistle.
- `StartLecturer` đã có pose whistle, command, cheer và penalty nhưng chưa có cue riêng trước từng lượt sút/serve.
- `GameplayDialogueView.Hide()` hiện tắt ngay, chưa có fade-out lifecycle riêng.
- Asset cho pose giáo viên/keeper đã có; không tạo asset mới trong buổi này.

## 3. Cách checkout và đặt tên nhánh

Mỗi thành viên pull code mới nhất trước khi tạo nhánh. Dùng tên nhánh tương ứng:

```bash
git pull origin <default-branch>
git checkout -b codex/sprint-level1
git checkout -b codex/football-core
git checkout -b codex/teacher-volleyball
git checkout -b codex/ui-menu
```

Không dùng chung một working tree khi làm song song. Mỗi agent nên làm trong một worktree/checkout riêng.

Mỗi nhánh phải commit độc lập, không cherry-pick commit của nhánh khác trong lúc triển khai. Merge sẽ thực hiện sau khi cả 4 agent gửi kết quả.

## 4. Ownership và task từng agent

---

## Agent 1 - Sprint level 1

**Branch:** `codex/sprint-level1`

### Ownership

Được sửa:

```text
Assets/_Project/Scripts/Gameplay/Sprint/
Assets/Editor/StudentJourneyContentBuilder.cs
Assets/_Project/Resources/Journey/ChallengeCatalog.asset
Assets/_Project/Resources/Journey/JourneyDialogues.asset
Assets/Tests/EditMode/Gameplay/Running/
Assets/Tests/PlayMode/Gameplay/Running/
Assets/Tests/EditMode/Progression/JourneyCatalogTests.cs
Assets/Tests/EditMode/Progression/JourneyDialogueDataTests.cs
Assets/Tests/PlayMode/Presentation/SprintPresentationGateTests.cs
```

Không sửa Football, Volleyball, shared UI, `Menu.unity`, `Map.unity`, `GameOver.unity` hoặc `Celebration.unity`.

### Công việc

1. Đổi challenge data trong `StudentJourneyContentBuilder.cs`:
   - `sprint_learn = 50f`
   - `sprint_practice = 45f`
   - `sprint_exam = 40f`
2. Cập nhật objective/dialogue text để không còn các giá trị 45/20/15 cũ.
3. Sửa `SprintRules`:
   - sau combo cuối, nếu không có tap tiếp thì tốc độ về 0 và nhân vật đứng im;
   - không tiếp tục trượt theo idle brake hiện tại.
4. Sửa hành vi tap sai/lệch nhịp:
   - reset combo;
   - dừng nhân vật;
   - chuyển sang stumble/fall;
   - recover về idle/run sau thời gian ngắn;
   - có cooldown để không ngã liên tục.
5. Giữ/hoàn thiện random opening speed và pace segment của 3 NPC.
6. Khi NPC ở gần đích, chuyển burst state/animation; không teleport.
7. Cập nhật toàn bộ test Sprint và progression liên quan.

### Acceptance criteria

- Catalog trả đúng 50/45/40 giây.
- Không còn assertion hoặc dialogue cũ về 45/20/15.
- Ngừng bấm sau combo khiến player đứng im.
- Tap sai/lệch nhịp khiến player ngã.
- Nhiều seed khác nhau cho phép mỗi NPC từng là người nhanh nhất.
- NPC có surge/fade/overtake và burst gần đích.

### Báo cáo khi hoàn tất

- Commit hash.
- Danh sách test đã chạy.
- Screenshot/video ngắn cho stop/fall và NPC burst nếu có.
- Ghi rõ các file ngoài ownership nếu bắt buộc phải chạm vào.

---

## Agent 2 - Football core và shot timer

**Branch:** `codex/football-core`

### Ownership

Được sửa:

```text
Assets/_Project/Scripts/Gameplay/Football/FootballController.cs
Assets/_Project/Scripts/Gameplay/Football/FootballRules.cs
Assets/_Project/Scripts/Gameplay/Football/FootballPresentation.cs
Assets/_Project/Scripts/Gameplay/Football/FootballFlightSimulation.cs
Assets/_Project/Scripts/Gameplay/Football/FootballShotSolver.cs
Assets/_Project/Scripts/Gameplay/Football/FootballHud.cs
Assets/_Project/Scripts/Gameplay/Football/FootballTuning.cs
Assets/_Project/Scripts/Gameplay/Football/FootballTypes.cs
Assets/_Project/Scripts/Gameplay/Football/FootballMatchOptions.cs
Assets/Tests/EditMode/Gameplay/Ball/
Assets/Tests/PlayMode/Gameplay/Football/
```

Không sửa `FootballDialogueAdapter.cs`, `StartLecturer.cs`, `FootballSceneConfigurator.cs` hoặc `GameplayDialogueView.cs`.

### Công việc

1. Thêm shot window 3 giây vào `FootballRules`.
2. Timer bắt đầu khi lượt sút được mở bởi cue whistle, không phải khi nhấn giữ.
3. Timer chạy xuyên suốt:

   ```text
   Aiming + Charging
   ```

4. Nếu hết 3 giây chưa thả sút:
   - tạo timeout/miss outcome;
   - đóng lượt hiện tại;
   - không cho tiếp tục chỉnh hướng/nạp lực;
   - chuyển sang lượt tiếp theo hoặc match result.
5. Expose API/event để Agent 3 dùng:

   ```text
   ShotWindowStarted(int kickIndex)
   ShotResolved(FootballOutcome outcome)
   ShotTimeRemaining
   ```

6. Mở rộng keeper:
   - di chuyển ngang theo toàn vùng goal;
   - xử lý sút trái/phải không tự động vào;
   - dive có góc nghiêng và điểm rơi xa hơn.
7. Thêm timer vào HUD:
   - hiện trong `Aiming`/`Charging`;
   - cảnh báo khi còn khoảng 1 giây;
   - ẩn sau khi sút/timeout.
8. Giữ state machine không double-count goal/kick.

### Acceptance criteria

- `Aiming` bắt đầu cửa sổ 3 giây sau whistle.
- Người chơi có tối đa 3 giây cho cả chỉnh góc và nạp lực.
- Timeout trong `Aiming` và `Charging` đều kết thúc đúng lượt.
- Keeper có thể bắt bóng ở góc trái/phải.
- Dive có rotation/displacement phù hợp.
- Retry, pause và reset không giữ timer cũ.

### API contract bắt buộc

Agent 3 không được đọc field private hoặc tự suy luận từ `Update()`. Chỉ dùng event/property public do Agent 2 cung cấp.

### Báo cáo khi hoàn tất

- Commit hash.
- Test timer, timeout, keeper edge save/goal.
- Mô tả chính xác event/property public mới.

---

## Agent 3 - Giáo viên, Volleyball và scene integration

**Branch:** `codex/teacher-volleyball`

### Ownership

Được sửa:

```text
Assets/_Project/Scripts/Gameplay/Common/StartLecturer.cs
Assets/_Project/Scripts/Gameplay/Volleyball/
Assets/_Project/Scripts/Gameplay/Football/FootballDialogueAdapter.cs
Assets/Editor/StartLecturerAuthoring.cs
Assets/Editor/VolleyballSceneConfigurator.cs
Assets/Editor/FootballSceneConfigurator.cs
Assets/_Project/Scenes/MG_Volleyball.unity
Assets/_Project/Scenes/MG_Football.unity
Assets/Tests/EditMode/EditorTools/StartLecturerSceneTests.cs
Assets/Tests/EditMode/EditorTools/VolleyballSceneConfiguratorTests.cs
Assets/Tests/EditMode/EditorTools/FootballSceneConfiguratorTests.cs
Assets/Tests/PlayMode/Gameplay/Volleyball/
```

Không sửa Football rules/controller/HUD. Nếu cần API mới từ Football, gửi yêu cầu cho Agent 2 thay vì tự sửa core.

### Công việc Volleyball

1. Scale giáo viên gần bằng player trên sân.
2. Gắn cue/event:
   - ghi sổ khi ghi điểm;
   - đánh trượt: point rồi write;
   - whistle khi bắt đầu lượt phát bóng.
3. Tăng joystick visual và hitbox khoảng 20%.
4. Đưa joystick sát góc dưới trái nhưng vẫn tôn trọng safe area.
5. Không che nút thao tác hoặc vùng gameplay.

### Công việc Football teacher

Subscribe event của Agent 2:

```text
ShotWindowStarted -> whistle
ShotResolved(Goal) -> write score
ShotResolved(Miss/Save/Timeout) -> point then write
```

1. Đặt giáo viên ngoài vòng penalty theo ảnh tham chiếu.
2. Whistle trước từng lượt sút, không chỉ khi scene bắt đầu.
3. Không tạo timer riêng trong `StartLecturer`.
4. Dùng asset pose hiện có; không tạo asset mới.

### Acceptance criteria

- Teacher Volleyball có scale hợp lý và không che sân.
- Joystick lớn khoảng 20%, hitbox cũng lớn tương ứng.
- Teacher Football đứng đúng phía ngoài penalty area.
- Có đúng một whistle cho mỗi serve/shot.
- Goal có write; miss/timeout có point rồi write.
- Scene configurator và scene tests pass.

### Báo cáo khi hoàn tất

- Commit hash.
- Danh sách scene đã re-author.
- Screenshot vị trí teacher/joystick.
- Ghi rõ event Football đang consume.

---

## Agent 4 - Shared UI, dialogue, menu và typography

**Branch:** `codex/ui-menu`

### Ownership

Được sửa:

```text
Assets/_Project/Scripts/UI/
Assets/_Project/Settings/UI/
Assets/_Project/Fonts/
Assets/Fonts/
Assets/_Project/Scenes/Menu.unity
Assets/_Project/Scenes/Map.unity
Assets/_Project/Scenes/GameOver.unity
Assets/_Project/Scenes/Celebration.unity
Assets/Tests/EditMode/Presentation/
Assets/Tests/PlayMode/Presentation/
Assets/Tests/PlayMode/Progression/JourneyMapTests.cs
Assets/Tests/PlayMode/Progression/FootballResultRoutingTests.cs
```

Không sửa Football gameplay, Sprint timing, `MG_Football.unity` hoặc `MG_Volleyball.unity`.

### Công việc Football feedback

1. Sửa `GameplayDialogueView`/scheduler để notification có lifecycle:

   ```text
   Show -> Hold -> Fade -> Hide
   ```

2. Text trắng phải nằm trên nền/panel tương phản.
3. Khi fade xong:
   - alpha về 0;
   - object inactive;
   - không chặn raycast;
   - không còn tồn tại khi mở result/menu.
4. Không để feedback cũ lưu trên màn hình và đè ResultPanel.

### Công việc guide/menu

1. Di chuyển label “Đạt” để không che mặt giáo viên.
2. Ưu tiên anchor/safe-area thay vì tọa độ cố định.
3. Tăng title và nút back trong menu chọn môn học.
4. Cân lại kích thước header/back với các nút môn.

### Công việc typography

1. Chỉnh title Settings và thông báo qua môn.
2. Tránh font quá nặng/nhòe.
3. Kiểm tra text tiếng Việt, fallback glyph và drop-shadow/stroke.
4. Không đổi font gameplay nếu không cần thiết cho yêu cầu này.

### Acceptance criteria

- Feedback Football dễ đọc trên nền sáng/tối.
- Feedback fade-out và biến mất hoàn toàn.
- Không còn feedback khi chuyển Result/Menu.
- “Đạt” không che mặt giáo viên.
- Title Settings/result rõ ràng hơn.
- Header/back menu chọn môn đủ lớn và cân đối.
- UI tests và layout tests pass.

### Báo cáo khi hoàn tất

- Commit hash.
- Screenshot trước/sau cho notification, guide, font và subject menu.
- Danh sách test đã chạy.

## 5. Quy tắc phối hợp giữa các nhánh

1. Không có hai agent sửa cùng một scene.
2. Không có hai agent sửa cùng một file runtime.
3. Agent 2 là chủ sở hữu Football state/timer.
4. Agent 3 chỉ consume API Football để điều khiển giáo viên.
5. Agent 4 sở hữu shared dialogue presentation; Agent 3 chỉ phát event/dialogue request.
6. Agent 1 sở hữu toàn bộ Sprint timing/catalog/test cập nhật, kể cả text Sprint được build từ editor.
7. Nếu phát hiện cần sửa file ngoài ownership:
   - không tự sửa trực tiếp;
   - ghi lại trong handoff;
   - báo lead để tạo commit/API riêng.

## 6. Lịch làm việc 3-4 giờ

### 0:00-0:15 - Chuẩn bị

- Pull branch gốc mới nhất.
- Tạo branch theo tên ở trên.
- Kiểm tra Unity version/package state.
- Mỗi agent xác nhận ownership không bị trùng.

### 0:15-2:30 - Code song song

- Agent 1: Sprint timing + stop/fall + NPC.
- Agent 2: Football timer + keeper + HUD timer.
- Agent 3: teacher/Volleyball + scene wiring.
- Agent 4: notification fade + menu/font/layout.

### 2:30-3:15 - Test riêng từng lane

- Chạy test gần nhất với phần đã sửa.
- Mở scene thật để kiểm tra visual tối thiểu.
- Không chờ merge mới test.

### 3:15-4:00 - Handoff cho merge

Mỗi agent gửi:

- branch name;
- commit hash;
- file đã sửa;
- test đã chạy và kết quả;
- screenshot/video nếu có UI/animation;
- known issue hoặc phần chưa kiểm chứng;
- public API mới nếu có.

## 7. Thứ tự merge đề xuất

Merge sau khi cả 4 agent hoàn thành, theo thứ tự:

1. `codex/football-core`
2. `codex/teacher-volleyball`
3. `codex/ui-menu`
4. `codex/sprint-level1`

Sau mỗi merge chạy test liên quan. Cuối cùng chạy full smoke test cho:

```text
MG_Sprint
MG_Volleyball
MG_Football
Menu
Map
GameOver
Celebration
```

## 8. Definition of Done chung

- Không có compile error.
- Không có test cũ còn kiểm tra thời lượng Sprint 45/20/15.
- Football timeout không làm treo state machine.
- Teacher cue không bị gọi trùng.
- Notification không còn sau khi chuyển result/menu.
- Scene references không null.
- Các thay đổi visual được kiểm tra ít nhất một lần trong Game view landscape.
- Mỗi nhánh có commit độc lập và handoff rõ ràng.
