# PLAN — Vượt Thể KMA

> Phạm vi hiện tại của prototype Unity: Sprint, Volleyball và Football. Cả ba môn có thể chọn trên bản đồ; Football dùng thanh hướng và giữ/thả SÚT theo preview đã duyệt.

## 1. Nền tảng

- Unity 6, C#, Android landscape, chạy offline.
- Input System dùng chung cho thao tác chạm, vuốt, giữ và nhịp.
- uGUI và TextMeshPro; lưu tiến trình JSON tại `Application.persistentDataPath`.
- Mỗi màn gameplay trả kết quả chuẩn hóa từ 0 đến 10; xếp hạng chung: S ≥ 9, A ≥ 8, B ≥ 7, C ≥ 6, D ≥ 5, còn lại F.

## 2. Luồng và tiến trình

`Bootstrap → Menu → Map → Subject → Result`; thất bại làm mất một mạng và trở về Map, hoặc đến GameOver khi hết mạng.

`SceneRouter` duy trì `GameSession` qua các lần tải scene và ngăn chuyển cảnh trùng lặp. Save có phiên bản và migration để giữ tiến trình tương thích khi cấu trúc môn học thay đổi. Bản lưu hiện tại là v9 (thêm kỷ lục cờ vua và cờ `celebrationSeen`).

## 3. Môn học được giữ lại

| Môn | Scene | Trạng thái bản đồ |
|---|---|---|
| Sprint | `MG_Sprint` | Có thể chơi |
| Volleyball | `MG_Volleyball` | Có thể chơi |
| Football | `MG_Football` | Penalty: thanh hướng, lực sút và trọng lực |
| Bài kiểm tra cuối (cờ vua) | `MG_ChessFinal` | Trạm thứ 4 trên bản đồ: chiếu hết thế cờ do AI sinh theo độ khó (Dễ 1/2, Vừa 2/3, Khó 3/5 nước tối ưu/tối đa), thời gian tính theo từng thế cờ, chấm F–A+; không trừ lượt, không bật cóc. Thắng lần đầu mở `Celebration` |

Mỗi môn có luật và điều kiện hoàn thành riêng. Kết quả dùng cùng thang điểm và vòng đời chung: `Tutorial → Countdown → Play → Resolve`.

## 4. Scene và hệ thống dùng chung

- `Bootstrap`, `Map`, `MG_Sprint`, `MG_Volleyball`, `MG_Football`, `MG_ChessFinal`, `Celebration`, `Punishment`, `GameOver`.
- `GameSession`, `SaveSystem`, `SceneRouter`, subject configuration và bộ trình bày bản đồ quản lý tiến trình.
- Input dùng chung gồm tap, alternate tap, rhythm, hold, swipe và timing window; scene chỉ sử dụng các detector phù hợp.
- Thành phần UI chia sẻ theme, safe-area handling, HUD, tutorial, countdown và kết quả.

## 5. Nguyên tắc gameplay và trình bày

- Mục tiêu chính quyết định pass/fail; combo và thành tích phụ chỉ đóng góp điểm mastery.
- Cue hình ảnh/âm thanh báo trước các sự kiện gây bất lợi và thao tác phản ứng phải rõ ràng.
- Giao diện landscape thích ứng safe area Android; vùng chạm lớn và tránh cạnh gesture hệ thống.
- Pattern gameplay được author hóa để kết quả có thể dự đoán và kiểm tra.

## 6. Kiểm chứng

Khi được yêu cầu chạy xác minh, dùng Unity Test Framework cho EditMode/PlayMode và kiểm tra build Android riêng. Kết quả lịch sử trong QA không đại diện cho trạng thái hiện tại nếu chưa chạy lại sau thay đổi.
