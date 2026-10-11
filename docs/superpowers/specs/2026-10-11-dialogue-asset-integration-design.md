# Chạy trốn thể chất — ghép lời thoại, biểu cảm và sự kiện

**Ngày:** 11/10/2026.

**Trạng thái:** Người dùng đã duyệt thiết kế nội dung, cơ chế, UI và kiểm chứng trong hội thoại. Đặc tả này chờ duyệt trước khi lập kế hoạch triển khai. Mức tăng tốc thanh trượt Bật cóc 0,9 → 0,7 giây là đề xuất cụ thể hóa yêu cầu mới, được duyệt cùng đặc tả.

## 1. Mục tiêu và nguồn yêu cầu

Đưa lời thoại biên tập và bảng ghép asset vào 13 node bản đồ, 10 level, Bật cóc, bảng kết quả, Game Over và Tổng kết học phần. SV năm 1 rụt rè, qua bài thì nhẹ nhõm; giảng viên nghiêm từ đầu đến cuối. Lời thoại phản ứng đúng sự kiện thật, không gây vướng điều khiển hoặc tiêu tốn giờ chơi trước bài.

Nguồn câu chữ: `Chay-tron-the-chat-loi-thoai-bien-tap.md` ở gốc repo. Nguồn ghép sprite: tài liệu “CHẠY TRỐN THỂ CHẤT — HƯỚNG DẪN GHÉP LỜI THOẠI VỚI ASSET”, bản 11/10/2026, người dùng cung cấp trong hội thoại. Các bảng trong đặc tả lưu những mapping bắt buộc của tài liệu đó để triển khai không phụ thuộc vào việc có file hướng dẫn riêng.

Quyết định người dùng: “giữ lại assets đang chơi”. Cấu hình bài hiện có là nguồn chuẩn; thay chữ, reference biểu cảm và builder đồng bộ với cấu hình này. Yêu cầu bổ sung: “trong game bật cóc, tăng tốc độ thanh trượt lên”. Đây là thay đổi cân bằng duy nhất của đợt này.

Không đổi ID, save schema, vai trò điều khiển, thứ tự môn, routing, điều kiện thắng/thua, cách tính điểm, số lượt thi hoặc tác dụng của Bật cóc. Không kích hoạt node `supplementary`. Giữ animation gameplay hiện có; các frame thể thao bổ sung không thuộc phạm vi nối mới trong đợt này.

## 2. Cấu hình đang chơi và ngoại lệ đã yêu cầu

| Bài / tham số | Giá trị chuẩn |
|---|---|
| Chạy làm quen | 12 nhịp liên tiếp, không tính giờ |
| Chạy luyện | 150 m, 20 giây |
| Chạy thi | 150 m, 15 giây |
| Bóng chuyền làm quen | 3 lần đỡ thành công, không thêm điều kiện liên tiếp |
| Bóng chuyền luyện | 5 điểm, 120 giây, đối thủ giao bóng |
| Bóng chuyền thi | 10 điểm, không giới hạn giờ, luân phiên giao bóng |
| Bóng đá làm quen | 3 bàn, không thủ môn, không giới hạn lượt sút |
| Bóng đá luyện | 2 bàn / tối đa 6 lượt, thủ môn Normal |
| Bóng đá thi | 3 bàn / đủ 5 lượt, thủ môn Normal |
| Cờ vua | 2 nước, 90 giây suy nghĩ, 2 lần sửa; sai lần 3 mới trượt |
| Bật cóc | 60 m, 60 giây, hồi phục ngã 2,5 giây |
| Thanh trượt Bật cóc | `sweepSeconds`: 0,9 → **0,7 giây** |

`sweepSeconds` là thời gian kim đi một chiều từ mép này sang mép kia; vòng đi và về giảm từ 1,8 xuống 1,4 giây, tốc độ tăng khoảng 29%. Sửa asset `Assets/_Project/ScriptableObjects/FrogJump/FrogJumpBalance.asset` và mặc định `FrogJumpTuning` đồng bộ. `FrogJumpSceneConfigurator.EnsureBalance()` đang giữ asset tồn tại; giữ hành vi đó để tạo lại scene không ghi đè tuning.

Giữ `safeZone = 0,4`, quãng bật tối đa/tối thiểu 1,5/0,25 m, thời gian bật 0,6 giây, hồi phục 2,5 giây và thuật toán tính quãng bật. Thanh trượt vẫn dừng trong Jumping/Fallen và reset khi quay lại Aiming như hiện tại. Không thêm tăng tốc theo thời gian hoặc cấp độ khó.

Tài liệu lời thoại còn dùng chạy 30/22 giây và Bật cóc 40 m. Khi đưa vào game, sửa các số theo bảng trên, gồm `sprint_exam.01`, sticker thời gian, `sp.practice.start` và `frog.start.save`. Mục tiêu/hướng dẫn và câu chứa số lấy từ cấu hình thực tế; không duy trì bản chữ riêng với số cũ. Bỏ câu “không giới hạn thể lực” vì cơ chế thể lực đã gỡ.

`StudentJourneyContentBuilder.BuildChallenges()` hiện còn seed chạy 30/22; phải sửa thành 20/15 cùng đợt để tái tạo asset không đổi luật đang chơi. Các con số khác cũng phải đối chiếu controller/rules thực tế trước khi tạo nội dung.

## 3. Nội dung, cast và node bản đồ

Giữ các ID `tan_thu`, `co_the_chat`, `mai_toang`, `anh_khoa_tren`. Tên hiển thị lần lượt: **SV năm 1**, **Giảng viên thể chất**, **Mai**, **Anh Tuấn**. Dùng thống nhất **Bật cóc** và **lượt thi**; “lượt sút” chỉ dành cho bóng đá.

Trong builder đổi cast thành SV năm 1 → MaleAdventurer, Anh Tuấn → MalePerson. Giữ `CharacterArt.Hero = MaleAdventurer`; thủ môn luôn là StudentKeeper. Không gán thủ môn thành giảng viên hoặc Anh Tuấn.

Giữ enum `DialoguePose` và các giá trị số đã serialize. Mapping sprite:

| Cast | Idle | Cheer | Hurt | Jump | Duck |
|---|---|---|---|---|---|
| MaleAdventurer | fear | happy | disappointed | jump | duck |
| BossPE | strictLook | scoreWrite | angry | jump | duck |
| FemalePerson | idle | cheer0 | hurt | jump | duck |
| MalePerson | idle | cheer0 | hurt | jump | duck |

Giữ node ID, thứ tự speaker và số dòng. Thay toàn bộ chữ theo mục 2 của file biên tập, bỏ emoji/sticker cũ; chỉ giữ sticker được chỉ định trong bản mới. Pose theo thứ tự dòng của từng node:

| Node | Pose từng dòng |
|---|---|
| opening | Idle, Idle, Idle, Idle |
| sprint_intro | Idle, Idle, Idle |
| sprint_exam | Idle, Idle |
| sprint_pass | Cheer, Idle |
| volleyball_intro | Idle, Idle, Idle |
| volleyball_exam | Idle, Idle, Idle |
| volleyball_pass | Cheer, Cheer |
| soccer_intro | Idle, Idle, Idle |
| soccer_exam | Idle, Idle |
| soccer_pass | Cheer, Idle |
| chess_intro | Idle, Idle, Idle |
| supplementary | Idle, Idle |
| course_complete | Cheer, Cheer, Idle, Idle |

Thay chữ hướng dẫn, mục tiêu 10 bài, HUD cờ, phản hồi Bật cóc, ResultPanel, Game Over và tổng kết theo mục 3–6 của file biên tập, với số liệu theo mục 2 của đặc tả. Giữ nội suy số, điều kiện hiện nút và hành động nút thực tế. Lỗi lưu dữ liệu/lỗi chấm dữ liệu vẫn là thông báo hệ thống.

## 4. Thành phần và luồng dữ liệu

Tách thành bốn trách nhiệm, tận dụng UiKit, typography và UITheme hiện có:

1. **Danh mục nội dung:** dữ liệu event ID, lượt nói, speaker, chữ, reference sprite, thời lượng, mức ưu tiên và chính sách lặp. Builder tạo asset nội dung dùng chung; không load PNG bằng tên suy đoán ở runtime. Không đưa câu gameplay vào node bản đồ.
2. **Bộ điều phối thuần:** nhận yêu cầu phát, kiểm tra cooldown/số lần/context, chọn câu, phát tuần tự và bỏ câu hết hiệu lực. Có clock đầu vào để kiểm tra thời gian bằng EditMode. Chưa cần hệ thống hội thoại tổng quát hoặc nhánh lựa chọn.
3. **View theo scene:** HUD minigame, ChessCastView, ô thoại kết quả/Game Over. View hiển thị speaker, chữ, sprite và icon theo lệnh điều phối; không quyết định luật hoặc trừ lượt.
4. **Adapter sự kiện:** mỗi môn nối trạng thái/callback thật vào event ID. Ưu tiên callback hiện có; thêm thông báo trình diễn khi thiếu, không sửa rules để tạo tình huống thoại.

Luồng: cấu hình bài + sự kiện thật → adapter tạo yêu cầu chứa context lượt hiện tại → bộ điều phối → view. Context gồm scene/lượt, challenge ID/kind, trạng thái còn hiệu lực và số liệu cần nội suy. Các phản ứng chỉ được gửi lúc chuyển trạng thái hoặc callback, không gửi mỗi frame. Mốc lowtime dùng kiểm tra vượt ngưỡng một lần.

Assembly chứa bộ điều phối/dữ liệu chỉ phụ thuộc các kiểu UI và progression cần thiết, không tham chiếu ngược controller từng môn. Sprite là reference Unity; logic chọn câu/cooldown không phụ thuộc MonoBehaviour.

## 5. Chính sách phát và vòng đời

- Ưu tiên kết quả cuối → ngã/lỗi → nhắc kỹ thuật/áp lực thời gian → nhận xét thành công. Thoại mở đầu là luồng trước bài, tách khỏi hàng đợi gameplay.
- Một bong bóng hoạt động tại một thời điểm. Hai speaker của một event phát SV năm 1 trước, giảng viên sau. Nếu câu thứ hai không còn đúng tình huống, bỏ nó.
- Phản ứng lặp do ngã, bấm sai hoặc mất điểm: cooldown 10 giây, tối đa 2 lần mỗi nhóm phản ứng trong một lượt. Các biến thể first/repeat của cùng phản ứng dùng chung bộ đếm; không dùng ID khác để vượt giới hạn.
- Mốc áp lực thời gian: chạy 5 giây, cờ 15 giây trong lượt người chơi, Bật cóc 10 giây. Phát một lần khi vượt ngưỡng. Nếu đang ngã hoặc hiện kết quả, bỏ yêu cầu đó thay vì phát trễ.
- Thông báo chấm cờ lần 1/2/3, lỗi dữ liệu, nước bất hợp lệ và gợi ý người chơi chủ động bấm vẫn phải phản hồi mỗi lần phù hợp. Chúng không bị cooldown của các câu phản ứng phụ làm mất thông tin.
- Khi biết kết quả cuối, xóa câu gameplay đang chờ và kết thúc câu không còn đúng; chọn đúng một đoạn kết. Câu hoàn thành riêng của level có ưu tiên hơn câu `result.pass.*` dự phòng. Node đạt môn trên bản đồ không phát thêm câu kết quả chung.
- Giữ timing resolve hiện có, kể cả 0,9 giây trên bàn cờ. Nếu bảng kết quả mở, nó tiếp nhận phần kết phù hợp hoặc kết thúc bong bóng cũ; không kéo dài resolve để đọc thoại. `ch.extra.relief` chỉ phát nếu bảng kết quả chưa mở và đủ thời gian.
- Pause/guide review dừng cả thời lượng hiển thị và cooldown; resume tiếp tục câu đang hiện, không phát lại event. Reset khi bắt đầu lượt mới; unsubscribe/hủy khi unload/disable để không giữ câu từ scene cũ.
- Câu ngắn mặc định khoảng 1–1,5 giây, dài khoảng 2–3 giây. Giữ thời lượng đủ đọc của gợi ý cờ. Hai câu đầu bài được đọc trước khi giờ chơi bắt đầu.
- Bật cóc lần ngã đầu: “Ui da!” 0,7 giây, giảng viên 1,8 giây, toàn bộ nằm trong hồi phục 2,5 giây. Lần ngã tiếp được phát chỉ khi qua cooldown; phân chia cùng ngân sách hồi phục, bỏ câu SV dài nếu không đủ đọc và ưu tiên lệnh giảng viên. Không cộng thêm thời gian phạt.

## 6. Cổng bắt đầu

Giữ cổng bắt đầu cho đến khi hướng dẫn lần đầu, intro riêng cần thiết và thoại đầu bài đã hoàn tất. Không dùng một lệnh boolean mở gate có thể ghi đè phần đang giữ gate khác. Dùng trạng thái điều phối các điều kiện trước bài, chỉ chuyển sang countdown/bắt đầu một lần khi đủ điều kiện.

- Hướng dẫn lần đầu vẫn theo MinigameGuideHost; khi không tự mở, thoại đầu bài vẫn chạy một lần.
- Chạy giữ luồng bắt đầu riêng của môn; thoại đầu bài hoàn tất trước countdown riêng.
- Bóng đá giữ yêu cầu bấm bắt đầu hiện có; nếu thoại hoặc hướng dẫn chưa hoàn tất, trì hoãn việc mở gate sau yêu cầu bắt đầu. Không cho bấm yêu cầu hai lần tạo hai lượt.
- Bóng chuyền/Bật cóc tích hợp gate với luồng countdown chung; PhaseOverlay không tự mở gate sớm.
- Cờ vua giữ `chess.start` trước Bắt đầu, không phát hai lần. `ch.extra.begin` là câu sau Bắt đầu, chỉ cho `m2_001`, không trì hoãn đồng hồ hoặc biến thành một gợi ý lời giải mới.

Không đóng gate/khóa điều khiển trong lượt chỉ để đọc thoại. Các hạn chế input do gameplay, guide và pause hiện có vẫn giữ trách nhiệm của chúng.

## 7. View và asset

HUD dùng một ô thoại nhỏ có tên, hình và chữ; chọn vị trí theo từng scene để không che joystick/nút, đồng hồ, thanh lực, vùng xanh/đỏ, bóng rơi hoặc đường bay dự kiến. Neo trong safe area, kích thước dùng theme hiện có. Giảng viên ngoài sân dùng ô HUD, không thêm NPC có collider. Mọi Image/TMP của thoại và icon đặt `raycastTarget = false`.

Đổi biểu cảm trong ô thoại không được gọi thay sprite nhân vật đang chạy/đỡ/sút. Giữ sprite gameplay được view vận động điều khiển. Không biến sút hỏng thành ngã hoặc mất điểm thành lao cứu bóng.

Mapping biểu cảm gameplay (H = MaleAdventurer, B = BossPE):

| Nhóm event | SV năm 1 | Giảng viên |
|---|---|---|
| sp.learn.start | think | command |
| sp.learn.wrong | disappointed | taunt |
| sp.learn.done, sp.practice.done | happy | scoreWrite |
| sp.practice.start | fear | handsOnHips |
| sp.practice.break | disappointed | command |
| sp.exam.start | fear | whistle0 → whistle1 → strictLook |
| sp.exam.lowtime | fear | handsOnHips |
| sp.exam.stumble | hurt | command |
| vb.learn.start | fear | command |
| vb.learn.miss, vb.exam.lostpoint | disappointed | taunt |
| vb.learn.done, vb.practice.done | happy | scoreWrite |
| vb.practice.start | think | handsOnHips |
| vb.practice.badtime | disappointed | command |
| vb.exam.start, vb.exam.matchpoint | fear | strictLook |
| vb.exam.dive | hurt | command |
| fb.learn.start | fear | command |
| fb.learn.miss, fb.practice.saved | disappointed | taunt |
| fb.learn.done, fb.practice.done | happy | scoreWrite |
| fb.practice.start | fear | strictLook |
| fb.exam.start | think | strictLook |
| fb.exam.goal | happy | scoreHold |
| fb.exam.lastkick | fear | command |
| fb.exam.impossible | disappointed | scoreWrite |
| frog.start.save | fear | penalty0 |
| frog.start.charged | disappointed | penalty1 |
| frog.fall.first | hurt | command |
| frog.fall.repeat | disappointed | angry |
| frog.goodjump | happy | strictLook |
| frog.lowtime | fear | command |
| frog.finish | happy | scoreHold |
| frog.timeout | disappointed | handsOnHips |
| result.fail.first | disappointed | penalty0 |
| result.fail.repeat | disappointed | angry → penalty1 |
| result.pass.practice, result.pass.exam | happy | scoreWrite |
| gameover.comment | disappointed | strictLook |

Mỗi dòng chỉ hiển thị speaker có lời trong file biên tập. Giữ reaction stumble của chạy chỉ khi hình vấp thật được kích hoạt; lỗi learn không thêm ngã. Volley dive chỉ khi vận động thật có Dive/tiếp đất. Football saved chỉ cho outcome thủ môn cản; lastkick trước cú thứ 5 ở Aiming, không trong lúc bóng bay. Practice done và impossible chỉ khi rules chốt kết quả cuối.

Bật cóc khi Fallen dùng `H/fallSitRight` trên sân, khi hồi phục về `H/frogReadyRight`. Giữ ba pose đang nối: frogReadyRight/frogAirRight/fallSitRight. Không nối thêm takeoff/land/getUp trong đợt này.

Tạo/import `Assets/_Project/Art/UI/Dialogue/PointingHandRight.png`: khoảng 96×96 px, nền trong suốt, bàn tay chỉ phải viền đậm hợp art. Hiện bằng Image riêng khoảng 28–36 px, cạnh nhãn hoặc câu giảng viên, giữ tỉ lệ và không che chữ. Dùng cho frog.fall.first, frog.fall.repeat, frog.lowtime. Lật riêng icon nếu hướng sân cần đổi; không dùng emoji chưa đăng ký hoặc salute thay thế. Chỉ coi phần icon hoàn thành khi file tồn tại, import đúng và có reference UI.

## 8. Cờ vua và tổng kết

ChessCastView thêm speaker và vị trí bong bóng cho hai phía; giữ một view hoạt động, speaker sinh viên không dùng đuôi phía giảng viên. Mở rộng cả key controller và mảng pose configurator đồng bộ:

- Student `idle` → think; `hurt` → disappointed; `cheer0`, `cheer1` → happy; thêm fear, think, happy, disappointed nếu gọi trực tiếp.
- Teacher `idleBoss` → handsOnHips; `taunt` → taunt; `chessThink` → think; `cheer0` → scoreWrite; giữ strictLook/whistle0/whistle1/chessMove; thêm command, angry, scoreWrite, handsOnHips khi gọi trực tiếp.
- Start: fear/handsOnHips. Mistake1: disappointed, whistle0 → whistle1 → strictLook. Mistake2: disappointed, whistle0 → whistle1 → angry. Failed lần 3: disappointed/angry. Timeout: disappointed/handsOnHips. Solved: happy/scoreWrite.
- Hint1: think/taunt; hint2/3: think/command. Illegal/data_error giữ pose, chỉ toast hệ thống. Extra begin/firstcorrect: think/strictLook; lowtime: fear/taunt; relief: happy/scoreWrite.

Giữ tiếng còi lỗi hiện có, không thêm tiếng còi trùng. `chessMove` chỉ cho giảng viên đi xe; bài hiện tại đi vua nên dùng think. Gợi ý 2/3 lấy từ node đúng hiện tại của PuzzleHints: m2_001 lần đầu e6 → h6, sau lượt giảng viên h6 → g7. Không giữ hint của node cũ trong hàng đợi qua lượt mới.

Celebration giữ timeline giây 5 phát câu giảng viên, giây 8 mở tổng kết. TeacherFrames đủ ba vị trí strictLook/scoreHold/scoreWrite; hero đủ ba vị trí idle/happy/happy. Mai giữ idle rồi có thể cheer ngắn như luồng đang có. Giảng viên không dùng Cheer/Clap/Congratulate để công nhận kết quả. Các action và dữ liệu tổng kết giữ nguyên.

## 9. Lưu tiến trình, lỗi và tái tạo nội dung

Trước/sau Bật cóc chọn chữ từ PendingFrogJump.SavesLife và kết quả áp dụng thực tế. Lần trượt đầu của cùng challenge có thể giữ lượt bằng về đích; lần trượt lặp đã trừ lượt thì dù kết quả Bật cóc thế nào cũng không trừ thêm. Qua bài xóa bộ đếm trượt theo logic đang có. Không sửa save/routing để khớp câu thoại.

Kết quả/UI tiếp tục dựa trên trạng thái lưu thành công/thất bại hiện có. Thoại không chạy thay một lỗi lưu kỹ thuật, không hứa retry khi hết lượt. Giữ các action Game Over và Celebration hiện có; không khôi phục lượt hoặc tiến trình qua một câu dẫn.

Builder phải kiểm tra đủ cast, pose key, sprite reference và event ID trước khi lưu asset/scene. Sprite thiếu hoặc key chưa đăng ký là lỗi cấu hình rõ ràng, không âm thầm chọn ảnh khác. Runtime bỏ yêu cầu không còn context hoặc thiếu nội dung hợp lệ, ghi lỗi chẩn đoán và vẫn để game tiếp tục; không hiện ID/path/lỗi kỹ thuật như lời nhân vật.

Cập nhật nguồn builder/configurator và kết quả sinh ra cùng đợt. Tạo lại nội dung giữ cấu hình hiện tại, không tạo UI trùng hoặc reference sprite tạm. File lời thoại do người dùng cung cấp được giữ làm nguồn; số khác thực tế được giải thích bằng mục 2 của đặc tả.

## 10. Kiểm chứng và tiêu chí hoàn thành

### EditMode

- 13 node, 10 bài, cast/ID/speaker/pose đúng; tên mới, chữ Bật cóc/lượt thi; sticker cũ và emoji cũ đã bỏ.
- Sprite path/reference tồn tại, mọi key ChessCastView gọi có cấu hình. Teacher không dùng clap/congratulate ở các đoạn công nhận.
- Điều phối: thứ tự speaker, ưu tiên/preemption, cooldown 10 giây, cap 2, biến thể dùng chung nhóm, bỏ câu hết hiệu lực, lowtime một lần, pause và reset.
- Gợi ý đúng hai node của m2_001; lỗi cờ 2 vẫn cho đi đúng, lỗi 3 mới trượt.
- Tuning kim 0,7 giây: kiểm tra vị trí theo thời gian và vòng đi/về, giữ phân loại ngã/quãng bật/thời gian hồi phục. Không dùng test chỉ so sánh một hằng số.
- Tạo lại nội dung giữ Sprint 20/15, Bật cóc 60 m/60 giây và sweep 0,7; lời dẫn lấy đúng số.

### PlayMode và thao tác thực tế

- Hướng dẫn lần đầu/chơi lại/intro riêng: đồng hồ không chạy trong thoại đầu bài, countdown bắt đầu đúng một lần, không thêm còi trùng.
- Mỗi môn có lượt đúng/lỗi/hết giờ khi áp dụng; kiểm tra football saved/miss/goal/lastkick và volley badtime/lostpoint/dive/matchpoint đúng sự kiện.
- Bật cóc cả SavesLife true/false, ngã đầu/ngã lặp/cú chính xác/hết giờ; thoại ngã nằm trong 2,5 giây, tốc độ kim mới quan sát đúng, không đổi tác dụng trừ/giữ lượt.
- Pause/resume, mở hướng dẫn lại, retry và đổi scene không giữ câu cũ/nhân đôi bong bóng. Joystick, nút ĐÁNH/NHẢY/SÚT và tap Bật cóc nhận thao tác như trước.
- PNG tại 16:9, 4:3 và màn rộng; xem trực tiếp để xác nhận chữ đủ đọc, đúng speaker, safe area và không che thông tin/nút/bàn cờ. Screenshot tĩnh không thay thế kiểm tra tương tác.
- Chạy suite phù hợp và full EditMode/PlayMode của project; đọc XML, báo riêng test, build, visual/device QA và Git/push. Android/device chưa chạy phải ghi rõ chưa kiểm chứng.

Hoàn thành khi nội dung và sprite được nối trong scene thật, nguồn tái tạo giữ nguyên kết quả, icon riêng đã import/wire, sự kiện và điều khiển được kiểm chứng. Đặc tả được duyệt mới chuyển sang kế hoạch triển khai; chưa sửa gameplay trong giai đoạn này.
