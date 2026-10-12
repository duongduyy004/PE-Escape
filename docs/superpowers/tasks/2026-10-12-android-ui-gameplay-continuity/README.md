# Task index — Sửa Android UI, flow và animation

Ngày: 2026-10-12. **36 task / 10 nhóm / R1–R10**. Baseline đã rà: `10b86bc`. Phạm vi đợt này là chia việc để triển khai; runtime chưa sửa, Unity/APK chưa xác minh.

[Spec](../../specs/2026-10-12-android-ui-gameplay-continuity-design.md) · [Plan tổng thể](../../plans/2026-10-12-android-ui-gameplay-continuity.md) · [Manifest](task-manifest.json) · [Kết quả kiểm cấu trúc](task-audit.json)

## Cách dùng

1. Chọn task có dependencies đã Done; giao theo ID. Mỗi task có phạm vi file, checklist, acceptance, fixture và defaults đề xuất.
2. Hoàn thành một task bao gồm source/config/scene liên quan và kiểm chứng thực; chỉ đổi C# mà bỏ serialized assets chưa đủ.
3. Cập nhật trạng thái `not_started → in_progress → implemented → verified → done`; khi thiếu công cụ/evidence ghi `blocked` kèm lý do. `implemented` không đồng nghĩa đã kiểm trên Android. Các trạng thái task này là nội bộ tài liệu, không phải goal/thread status.
4. Ghi evidence XML/log/ảnh/video tại QA kèm ID. Không tick Done theo tên PNG, enum hay câu mô tả.
5. Manifest và checklist Markdown phải cùng trạng thái khi bàn giao; audit cấu trúc này là snapshot ở lúc chia task, không phải CI hoặc kết quả runtime.

## Bắt buộc và đề xuất

Bắt buộc từ người dùng: fit menu Android, popup/icon/font lớn khoảng 50%, tên Chạy trốn thể chất/logo phù hợp, nút chân lớn khoảng 30% và gần góc, nhân vật lớn hơn, Học chạy45 hoặc 60 giây, Học Volley có động tác thật/nút icon transparent, Tiếp tục→bài sau, chữ rõ/không tràn, frog 30 giây/vùng xanh hẹp và có đường thắng, animation Volley/Keeper đúng asset.

Defaults triển khai có thể tune: Learn 45 s + bestStreak 12 (ngưỡng12 là bổ sung chống idle); nhân vật +25%; Volley drills 3/2/2; popup 75% high/92–96% wide và token 186/69/54/42/39; frog safe20%/2,2–3,2m; keeper near/far 1 m và durations. Giữ phân biệt defaults với yêu cầu gốc; tune phải ghi lý do/evidence và cập nhật spec. Core R6 là Journey pass→next; menu new/resume intent, Review/FreePlay CTA và error recovery là supporting scope bảo vệ flow. Không thêm menu hệ thống/quy tắc score/sport mới.

## Thứ tự và phụ thuộc

A01 là điều kiện bắt đầu code. A02 cần baseline thiết bị cho so sánh, có thể làm sau khi có máy nhưng bắt buộc trước gate Q05. U01→U02/U03 là nền UI. M/Brand/Sprint/Volley/Frog/Keeper được làm theo dependencies thực trong từng task; không ép tất cả thành chuỗi T0→T9. C01–C04 có thể xây độc lập rulegame, C05 đợi S04/V04/V05 để kiểm Journey thực. Q01→Q02→Q03→Q04→Q05→Q06 là gate tích hợp/bàn giao; Q02 cũng đợi hết các nhánh tính năng.

| ID | Task | R | Phụ thuộc |
|---|---|---|---|
| [A01](00-baseline.md#a01) | Xác nhận code, công cụ và dữ liệu thử | nền | — |
| [A02](00-baseline.md#a02) | Chụp baseline Android và đo bố cục | R1, R2, R3, R5, R7, R9, R10 | A01 |
| [U01](01-ui-foundation.md#u01) | Chuẩn hóa chuyển đổi safe area và cập nhật động | R1, R3, R7 | A01 |
| [U02](01-ui-foundation.md#u02) | Tách nền phủ màn khỏi lớp tương tác an toàn | R1, R2, R7 | U01 |
| [U03](01-ui-foundation.md#u03) | Text roles, contrast và quy tắc containment | R7 | U01 |
| [M01](02-map-popup.md#m01) | Map chọn môn fit safe rect | R1 | U02, U03 |
| [M02](02-map-popup.md#m02) | Popup ba bước lớn hơn và CTA không tràn | R1, R7 | M01 |
| [B01](03-branding.md#b01) | Tạo logo và icon theo phong cách game | R2, R3, R5 | A01 |
| [B02](03-branding.md#b02) | Menu thật thay title/button vẽ sẵn | R1, R2, R7 | B01, U02, U03 |
| [B03](03-branding.md#b03) | Splash và tên/icon ứng dụng đồng nhất | R2, R7 | B02 |
| [S01](04-sprint.md#s01) | Nút hai chân lớn và gần góc | R3 | B01, U01, U03 |
| [S02](04-sprint.md#s02) | Phóng visual nhân vật chạy và giữ chân đúng lane | R3 | U01, S01 |
| [S03](04-sprint.md#s03) | Learn chạy đủ thời gian và điều kiện hoàn thành | R4 | A01 |
| [S04](04-sprint.md#s04) | HUD, guide, thoại và result cho timed Learn | R4, R7 | S03, U03 |
| [V01](05-volleyball.md#v01) | Sửa reset Học để động tác và bóng kịp hiển thị | R5, R9 | A01 |
| [V02](05-volleyball.md#v02) | Action sequence và replay animation | R9, R5 | V01 |
| [V03](05-volleyball.md#v03) | Wire pose bóng chuyền đúng tình huống | R9, R5 | V02 |
| [V04](05-volleyball.md#v04) | Học bóng chuyền có đỡ, phát và nhảy đánh | R5 | V03 |
| [V05](05-volleyball.md#v05) | Hai nút Volley transparent có icon | R5 | B01, U01, U03 |
| [C01](06-continuation.md#c01) | Policy chọn đích Tiếp tục | R6 | A01 |
| [C02](06-continuation.md#c02) | Dialogue queue báo hoàn tất đúng một lần | R6 | C01 |
| [C03](06-continuation.md#c03) | Map bridge và intent đến đúng bài | R6 | C02 |
| [C04](06-continuation.md#c04) | CTA kết quả và phục hồi lỗi save/load | R6, R7 | C03, U03 |
| [C05](06-continuation.md#c05) | Kiểm end-to-end luồng mới với Learn mới | R6, R4, R5 | C04, S04, V04, V05 |
| [F01](07-frog-jump.md#f01) | Balance bật cóc 30 giây và dữ liệu nhất quán | R8 | A01 |
| [F02](07-frog-jump.md#f02) | Vùng xanh và chuyển động bật cóc mới | R8, R7 | F01, U03 |
| [F03](07-frog-jump.md#f03) | Mô phỏng trường hợp tốt nhất và biên an toàn | R8 | F02 |
| [K01](08-goalkeeper.md#k01) | Mapping keeper gần/xa và hướng đã chốt | R10 | A01 |
| [K02](08-goalkeeper.md#k02) | Timeline landing rồi Cheer đứng thẳng | R10 | K01 |
| [K03](08-goalkeeper.md#k03) | Kiểm matrix keeper và luật bắt bóng | R10 | K02 |
| [Q01](09-integration-qa.md#q01) | Summary và toàn bộ chữ thông báo không tràn | R7 | U03, M02, B03, S04, V04, V05, C04, F02 |
| [Q02](09-integration-qa.md#q02) | Regenerate, reopen và tương thích serialized data | R1, R2, R3, R4, R5, R6, R7, R8, R9, R10 | M02, B03, S02, S04, V04, V05, C05, F03, K03, Q01 |
| [Q03](09-integration-qa.md#q03) | Focused và full Unity test gate | R1, R2, R3, R4, R5, R6, R7, R8, R9, R10 | Q02 |
| [Q04](09-integration-qa.md#q04) | Build và smoke test APK ARM64 | R2, R6 | Q03 |
| [Q05](09-integration-qa.md#q05) | Nghiệm thu touch và hành trình trên Android | R1, R2, R3, R4, R5, R6, R7, R8, R9, R10 | Q04, A02 |
| [Q06](09-integration-qa.md#q06) | Bàn giao task status và báo cáo không bỏ sót | R1, R2, R3, R4, R5, R6, R7, R8, R9, R10 | Q05 |

## Ma trận không bỏ sót yêu cầu

### R1 — Fit menu Android; popup và icon/font +50%

- Home/Map full render surface: [U01](01-ui-foundation.md#u01), [U02](01-ui-foundation.md#u02), [B02](03-branding.md#b02)
- Map đủ4trạm: [M01](02-map-popup.md#m01)
- Popup/icon/font1,5×,objective/close/CTA: [M02](02-map-popup.md#m02)
- Ảnh cùngdevice/aspect: [A02](00-baseline.md#a02), [Q05](09-integration-qa.md#q05)

### R2 — Tên Chạy trốn thể chất, logo và icon

- Logo/icon/source/credit: [B01](03-branding.md#b01)
- Menu visible title và callback: [B02](03-branding.md#b02)
- Splash/launcher/configurator: [B03](03-branding.md#b03)
- Name/icon trênAPK: [Q04](09-integration-qa.md#q04), [Q05](09-integration-qa.md#q05)

### R3 — Chân trái/phải, nút sát góc +30%, nhân vật lớn

- Sprite chân: [B01](03-branding.md#b01)
- Visual1,3×/safe corners/input/cue: [S01](04-sprint.md#s01)
- Nhân vật lớn/pivot/lane/label: [S02](04-sprint.md#s02)
- Đo vàtouch thật: [Q05](09-integration-qa.md#q05)

### R4 — Học chạy có thời lượng đủ làm quen

- Timer45/60Play,bestStreak,không early finish: [S03](04-sprint.md#s03)
- HUD/guide/dialogue/result nhất quán: [S04](04-sprint.md#s04)
- Pause,fail Learn,no penalty,flow: [S03](04-sprint.md#s03), [C05](06-continuation.md#c05), [Q05](09-integration-qa.md#q05)

### R5 — Học bóng chuyền thực sự đỡ/đánh và nút transparent

- Không reset xóa động tác/flight: [V01](05-volleyball.md#v01)
- Replay/phase/contact: [V02](05-volleyball.md#v02), [V03](05-volleyball.md#v03)
- Drill Receive/Serve/JumpHit: [V04](05-volleyball.md#v04)
- Transparent icon/multi-touch: [B01](03-branding.md#b01), [V05](05-volleyball.md#v05)
- Input/guide/PlayMode/device: [V04](05-volleyball.md#v04), [V05](05-volleyball.md#v05), [Q05](09-integration-qa.md#q05)

### R6 — Tiếp tục vào bài kế tiếp sau lời dẫn

- Policy checkpoint saucommit: [C01](06-continuation.md#c01)
- Final line/skip/empty/seen/save marker: [C02](06-continuation.md#c02)
- Bridgeintent,menu/browse/cancel: [C03](06-continuation.md#c03)
- ResultCTA/save-loadrollback/doubletap: [C04](06-continuation.md#c04)
- Full journey/failure/frog/final/review/restart: [C05](06-continuation.md#c05), [Q05](09-integration-qa.md#q05)

### R7 — Chữ đọc rõ, có nền/khung và không tràn

- Contrast/textroles/fonts: [U03](01-ui-foundation.md#u03)
- Popup/Home/Splash: [M02](02-map-popup.md#m02), [B02](03-branding.md#b02), [B03](03-branding.md#b03)
- HUD/feedback/guide/dialogue/result/errors: [S04](04-sprint.md#s04), [V04](05-volleyball.md#v04), [V05](05-volleyball.md#v05), [C04](06-continuation.md#c04), [F02](07-frog-jump.md#f02), [Q01](09-integration-qa.md#q01)
- Celebration/JourneyCourseSummary/minigameResult: [Q01](09-integration-qa.md#q01)
- Longeststrings và aspect/device: [Q01](09-integration-qa.md#q01), [Q05](09-integration-qa.md#q05)

### R8 — Bật cóc vùng xanh hẹp, 30s và hoàn thành được

- 30s/config/fallback/thoại: [F01](07-frog-jump.md#f01)
- Greenrule[0,4;0,6]/viewdistance: [F02](07-frog-jump.md#f02)
- Best+safe-edge frame simulation/timeout/penalty: [F03](07-frog-jump.md#f03)
- Playtest và tune: [Q05](09-integration-qa.md#q05)

### R9 — Animation Volley mượt và đúng asset

- Delayedreset: [V01](05-volleyball.md#v01)
- Actionidentity,Serve!=Smash/contact-follow-land: [V02](05-volleyball.md#v02)
- Dig/SpikeContact/Follow/Land/Recover,pivot/net/opponentregression: [V03](05-volleyball.md#v03)
- Học/Luyện/Thi và repeats: [V04](05-volleyball.md#v04), [Q05](09-integration-qa.md#q05)

### R10 — Landing thủ môn đúng phía/cự ly, cheer khi bắt được

- MappingfarDiveEmpty,nearReachEmpty,center: [K01](08-goalkeeper.md#k01)
- Landingtimeline,rotation0,Cheer,resetshot,oneball: [K02](08-goalkeeper.md#k02)
- Matrixshots/unchangedcollision/outcome: [K03](08-goalkeeper.md#k03)
- Video vàdevice: [Q05](09-integration-qa.md#q05)

## Tránh xung đột file khi giao subagent

Dependency là điều kiện logic; không phải khóa ghi file. Một agent sở hữu file đang sửa, agent khác gửi patch đề xuất hoặc chờ. Không chạy nhiều Unity batch/authoring vào cùng project; không sửa file .unity YAML bằng tay khi pipeline authoring tạo ra chúng. Không tạo thread/user-facing GitHub issue hay gửi message ra ngoài trong scope chia task này.

| File chia sẻ | Task có thể ảnh hưởng |
|---|---|
| `Assets/_Project/Scripts/UI/JourneyDialoguePresenter.cs` | U02, C02, C03, Q01 |
| `Assets/Editor/ShellSceneAuthoring.cs` | U02, M02, B02, B03, Q02 |
| `Assets/_Project/Scripts/UI/UITheme.cs` | U03, M01, M02 |
| `Assets/_Project/Settings/UI/UITheme.asset` | U03, M01, M02 |
| `Assets/Editor/StudentJourneyContentBuilder.cs` | S03, S04, V04, Q02 |
| `Assets/_Project/Scripts/UI/ResultPanel.cs` | S04, C04, F01, Q01 |
| `Assets/Editor/GameplayDialogueContentBuilder.cs` | S04, V04, F01, Q02 |
| `Assets/Editor/VolleyballSceneConfigurator.cs` | V03, V05, Q02 |
| `Assets/_Project/Scripts/Core/SceneRouter.Journey.cs` | C01, C03, C04 |
| `Assets/Editor/FootballSceneConfigurator.cs` | K01, K02, Q02 |

C05 tests và các task nguồn cũng có thể cùng sửa fixture; áp dụng owner cho cả Tests. B01 cấp asset cho S01/V05; StudentJourneyContentBuilder/GameplayDialogueContentBuilder phải tích hợp patch từng môn rồi regenerate một lần dưới owner. SceneRoot/UITheme/ResultPanel thay đổi phải được phối hợp, không overwrite diff người khác.

## Khi QA phát hiện lỗi sau build

Mọi sửa code/config/asset sau Q02–Q04 làm mất hiệu lực evidence của phần bị ảnh hưởng. Reopen task nguồn → Q02 regenerate/reopen → Q03 kiểm lại phạm vi liên quan → Q04 build APK/hash mới → Q05 nghiệm thu lại; Q06 chỉ đóng khi evidence cùng phiên bản. Không thêm dependency ngược vào DAG gây cycle; đây là quy tắc tái mở gate.

## Gate evidence

- Source audit: 2 subagent Luna high, read-only; xác nhận source hazards và tên fixture. Không chạy Unity trong audit.
- Cấu trúc:36 ID unique,dependency tồn tại,DAG không cycle,277 path tham chiếu tồn tại,R1–R10 và từng yêu cầu con có task. Xem task-audit.json.
- Runtime: chưa chạy. Q03 chỉ Done khi XML có count > 0 / zero failure / Passed từ Unity đúng version.
- APK: Q04 build ARM64 + hash/cold start; Q05 ít nhất 1 máy thật và geometry matrix có evidence.
- Handoff: Q06 chỉ Done khi các acceptance đều đạt, save cũ giữ được, source builder/config/scenes đồng bộ và pending được ghi rõ.

Trạng thái hiện tại: 29 task nguồn `implemented` (chờ nghiệm thu); Q02 `verified` cho authoring/reopen; A01 `in_progress`; A02 cùng Q03–Q06 vẫn pending. `verified` ở Q02 chỉ xác nhận serialized authoring/reopen evidence, không xác nhận Unity test, APK hay Android/device QA.
