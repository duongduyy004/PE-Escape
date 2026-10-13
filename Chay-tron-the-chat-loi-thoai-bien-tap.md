# CHẠY TRỐN THỂ CHẤT — LỜI THOẠI, LỜI DẪN VÀ HƯỚNG DẪN BIÊN TẬP

**Bản biên tập:** 11/10/2026.
**Tài liệu đi kèm:** `Chay-tron-the-chat-huong-dan-ghep-asset.md`.

Bản này thay thế nội dung chữ trong `tong-hop-kich-ban-va-loi-dan.md`, dựa trên mã nguồn đã đọc tại `projects/the-chat-KMA`. Các chỉ dẫn “giữ nguyên văn” trong tài liệu trích xuất cũ chỉ mô tả bản cũ, không áp dụng cho bản biên tập này. Chưa thay đổi code, scene hoặc asset của game.

## 1. Nhân vật và giọng nói thống nhất

| ID đang có trong code | Tên hiển thị mới | Vai trò và cách nói |
|---|---|---|
| `tan_thu` | SV năm 1 | Nhân vật chính. Rụt rè, sợ trượt, hay tự nhắc mình làm đúng. Xưng “em”, gọi giảng viên là “cô”; với Mai dùng “tớ — cậu”, với Tuấn dùng “em — anh”. Qua bài thì nhẹ nhõm, không khoe khoang. |
| `co_the_chat` | Giảng viên thể chất | Vẫn là giảng viên nữ theo asset hiện có. Xưng “tôi”, gọi sinh viên là “cậu”, gọi cả nhóm là “các em”. Khó gần, nhận xét ngắn, mỉa mai vừa đủ. Qua bài chỉ công nhận kết quả; không chuyển thành người cổ vũ ở đoạn kết. |
| `mai_toang` | Mai | Bạn cùng lớp năm 1. Nói thẳng, thực tế, đôi khi cũng lo. Nhắc bạn nhìn bóng, giữ nhịp, chú ý lực sút; không hô khẩu hiệu. Không mặc định là đồng đội điều khiển trong bóng chuyền. |
| `anh_khoa_tren` | Anh Tuấn | Sinh viên năm 2. Biết môn học, nói chuyện có trải nghiệm, trêu nhẹ nhưng không bịa chuyện quá đà. Xưng “anh — em”. |

Giữ nguyên ID kỹ thuật để không ảnh hưởng dữ liệu lưu. Đổi tên hiển thị ở thư viện thoại, nhãn tên trên bàn cờ và mọi UI liên quan. Nhân vật chính không cần tên riêng để câu chuyện hoạt động.

**Quy ước biên tập:**

- Hình phạt luôn gọi là **Bật cóc**; tài nguyên luôn gọi là **lượt thi**. “Lượt sút” chỉ dùng trong bóng đá.
- Ba nội dung vận động: chạy nước rút → bóng chuyền → bóng đá. Sau đó là bài kiểm tra cuối bằng cờ vua. Đạt từng phần chưa đồng nghĩa đã hoàn thành học phần.
- Giảng viên không dùng “nha”, lời khen phấn khích, tiếng lóng mạng hoặc emoji chèn trong câu. Sinh viên cũng không nói kiểu “flex”, “toang”, “đỉnh nóc”, “ét o ét”.
- Không thêm dấu ba chấm vào mọi câu của SV năm 1. Sự lo lắng thể hiện qua nội dung và biểu cảm, không qua nói lắp liên tục.
- Chữ trong bảng thoại dưới đây là câu hoàn chỉnh để đưa vào game. ID biên tập và mô tả tình huống không được hiển thị cho người chơi.
- Icon trỏ tay là thành phần hình riêng, không phải chữ trong câu. Cách ghép nằm trong tài liệu asset.

## 2. Hội thoại cốt truyện — thay thế 13 node hiện có

Nguồn: `Assets/Editor/StudentJourneyContentBuilder.cs`, hàm `BuildDialogues()`. Giữ nguyên node ID, số dòng và thứ tự người nói. Toàn bộ sticker cũ được bỏ, trừ các nhãn ngắn được chỉ định rõ dưới đây. Không giữ emoji cũ sau khi thay câu.

### `opening` — Buổi thể chất đầu tiên

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `opening.01` | Anh Tuấn | Buổi đầu nhớ nghe kỹ yêu cầu. Môn này không phải cứ có mặt là qua đâu. | — |
| `opening.02` | Mai | Tớ cứ tưởng đầu kỳ chỉ điểm danh. Cậu có tập chạy trước không? | — |
| `opening.03` | SV năm 1 | Chưa... Tớ chỉ mong không phải học lại ngay năm đầu. | — |
| `opening.04` | Giảng viên thể chất | Chạy, bóng chuyền, bóng đá, rồi bài kiểm tra cuối. Đạt phần trước mới học phần sau. Không học được thì nợ. | — |

### `sprint_intro` — Làm quen nhịp chạy

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `sprint_intro.01` | Giảng viên thể chất | Trái, phải luân phiên. Làm đúng 12 nhịp liên tiếp. Đừng để tôi phải nhắc lại chuyện trái với phải. | — |
| `sprint_intro.02` | Mai | Bấm nhầm là đếm lại từ đầu. Cứ chậm một chút cho chắc. | — |
| `sprint_intro.03` | SV năm 1 | Ừ. Để tớ thử từng nhịp đã. | — |

### `sprint_exam` — Trước bài thi chạy

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `sprint_exam.01` | Giảng viên thể chất | 150 mét, tối đa 22 giây. Tôi bấm giờ, không nghe giải thích. | 22 GIÂY |
| `sprint_exam.02` | SV năm 1 | Dạ. Mong là em giữ được nhịp như lúc tập. | — |

### `sprint_pass` — Đạt phần chạy

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `sprint_pass.01` | Giảng viên thể chất | Tạm được. Đạt phần chạy. Đừng tưởng thế là xong học phần. | ĐẠT |
| `sprint_pass.02` | Mai | Qua phần chạy rồi. Thở lại đã, còn bóng chuyền nữa. | — |

### `volleyball_intro` — Làm quen bóng chuyền

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `volleyball_intro.01` | Mai | Nhìn chỗ bóng rơi trước. Đứng lệch là đỡ hụt ngay. | — |
| `volleyball_intro.02` | Giảng viên thể chất | Bắt đầu bằng ba lần đỡ bóng. Mỗi bên chạm tối đa ba lần, không phải thích chạm bao nhiêu cũng được. | — |
| `volleyball_intro.03` | SV năm 1 | Dạ. Cô cho em vào vị trí đã ạ. | — |

### `volleyball_exam` — Trước bài thi bóng chuyền

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `volleyball_exam.01` | Giảng viên thể chất | Bên nào đủ 10 điểm trước thì thắng. Bóng chạm sân rồi mới tiếc thì muộn. | 10 ĐIỂM |
| `volleyball_exam.02` | Mai | Đừng nhìn bảng điểm suốt. Cứ theo bóng, xong lượt rồi tính. | — |
| `volleyball_exam.03` | SV năm 1 | Ừ. Tớ chỉ sợ cuống rồi bấm sớm. | — |

### `volleyball_pass` — Đạt phần bóng chuyền

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `volleyball_pass.01` | Giảng viên thể chất | Đạt. Ít ra cậu đã biết đón bóng, không đứng nhìn nữa. | ĐẠT |
| `volleyball_pass.02` | SV năm 1 | Dạ... Qua được hai phần rồi. | — |

### `soccer_intro` — Làm quen sút bóng

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `soccer_intro.01` | Mai | Đến bóng đá rồi. Tớ thấy lúc lấy lực dễ thả tay muộn lắm. | — |
| `soccer_intro.02` | Anh Tuấn | Anh cũng từng sút vọt xà vì giữ nút lâu quá. Đừng chỉ chăm chăm sút mạnh. | — |
| `soccer_intro.03` | Giảng viên thể chất | Tập góc sút và lực sút trước. Bài thi có đúng năm lượt. Sút ra ngoài cũng tính một lượt. | — |

### `soccer_exam` — Trước bài thi bóng đá

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `soccer_exam.01` | Giảng viên thể chất | Năm lượt sút, ít nhất ba bàn. Thủ môn không có nhiệm vụ giúp cậu qua môn. | 5 LƯỢT |
| `soccer_exam.02` | SV năm 1 | Dạ. Em sẽ nhìn lại góc trước khi sút. | — |

### `soccer_pass` — Đạt ba phần vận động

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `soccer_pass.01` | Giảng viên thể chất | Đạt phần bóng đá. Còn bài kiểm tra cuối với tôi. Chưa đến lúc cất đồ. | ĐẠT |
| `soccer_pass.02` | Mai | Còn một bài nữa. Ngồi xuống cho đỡ run chân đã. | — |

### `chess_intro` — Bài kiểm tra cuối

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `chess_intro.01` | Giảng viên thể chất | Bài cuối là một thế cờ. Chiếu hết trong hai nước. Lần này dùng đầu, không dùng chân. | BÀI CUỐI |
| `chess_intro.02` | SV năm 1 | Dạ, cờ vua ạ? Em chưa chơi nhiều... | — |
| `chess_intro.03` | Giảng viên thể chất | Cậu có 90 giây suy nghĩ và hai lần sửa sai. Lần thứ ba sai là trượt. Đọc đề cho kỹ. | 90 GIÂY |

### `supplementary` — Nội dung dự phòng, chưa kích hoạt

Node vẫn có trong thư viện, nhưng không thấy luồng thi bổ sung đang hoạt động trong code đã đọc; `ChallengeContracts.cs` ghi rõ nhánh luyện bổ sung cũ đã bị bỏ. Chỉ thay chữ của node tồn tại, không tự tạo cơ chế cấp lượt hoặc gắn node này vào Game Over.

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `supplementary.01` | Giảng viên thể chất | Phần đã đạt tôi giữ nguyên. Phần chưa đạt thì ôn lại. Lần sau tôi vẫn chấm theo yêu cầu cũ. | — |
| `supplementary.02` | Mai | Cậu hay vướng chỗ nào thì tập lại chỗ đó. Làm y như lần trước lại trượt thôi. | — |

### `course_complete` — Hoàn thành học phần

| ID biên tập | Nhân vật | Lời thoại mới | Sticker |
|---|---|---|---|
| `course_complete.01` | Giảng viên thể chất | Đạt học phần. Cậu không phải học lại với khóa sau. Nhớ giữ kết quả này. | HOÀN THÀNH |
| `course_complete.02` | SV năm 1 | Dạ, em cảm ơn cô. Cuối cùng cũng qua rồi. | — |
| `course_complete.03` | Anh Tuấn | Qua rồi thì về nghỉ. Lần sau có môn thể chất, nhớ tập sớm hơn. | — |
| `course_complete.04` | Mai | Đi thôi. Tớ đói từ lúc thi chạy đến giờ. | — |

## 3. Mục tiêu và hướng dẫn cho đủ 10 level

Đây là chữ hướng dẫn của hệ thống, dùng giọng trung tính, không giả làm lời giảng viên. Giữ nguyên thông số và luật chơi; số lấy từ cấu hình, không ghi cứng nếu hàm hiện tại đang nội suy.

### 3.1. Mục tiêu trên thẻ bài

Nguồn: `StudentJourneyContentBuilder.BuildChallenges()` và các challenge asset sinh ra.

| Challenge ID | Mục tiêu mới |
|---|---|
| `sprint_learn` | Bấm TRÁI, PHẢI luân phiên đúng 12 nhịp liên tiếp. Không giới hạn thời gian. |
| `sprint_practice` | Chạy 150 m trong tối đa 30 giây. Bài luyện không giới hạn thể lực. |
| `sprint_exam` | Chạy 150 m trong tối đa 22 giây. |
| `volleyball_learn` | Đỡ bóng thành công 3 lần trong bài làm quen. |
| `volleyball_practice` | Ghi đủ 5 điểm trước đối thủ trong tối đa 120 giây. |
| `volleyball_exam` | Ghi đủ 10 điểm trước đối thủ. |
| `soccer_learn` | Ghi 3 bàn vào khung thành không có thủ môn. Không giới hạn lượt sút. |
| `soccer_practice` | Ghi ít nhất 2 bàn trong tối đa 6 lượt sút. Có thủ môn. |
| `soccer_exam` | Sút đủ 5 lượt và ghi ít nhất 3 bàn. Có thủ môn. |
| `chess_final` | Chiếu hết trong 2 nước. Có 90 giây suy nghĩ và 2 lần sửa sai. |

Bóng chuyền làm quen đếm các lần đỡ theo `VolleyballChallengeRules`; không thêm điều kiện “ba lần liên tiếp” hoặc “đủ ba chạm trong một pha bóng”. `Normal` là độ khó nội bộ của thủ môn, không cần đưa vào câu hướng dẫn.

### 3.2. Trang MỤC TIÊU của từng level

| Challenge ID | Nội dung trang MỤC TIÊU |
|---|---|
| `sprint_learn` | Bấm TRÁI, PHẢI luân phiên đúng 12 nhịp liên tiếp. Bài này không tính giờ. |
| `sprint_practice` | Về đích 150 m trong tối đa 30 giây. |
| `sprint_exam` | Về đích 150 m trong tối đa 22 giây. Có 3 sinh viên chạy cùng. |
| `volleyball_learn` | Đỡ bóng thành công 3 lần. Đối thủ giao bóng trong bài làm quen. |
| `volleyball_practice` | Ghi đủ 5 điểm trước đối thủ trong tối đa 120 giây. Đối thủ luôn giao bóng. |
| `volleyball_exam` | Ghi đủ 10 điểm trước đối thủ. Hai bên luân phiên giao bóng. |
| `soccer_learn` | Ghi 3 bàn. Không có thủ môn và không giới hạn lượt sút. |
| `soccer_practice` | Ghi ít nhất 2 bàn trong tối đa 6 lượt sút. Có thủ môn. |
| `soccer_exam` | Ghi ít nhất 3 bàn sau 5 lượt sút. Có thủ môn. |
| `chess_final` | Chiếu hết trong 2 nước. Có 90 giây suy nghĩ. Trượt bài này không bị trừ lượt thi và không phải Bật cóc. |

Với chạy, câu “Có {rivalCount} sinh viên chạy cùng.” chỉ xuất hiện khi `rivalCount > 0`, kể cả nếu cấu hình bài luyện thay đổi. Bóng chuyền thi không có giới hạn giờ trong cấu hình hiện tại; không thêm mốc 120 giây vào bài thi.

### 3.3. Các trang dùng chung trong môn chạy

Nguồn: `Assets/_Project/Scripts/Gameplay/Sprint/SprintGuide.cs`.

| Trang / trường | Nội dung mới |
|---|---|
| ĐIỀU KHIỂN / `Controls` | Bấm TRÁI rồi PHẢI, luân phiên để chạy. Giữ nhịp đúng để tăng chuỗi và bứt tốc. Ngừng bấm thì tốc độ giảm. |
| LUẬT làm quen / `LearnRules` | Bấm nhầm bên sẽ đưa chuỗi về 0. Bạn cần thực hiện lại đủ 12 nhịp liên tiếp. |
| LUẬT luyện và thi / `RaceRules` | Bấm nhầm bên sẽ mất chuỗi và tăng tốc rất ít. Hết giờ mà chưa về đích là chưa đạt. Điểm tính theo độ chính xác, thứ hạng và thời gian còn lại. |

### 3.4. Các trang dùng chung trong bóng chuyền

Nguồn: `Assets/_Project/Scripts/Gameplay/Volleyball/VolleyballGuide.cs`.

| Trang / trường | Nội dung mới |
|---|---|
| DI CHUYỂN / `Move` | Kéo joystick để di chuyển đến vị trí bóng rơi. |
| ĐÁNH VÀ NHẢY / `HitAndJump` | Nút ĐÁNH tự chọn động tác giao, đỡ, chuyền, đập hoặc chắn theo tình huống. Khi nút NHẢY sáng, bấm NHẢY rồi kéo joystick để chọn hướng đập. |
| LUẬT / `Rules` | Thời điểm bấm được đánh giá là HOÀN HẢO, TỐT, SỚM hoặc MUỘN. Mỗi bên chạm bóng tối đa 3 lần. Để bóng rơi trong sân mình hoặc đánh không qua lưới sẽ mất điểm. |

### 3.5. Các trang dùng chung trong bóng đá

Nguồn: `Assets/_Project/Scripts/Gameplay/Football/FootballGuide.cs`.

| Trang / trường | Nội dung mới |
|---|---|
| NGẮM / `Aim` | Kéo thanh hướng sang TRÁI hoặc PHẢI để chọn góc sút. Đường bay dự kiến xuất hiện khi bạn lấy lực. |
| LỰC SÚT / `Power` | Giữ nút SÚT để lấy lực; lực sẽ tăng rồi giảm liên tục. Thả nút để sút. Lực quá mạnh dễ làm bóng vọt xà; quá yếu có thể khiến bóng dừng trước khung thành. |
| LUẬT / `Rules` | Mỗi cú sút tính một lượt, kể cả khi bóng trúng cột, trúng xà, ra ngoài hoặc bị thủ môn cản phá. Bài làm quen không giới hạn lượt sút. |

### 3.6. Hướng dẫn cờ vua

Nguồn: `Assets/_Project/Scripts/Gameplay/Chess/ChessGuide.cs`.

| Trang | Nội dung mới |
|---|---|
| MỤC TIÊU | Dùng câu cho `chess_final` tại mục 3.2. |
| ĐIỀU KHIỂN | Chạm quân của bạn, sau đó chạm ô muốn đi. Nếu tốt được phong cấp, chọn quân thay thế. Nút GỢI Ý có 3 mức: ý tưởng, quân cần xem và nước đi cụ thể. |
| LUẬT | Nước không hợp lệ không tính lỗi. Nước hợp lệ nhưng sai lời giải tính 1 lỗi, bàn cờ giữ nguyên. Được sửa sai 2 lần; sai lần thứ 3 hoặc hết giờ là chưa đạt. Đồng hồ chỉ chạy trong lượt của bạn. Đi sai, dùng gợi ý và dùng nhiều thời gian sẽ làm giảm điểm. |

**Thẻ trước khi bắt đầu:**

```text
Thời gian suy nghĩ: 90 giây.
Được sửa sai 2 lần; sai lần thứ 3 là chưa đạt.
```

Nút: **Bắt đầu**. Không đổi số lần sửa thành “chỉ được sai một lần” khi sửa lời thoại.

### 3.7. Trang NẾU CHƯA ĐẠT — dùng cho bài luyện và bài thi vận động

Nguồn: `Assets/_Project/Scripts/UI/MinigameGuidePages.cs`.

```text
Lần đầu trượt bài đang chơi: hoàn thành Bật cóc để giữ lượt thi; không hoàn thành sẽ mất 1 lượt thi. Từ lần trượt thứ 2 của cùng bài: mất 1 lượt thi ngay và vẫn phải Bật cóc. Bật cóc không trừ thêm lượt đã bị trừ.
```

Đây là cùng một trang cho `sprint_practice`, `sprint_exam`, `volleyball_practice`, `volleyball_exam`, `soccer_practice`, `soccer_exam`. Không thêm trang này vào bài làm quen hoặc cờ vua. Lần trượt tính theo challenge ID trong tiến trình, không phải tổng số lần trượt toàn game. Qua bài thì bộ đếm trượt của bài đó được xóa theo code hiện tại.

### 3.8. Hướng dẫn Bật cóc

Nguồn: `Assets/_Project/Scripts/Gameplay/FrogJump/FrogJumpGuide.cs`.

| Trang / điều kiện | Nội dung mới |
|---|---|
| MỤC TIÊU, `savesLife = true` | Bật cóc hết 40 m trong tối đa 60 giây. Đây là hình phạt sau khi trượt bài. Về đích để giữ lượt thi; hết giờ sẽ mất 1 lượt thi. |
| MỤC TIÊU, `savesLife = false` | Bật cóc hết 40 m trong tối đa 60 giây. Bạn đã bị trừ 1 lượt thi khi trượt bài. Hình phạt này không trừ thêm lượt thi. |
| ĐIỀU KHIỂN | Kim di chuyển qua lại trên thanh lực. Chạm màn hình khi kim nằm trong vùng xanh. Chạm càng gần giữa, bạn bật càng xa. |
| LUẬT | Chạm khi kim ở vùng đỏ sẽ bị ngã: không tiến thêm và mất 2,5 giây hồi phục. Đồng hồ vẫn chạy trong thời gian này. |

Giữ nội suy `trackMetres`, `timeLimitSeconds`, `recoverSeconds` từ cấu hình. “Đứng dậy và tiếp tục ngay” là lời thúc giục, không cho phép bỏ qua 2,5 giây hồi phục.

## 4. Lời giảng viên và chữ trên bàn cờ — thay thế hiện có

Nguồn: `ChessFinalController.cs`, `ChessFinalHud.cs`, `ChessFinalSceneConfigurator.cs`.

| ID biên tập | Tình huống | Nội dung mới | Kênh |
|---|---|---|---|
| `chess.start` | Giới thiệu trên bàn cờ | Thế cờ này có giới hạn số nước và thời gian riêng. Tôi không chấm điểm cho việc ngồi nhìn. | Giảng viên, bong bóng |
| `chess.mistake1` | Không còn phát: nước hợp lệ nhưng kém tối ưu là một nước thật, không bị coi là lỗi | Sai rồi. Còn một lần sửa, nghĩ kỹ trước khi đi. | Không còn phát |
| `chess.mistake2` | Không còn phát (xem dòng trên) | Hai lần sai rồi. Sai thêm một lần là dừng bài. | Không còn phát |
| `chess.illegal` | Nước không hợp lệ | Nước đi không hợp lệ. Chọn ô khác. | Thông báo hệ thống |
| `chess.data_error` | Không còn dùng: lỗi phân tích hiện là thẻ “Chưa phân tích được nước đi này” với nút Thử lại và Chơi lại đề | Không thể chấm nước đi này do lỗi dữ liệu. Bàn cờ giữ nguyên. | Thẻ trên màn hình |
| `chess.solved` | Giải đúng | Tạm được. Cậu đạt bài cuối. | Giảng viên, bong bóng |
| `chess.timeout` | Hết giờ | Hết giờ. Ngồi lâu không làm nước đi đúng hơn. | Giảng viên, bong bóng |
| `chess.failed` | Hết số nước, bị chiếu hết hoặc hòa (hết giờ dùng `chess.timeout`) | Chưa chiếu hết được. Làm lại bài. | Giảng viên, bong bóng |

`chess.start` đang xuất hiện từ lúc mở bàn cờ, trước khi bấm Bắt đầu; không coi nó là một event mới để phát thêm lần nữa. Lỗi dữ liệu không phải lỗi của sinh viên: không chèn câu mỉa mai hoặc mô tả sai thành “chưa học bài”.

### Gợi ý — lời giảng viên, giữ đúng lời giải

Nguồn: `ChessHintBuilder.cs`: gợi ý tính từ bàn cờ hiện tại, mỗi bàn cờ mới đặt lại mức gợi ý.

| ID biên tập | Mức | Nội dung mới |
|---|---|---|
| `chess.hint1` | Ý tưởng | Chiếu để ép vua đen rời góc. Tượng đang hỗ trợ hậu; nhìn cả hai quân. |
| `chess.hint2` | Quân cần xem | Xem lại quân {piece} ở {from}. |
| `chess.hint3` | Nước đi | Đi {piece} từ {from} đến {to}. Tôi đã chỉ rõ rồi. |

`{piece}`, `{from}`, `{to}` là ký hiệu biên tập; khi triển khai lấy từ nước đúng của node hiện tại trong `PuzzleHints.For()`, không dùng nguyên văn placeholder.

Nước nêu trong gợi ý 2 và 3 luôn là một nước tối ưu của bàn cờ đang chơi (đề sinh ngẫu nhiên, không còn đề cố định). Khi không còn đường chiếu hết bắt buộc, gợi ý nói đúng điều đó và nêu một nước chờ an toàn, không bịa đường chiếu hết.

### HUD và chi tiết kết quả cờ vua

| Trường / trạng thái | Chữ hiển thị |
|---|---|
| Tên bên sinh viên | SV năm 1 |
| Tên bên giảng viên | Giảng viên thể chất |
| Mục tiêu | Chiếu hết trong {moves} nước |
| Đếm lỗi | Sai: {made}/{max} |
| Chưa bắt đầu | Đọc đề, sau đó bấm Bắt đầu |
| Lượt người chơi / đang kiểm tra nước đi | Lượt của bạn |
| Lượt đối thủ | Lượt giảng viên |
| Hoàn thành | Hoàn thành |
| Tạm dừng | Tạm dừng |
| Hết giờ | Hết giờ |
| Hết số nước, bị chiếu hết hoặc hòa | Hết số nước / Bị chiếu hết / Hòa cờ |
| Kết quả đạt | CHIẾU HẾT |
| Kết quả trượt | CHƯA ĐẠT |
| Chi tiết khi đạt | Thời gian {clock} · Sai: {made}/{max} |
| Có dùng gợi ý, nối vào chi tiết khi đạt | · Có dùng gợi ý |
| Chi tiết trượt do lỗi | Sai quá {max} lần |

Giữ các nút Gợi ý, Bắt đầu và lựa chọn phong cấp Hậu, Xe, Tượng, Mã. Giữ cách định dạng đồng hồ đang có.

## 5. Bảng kết quả, hình phạt và Game Over

### 5.1. Trước và sau Bật cóc

Nguồn: `Assets/_Project/Scripts/UI/ResultPanel.cs`.

| Điều kiện / trường | Chữ mới |
|---|---|
| Trượt lần đầu, chuẩn bị Bật cóc | Bật cóc về đích trong 60 giây để giữ lượt thi. |
| Trượt từ lần 2, chuẩn bị Bật cóc | Đã trừ 1 lượt thi. Hoàn thành lượt Bật cóc để kết thúc hình phạt. |
| Nút vào hình phạt | BẬT CÓC |
| Về đích, tiêu đề | VỀ ĐÍCH |
| Về đích và `SavesLife = true` | Giữ được lượt thi. |
| Hết giờ, tiêu đề | HẾT GIỜ |
| Hết giờ và `SavesLife = true` | Đã trừ 1 lượt thi. |
| `SavesLife = false`, dù về đích hay hết giờ | Lượt thi đã bị trừ khi trượt bài; không trừ thêm. |
| Còn lượt, quay lại bài luyện | LUYỆN LẠI |
| Còn lượt, quay lại bài thi | THI LẠI |
| Hết lượt, hành động đang có | VỀ BẢN ĐỒ |
| Bộ đếm | LƯỢT THI: {remaining}/{max} |

Không đổi nút VỀ BẢN ĐỒ thành THI LẠI khi `remaining = 0`. Câu trước hình phạt không được hứa “chắc chắn được thi lại” nếu lần trượt vừa làm hết lượt.

**Chữ nổi trong Bật cóc** (`FrogJumpController.OnLanded`):

| Tình huống | Chữ mới |
|---|---|
| Ngã | NGÃ |
| Cú bật đạt ít nhất 90% quãng bật tối đa | CHÍNH XÁC · {metres} m |
| Cú bật bình thường | {metres} m |

Đây là phản hồi hệ thống. Câu “Ui da!” của sinh viên nằm trong bong bóng riêng, không thay thế thông báo NGÃ.

### 5.2. Chữ chung trên bảng kết quả

| Trường | Chữ mới / giữ dùng |
|---|---|
| Hoàn thành lượt ôn / chơi ngoài tiến trình | LUYỆN TẬP HOÀN THÀNH |
| Số mục tiêu | {count} MỤC TIÊU |
| Xếp hạng | XẾP HẠNG {rank} |
| Đã lưu | Kết quả đã lưu. |
| Chờ lưu | Kết quả đang chờ lưu. |
| Thời gian | Thời gian: {elapsed} giây |
| Nút tiếp | TIẾP TỤC |
| Nút thử lại thao tác | THỬ LẠI |
| Nút lưu lại | LƯU LẠI |
| Nút chơi lại bài cuối | CHƠI LẠI |

Giữ nguyên điều kiện hiện/ẩn nút và nội dung lỗi kỹ thuật do hệ thống trả về. Không chèn lời giảng viên vào lỗi lưu dữ liệu.

### 5.3. Game Over

Nguồn: `Assets/_Project/Scripts/UI/GameOverPresentationBuilder.cs`.

| Trường | Chữ mới |
|---|---|
| Tiêu đề trên | HẾT LƯỢT THI |
| Tiêu đề chính | CHƯA QUA HỌC PHẦN |
| Phụ đề | Bạn đã dùng hết {max} lượt thi. Chọn một thao tác bên dưới để tiếp tục. |
| Nhãn thống kê 1 | MÔN ĐÃ QUA |
| Nhãn thống kê 2 | LẦN CHƯA ĐẠT |
| Nhãn thống kê 3 | TỔNG ĐIỂM |
| Các nút, giữ hành động hiện có | CHƠI LẠI / CHƠI MỚI / MENU CHÍNH |

`{max}` lấy từ `GameSession.MaxLives`, hiện là 5. Không viết phụ đề hứa khôi phục lượt hoặc giữ tiến trình nếu hành động của nút không bảo đảm điều đó.

## 6. Scene Celebration — hoàn thành nhưng giảng viên vẫn nghiêm

Giữ scene ID `Celebration` và thời điểm xuất hiện hiện tại. Đổi cách gọi trong tài liệu/UI thành **Tổng kết học phần**, không cần xây thêm lễ vinh danh.

| ID / trường | Nội dung mới |
|---|---|
| `celebration.teacher`, bong bóng tại giây 5 | Tốt, cậu này được. Qua rồi thì về, đừng đứng chắn sân. |
| Tiêu đề bảng tại giây 8 | HOÀN THÀNH HỌC PHẦN THỂ CHẤT |
| Tên các phần | Chạy nước rút / Bóng chuyền / Bóng đá / Bài kiểm tra cuối |
| Trạng thái | Đạt / Chưa đạt |
| Gợi ý | Có dùng gợi ý / Không dùng gợi ý |
| Dữ liệu xem trước | Dữ liệu mẫu |
| Dữ liệu cũ có thi bổ sung | Thi bổ sung: {rounds} đợt |
| Nút | Bỏ qua / Về menu / Chơi lại |

Giữ thông tin điểm, hạng, thời gian cờ vua, số lỗi và định dạng số đang dùng. Nhãn “Thi bổ sung” chỉ đọc dữ liệu cũ, không chứng minh đang có nhánh thi bổ sung hoạt động. Nút Chơi lại của scene này đang gọi `GoToMap`; không sửa lời dẫn thành “bắt đầu lại từ đầu”.

## 7. Câu ngắn bổ sung trong gameplay

**Đây là đề xuất mới, cần nối sự kiện và thêm UI bong bóng ở nơi chưa có.** Không đưa các dòng này vào node trên bản đồ. Mỗi dòng gồm tối đa hai lượt nói: SV năm 1 trước, giảng viên sau; dấu “—” nghĩa là không phát lời của nhân vật đó. ID tình huống dùng chung với tài liệu asset.

### 7.1. Chạy nước rút — đủ ba level

| ID tình huống | Điều kiện phát | SV năm 1 | Giảng viên thể chất |
|---|---|---|---|
| `sp.learn.start` | Sau hướng dẫn, trước khi bắt đầu bấm | Trái trước... rồi phải. | Làm đúng nhịp đã. Chưa ai bắt cậu chạy nhanh. |
| `sp.learn.wrong` | Bấm nhầm, chuỗi về 0 | Em nhầm bên rồi. | Đếm lại từ đầu. Trái với phải không đổi chỗ đâu. |
| `sp.learn.done` | Đủ 12 nhịp | Dạ, đủ rồi ạ. | Được. Sang bài luyện. |
| `sp.practice.start` | Trước lượt luyện | Lần này phải kịp 30 giây... | Có thời gian thì tập cho tử tế. |
| `sp.practice.break` | Mất chuỗi khi đang chạy | Hỏng nhịp rồi... | Chân còn chạy được thì giữ nhịp. |
| `sp.practice.done` | Đạt bài luyện | May quá, em kịp rồi. | Bài luyện đạt. Bài thi không cho cậu từng ấy thời gian. |
| `sp.exam.start` | Trước đếm ngược thi | Đừng bấm nhầm lúc đầu... | Vào vị trí. Tôi bắt đầu bấm giờ. |
| `sp.exam.lowtime` | Còn 5 giây, chưa kết thúc | Sắp hết giờ rồi... | Đồng hồ không chờ cậu đâu. |
| `sp.exam.stumble` | Phản ứng vấp khi bấm sai được trình diễn | Ui da! | Giữ nhịp và chạy tiếp. |

`sp.exam.stumble` là phản ứng hình ảnh cho lỗi bấm, không tạo thêm cơ chế ngã, hồi phục hoặc trừ thời gian trong môn chạy. Ưu tiên câu ngắn khi nhân vật còn đang chạy.

### 7.2. Bóng chuyền — đủ ba level

| ID tình huống | Điều kiện phát | SV năm 1 | Giảng viên thể chất |
|---|---|---|---|
| `vb.learn.start` | Trước quả giao đầu | Bóng rơi bên nào nhỉ... | Nhìn bóng. Không phải nhìn tôi. |
| `vb.learn.miss` | Bóng rơi sân mình, mất điểm | Em đến chậm rồi. | Đứng nhìn thì bóng vẫn rơi. Di chuyển sớm hơn. |
| `vb.learn.done` | Đủ ba lần đỡ | Dạ, em đỡ được rồi. | Tạm được. Đến bài luyện. |
| `vb.practice.start` | Trước bài luyện | Năm điểm... cứ từng quả thôi. | Hai phút. Đừng dùng hết thời gian để lúng túng. |
| `vb.practice.badtime` | Động tác nhận đánh giá SỚM hoặc MUỘN | Em lại bấm lệch nhịp rồi. | Đợi đúng tầm bóng, đừng đánh theo cảm tính. |
| `vb.practice.done` | Đạt bài luyện | Đủ năm điểm rồi... | Bài luyện đạt. Vào thi thì làm như thế. |
| `vb.exam.start` | Trước bài thi | Tớ vẫn thấy run tay. | Vào vị trí. Đủ 10 điểm mới tính là đạt. |
| `vb.exam.lostpoint` | Mất điểm, giữa hai pha bóng | Lại mất một điểm... | Còn đứng tiếc thì quả sau cũng mất. |
| `vb.exam.dive` | Có động tác lao cứu bóng và tiếp đất | Ui da... bóng còn trong sân chứ? | Đứng dậy, theo tiếp đường bóng. |
| `vb.exam.matchpoint` | Người chơi có 9 điểm, trận chưa kết thúc | Còn một điểm nữa... | Chưa đủ điểm thì chưa được thở phào. |

Không phát câu ngã cho mọi lần mất điểm. `vb.exam.dive` chỉ dùng khi có động tác lao cứu bóng thực tế; không buộc game phải thêm ngã. Mai có thể đứng ngoài sân, nhưng không phát câu nhận mình đang đỡ bóng thay người chơi.

### 7.3. Bóng đá — đủ ba level

| ID tình huống | Điều kiện phát | SV năm 1 | Giảng viên thể chất |
|---|---|---|---|
| `fb.learn.start` | Trước cú sút đầu | Không có thủ môn... chắc em tập được. | Khung thành trống. Đừng sút ra ngoài là được. |
| `fb.learn.miss` | Sút ra ngoài hoặc trúng khung không thành bàn | Trống thế mà vẫn trượt... | Khung thành ở phía trước, không ở trên trời. |
| `fb.learn.done` | Đủ ba bàn | Dạ, em ghi đủ rồi. | Được. Bài sau có thủ môn. |
| `fb.practice.start` | Trước bài luyện | Có thủ môn rồi... em phải chọn góc kỹ hơn. | Sáu lượt, ít nhất hai bàn. Tính từng cú sút. |
| `fb.practice.saved` | Thủ môn cản phá thành công | Bị bắt mất rồi. | Sút thẳng vào người thì cậu mong điều gì? |
| `fb.practice.done` | Đạt bài luyện | Dạ, đủ hai bàn rồi ạ. | Đạt bài luyện. Bài thi cần ba bàn. |
| `fb.exam.start` | Trước bài thi | Năm lượt thôi... đừng giữ lực quá lâu. | Chuẩn bị sút. Tôi đếm cả những cú ra ngoài. |
| `fb.exam.goal` | Một bàn được ghi, chưa kết thúc bài | Vào rồi... | Một bàn. Còn các lượt sau. |
| `fb.exam.lastkick` | Chuẩn bị lượt thứ 5 và bài còn diễn ra | Cú cuối rồi... | Lượt cuối. Nhìn góc rồi lấy lực. |
| `fb.exam.impossible` | Bài kết thúc với số bàn chưa đạt | Em vẫn chưa đủ ba bàn... | Chưa đạt. Thủ môn không nhận lời xin thêm lượt. |

`fb.practice.saved` chỉ dùng khi quả đó thực sự bị cản phá; không dùng cho bóng ra ngoài. Câu bàn thắng không tuyên bố qua bài sớm: kết quả bài thi theo đủ năm lượt và điều kiện hiện có.

### 7.4. Cờ vua — câu bổ sung ngoài các câu thay thế ở mục 4

| ID tình huống | Điều kiện phát | SV năm 1 | Giảng viên thể chất |
|---|---|---|---|
| `ch.extra.begin` | Người chơi bấm Bắt đầu | Phải nhìn cả hai quân phối hợp... | — |
| `ch.extra.firstcorrect` | Nước đầu đúng, sau khi giảng viên đi xong | Nước đầu hợp lý rồi... còn phải ép cho xong. | Mới một nước. Đừng mừng sớm. |
| `ch.extra.lowtime` | Còn 15 giây suy nghĩ, lượt người chơi | Còn ít thời gian quá... | Cậu định để đồng hồ giải hộ à? |
| `ch.extra.relief` | Giải đúng, sau `chess.solved` nếu còn chỗ hiển thị | Dạ... em qua thật rồi. | — |

Phát `ch.extra.begin` khi trên bàn có ít nhất hai quân tấn công của người chơi ngoài vua và tốt; không phụ thuộc mã đề. Gợi ý ở mục 4 là nguồn duy nhất tự động đọc lời giải, không phát thêm gợi ý ngẫu nhiên khi đang suy nghĩ.

### 7.5. Bật cóc — mọi lần đi vào hình phạt

| ID tình huống | Điều kiện phát | SV năm 1 | Giảng viên thể chất |
|---|---|---|---|
| `frog.start.save` | Vào hình phạt, có thể giữ lượt thi | Em phải về đích mới giữ được lượt... | 40 mét trong 60 giây. Bật hết rồi nói. |
| `frog.start.charged` | Vào hình phạt, lượt đã bị trừ | Đã mất một lượt rồi... | Mất lượt không có nghĩa là được bỏ hình phạt. |
| `frog.fall.first` | Ngã lần đầu trong lượt Bật cóc | Ui da! | Đứng dậy và tiếp tục ngay. |
| `frog.fall.repeat` | Ngã thêm lần nữa, đủ khoảng cách giữa các câu | Em lại trượt chân rồi... | Tôi bảo bật cóc, không bảo ngồi nghỉ. |
| `frog.goodjump` | Lần đầu bật chính xác, ≥90% quãng tối đa | Lần này được rồi. | Giữ cách đó. Đừng bấm vội. |
| `frog.lowtime` | Còn 10 giây, chưa kết thúc | Sắp hết giờ rồi... | Vạch đích chưa tới. Tiếp tục. |
| `frog.finish` | Về đích | Cuối cùng cũng tới... | Xong hình phạt. Xem còn bao nhiêu lượt rồi làm tiếp. |
| `frog.timeout` | Hết giờ | Em chưa tới kịp... | Hết giờ. Kết quả thế nào thì nhận thế ấy. |

Icon trỏ tay đi kèm lời giảng viên ở `frog.fall.first`, `frog.fall.repeat` và `frog.lowtime`; hướng dẫn cụ thể nằm trong file asset. Không dùng câu “giữ được lượt” trong `frog.finish` nếu lượt đã bị trừ trước đó.

### 7.6. Câu kết quả dùng chung cho vận động

Chỉ phát sau khi có kết quả cuối; không phát cùng lúc với câu hoàn thành riêng của level hoặc node đạt môn trên bản đồ.

| ID tình huống | Điều kiện | SV năm 1 | Giảng viên thể chất |
|---|---|---|---|
| `result.fail.first` | Trượt lần đầu của bài, có Bật cóc giữ lượt | Dạ... em làm chưa được. | Chưa đạt. Sang Bật cóc, đừng đứng chờ tôi đổi điểm. |
| `result.fail.repeat` | Trượt từ lần 2 của bài | Em lại sai chỗ đó... | Vẫn lỗi cũ. Trừ một lượt thi, rồi Bật cóc. |
| `result.pass.practice` | Đạt bài luyện nếu không dùng câu riêng | Dạ, em làm được rồi. | Tạm được. Đến bài thi. |
| `result.pass.exam` | Đạt bài thi nếu không dùng câu riêng | May quá... | Đạt. Tôi ghi kết quả. |
| `gameover.comment` | Màn hình hết lượt, phát một lần | Năm đầu mà đã có môn chưa qua... | Hết lượt rồi. Chịu khó học lại với khóa sau. |

Câu học lại là lời mỉa mai trong bối cảnh nhân vật, không thay thế luật tiếp tục/chơi lại của game. Không dùng khi chỉ mất một điểm hoặc trượt một lần mà vẫn còn lượt.

## 8. Quy tắc để lời thoại không gây vướng khi chơi

- Câu mở đầu level phát sau khi hướng dẫn đóng, trước khi đồng hồ hoặc lượt chơi bắt đầu. Không tiêu tốn thời gian chơi để người chơi đọc hai câu dài.
- Trong gameplay, phát theo sự kiện, không phát ngẫu nhiên mỗi vài giây. Một tình huống tối đa hai câu; có thể chỉ phát một câu nếu trò chơi đang nhanh.
- Câu ngã, bấm sai và mất điểm dùng khoảng nghỉ tối thiểu 10 giây giữa các lần nhắc cùng loại. Mỗi loại phản ứng lặp phát tối đa hai lần trong một lượt chơi.
- Câu áp lực thời gian phát một lần khi vượt ngưỡng. Nếu nhân vật đang ngã hoặc UI đang hiện kết quả, bỏ câu áp lực thời gian đang chờ.
- Ưu tiên kết quả cuối → ngã/lỗi → nhắc kỹ thuật → nhận xét thành công. Xóa câu còn chờ khi đổi scene hoặc kết thúc level.
- Chỉ giữ một bong bóng hoạt động; đuôi và nhãn tên phải đúng người nói. Nếu câu thứ hai tới khi tình huống đã qua, bỏ câu đó thay vì kéo dài cuộc trò chuyện.
- Tạm dừng game thì tạm dừng thời gian hiển thị lời thoại. Bong bóng không nhận thao tác chạm, không che nút điều khiển, đường bay bóng, thanh lực hoặc bàn cờ.
- Ngã trong Bật cóc có 2,5 giây hồi phục: phát “Ui da!” khoảng 0,7 giây rồi giảng viên khoảng 1,8 giây; không khóa điều khiển thêm để đọc xong câu.
- Không phát thêm `result.pass.exam` ở bản đồ nếu node `sprint_pass`, `volleyball_pass` hoặc `soccer_pass` đã đảm nhiệm đoạn kết đó.

## 9. Bản đồ vị trí thay chữ cho agent

Các đường dẫn sau tính từ gốc `projects/the-chat-KMA`. Tìm theo class, method, constant và node ID, không dựa vào số dòng của bản trích xuất cũ.

| Nội dung | File / điểm sửa |
|---|---|
| Tên cast, 13 node, mục tiêu 10 bài | `Assets/Editor/StudentJourneyContentBuilder.cs` → `BuildDialogues`, `BuildChallenges` |
| Thư viện thoại sinh ra | `Assets/_Project/Resources/Journey/JourneyDialogues.asset` |
| Mục tiêu challenge sinh ra | `Assets/_Project/ScriptableObjects/Journey/*.asset` |
| Chạy | `Assets/_Project/Scripts/Gameplay/Sprint/SprintGuide.cs` |
| Bóng chuyền | `Assets/_Project/Scripts/Gameplay/Volleyball/VolleyballGuide.cs` |
| Bóng đá | `Assets/_Project/Scripts/Gameplay/Football/FootballGuide.cs` |
| Cờ vua: modal | `Assets/_Project/Scripts/Gameplay/Chess/ChessGuide.cs` |
| Cờ vua: câu giảng viên, toast, kết quả | `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs` |
| Cờ vua: HUD | `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalHud.cs` |
| Cờ vua: thẻ intro, nhãn tên | `Assets/Editor/ChessFinalSceneConfigurator.cs` |
| Gợi ý động | `Assets/_Project/Scripts/Gameplay/Chess/Core/PuzzleHints.cs` |
| Ý tưởng gợi ý của thế cờ | `ChessHintBuilder.cs` (tính từ bàn cờ, không còn lưu trong đề) |
| Bật cóc: modal | `Assets/_Project/Scripts/Gameplay/FrogJump/FrogJumpGuide.cs` |
| Bật cóc: chữ nổi | `Assets/_Project/Scripts/Gameplay/FrogJump/FrogJumpController.cs` → `OnLanded` |
| Cảnh báo thất bại chung | `Assets/_Project/Scripts/UI/MinigameGuidePages.cs` |
| Kết quả và nút hành động | `Assets/_Project/Scripts/UI/ResultPanel.cs` → `ShowChallengeCore`, `ShowFrogJump` |
| Tổng kết | `Assets/_Project/Scripts/Gameplay/Celebration/CelebrationSceneController.cs`, `Assets/_Project/Scripts/Progression/Journey/CelebrationSummary.cs` |
| Nút và asset tổng kết | `Assets/Editor/CelebrationSceneConfigurator.cs` |
| Hết lượt | `Assets/_Project/Scripts/UI/GameOverPresentationBuilder.cs` |

Đổi chữ trong nguồn tạo nội dung và cập nhật asset/scene được sinh ra bằng workflow của project; chỉ sửa asset sinh ra có thể bị ghi đè khi chạy builder. Những câu gameplay mới tại mục 7 cần triển khai riêng theo bảng sự kiện và ghép asset của tài liệu đi kèm.
