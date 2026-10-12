# Cờ vua: AI sinh thế chiếu hết, chấm chất lượng nước đi và thời gian theo thế cờ

Ngày: 2026-10-12. Trạng thái: thiết kế hội thoại đã được duyệt; bản đặc tả này chờ người dùng duyệt trước khi lập kế hoạch triển khai.

## 1. Mục tiêu và các quyết định đã thống nhất

Thay bài cờ vua theo đáp án cố định bằng bài giải thế cờ do AI sinh và kiểm chứng. Người chơi phải chiếu hết đối thủ trong cả giới hạn lượt lẫn thời gian. Đánh giá F, D, C, B, A, A+ phản ánh chất lượng nước đi và hiệu quả số lượt.

- Mọi nước hợp lệ đều thực hiện thật, tiêu tốn một lượt của người chơi. Không hoàn tác tự động khi đi kém tối ưu.
- Nước không hợp lệ không đổi bàn cờ và không tính lượt; thời gian suy nghĩ vẫn chạy.
- AI tạo thế tàn cuộc ít quân, kiểm chứng có đường chiếu hết bắt buộc trước mọi phòng thủ.
- AI đối thủ chọn phòng thủ tốt nhất; các nước ngang nhau có thể chọn ngẫu nhiên.
- Thời gian được tính từ phân tích từng thế cờ, không lấy một hằng số theo nhãn độ khó.
- Đồng hồ chỉ chạy trong thời gian người chơi suy nghĩ. Thời gian AI phân tích, diễn hoạt và tạm dừng không bị tính.
- Điểm gồm 70% chất lượng nước đi và 30% hiệu quả số lượt. Thời gian là điều kiện hoàn thành, không cộng/trừ điểm.
- Bài mới sinh đề mới; chơi lại giữ nguyên đề, giới hạn lượt và thời gian.
- Gợi ý chỉ rõ nước tốt nhất giới hạn hạng tối đa ở A; gợi ý chung không áp trần này.
- Giữ cách trình bày bàn cờ, nhân vật và hoạt ảnh hiện tại, cập nhật thông tin HUD, hướng dẫn, thoại và kết quả cho luật mới.

Đây là AI cờ vua dựa trên tìm kiếm và đánh giá trạng thái, không phải mô hình tự học hay LLM. Chất lượng nước đi được so với kết quả tìm kiếm trong phạm vi của bài, không tuyên bố tương đương đánh giá của engine chuyên nghiệp ở độ sâu không giới hạn.

## 2. Hiện trạng code và phạm vi

`ChessPuzzleLibrary` đọc hai JSON trong `Resources/Chess/Puzzles`. `ChessFinalController.Awake` luôn chọn Normal. `PuzzleGrader` nhận đáp án từ cây định sẵn, trả bàn cờ cũ khi một nước hợp lệ không có trong đáp án và chọn phản hồi đầu tiên có sẵn. `ChessFinalStateMachine` thất bại do quá lỗi hoặc hết giờ; số nước chỉ tăng khi đi đúng đáp án. `PuzzleHints` và một số thoại phụ thuộc nút cây hoặc ID `m2_001`.

`ChessPosition`, `ChessMove` và `MoveGenerator` đã có FEN, luật đi, chiếu, chiếu hết, hòa hết nước đi, nhập thành, bắt tốt qua đường và phong cấp. Tái sử dụng các luật này; không viết lại bàn cờ hoặc hệ thống input.

`ScoreUtil` dùng chung cho các môn, hạng cao nhất là `Rank.S`. `JourneyChessRecordData` hiện lưu điểm, thời gian suy nghĩ, lỗi và dùng gợi ý. Các trường này chưa đủ cho bài sinh ngẫu nhiên.

Phạm vi thay đổi: core cờ vua, controller/HUD/gợi ý/thoại/hướng dẫn, scene authoring nếu cần, dữ liệu kết quả và lưu bài cờ vua, hiển thị kết quả/tổng kết cờ vua, các kiểm thử liên quan. Không đổi cách chấm các môn khác. Các thay đổi Volleyball đang có trong workspace nằm ngoài phạm vi.

## 3. Sinh đề và độ khó

| Mức | Số nước chiếu hết tối ưu N | Giới hạn lượt M |
| --- | ---: | ---: |
| Easy / Dễ | 1 | 2 |
| Normal / Vừa | 2 | 3 |
| Hard / Khó | 3 | 5 |

N chỉ tính nước của bên người chơi, không tính nước phòng thủ. AI phải chứng minh không có chiến lược chiếu hết ngắn hơn N và có chiến lược chiếu hết trong N trước mọi phản hồi hợp lệ. Không suy độ khó từ số quân hoặc một biến thể đơn lẻ.

### 3.1 Sinh ứng viên

- Phiên bản đầu sinh tàn cuộc 4–6 quân: đúng một vua mỗi bên và ít nhất một quân có khả năng phối hợp chiếu hết cho bên người chơi. Chỉ dùng vua/hậu/xe/tượng/mã để giảm không gian tìm kiếm; luật tốt và phong cấp hiện có vẫn giữ cho các thế dự phòng/kiểm thử.
- Cấu hình sinh không có nhập thành hoặc bắt tốt qua đường, bộ đếm ban đầu không gần ngưỡng hòa 50 nước. Bên người chơi đi trước.
- Không chồng quân, hai vua không kề nhau, bên vừa đi không để vua bị chiếu, hai vua không đồng thời bị chiếu. Loại vị trí đã kết thúc hoặc không có nước hợp lệ.
- Không yêu cầu dựng lại lịch sử từ khai cuộc để chứng minh khả năng xuất hiện của vị trí; đây là thế cờ bài tập có tính hợp lệ của bàn cờ được kiểm tra.
- Bộ kiểm chứng duyệt tất cả phòng thủ trong chứng minh chiếu hết. Thế thiếu chứng minh hoặc vượt ngân sách bị loại, không gán nhãn đã kiểm chứng.
- Lưu FEN chuẩn hóa và seed. Trong một phiên ứng dụng, tránh lặp lại FEN vừa dùng nếu có ứng viên thay thế đã kiểm chứng.

### 3.2 Ngân sách và dự phòng

Bộ sinh chạy ở nền, giới hạn ban đầu 2 giây và 200.000 nút tổng, dừng khi chạm một trong hai. Các giá trị nằm trong cấu hình tập trung. Nếu không sinh kịp, dùng ngẫu nhiên một đề dự phòng đã kiểm chứng cùng mức, áp cùng cách tính thời gian và chấm điểm. Kho dự phòng phải có cả ba mức; không lấy đề Normal thay cho Hard.

Theo dõi nguồn Generated/Fallback trong dữ liệu QA. Dự phòng đảm bảo khả năng bắt đầu bài, không được dùng để che việc bộ sinh luôn thất bại: kiểm chứng hiệu năng phải báo riêng tỷ lệ sinh thành công và tỷ lệ dự phòng.

Độ khó lấy từ context của lượt chơi Journey sau khi được cấu hình; scene mở trực tiếp dùng Normal. Không khởi tạo đề trước rồi bỏ qua context đến sau. Không thêm màn chọn độ khó mới trong phạm vi này.

## 4. AI tìm kiếm và phòng thủ

Core thuần C#, không tham chiếu UnityEngine. Tìm kiếm Minimax/Negamax với Alpha–Beta, sắp thứ tự nước, bộ nhớ trạng thái và giới hạn hủy. Tách kết quả tìm kiếm hoàn tất với kết quả bị cắt do thời gian/nút/hủy.

Chứng minh chiếu hết dùng lượt của bên tấn công: nút tấn công cần ít nhất một lựa chọn thắng, nút phòng thủ cần tất cả phản hồi đều thua trong giới hạn. Nước kết thúc phải thực sự là chiếu hết, không phải hết nước đi nhưng không bị chiếu. Tính khoảng cách chiếu hết theo phòng thủ kéo dài tốt nhất.

Đối thủ ưu tiên: chiếu hết người chơi nếu có chiến lược chứng minh được; tránh bị chiếu hết trong các lượt còn lại nếu có; nếu mọi lựa chọn đều thua thì kéo dài số nước tới chiếu hết. Các lựa chọn không phân biệt được bởi chứng minh dùng đánh giá chiến thuật/vật chất ở độ sâu hoàn tất để chọn. Không ngẫu nhiên trên những lựa chọn có chất lượng khác nhau; tie-break dùng seed để tái hiện test.

Ngân sách mỗi yêu cầu runtime ban đầu là 2 giây/200.000 nút. Cache theo trạng thái, bên tấn công và giới hạn tìm kiếm. Nếu chứng minh phục vụ chấm điểm chưa hoàn tất, trả trạng thái Unknown, tuyệt đối không đổi thành mất lời giải hoặc điểm 0. Giữ nước đã thực hiện, dừng đồng hồ/input và hiển thị lỗi phân tích có thể thử lại; không phát kết quả F, không trừ mạng. Cho phép thử phân tích lại hoặc chơi lại chính đề. Đây là xử lý lỗi hệ thống, không phải một điều kiện thua.

Worker chỉ nhận snapshot dữ liệu bất biến; cập nhật GameObject/UI trên luồng chính. Hủy công việc khi đổi scene/chơi lại/disable và bỏ kết quả thuộc generation ID hoặc board revision cũ. Một yêu cầu tính gợi ý cũng dừng thời gian trong lúc tính, sau khi hiển thị gợi ý thì đồng hồ tiếp tục chạy.

## 5. Thời gian theo từng thế cờ

AI cung cấp chỉ số từ chứng minh chiếu hết tối ưu, rồi một bộ ước lượng xác định tính thời lượng. Công thức sau là cấu hình ban đầu để người dùng duyệt và hiệu chỉnh bằng chơi thử, không phải kết quả đo khả năng người chơi:

`T = roundUpTo5(clamp(25 + 25*N + 8*B + 15*Q, 60, 240))` giây.

- N: số nước người chơi cần để chiếu hết tối ưu.
- B: trung bình `log2(1 + số nước hợp lệ)` của các trạng thái không kết thúc trong cây chứng minh tối ưu. Giữ tất cả lựa chọn tấn công tối ưu và tất cả phòng thủ; mỗi trạng thái chuẩn hóa chỉ tính một lần để tránh thiên lệch do chuyển vị.
- Q: số nước tấn công yên lặng lớn nhất trên một đường của cây chứng minh tối ưu. Nước yên lặng không ăn quân và không chiếu, thường khó nhận ra hơn nước chiếu trực tiếp.
- Làm tròn lên bội số 5 giây để HUD dễ đọc. Cùng N vẫn có thể có B/Q khác nhau và T khác nhau; không bắt buộc mọi đề cùng mức có thời lượng khác nhau.

Lưu chỉ số B/Q, T và phiên bản bộ ước lượng trong snapshot đề. Chơi lại không tính lại T. Một seed và phiên bản thuật toán cho kết quả tái hiện được.

Đồng hồ chạy ở PlayerTurn, kể cả đang chọn quân, đọc gợi ý đã hiện hoặc chọn phong cấp. Dừng ở Intro/Generating/Analyzing/BossTurn/Paused/Completed/Failed/AnalysisError. Chọn phong cấp chưa tính lượt cho đến khi xác nhận quân.

Đầu mỗi Submit kiểm tra thời gian còn lại trên luồng chính: nếu đã bằng 0 thì hết giờ, không nhận nước. Nước được nhận khi thời gian còn dương sẽ được xét chiếu hết kể cả việc phân tích/diễn hoạt kết thúc sau đó. Thứ tự này áp dụng cho input thực và test để tránh tranh chấp ở frame cuối.

## 6. Chấm điểm và điều kiện kết thúc

### 6.1 Chất lượng từng nước

Đề xuất công thức cụ thể để hiện thực hóa trọng số đã duyệt:

Trước một nước của người chơi, R là số lượt còn lại, `d_best` là số nước chiếu hết tối ưu trước mọi phòng thủ trong R. Với nước đã chọn, `d_chosen` là tổng số nước tấn công cần để chiếu hết theo phòng thủ tốt nhất, bao gồm nước vừa đi, trong R.

- Nước chiếu hết ngay: `d_chosen = 1`.
- Khi cả hai khoảng cách có chứng minh: `q = d_best / d_chosen`.
- Nước làm mất khả năng chiếu hết trong R, đã được chứng minh: `q = 0`.
- Vị trí đầu lượt đã không còn đường chiếu hết bắt buộc trong R: các nước không có đường chiếu hết được chấm q = 0. Vẫn cho chơi đến điều kiện kết thúc; không kết thúc sớm chỉ vì phân tích không thấy đường thắng.
- Unknown do hết ngân sách không phải q = 0; xử lý theo mục 4.

q nằm trong [0,1]; các nước giữ khoảng cách tối ưu đều nhận 1. Không dùng đáp án UCI duy nhất. Trung bình q của tất cả nước hợp lệ đã thực hiện là Qmean. Ghi nhận cả lịch sử nước và q để giải thích kết quả.

### 6.2 Điểm toàn bài

Nếu chiếu hết thành công trong thời gian và lượt cho phép, U là số lượt người chơi thực sự dùng:

`efficiency = clamp(N / U, 0, 1)`

`rawScore = 10 * (0.7 * Qmean + 0.3 * efficiency)`

Làm tròn một chữ số thập phân. Khi từng dùng gợi ý chỉ rõ nước đi, chặn điểm sau làm tròn ở 8,9. Phân hạng sau khi áp trần:

| Hạng | Điểm |
| --- | --- |
| A+ | >= 9 |
| A | >= 8 và < 9 |
| B | >= 7 và < 8 |
| C | >= 6 và < 7 |
| D | >= 5 và < 6 |
| F | < 5 |

Chiếu hết thành công quyết định Pass; điểm/hạng biểu thị chất lượng. Nếu không hoàn thành, Pass=false, điểm 0, hạng F; vẫn hiển thị đánh giá nước đã đi, không cộng thành tích tốt nhất.

### 6.3 Thứ tự kết thúc

Sau một nước hợp lệ của người chơi: ghi nhận nước và tăng U; kiểm tra chiếu hết trước; tiếp theo kiểm tra hòa; nếu chưa kết thúc và U=M thì thất bại do hết lượt; nếu còn lượt thì AI đáp trả.

Sau nước AI: kiểm tra người chơi bị chiếu hết, hòa, rồi mới trở lại PlayerTurn. Hòa gồm stalemate, ba lần lặp vị trí, luật 50 nước và vật chất không đủ; lịch sử vị trí/bộ đếm cần cập nhật đúng. Một nước chiếu hết được ưu tiên hơn điều kiện hòa tự động và giới hạn lượt.

Thất bại: hết giờ, hết lượt, bị chiếu hết hoặc hòa. Bỏ cơ chế dừng sau số lần chọn sai; các nước kém tối ưu là một phần ván thật.

## 7. Gợi ý, UI và thoại

Ba mức gợi ý lấy từ bàn cờ hiện tại: định hướng chiến thuật; chỉ quân nên xem; chỉ rõ nước từ/đến/phong cấp. Chỉ mức ba áp trần A. Nếu không còn chiếu hết bắt buộc trong lượt còn lại, nói đúng điều đó và gợi ý nước phòng thủ tốt nhất tìm được, không bịa đường chiếu hết. Mức ba vẫn áp trần khi nó chỉ rõ một nước.

Reset mức hiển thị gợi ý theo board revision mới nhưng giữ cờ đã dùng gợi ý chỉ rõ nước trong toàn lượt chơi. Thoại gợi ý cũ bị thay thế và không phát lại sau đổi bàn cờ/chơi lại. Thay điều kiện thoại phụ thuộc `m2_001` bằng trạng thái ngữ nghĩa; nội dung không nhắc số lỗi được sửa hoặc thời gian cố định.

HUD: mục tiêu chiếu hết, nhãn độ khó, `Nước: U/M`, đồng hồ. Intro ghi thời gian riêng của đề và giới hạn lượt; không cho Bắt đầu trước khi đề/thoại mở đầu sẵn sàng. Trạng thái tính toán và lỗi phân tích có phản hồi rõ ràng.

Kết quả: điểm, F–A+, số nước dùng/giới hạn, số nước tối ưu, thời gian dùng/được cấp, việc dùng gợi ý và lý do kết thúc. Với bài chưa đạt, có thông tin chất lượng các nước đã đi để luyện lại.

Hạng cao nhất cờ vua sử dụng giá trị tương thích `Rank.S` trong dữ liệu hiện có và formatter theo ngữ cảnh cờ vua hiển thị A+. Không đổi enum đã lưu hoặc cách hiển thị S của các môn khác. Áp formatter cho cả scene mở trực tiếp, Journey result và tổng kết cờ vua.

## 8. Thành phần và luồng dữ liệu

Tên dưới đây mô tả trách nhiệm; kế hoạch triển khai có thể chọn tên cụ thể tương đương:

| Thành phần | Trách nhiệm |
| --- | --- |
| ChessSearchEngine | Chứng minh chiếu hết, khoảng cách, phòng thủ; trạng thái Complete/Unknown/Cancelled |
| ChessPuzzleGenerator | Tạo ứng viên theo seed, kiểm chứng N, chọn dự phòng đúng mức |
| ChessPuzzleAnalysis / AttemptSnapshot | FEN, seed, N/M/T, B/Q, phiên bản, nguồn đề, chứng minh |
| ChessTimeEstimator | Công thức thời gian xác định từ chỉ số AI |
| ChessAttemptGrader | q theo từng nước, điểm tổng và trần gợi ý |
| ChessFinalStateMachine | Bàn cờ thật, lượt, đồng hồ, lịch sử hòa, trạng thái và kết quả |
| ChessFinalController | Vòng đời Unity, context độ khó, worker, input, hoạt ảnh và kết quả |
| ChessFinalHud / ChessDialogueAdapter / ChessGuide | Hiển thị và hướng dẫn theo snapshot/state |

Luồng: context → sinh/kiểm chứng đề → snapshot/thời gian → intro → nước người chơi thật → phân tích/chấm → kết thúc hoặc phòng thủ AI → lượt tiếp theo → kết quả → lưu Journey.

Một lần hoàn thành chỉ phát một kết quả. Pause không để coroutine hoặc worker commit nước hai lần. Khôi phục sau pause không chạy đồng hồ ở lượt AI.

## 9. Lưu dữ liệu và chơi lại

Lưu snapshot đề đủ để dựng lại FEN, độ khó, N/M/T, seed, chỉ số thời gian và phiên bản; ghi lại lịch sử nước, q, thời gian và gợi ý trong kết quả. Dùng cùng snapshot cho Retry trong Journey hoặc scene trực tiếp; lượt chơi mới tạo snapshot mới. Snapshot gắn với bài đang chơi và dữ liệu retry, không lấy từ thành tích tốt nhất.

Mở rộng bản ghi thành tích cờ vua với số lượt đã dùng, lượt tối ưu, giới hạn lượt, thời gian được cấp, hạng hiển thị và phiên bản chấm; dữ liệu cũ giữ nguyên điểm/thời gian/gợi ý, đánh dấu thiếu metadata mới, không suy đoán dữ liệu thiếu. Giữ các giá trị enum đã lưu.

Journey vẫn chỉ cập nhật best khi thành công và điểm cao hơn; bài Final thất bại không trừ mạng như hiện tại. Tổng kết dùng số lượt/điểm mới khi có; dữ liệu cũ có nhánh hiển thị tương thích. Không giả vờ điểm cũ đã được chấm bằng công thức mới.

Không bổ sung chức năng tiếp tục ván đang dở sau đóng ứng dụng. Sau reload để retry, khởi động lại cùng thế ban đầu với đồng hồ/lượt reset; kết quả cũ vẫn chỉ được commit một lần.

## 10. Kiểm chứng và tiêu chí chấp nhận

### EditMode

- Các thế mẫu mate-in-1/2/3, không có mate, nhiều nước tối ưu và phòng thủ phân nhánh; xác nhận khoảng cách theo nước người chơi, không đếm ply.
- Với tập đề có seed cố định, kiểm tra điều kiện hợp lệ và đối chiếu chứng minh bằng bộ kiểm chứng độc lập, không chỉ gọi lại engine cùng thuật toán. Giữ kiểm thử luật đi/perft, phong cấp và hòa.
- Nước kém vẫn thực hiện/tính lượt; nước bất hợp lệ không tính; nước tối ưu ngang nhau có cùng q; Unknown không bị chấm 0.
- Công thức điểm, làm tròn, ranh giới từng hạng, trần 8,9, điều kiện Pass và điểm 0 khi thất bại.
- Công thức T xác định, giới hạn 60–240 và làm tròn; hai đề cùng N nhưng khác chỉ số có thể có T khác nhau.
- Seed tái hiện, ngân sách bộ sinh, đúng mức dự phòng, metadata và migration dữ liệu cũ.

### PlayMode và UI

- Bắt đầu, đi nước hợp lệ ngoài lời giải, AI đáp trả, hết lượt, hết giờ, thắng lượt cuối, hòa, bị chiếu hết.
- Đồng hồ chạy khi suy nghĩ/phong cấp/đọc gợi ý; dừng khi AI tính/diễn hoạt/pause; race frame hết giờ theo mục 5.
- Hủy worker/thoại cũ khi retry, disable hoặc đổi scene; kết quả được phát/commit đúng một lần.
- Retry đúng FEN/M/T, bài mới sinh đề mới, context Easy/Normal/Hard và scene trực tiếp Normal.
- F–A+ ở kết quả/tổng kết cờ vua; hạng các môn khác giữ cách hiển thị hiện có.
- Kiểm tra tương tác thực, screenshot intro/gameplay/result và giới hạn chữ ở khung hình mục tiêu.

### Hiệu năng và báo cáo

Đo ít nhất 30 seed/mức trên môi trường chạy QA, ghi thời gian sinh, số nút, tỷ lệ Generated/Fallback và thời gian phân tích runtime. Mục tiêu ban đầu >=80% đề Generated mỗi mức trong ngân sách; không coi là đạt nếu chỉ hoạt động nhờ fallback. Báo riêng số liệu Editor và Android nếu có. Với tác vụ nền, đo độ phản hồi UI và xác nhận không gọi Unity API từ worker.

Các hệ số thời gian chỉ được xem là cấu hình ban đầu; kiểm tra hợp lý bằng chơi thử các đề cùng mức có T khác nhau. Chỉ chỉnh các hệ số tập trung sau khi có dữ liệu, tăng phiên bản bộ ước lượng khi thay đổi.

Chạy các suite cờ vua/progression/thoại liên quan và kiểm tra hồi quy toàn suite theo quy trình dự án; báo riêng test, build, QA ảnh, tương tác thiết bị, commit và push. Không tuyên bố Android đã được kiểm chứng nếu chỉ chạy Editor.

## 11. Rủi ro và giới hạn

Chứng minh mate-in-3 và chấm lại sau nước lệch có thể tốn nhiều nút. Giới hạn số quân, cache, worker và kho dự phòng giảm chi phí, nhưng tỷ lệ sinh cùng thời gian phân tích vẫn phải đo. Nếu không đạt ngân sách/tiêu chí trên, quay lại điều chỉnh thiết kế hoặc cấu hình có bằng chứng; không hạ yêu cầu kiểm chứng, không gọi Unknown là một đánh giá chính xác.

Thời gian do AI suy ra là ước lượng độ khó tư duy, không phải thời gian tính toán engine và không được tăng giảm giữa ván theo tốc độ người chơi. Cùng mức có thể có thời gian khác nhau, nhưng không cam kết AI đo chính xác năng lực mọi người chơi.

## 12. Bước tiếp theo

Người dùng duyệt bản đặc tả này, đặc biệt công thức q/điểm/T và cách xử lý lỗi phân tích. Sau đó dùng skill writing-plans để viết kế hoạch triển khai và chọn cách thực hiện. Chưa sửa gameplay trong giai đoạn đặc tả.
