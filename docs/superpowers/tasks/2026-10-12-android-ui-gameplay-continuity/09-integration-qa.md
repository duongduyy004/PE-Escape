# 09-integration-qa

[Task index](README.md) · [Spec](../../specs/2026-10-12-android-ui-gameplay-continuity-design.md)

Task source states below show implementation progress; implemented means Unity/device checks are still pending. File liệt kê là phạm vi kiểm tra/có thể ảnh hưởng, không mặc định phải sửa mọi file. Test liệt kê là fixture hiện có cần chạy/mở rộng, chưa được chạy trong đợt chia task.

## Q01

**Summary và toàn bộ chữ thông báo không tràn**

- Trạng thái: `implemented`

## Q02

**Regenerate, reopen và tương thích serialized data**

- Trạng thái: `verified`

## Q03

**Focused và full Unity test gate**

- Trạng thái: `in_progress`
- Bao phủ: R1, R2, R3, R4, R5, R6, R7, R8, R9, R10
- Phụ thuộc: [Q02](09-integration-qa.md#q02)

**Phạm vi file**

- [README.md](../../../../README.md)
- [ProjectSettings/ProjectVersion.txt](../../../../ProjectSettings/ProjectVersion.txt)

**Việc cần làm**

- [ ] Mỗi task chạy fixture cần thiết sau thay đổi, rồi full EditMode/PlayMode sau tích hợp; chỉ mở rộng chạy lại khi code thay/fail.
- [ ] Các assertion cũ TitleKMA/12 nhịp/60 s/Recover cập nhật theo contract mới, không xóa test bảo vệ unrelated behavior.
- [x] Focused XML/log `Builds/TestResults/focused-fixes.xml` ghi 63 tests, 63 passed, 0 failures/skipped, Passed; compile sạch.
- [ ] Full EditMode/PlayMode XML vẫn cần chạy trước khi đóng Q03.

**Điều kiện Done**

- [ ] Evidence thực từ Unity hiện tại, không dựa báo cáo lịch sử; failures giải quyết hoặc gate chưa Done.
- [ ] Nếu tool/runtime không chạy được ghi blocker cụ thể, không gọi tests passed; tasks implementation chưa nghiệm thu đầy đủ.

**Kiểm chứng**

Evidence và acceptance ở trên; không tạo unit test chỉ để kiểm thay asset/đường dẫn đơn giản.

## Q04

**Build và smoke test APK ARM64**

- Trạng thái: `not_started`
- Bao phủ: R2, R6
- Phụ thuộc: [Q03](09-integration-qa.md#q03)

**Phạm vi file**

- [tools/build-apk.cmd](../../../../tools/build-apk.cmd)
- [tools/build-apk.ps1](../../../../tools/build-apk.ps1)
- [Assets/Editor/AndroidBuildMatrix.cs](../../../../Assets/Editor/AndroidBuildMatrix.cs)
- [Assets/Editor/BuildScript.cs](../../../../Assets/Editor/BuildScript.cs)

**Việc cần làm**

- [ ] Dùng Unity version của project, đóng Editor trước batch; build ARM64 bằng wrapper hiện có với tên/output xác định.
- [ ] Ghi commit/build hash/APK SHA-256/log, name/icon/ID; cài update không uninstall làm mất save.
- [ ] Emulator x86_64 nếu cần là bổ sung, không thay phone gate. Kiểm cold start/menu/scene transitions/offline.

**Điều kiện Done**

- [ ] APK ARM64 build thành công, launch đúng tên/icon và route cơ bản, có log/hash reproducible.
- [ ] Không đổi keystore/application ID ngoài yêu cầu; không coi build-only là device QA xong.

**Kiểm chứng**

- [AndroidBuildMatrixTests.cs](../../../../Assets/Tests/EditMode/EditorTools/AndroidBuildMatrixTests.cs)

## Q05

**Nghiệm thu touch và hành trình trên Android**

- Trạng thái: `not_started`
- Bao phủ: R1, R2, R3, R4, R5, R6, R7, R8, R9, R10
- Phụ thuộc: [Q04](09-integration-qa.md#q04), [A02](00-baseline.md#a02)

**Phạm vi file**

- [Assets/_Project/Scenes/Menu.unity](../../../../Assets/_Project/Scenes/Menu.unity)
- [Assets/_Project/Scenes/Map.unity](../../../../Assets/_Project/Scenes/Map.unity)
- [Assets/_Project/Scenes/MG_Sprint.unity](../../../../Assets/_Project/Scenes/MG_Sprint.unity)
- [Assets/_Project/Scenes/MG_Volleyball.unity](../../../../Assets/_Project/Scenes/MG_Volleyball.unity)
- [Assets/_Project/Scenes/MG_Football.unity](../../../../Assets/_Project/Scenes/MG_Football.unity)
- [Assets/_Project/Scenes/MG_FrogJump.unity](../../../../Assets/_Project/Scenes/MG_FrogJump.unity)
- [Assets/_Project/Scenes/Celebration.unity](../../../../Assets/_Project/Scenes/Celebration.unity)

**Việc cần làm**

- [ ] Device matrix: 1280×720, 1920×1080, 2400×1080, 2560×1600, 2048×1536; simulator geometry bổ sung, ít nhất 1 phone ARM64 thật cho touch/safe area.
- [ ] Landscape hai chiều, notch, gesture/3-button nav,resume/background; đo visible popup1,5×,footbutton1,3×,touch>=48 dp thực.
- [ ] Video Learn 45/60,Volley3 drills/action contact/landing,Continue tới next,failure/frog/retry,no life/chess/summary; kiểm save cũ và double tap.
- [ ] Frog sampled first-window no-fall và feel; keeper matrix near/far/left/right Saved/Goal; đọc thông báo trên nền trắng/đậm.
- [ ] Nếu QA/tune sửa code/config sau Q02–Q04: reopen task bị ảnh hưởng, regenerate/reopen lại Q02, chạy lại tests Q03 và build/hash Q04 rồi nghiệm thu Q05 bằng APK mới; không giữ evidence xanh của bản cũ.

**Điều kiện Done**

- [ ] R1–R10 có screenshot/video/evidence pass cụ thể; cảm giác/độ rõ không chỉ dựa test enum/file name.
- [ ] Cases thiếu device hoặc chưa thực hiện giữ Pending; không gộp toàn matrix thành đã pass từ1emulator.

**Kiểm chứng**

Evidence và acceptance ở trên; không tạo unit test chỉ để kiểm thay asset/đường dẫn đơn giản.

## Q06

**Bàn giao task status và báo cáo không bỏ sót**

- Trạng thái: `not_started`
- Bao phủ: R1, R2, R3, R4, R5, R6, R7, R8, R9, R10
- Phụ thuộc: [Q05](09-integration-qa.md#q05)

**Phạm vi file**

- [README.md](../../../../README.md)

**Việc cần làm**

- [ ] Lập QA mới liên kết XML/log/APK/hash/device/ảnh/video và task IDs; cập nhật README/design khi thay final tune.
- [ ] Đánh dấu Done chỉ khi các acceptance/test/authoring của task đủ; tách implemented/tested/device verified.
- [ ] Trace lại R1–R10 và từng yêu cầu con đến task+evidence, ghi limitation còn tồn tại; không để hidden TODO ngoài backlog.

**Điều kiện Done**

- [ ] Mọi R có evidence và owner/handoff/status rõ; không claim runtime verified khi mới review source.
- [ ] Chỉ bàn giao hoàn thành toàn bộ khi Q01–Q05 đạt; giữ issue/task Pending cho công việc chưa đạt.

**Kiểm chứng**

Evidence và acceptance ở trên; không tạo unit test chỉ để kiểm thay asset/đường dẫn đơn giản.
