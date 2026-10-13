# UI theme

Chỉnh `Assets/_Project/Settings/UI/UITheme.asset` trong Inspector. `UiKitAssets` trong Resources giữ tham chiếu tới asset này để UI tạo bằng code và các prefab đều dùng được trong bản build.

- Hai họ style: **Shell** cho mọi màn ngoài minigame (Menu, Settings, Map, popup bài học, hộp thoại hành trình, màn chuyển cảnh, GameOver, Splash) và **Kit** cho minigame. Shell dựng bằng `ShellKit`, Kit bằng `UiKit`; không dùng chéo.
- Shell (`ShellStyle` trong asset): màu kem, navy (`ink`), vàng nút, đỏ tiêu đề, chữ phụ, disabled; viền card 6 / control 4; bo 20 / 12; bóng navy cứng (8,-8) / (5,-5); bóng vàng tiêu đề (3,-3); góc nghiêng tiêu đề và thanh tải. Khi nhấn, thân nút trượt xuống trùng bóng.
- Palette chung: Primary (= đỏ nhấn Shell), Accent, Surface, Text Primary, Scrim. Thang chữ chung 160/54/48/40/32/28, tối thiểu 28 (Splash 24).
- Corner Radius / Border Width / Shadow Offset gốc dùng cho Kit (nút 1.5 lần bán kính, nút pause 5/6, viền kit 3/4).
- Motion: thời gian phản hồi nút, fade, các bước hiện kết quả và chu kỳ ánh sáng badge. Animation dùng thời gian unscaled để hoạt động khi pause.
- `ShellStyleAudit` và `MinigameStyleAudit` kiểm tra mọi màn; ảnh nội dung (icon, chân dung, huy hiệu) gắn `StyleExempt`.

Các màn kế thừa `ScreenBase` tự lấy `UITheme.Shared` khi chưa gán Theme trong Inspector. UI Kit và các builder dùng asset chung; không tạo thêm bản sao theme cho từng scene. Giá trị được áp dụng khi dựng màn; kết quả áp dụng lại khi `Show`. Sau khi chỉnh asset, mở lại màn hoặc chạy lại Play Mode để kiểm tra toàn bộ giao diện. Không cần bake lại prefab kết quả.

Gameplay, điều hướng và vùng nhận input không thuộc theme.
