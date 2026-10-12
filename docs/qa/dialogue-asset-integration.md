# Kiểm chứng ghép lời thoại và asset

Ngày kiểm tra: 12/10/2026. Nhánh `feat/dialogue-asset-integration`, worktree `/tmp/kma-dialogue-integration`.

## Phạm vi

Nguồn câu chữ đã được đưa vào nhánh. Nội dung runtime nằm trong ScriptableObject; không đọc Markdown khi chơi. Git diff của `Assets/_Project/Art/Characters/` không có thay đổi; giữ asset và animation gameplay; sửa cast hội thoại, biểu cảm giảng viên, UI thoại và nhịp thanh trượt Bật cóc từ 0,9 xuống 0,7 giây một chiều (chu kỳ 1,4 giây).

## Bằng chứng tự động

- Task 11: `Builds/TestResults/dialogue-t11-final-edit.xml` 26/26 và `dialogue-t11-final-play.xml` 26/26. Đã thấy RED của duplicate GameOver host và mất bong bóng khi author scene, rồi GREEN.
- `Builds/TestResults/dialogue-t12-acceptance.xml`: 3/3. Mở trực tiếp năm scene và qua cả 10 checkpoint; một host; reset/unload bỏ câu cũ. Pointer đi qua EventSystem.RaycastAll trước khi dispatch và xác nhận input gameplay thật nhận thao tác: tap chạy, nút ĐÁNH, giữ/thả sút, chọn quân cờ, vùng chạm Bật cóc.
- Hai lần full regeneration được chạy qua `DialogueIntegrationAuthoring.BuildAll` (Task 11) và `RegenerateAndVerify` (Task 12). Hai manifest `dialogue-semantic-before.txt` / `dialogue-semantic-after.txt` giống nhau khi loại `m_FileID/m_PathID` tự sinh; vẫn so sánh đường dẫn asset, hierarchy component, câu chữ, pose và tuning. Chênh lệch ban đầu là lỗi phép so sánh ID, không phải mất nội dung.
- GameOver layout RED 0/3 tại 1440, 1920, 2400 ×1080 do tên người nói tràn; sau thay bố cục GREEN 3/3. Regeneration test ban đầu dùng sai đường dẫn tuning, đã sửa trước full suite.
- Full EditMode: `Builds/TestResults/dialogue-full-edit-release.xml` **1028/1028**, không skip.
- Full PlayMode: `Builds/TestResults/dialogue-full-play-release.xml` **372/372**, không skip.
- QA phát hiện lỗi binding trước khi HUD được tạo ở Frog/Volley; test scene mở lại RED 3/5 và GREEN 5/5. Chess tên/bong bóng tránh bàn cờ và bộ đếm: RED 0/3 rồi GREEN cả ba kích thước.
- Kết quả dùng ô thoại cạnh bảng, canvas trên scrim; RED saved-scene reveal 0/1 và GREEN 1/1 (`dialogue-t12-canvas-scene-green.xml`). Font/geometry tests `dialogue-t12-final-layout-green.xml` 5/5, gồm khoảng trống thanh tiến độ Bật cóc.

Sau full suite, scene/prefab serialize churn được bỏ bằng phục hồi scene ở Task 11 rồi chạy `RepairSavedBindings` để áp lại riêng các sửa bố cục/binding Task 12. Saved scene + geometry kiểm tra lại `dialogue-t12-clean-scene-edit.xml` **13/13**; pointer/journey/reveal `dialogue-t12-clean-scene-play.xml` **4/4** sau bước này. Không coi diff lớn do ID tự sinh là thay đổi gameplay.

## Kiểm tra trực quan

Đã chụp và xem trực tiếp 36 PNG (12 trạng thái × 3 kích thước 1920/1440/2400×1080) bằng `.claude/skills/testing-unity-ui-with-screenshots/SKILL.md`, request/done ID và PNG ở `Builds/Screenshots/`. Manifest unique request/done ID: `Builds/Screenshots/dialogue-captures.json`. Các ảnh có tiền tố `dialogue-{qaState}-{width}x1080.png`. Các trạng thái gồm ngã đầu/lệnh đứng dậy/ngã lặp/kết quả đã trừ lượt Bật cóc, lời sinh viên/lỗi thứ hai cờ vua, chạy còn 5 giây, bóng chuyền mất điểm, bóng đá trước lượt 5, Game Over và giảng viên tổng kết. QA dùng trạng thái/controller thực và production content ID, không ghi fake text hoặc sprite. Game Over yêu cầu render ngay khi event phát để không lỡ câu do Editor chạy nền.

Ảnh tĩnh là bằng chứng bố cục; bằng chứng pointer ở fixture acceptance riêng.

Ở 1440×1080, hình sân Bật cóc cắt một phần nhân vật tại mép trái và hướng dẫn kéo hướng bóng đá bị tràn trái; đây là bố cục gameplay giữ lại, không đổi asset/camera trong nhiệm vụ thoại. Ô thoại mới không che thanh trượt, nút hoặc quân cờ.

Ở 2400×1080, bong bóng Celebration che một phần đầu giảng viên; tư thế ghi sổ vẫn thấy rõ. Đây là hạn chế bố cục còn ghi nhận.

## Giới hạn bàn giao

Chưa build APK hoặc kiểm tra thiết bị Android trong nhiệm vụ này. Chưa merge/push. Các file user ở checkout gốc không được stage. `RunnerBreathing.cs.meta` không thuộc commit.

## Quyết định trong quá trình triển khai

- Ruling: create isolated worktree as part of the approved Native execution plan — keeps original user checkout untouched — implementation lives under /tmp until integration is authorized. Cost: open this worktree or integrate explicitly before using changes in the original checkout.
- Ruling: proceed despite existing baseline failures — isolate FMOD with ALSA null and investigate relevant scene authoring during integration — do not claim full suite green until resolved. Cost: null audio sink does not prove audible output on a physical device.
- Ruling: task-done validates fresh Unity XML rather than rerunning an unchanged suite for bookkeeping — developer requires avoiding redundant passes after green — evidence is the actual Unity wrapper command recorded above. Cost: stale XML would give false evidence, so full-suite results are refreshed on the final tree.
- Ruling: scene reopening and pointer acceptance are consolidated in Task 12 after real scene wiring; Task 4 verifies host/view isolation and idempotent authoring; cost if wrong: layout fixes during acceptance.
- Ruling: retain existing SprintStartPresentation phase check, which already prevents countdown ticking while composite gate stays Tutorial; no duplicate readiness check needed. Verified existing countdown/freeze/input tests. Cost if wrong: countdown display drift.
- Ruling: shot feedback may continue into idle Aiming and stops on charging or another kick; current 0.9s feedback alone cannot fit even the short student line. No gameplay timing changed. Cost if wrong: brief speech while choosing direction.
- Ruling: extend shared EmitText with context validity and refresh unlimited mandatory event IDs; preserve public ChessCastView.Say through host EmitLine for compatibility, while controller uses content IDs. Prevents stale hints and competing timers. Cost if wrong: compatibility speech can preempt a prior line.
- Ruling: Frog guide context becomes nullable so direct scene entry does not claim an attempt was already charged; true/false Journey branches stay explicit. Found RED invented charged-attempt text before fixing. Cost if wrong: direct-entry guide omits penalty-specific life language.
- Ruling: include existing StartLecturer authoring in Task 11 so the ambient lecturer also records results instead of cheering; no NPC added and no gameplay changed. Supplement mapping tests RED confirm old friendly assets. Cost if wrong: ambient gesture timing retains legacy cheer cadence while drawing scoreWrite.
- Ruling: larger shared dialogue geometry and 28px font floor fix proven 17px RED; runtime slot enum supplies result/GameOver placements. Add a read-only common timing interface and serialized balance reference to avoid a UI/Frog assembly cycle. Cost if wrong: reserved space needs adjustment in visual QA.
- Task 12: Ruling: update the legacy PhaseOverlay assertion to retain the guide hold until guide closure, and life label to edited LƯỢT THI remaining/max — the approved start-gate and source-text contracts supersede these old expectations — cost if wrong: callers treating PhaseOverlay.Bind as closing a guide must now close it explicitly.
- Task 12: Ruling: adapt existing real-controller/audio journey fixtures to close guides and wait for opening completion; old name/pose assertions follow edited source, emoji rendering uses isolated fixture content — old tests bypassed the newly approved gate and assumed retired dialogue jokes — cost if wrong: longer journey test runtime; emoji fixture does not prove an emoji exists in edited story. Keyboard offset assertion measures DSP at callback instead of assuming zero host dispatch latency (observed 320ms overhead) — verifies 125ms compensation against actual audio clock — cost if wrong: up to one 30ms measurement tolerance.

## Review toàn nhánh và sửa lỗi

Fresh-context reviewer đã xem diff `1c5485a..6f718f7`: 4 Important, 1 Minor, không Critical. Đã kiểm tra lại từng nhận xét trên code và nhận cả bốn Important. Một lượt sửa bằng TDD, không dispatch review thứ hai:

- Missing host/adapter: serialized `GameplayOpeningFlow.Awake` đã giữ cổng nhưng adapter không Configure. Flow bỏ riêng hold của thoại sau Start; Chess vẫn cho Bắt đầu/gợi ý hoạt động khi presentation thiếu. Test `SerializedAwakeHoldFailsOpenWhenAdapterCannotConfigure` và `MissingHostKeepsBeginAndHintsPlayable` RED → GREEN.
- Gợi ý cờ: cấp mới thay ngay câu/gợi ý cũ, validity kiểm tra cả node lẫn cấp gợi ý. Test `LatestHintPreemptsEarlierSpeechNearTimeout` gọi 3 lần khi còn khoảng 10 giây, RED → GREEN.
- Bật cóc: callback giữ rules của lượt cũ và generation; disable/rebind làm request cũ hết hiệu lực. Test `DisabledOrReboundAdapterInvalidatesOldFall(false/true)` RED (exception hoặc câu còn sống) → GREEN.
- Nội dung hỏng: kiểm tra từng event trước enqueue ở Emit/EmitText/EmitLine; không kiểm tra toàn thư viện rồi chặn event lành. Null/empty lines, null line, thiếu sprite và icon bắt buộc được bỏ an toàn, diagnostic một lần. Test `MalformedSelectedContentIsOmittedBeforeQueueMutation` 5 trường hợp RED → GREEN; event lành sau đó vẫn phát.

`Builds/TestResults/dialogue-review-red.xml`: **20/30**, 10 lỗi tái hiện. `dialogue-review-final-green.xml`: **30/30**. Một lần GREEN trung gian 29/30 phát hiện câu mẫu hint2 có token để thay bằng gợi ý theo node; EmitText được sửa để kiểm tra template nhưng không resolve các token trước khi thay câu động.

### Rulings từ các mục reviewer chưa kết luận

- Volley giữ point pause 1,2 giây và có thể bỏ câu thứ hai đã lỗi thời theo mục 8 nguồn lời thoại. Cost: ít câu giảng viên trong pha nhanh; không kéo dài gameplay để đọc thoại.
- Giữ crop nhân vật Bật cóc/hướng dẫn bóng đá ở 4:3 vốn có, theo yêu cầu giữ sân/asset/camera. Cost: hạn chế đọc ở 4:3 vẫn tồn tại.
- Retry production tạo scene mới; không chứng nhận API tái sử dụng controller sau kết thúc ngay tại chỗ. Disable/rebind cụ thể đã có test và được sửa. Cost: caller bên ngoài muốn reuse tại chỗ có thể phải reset thêm flags adapter.
- Android/device chưa kiểm tra, đúng giới hạn bàn giao native đã duyệt. Cost: chưa biết lỗi render/input riêng thiết bị.

### Deferred minors

- Celebration ở 2400×1080: bong bóng che một phần đầu giảng viên, vẫn thấy bàn tay/sổ và không che nút hành động.

### Kiểm chứng toàn bộ sau sửa review

- `Builds/TestResults/dialogue-post-review-full-edit.xml`: **1028/1028**, 0 failed, 0 skipped.
- `Builds/TestResults/dialogue-post-review-full-play-retry.xml`: **382/382**, 0 failed, 0 skipped. Tăng 10 trường hợp hồi quy so với 372 test trước review.
- Lần Full PlayMode đầu (`dialogue-post-review-full-play.log`) Editor thoát 1 trước khi xuất XML, không có fatal diagnostic; không tính là pass. Khôi phục snapshot rồi chạy lại cùng mã nguồn cho kết quả đầy đủ trên.
- Đã phục hồi các scene/prefab/font/resources/settings từ snapshot authored trước test, kiểm tra diff để giữ nguyên đúng bản scene đã duyệt. Sửa review chỉ gồm runtime dialogue code và regression tests; không đổi geometry nên dùng lại 36 PNG đã kiểm tra.
- Chốt nhánh tại worktree để bàn giao theo phạm vi kế hoạch; không merge/push. Workspace SDD tạm được dọn sau khi fixes/QA đã commit; worktree, XML và PNG vẫn giữ lại.
