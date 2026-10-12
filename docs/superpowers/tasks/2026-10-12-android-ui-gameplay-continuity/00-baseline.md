# 00-baseline

[Task index](README.md) · [Spec](../../specs/2026-10-12-android-ui-gameplay-continuity-design.md)

Task source states below show implementation progress; implemented means Unity/device checks are still pending. File liệt kê là phạm vi kiểm tra/có thể ảnh hưởng, không mặc định phải sửa mọi file. Test liệt kê là fixture hiện có cần chạy/mở rộng, chưa được chạy trong đợt chia task.

## A01

**Xác nhận code, công cụ và dữ liệu thử**

- Trạng thái: `in_progress`

## A02

**Chụp baseline Android và đo bố cục**

- Trạng thái: `not_started`
- Bao phủ: R1, R2, R3, R5, R7, R9, R10
- Phụ thuộc: [A01](00-baseline.md#a01)

**Phạm vi file**

- [Assets/_Project/Scenes/Map.unity](../../../../Assets/_Project/Scenes/Map.unity)
- [Assets/_Project/Scenes/Menu.unity](../../../../Assets/_Project/Scenes/Menu.unity)
- [Assets/_Project/Scenes/MG_Sprint.unity](../../../../Assets/_Project/Scenes/MG_Sprint.unity)
- [Assets/_Project/Scenes/MG_Volleyball.unity](../../../../Assets/_Project/Scenes/MG_Volleyball.unity)
- [Assets/_Project/Scenes/MG_Football.unity](../../../../Assets/_Project/Scenes/MG_Football.unity)
- [Assets/_Project/Scenes/Celebration.unity](../../../../Assets/_Project/Scenes/Celebration.unity)

**Việc cần làm**

- [ ] Chụp đúng 2400×1080 và 1920×1080: Home/Splash/Map/popup/Sprint Học/Celebration; ghi safe rect, canvas scale, viewport và navigation mode.
- [ ] Quay Receive ở Học bóng chuyền và keeper gần/xa hai phía; xác minh nguyên nhân dải màu ảnh 2 từ UI/camera/system bars.
- [ ] Lưu đo baseline visible button, icon, font và chiều cao nhân vật để so trước/sau cùng device.

**Điều kiện Done**

- [ ] Có ảnh/video hoặc đánh dấu device baseline pending rõ ràng, không giả đã xem runtime.
- [ ] Không quy dải màu cho fullscreen/safe area nếu chưa có evidence. Khi thiếu device vẫn bắt đầu code task từ A01, nhưng A02/Q05/Q06 chưa thể Done; Q04 có thể Done nếu tự đáp ứng build/smoke gate.

**Kiểm chứng**

Evidence và acceptance ở trên; không tạo unit test chỉ để kiểm thay asset/đường dẫn đơn giản.
