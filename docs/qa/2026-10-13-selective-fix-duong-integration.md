# Tích hợp chọn lọc fix/Duong vào master

Nguồn: `master` tại `0182e0d`, `origin/fix/Duong` tại `5b446c2`. Thay đổi được commit local trên master; chưa push.

## Phạm vi đã áp dụng

- Sprint Học: 45 giây, đạt chuỗi 12 nhịp ít nhất một lần; giữ chuỗi tốt nhất khi bấm sai; dừng đồng hồ/input khi giáo viên nói.
- Bóng chuyền Học: đỡ 3 lần, phát 2 lần, nhảy và đánh trả 2 lần; đếm tiếp xúc hợp lệ, giữ trình diễn 1,05 giây và khóa nút trong khoảng chuyển lượt.
- Nhảy ếch: dùng vùng an toàn 40–60% thanh timing. Giữ 40 giây, khoảng nhảy 0,25–1,5 m, đường 60 m, quét 0,7 giây, nhảy 0,6 giây, hồi phục 2,5 giây.
- Bóng đá: dùng tư thế với tay/đổ người trái phải và ăn mừng cứu bóng từ fix/Duong; luật xác định bàn thắng giữ nguyên.
- UI và luồng tiến trình lấy từ fix/Duong; tiêu đề lễ mừng giữ **QUA MÔN**. Bốn file router/shell progression khớp nguồn fix/Duong.
- Giữ các sửa đổi Bóng chuyền đã có trước tác vụ: Luyện 5 điểm không giới hạn giờ; Thi 10 điểm trong 90 giây và quyền phát bóng của bên ghi điểm.
- Giữ tài liệu thiết kế cờ vua riêng của master.

## Điều chỉnh khi tích hợp

- SafeAreaFitter chờ Canvas có kích thước và transform hợp lệ trước khi đổi anchors, tránh co UI về 0 khi mở scene.
- Popup cho phép chữ mục tiêu co xuống 32 để hiện đủ nội dung trên màn hình hẹp.
- Đăng ký màu nền nút Bóng chuyền và sprite chân Sprint trong theme/audit chung, giữ hình ảnh từ fix/Duong.
- Đồng bộ lời hoàn tất bài Học Bóng chuyền với asset; cập nhật test theo luật và cách chuyển bài mới.
- Dùng ShellSceneAuthoring của fix/Duong để lưu lại sprite references cho shell UI; căn biểu tượng trường học trên splash trong scene lưu sẵn.

## Bằng chứng

- Full EditMode: **1.056/1.056**, không lỗi, không bỏ qua — `Builds/TestResults/selective-integration-edit-verified.xml`.
- Full PlayMode: **399/399**, không lỗi, không bỏ qua — `Builds/TestResults/selective-integration-play-verified.xml`.
- Sau bước authoring shell, kiểm tra lại Bootstrap/Menu và Map responsive: **19/19** — `Builds/TestResults/selective-shell-reopen-final.xml`.
- Full suite chạy trên mã cuối trước bước lưu lại shell scene; bước cuối chỉ đổi serialized UI assets, được kiểm tra lại bằng nhóm shell và ảnh render.
- Đã đọc PNG Menu 1920×1080, popup Map 1440×1080, lễ mừng 1920×1080 và splash 1920×1080 trong `Builds/Screenshots/selective-*.png`.
- Chưa build APK hoặc kiểm tra Android/thiết bị thật. Ảnh tĩnh không chứng minh tương tác trên thiết bị.

Bản sao lưu trước tác vụ: `/tmp/kma-selective-integration-20261013/before`; diff chưa commit ban đầu: `/tmp/kma-selective-integration-20261013/uncommitted.patch`.
