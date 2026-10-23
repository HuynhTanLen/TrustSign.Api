# Kế hoạch thực hiện khóa luận Chữ Tín

Cập nhật: 23/10/2026. Bản gốc có cột trạng thái cập nhật liên tục nằm ở Doc [Kế hoạch thực hiện khóa luận Chữ Tín](https://claude.ai/code/artifact/44dfd7f0-11bf-4c44-b044-f11af6c9b17d); trạng thái mới nhất xem ở đó, file này là bản chụp.

Lịch dời 1 tuần so với đề cương: lập trình 19/10 → đóng băng mã nguồn 04/12, viết báo cáo đến 21/12, sau đó chuẩn bị bảo vệ. Có 7 tuần cho 27 chức năng, nên mỗi chức năng làm ở mức tối thiểu còn đúng nghĩa.

## Hiện trạng và nguyên tắc

**Hiện trạng 18/10:** 4 service trong `D:\KLTN\TrustSign` mới có code khung. Frontend chưa có commit và chưa có remote. Mục tiêu tuần 1 của đề cương (F01, F15, F16, thử PAdES) chưa làm.

**Cập nhật 23/10:** backend chuyển sang modular monolith `D:\KLTN\TrustSign.Api` (7 module, 1 database chia schema theo module); thêm F27 chữ ký mẫu và xuất hóa đơn PDF. Cổng thanh toán đã chốt: **SePay** (chuyển khoản VietQR + webhook).

**Nguyên tắc:**

1. Lát cắt dọc: mỗi chức năng làm trọn backend + màn hình + test rồi mới sang chức năng sau; luôn có bản chạy được để demo cho GVHD.
2. Rủi ro lớn làm trước: thử PAdES đầu tuần 1, webhook SePay đầu tuần 2.
3. Làm sâu chỉ ở 3 điểm nhấn để bảo vệ: đa khách thuê, ký PAdES + PKI, hash-chain.
4. Một task code chỉ "Xong" khi: gọi được qua API, có test (unit hoặc integration Testcontainers), có màn hình nếu là chức năng người dùng, đã commit lên nhánh `develop` của TrustSign.Api, README cập nhật nếu đổi cách chạy.
5. Báo cáo viết song song mỗi tuần (task DOC); chụp màn hình và vẽ sơ đồ ngay khi xong chức năng.

Ước lượng tính theo ngày công (d), mỗi tuần khoảng 6d.

## Mốc và lịch gặp GVHD

| Mốc | Ngày | Nội dung |
| --- | --- | --- |
| M1 | 25/10 | Đăng nhập, tạo doanh nghiệp, mời thành viên; PDF ký thử hợp lệ trên Adobe và Foxit |
| M2 | 01/11 | Gói dùng thử; thanh toán SePay kích hoạt gói; vượt hạn mức bị chặn |
| M3 | 08/11 | Hợp đồng nháp hoàn chỉnh, gửi ký được và trừ hạn mức |
| M4 | 15/11 | 3 người ký lần lượt end-to-end |
| M5 | 22/11 | PKI + PAdES: file đã ký hợp lệ trên Adobe và Foxit |
| M6 | 29/11 | Đủ 27 chức năng ở mức tối thiểu |
| M7 | 04/12 | Đóng băng mã nguồn, chạy trên tên miền thật, đủ kết quả 23 kịch bản |
| – | 14/12 | Gửi bản báo cáo đầy đủ cho GVHD |
| – | 21/12 | Nộp khóa luận |

Gặp GVHD: 23/10, 06/11, 20/11, 04/12, 14/12; mỗi lần mang theo bản chạy của mốc gần nhất. Sau 04/12 không thêm chức năng, chỉ sửa lỗi.

## Tuần 0 – Chủ nhật 18/10: chuẩn bị

Tổng ước lượng: 1d.

| Mã | Việc | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- |
| INF-01 | Dựng modular monolith `TrustSign.Api`: 7 module, 1 database chia schema, 1 compose (PostgreSQL, MinIO, Mailpit); chuyển code Identity, rate limit, CORS sang | 0.5d | Build và 11 test xanh, commit đầu trên nhánh `develop` (23/10). Còn chạy thử với database khi bật Docker | Xong |
| INF-02 | Frontend: commit đầu, tạo remote GitHub `TrustSign.Frontend`, CI lint/build; tạo repo GitHub cho TrustSign.Api | 0.25d | Nhánh `develop` trên GitHub, CI xanh | Chưa làm |
| INF-03 | Bảng theo dõi task (Doc kế hoạch) | 0.25d | Doc có link, đã ghim | Chưa làm |

## Tuần 1 – 19/10 → 25/10: Identity + thử PAdES

**Mốc M1 (25/10):** đăng nhập qua API, tạo doanh nghiệp, mời thành viên; PDF ký thử hiển thị hợp lệ trên Adobe Reader và Foxit. Tổng ước lượng: 7.5d.

| Mã | Việc | F/TC | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| SIG-01 | **Spike PAdES** trong `tests/` của TrustSign.Api (iText 9 + BouncyCastle): tạo Root CA → Issuing CA → chứng thư người ký, ký 1 PDF, ký lần 2 không phá chữ ký 1 | F07 | 1.5d | Adobe + Foxit thấy 2 chữ ký hợp lệ (sau khi tin cậy Root CA); sửa 1 byte → không hợp lệ. Chưa xong 25/10 → chuyển CMS tách rời | Chưa làm |
| IDN-01 | Đăng ký tài khoản, băm mật khẩu, mail xác minh (token dùng 1 lần, có hạn) | F01 | 0.75d | Đăng ký → mail ở Mailpit → bấm link → tài khoản Active | Chưa làm |
| IDN-02 | Đăng nhập, refresh token xoay vòng, phát hiện dùng lại refresh token, đăng xuất | F01, TC11 | 1d | Test: refresh cũ dùng lại bị từ chối và phiên bị thu hồi | Chưa làm |
| IDN-03 | Hồ sơ `/me`, sửa hồ sơ (đổi/quên mật khẩu ở IDN-06, tuần 5) | F01 | 0.25d | Gọi được qua API | Chưa làm |
| IDN-04 | Entity `Tenant`, `Membership`; đăng ký doanh nghiệp (người tạo là Chủ DN); JWT thêm `tenant_id`, `tenant_role`, `tenant_status`; đổi doanh nghiệp đang làm việc | F15 | 1d | Token có claim mới; `JwtClaims.cs` đã cập nhật | Chưa làm |
| IDN-05 | Mời thành viên qua email, chấp nhận lời mời, đổi vai trò, xóa thành viên; policy phân quyền theo vai trò | F16, TC20 | 1d | Nhân viên gọi API mời → 403; Chủ DN → 200 | Chưa làm |
| GW-01 | Policy rate limit riêng cho `/api/auth/login` và `/register` (NFR04, 10 lần/phút) | TC13 | 0.25d | Test: lần thứ 11 trong một phút bị 429 | Chưa làm |
| FE-01 | Layout, trang đăng nhập / đăng ký / xác minh email, API client tự refresh token | F01 | 1d | Đăng nhập được trên trình duyệt | Chưa làm |
| FE-02 | Màn hình tạo doanh nghiệp, danh sách thành viên, mời, đổi vai trò | F15, F16 | 0.75d | Luồng mời chạy end-to-end với Mailpit | Chưa làm |
| DOC-01 | Chương 1: khảo sát VNPT eContract, FPT.eContract, DocuSign (bảng so sánh), phát biểu bài toán, đối tượng, phạm vi | – | song song | Bản nháp Chương 1 | Chưa làm |

## Tuần 2 – 26/10 → 01/11: Subscription + SePay

**Mốc M2 (01/11):** doanh nghiệp mới có gói dùng thử; thanh toán SePay kích hoạt gói; vượt hạn mức bị chặn. Tổng ước lượng: 6d.

| Mã | Việc | F/TC | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| SUB-01 | **Tìm hiểu SePay** (ngày đầu tuần): tài khoản, môi trường thử / giả lập giao dịch, định dạng webhook, cách xác thực (API key ở header `Authorization: Apikey …`), cách tạo QR có nội dung chuyển khoản. Viết lại luồng thanh toán UC14/UC15 trong SRS theo SePay | F19, TC17 | 0.5d | Ghi chú tích hợp trong README của TrustSign.Api; SRS đã sửa | Chưa làm |
| SUB-02 | Entity `Plan` (giá, thời hạn, hạn mức hợp đồng/tháng, cờ dùng thử), CRUD cho quản trị nền tảng; API công khai `GET /api/subscription/plans` | F17 | 0.75d | Seed 3 gói: Dùng thử / Cơ bản / Doanh nghiệp | Chưa làm |
| SUB-03 | `Subscription`: tạo gói dùng thử khi doanh nghiệp được tạo, trạng thái Dùng thử → Hiệu lực → Hết hạn, tác vụ nền quét hết hạn | F18, TC18 | 1d | DN mới có thuê bao Dùng thử; test giả lập thời gian thấy chuyển Hết hạn | Chưa làm |
| SUB-04 | `Invoice` + luồng thanh toán: hóa đơn có mã duy nhất (vd `TS0001234`) kèm QR SePay; webhook `/api/subscription/webhooks/sepay`: kiểm API key, khớp mã + số tiền, idempotent theo mã giao dịch SePay, gia hạn thuê bao | F19, TC16, TC17 | 1.5d | Giao dịch thử → thuê bao Hiệu lực, hóa đơn Đã thanh toán; webhook lặp không gia hạn lần 2; sai API key → 401 | Chưa làm |
| SUB-05 | Hạn mức: interface `IQuotaService.TryConsumeAsync` (module Contracts gọi trong cùng transaction khi gửi ký), cập nhật nguyên tử (`UPDATE … WHERE used < limit`), chặn khi thuê bao Hết hạn | F20, TC15, TC21 | 0.5d | 2 request đồng thời khi còn 1 lượt → chỉ 1 thành công | Chưa làm |
| SUB-06 | Xuất hóa đơn PDF (hóa đơn nội bộ / biên nhận thanh toán): thông tin doanh nghiệp, gói, kỳ, số tiền, mã giao dịch SePay; tải từ lịch sử hóa đơn | F19, TC16 | 0.5d | PDF mở được, tiếng Việt đúng, số liệu khớp hóa đơn | Chưa làm |
| INF-04 | Đường hầm công khai (cloudflared hoặc ngrok) để SePay gọi về máy dev | – | 0.25d | Webhook tới được máy local | Chưa làm |
| FE-03 | Trang bảng giá công khai, "Gói của tôi" (đã dùng / hạn mức, ngày hết hạn), trang thanh toán QR tự cập nhật khi đã trả, lịch sử hóa đơn, trang quản trị gói | F17–F20 | 1d | Luồng nâng gói chạy trên trình duyệt | Chưa làm |
| DOC-02 | Chương 2: pháp lý (Luật Giao dịch điện tử 2023, chữ ký điện tử và chữ ký số), mô hình SaaS đa khách thuê | – | song song | Bản nháp | Chưa làm |

## Tuần 3 – 02/11 → 08/11: Hợp đồng nháp và gửi ký

**Mốc M3 (08/11):** hợp đồng nháp hoàn chỉnh (từ mẫu hoặc PDF tải lên), gửi ký được và trừ hạn mức. Tổng ước lượng: 6.25d.

| Mã | Việc | F/TC | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| CON-01 | Nền đa khách thuê dùng chung cho mọi module: `ITenantContext` lấy từ JWT, cột `TenantId` + global query filter cho mọi entity | F15, TC10, TC14 | 0.75d | Test: user DN A đọc mã hợp đồng DN B → 404 | Chưa làm |
| CON-02 | MinIO: lưu file, tính SHA-256 khi tải lên, tải file qua URL ký ngắn hạn | F02 | 0.5d | Upload PDF, lưu được hash | Chưa làm |
| CON-03 | Mẫu hợp đồng: CRUD, nội dung HTML có biến `{{ten_ben_a}}`, khai báo danh sách biến, dùng chung trong DN | F21 | 0.75d | Tạo / sửa / xóa mẫu qua API | Chưa làm |
| CON-04 | Soạn từ mẫu: điền biến, xem trước, sinh PDF (đề xuất iText pdfHTML cho đồng bộ với iText ký; thử font tiếng Việt ngay ngày đầu) | F22, TC19 | 1d | PDF đúng giá trị đã điền, tiếng Việt hiển thị đúng | Chưa làm |
| CON-05 | Hợp đồng nháp: người ký (tên, email, thứ tự, tối đa 5), hạn ký, vị trí ô ký (trang, x, y, rộng, cao) | F03, F04 | 0.75d | API lưu và đọc lại được | Chưa làm |
| CON-06 | Gửi ký: kiểm thuê bao + gọi `IQuotaService` trong cùng transaction, khóa nội dung bằng hash, trạng thái Nháp → Chờ ký, phát sự kiện mời người ký thứ nhất | F05, TC15, TC21 | 0.5d | Gửi được và trừ hạn mức; vượt hạn mức → hợp đồng vẫn Nháp | Chưa làm |
| FE-04 | Màn hình mẫu hợp đồng (soạn HTML đơn giản, khai biến), soạn hợp đồng từ mẫu, tải PDF lên | F21, F22, F02 | 1d | Luồng tạo nháp chạy trên trình duyệt | Chưa làm |
| FE-05 | Đặt ô ký bằng pdf.js (kéo thả theo từng người ký), form người ký, nút gửi ký + thông báo vượt hạn mức mời nâng gói | F03–F05 | 1d | Gửi ký được từ giao diện | Chưa làm |
| DOC-03 | Chương 3: yêu cầu chức năng và phi chức năng, sơ đồ use case, sơ đồ kiến trúc | – | song song | Bản nháp | Chưa làm |

## Tuần 4 – 09/11 → 15/11: Luồng ký

**Mốc M4 (15/11):** 3 người ký lần lượt end-to-end, chưa cần PAdES thật. **Dự phòng:** trượt M4 thì bỏ dấu thời gian TSA và nhắc ký tự động. Tổng ước lượng: 6d.

| Mã | Việc | F/TC | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| SIG-02 | Máy trạng thái hợp đồng và lượt ký (Chờ ký, Đang ký, Hoàn tất, Từ chối, Đã hủy, Hết hạn); chỉ đúng lượt mới được ký | F05–F10, TC05 | 0.75d | Unit test bảng chuyển trạng thái; TC05 pass | Chưa làm |
| SIG-03 | Link ký có token riêng; OTP email 6 số, hết hạn 5 phút, sai 5 lần thì khóa phiên | F06, TC06 | 1d | TC06 pass | Chưa làm |
| SIG-04 | Ký (bản tạm): nhận ảnh chữ ký vẽ tay, idempotency key, ghi nhận lượt ký, chuyển lượt và mời người kế tiếp | F07, TC07 | 1d | Bấm ký 2 lần → chỉ 1 chữ ký | Chưa làm |
| SIG-05 | Từ chối kèm lý do; người tạo hủy; tác vụ nền quét quá hạn → Hết hạn, link vô hiệu; hạn mức đã trừ không hoàn lại | F08, F09, TC08, TC09 | 0.75d | TC08, TC09 pass | Chưa làm |
| AUD-01 | Bảng sự kiện nhật ký: mỗi bản ghi có `prev_hash`, `hash = SHA-256(prev_hash ‖ nội dung chuẩn hóa)`, ghi tuần tự theo hợp đồng; gắn vào mọi sự kiện từ tuần 3 | F11 | 0.75d | Test chuỗi hợp lệ và chuỗi gãy | Chưa làm |
| NTF-01 | Module thông báo email (SMTP, mẫu HTML): mời ký, OTP, từ chối, hoàn tất | F24 | 0.5d | Mail hiện ở Mailpit | Chưa làm |
| FE-06 | Trang ký công khai (không cần tài khoản): nhập OTP, xem PDF, vẽ chữ ký, ký hoặc từ chối; trang chi tiết hợp đồng có dòng thời gian | F06–F09 | 1.25d | 3 người ký lần lượt trên trình duyệt | Chưa làm |
| DOC-04 | Chương 3: sơ đồ trạng thái (hợp đồng, thuê bao), sơ đồ tuần tự luồng ký và luồng thanh toán | – | song song | Bản nháp | Chưa làm |

## Tuần 5 – 16/11 → 22/11: PKI, PAdES, hoàn tất

**Mốc M5 (22/11):** TC01–TC03 và TC05–TC09 pass; file đã ký mở trên Adobe và Foxit hiển thị mọi chữ ký hợp lệ. Tổng ước lượng: 6.25d.

| Mã | Việc | F/TC | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| PKI-01 | Đưa spike SIG-01 vào module Pki: khởi tạo Root CA + Issuing CA lúc cài đặt; khóa riêng mã hóa AES-GCM (khóa chủ từ cấu hình) lưu DB | F07 | 1d | Có lệnh xuất Root CA (.cer) để cài vào Adobe | Chưa làm |
| PKI-02 | Cấp chứng thư X.509 cho người ký ngay lúc ký (CN = tên, email), lưu và thu hồi (danh sách thu hồi đơn giản) | F07, F14 | 0.75d | Chứng thư do Issuing CA cấp, thu hồi được | Chưa làm |
| SIG-06 | Thay SIG-04 bằng ký PAdES thật: mỗi lượt thêm một chữ ký (incremental update), ảnh chữ ký đặt đúng ô | F07, TC01 | 1.25d | Sau 3 lượt file có 3 chữ ký hợp lệ | Chưa làm |
| SIG-07 | Hoàn tất: dấu số của hệ thống + dấu thời gian TSA (FreeTSA, RFC 3161), gửi bản cuối cho mọi bên | F10, TC01, TC02 | 1d | Adobe + Foxit: tất cả chữ ký hợp lệ | Chưa làm |
| SIG-08 | Test tự động: sửa 1 byte → kiểm bằng iText báo không hợp lệ | TC03 | 0.25d | TC03 pass | Chưa làm |
| SIG-09 | Chữ ký mẫu: bảng `signing.user_signatures`, lưu ảnh PNG vào MinIO, API thêm / sửa / xóa / đặt mặc định; trang ký dùng được chữ ký đã lưu | F27 | 0.75d | Chỉ chủ tài khoản đọc được chữ ký của mình | Chưa làm |
| FE-07 | Tải bản đã ký, hiển thị thông tin chữ ký trong chi tiết hợp đồng | F10 | 0.5d | | Chưa làm |
| IDN-06 | Đổi mật khẩu, quên mật khẩu qua email (+ màn hình) | F01 | 0.75d | Đặt lại mật khẩu xong thì mọi phiên cũ bị thu hồi | Chưa làm |
| DOC-05 | Chương 2: hàm băm, khóa công khai, PKI, X.509, CMS, PAdES, RFC 3161 | – | song song | Bản nháp | Chưa làm |

## Tuần 6 – 23/11 → 29/11: Xác minh, tra cứu, thống kê, quản trị

**Mốc M6 (29/11):** đủ 27 chức năng ở mức tối thiểu. Tuần này nặng nhất (7.75d): thiếu thời gian thì cắt F27 (FE-09, FE-10) trước tiên, theo thứ tự ở mục Rủi ro.

| Mã | Việc | F/TC | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| AUD-02 | Trang xác minh công khai: tải file hoặc nhập mã hợp đồng → kiểm chữ ký, chứng thư, trạng thái thu hồi, chuỗi nhật ký; chỉ ra vị trí gãy | F12, TC03, TC04 | 1.25d | Sửa 1 dòng nhật ký trong DB → trang báo đúng sự kiện bị gãy | Chưa làm |
| CON-07 | Tra cứu hợp đồng theo tên, bên ký, trạng thái, khoảng thời gian, có phân trang; Nhân viên chỉ thấy hợp đồng mình tạo, Quản lý và Chủ DN thấy cả DN | F23, TC14, TC20 | 0.75d | TC14 pass | Chưa làm |
| RPT-01 | Thống kê doanh nghiệp: hợp đồng theo trạng thái, theo tháng, mức đã dùng so với hạn mức (bảng điều khiển) | F13, F25, TC22 | 0.75d | Số liệu khớp dữ liệu thử | Chưa làm |
| RPT-02 | Thống kê nền tảng: số doanh nghiệp, thuê bao theo gói, doanh thu theo tháng | F26, TC22 | 0.5d | Số liệu khớp dữ liệu thử | Chưa làm |
| ADM-01 | Quản trị nền tảng: khóa / mở doanh nghiệp và người dùng (bị chặn khi `tenant_status` là khóa), thu hồi chứng thư, xem nhật ký | F14 | 1d | DN bị khóa gọi API → 403 | Chưa làm |
| NTF-02 | Email gói sắp hết hạn (tác vụ nền); nhắc ký tự động (cắt được) | F24 | 0.25d | Mail hiện ở Mailpit | Chưa làm |
| FE-08 | Màn hình xác minh công khai, tra cứu, bảng điều khiển có biểu đồ, khu quản trị nền tảng | F12–F14, F23, F25, F26 | 1.5d | | Chưa làm |
| FE-09 | Trang "Chữ ký của tôi": vẽ tay, gõ tên chọn font, tải ảnh; đặt mặc định; nút "Dùng chữ ký đã lưu" ở trang ký | F27 | 1d | Lưu và dùng lại được cả 3 kiểu chữ ký | Chưa làm |
| FE-10 | Tách chữ ký từ ảnh chụp trên trình duyệt: ảnh xám → ngưỡng Otsu → nền trong suốt → cắt sát nét → thanh chỉnh độ đậm (xử lý ảnh thường, không gọi là AI) | F27, TC23 | 0.75d | TC23 pass với 3 ảnh chụp thật khác nhau | Chưa làm |
| DOC-06 | Chương 2: hash-chain; Chương 3: thiết kế đa khách thuê, phân quyền, bảo mật, ERD (một database, mỗi module một schema) | – | song song | Bản nháp | Chưa làm |

## Tuần 7 – 30/11 → 06/12: Kiểm thử, triển khai, đóng băng

**Mốc M7 (04/12):** đóng băng mã nguồn, hệ thống chạy trên tên miền thật, có kết quả đủ 23 kịch bản. 05–06/12 là thời gian dự trữ. Tổng ước lượng: 5.25d.

| Mã | Việc | F/TC | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| QA-01 | Chạy đủ TC01–TC23, ghi bảng kết quả (đạt / không, ảnh bằng chứng) | TC01–TC23 | 1.5d | 23/23 có kết quả | Chưa làm |
| QA-02 | Bổ sung integration test (Testcontainers) cho đa khách thuê, hạn mức đồng thời, webhook, OTP | TC14, TC17, TC21, TC06 | 1d | CI xanh | Chưa làm |
| DEP-01 | VPS + tên miền + HTTPS (Caddy hoặc Nginx), compose production (ứng dụng + PostgreSQL + MinIO), SMTP thật (Gmail app password hoặc Brevo), webhook SePay trỏ về tên miền | – | 1d | Chạy trọn luồng trên tên miền thật | Chưa làm |
| DEP-02 | Seed dữ liệu demo: admin, 2 DN (A, B), 3 gói, mẫu hợp đồng, hợp đồng ở mọi trạng thái, dữ liệu thống kê nhiều tháng | TC22 | 0.5d | Bảng điều khiển có số liệu | Chưa làm |
| QA-03 | Sửa lỗi, rà giao diện trên điện thoại | – | 1d | Không còn lỗi chặn luồng demo | Chưa làm |
| REL-01 | 04/12: tag `v1.0` trên `develop`, README hướng dẫn cài đặt, xuất tài liệu API (Swagger) | – | 0.25d | Có tag trên GitHub | Chưa làm |

## Tuần 8–9 – 07/12 → 20/12: Viết báo cáo, nộp 21/12

Gửi bản đầy đủ cho GVHD ngày 14/12, nộp chính thức ngày 21/12. Bố cục theo file mẫu của Khoa (`Mẫu-KLTN.pdf`): lời mở đầu, 4 chương, kết luận. Tổng ước lượng: 10d.

| Mã | Việc | Ước lượng | Xong khi | Trạng thái |
| --- | --- | --- | --- | --- |
| DOC-07 | Hoàn thiện Chương 3: đặc tả use case F01–F27, ERD vật lý (một database, mỗi module một schema) | 2d | Chương 3 đủ mục theo đề cương | Chưa làm |
| DOC-08 | Chương 4: môi trường, cài đặt từng phân hệ (trích đoạn mã then chốt), giao diện (ảnh chụp), kết quả TC01–TC23, triển khai VPS | 3d | Chương 4 đủ mục | Chưa làm |
| DOC-09 | Lời mở đầu, Kết luận (đạt được, hạn chế, hướng phát triển), tài liệu tham khảo, phụ lục (cài đặt, API) | 1d | | Chưa làm |
| DOC-10 | Định dạng theo mẫu: mục lục, danh mục hình và bảng, đánh số, trích dẫn | 1d | | Chưa làm |
| DOC-11 | Gửi bản đầy đủ cho GVHD 14/12, sửa theo góp ý | 2d | Đã xử lý hết góp ý | Chưa làm |
| DOC-12 | 19/12 → 21/12: chỉnh sửa cuối, in và nộp theo quy định Khoa | 1d | Nộp đúng hạn 21/12 | Chưa làm |

## Chuẩn bị bảo vệ trước hội đồng

Ngày bảo vệ chưa biết; giả định sau 21/12. Slide bắt đầu nháp từ 15/12, song song với chỉnh báo cáo. Mục tiêu: trình bày 15 phút, trong đó demo khoảng 8 phút.

| Mã | Việc | Xong khi | Trạng thái |
| --- | --- | --- | --- |
| DEF-01 | Slide 18–20 trang theo dàn ý bên dưới | Trình bày vừa 15 phút | Chưa làm |
| DEF-02 | Kịch bản demo trên VPS theo thứ tự bên dưới | Chạy trọn không lỗi 3 lần liên tiếp | Chưa làm |
| DEF-03 | Dự phòng: video quay sẵn toàn bộ demo, bản chạy local bằng compose trên laptop, tài khoản demo ghi sẵn | Mất mạng vẫn demo được | Chưa làm |
| DEF-04 | Bộ câu hỏi dự kiến (bảng bên dưới), tập trả lời thành tiếng | Trả lời mỗi câu trong 1 phút | Chưa làm |
| DEF-05 | Tập dượt 2–3 lần có bấm giờ (nhờ bạn hoặc GVHD nghe thử), chỉnh slide | Đúng giờ, không vấp | Chưa làm |

**Dàn ý slide:**

1. Đặt vấn đề và lý do chọn đề tài
2. Mục tiêu và phạm vi (27 chức năng, 3 phân hệ, 7 module)
3. Khảo sát VNPT eContract, FPT.eContract, DocuSign và khoảng trống
4. Kiến trúc: modular monolith 7 module, vì sao không chọn microservices
5. Đa khách thuê: mã doanh nghiệp trong JWT, global query filter
6. Thuê bao, hạn mức và thanh toán SePay
7. Luồng ký tuần tự nhiều bên, OTP
8. PKI nội bộ và chữ ký PAdES
9. Nhật ký hash-chain và trang xác minh
10. Bảo mật: JWT RS256, JWKS, xoay vòng refresh token
11. Cơ sở dữ liệu
12. Demo
13. Kết quả kiểm thử TC01–TC23
14. Hạn chế và hướng phát triển

**Kịch bản demo (~8 phút):**

1. Quản trị nền tảng xem danh sách gói.
2. Doanh nghiệp mới đăng ký, nhận gói dùng thử, mời một thành viên.
3. Thanh toán SePay nâng gói; thuê bao chuyển Hiệu lực, có hóa đơn.
4. Tạo mẫu, soạn hợp đồng từ mẫu, đặt ô ký cho 3 người, gửi ký.
5. 3 người ký lần lượt bằng OTP email.
6. Mở file đã ký trong Adobe Reader: mọi chữ ký hợp lệ.
7. Sửa 1 byte trong file → trang xác minh báo file đã bị thay đổi.
8. Sửa 1 dòng nhật ký trong DB → trang xác minh chỉ ra chỗ chuỗi gãy.
9. Đăng nhập user doanh nghiệp B, thử mở hợp đồng của A → bị từ chối.
10. Xem thống kê doanh nghiệp và thống kê nền tảng.

**Câu hỏi dự kiến của hội đồng:**

| Câu hỏi | Ý chính cần trả lời |
| --- | --- |
| Vì sao dùng chung database thay vì database riêng cho mỗi doanh nghiệp? | Chi phí và vận hành cho DN vừa và nhỏ; cách ly bằng cột mã DN + global query filter; DB riêng là hướng phát triển |
| Làm sao chắc DN A không đọc được dữ liệu DN B? | Mã DN lấy từ JWT, không từ request; filter tự thêm vào mọi truy vấn; TC10, TC14 |
| Vì sao RS256 + JWKS mà không dùng HS256? | Khóa bí mật chỉ dùng để ký; bên ngoài (và module tách ra sau này) kiểm token chỉ với khóa công khai; xoay khóa bằng `kid` |
| Refresh token bị lộ thì sao? | Xoay vòng mỗi lần dùng; dùng lại token cũ → thu hồi phiên |
| PKI nội bộ có giá trị pháp lý không? | Không phải CA được cấp phép; đề tài mô phỏng và thiết kế sẵn adapter cho CA công cộng; chỉ phân tích, không xác nhận hiệu lực pháp lý |
| PAdES khác ký điện tử bằng OTP thế nào? | OTP xác thực người ký; PAdES gắn chữ ký số vào file, ai cũng kiểm được bằng Adobe mà không cần tin hệ thống |
| Hash-chain khác blockchain thế nào? | Không phân tán, không đồng thuận; chỉ phát hiện sửa đổi, đủ cho bài toán và rẻ |
| Webhook thanh toán bị giả mạo hoặc gửi lặp? | Xác thực API key, đối chiếu mã đơn + số tiền, idempotent theo mã giao dịch; TC17 |
| Hai người gửi ký cùng lúc khi còn 1 lượt? | Cập nhật nguyên tử có điều kiện trong DB; TC21 |
| Khóa riêng người ký lưu ở đâu? | Máy chủ, mã hóa AES-GCM, mô hình ký từ xa; hướng phát triển: HSM |
| Vì sao chọn monolith mà không phải microservices? | Một người, 10 tuần, tải thấp; gửi ký phải trừ hạn mức trong cùng transaction. Ranh giới module giữ rõ (thư mục, schema, chỉ gọi qua interface) nên tách service sau được; monolith-first (Fowler) |
| Ứng dụng sập thì sao? | Health check + Docker tự khởi động lại; dữ liệu nằm ở PostgreSQL nên không mất; cần mở rộng thì chạy nhiều bản sau load balancer |
| Tải ảnh chữ ký của người khác lên thì sao? | Ảnh chỉ là phần hiển thị; giá trị nằm ở chữ ký số PAdES gắn chứng thư cấp cho người đã xác thực OTP, nên file vẫn ghi đúng danh tính người ký thật |
| Hóa đơn có phải hóa đơn điện tử theo luật thuế không? | Không; là hóa đơn nội bộ / biên nhận thanh toán dạng PDF. Hóa đơn điện tử có mã của cơ quan thuế nằm ngoài phạm vi |

## Rủi ro và phương án

| Rủi ro | Dấu hiệu | Phương án |
| --- | --- | --- |
| PAdES không hiển thị hợp lệ trên Adobe | SIG-01 chưa xong 25/10 | Chữ ký CMS tách rời + xác minh trên trang của hệ thống |
| SePay không có môi trường thử phù hợp | SUB-01 | Dùng giả lập giao dịch của SePay hoặc tự gửi webhook mẫu có API key; vẫn giữ luồng QR |
| Một module lỗi làm sập cả ứng dụng (monolith) | Lỗi lúc chạy thử hoặc demo | Test tự động từng module, xử lý lỗi tập trung, health check + Docker tự khởi động lại; luôn có bản chạy local dự phòng |
| Sinh PDF tiếng Việt lỗi font | Ngày đầu của CON-04 | Nhúng font Unicode; đổi thư viện (QuestPDF hoặc PuppeteerSharp) |
| Webhook không tới được máy dev | INF-04 | Triển khai sớm một bản ứng dụng lên VPS ngay tuần 2 |
| Trễ tiến độ chung | Trượt mốc M4 | Cắt theo thứ tự bên dưới |

**Thứ tự cắt:** (1) F27 chữ ký mẫu và tách chữ ký từ ảnh → (2) dấu thời gian TSA → (3) nhắc ký tự động, đổi gói giữa kỳ, xuất báo cáo ra file → (4) PKI hai cấp rút còn một CA → (5) F26 rút còn một bảng số liệu → (6) cuối cùng mới thay PAdES bằng OTP + mã băm file + nhật ký.

**Không cắt** (ghi đích danh trong tờ nhiệm vụ): đa khách thuê, gói và hạn mức, thanh toán, mẫu hợp đồng, ký nhiều bên, xác minh, phân quyền, thống kê doanh nghiệp.

## Đối chiếu chức năng và kịch bản với task

Mỗi chức năng F01–F27 và mỗi kịch bản TC01–TC23 trong đề cương đều có ít nhất một task.

| Chức năng | Task | Kịch bản kiểm chứng |
| --- | --- | --- |
| F01 Tài khoản | IDN-01, IDN-02, IDN-03, IDN-06, FE-01 | TC11 |
| F02 Tải hợp đồng | CON-02, FE-04 | – |
| F03 Đặt vị trí ký | CON-05, FE-05 | – |
| F04 Cấu hình người ký | CON-05, FE-05 | – |
| F05 Gửi ký | CON-06, SIG-02 | TC15, TC21 |
| F06 Xác thực người ký | SIG-03, FE-06 | TC05, TC06 |
| F07 Ký hợp đồng | SIG-01, SIG-04, PKI-01, PKI-02, SIG-06 | TC01, TC07 |
| F08 Từ chối ký | SIG-05 | TC08 |
| F09 Hủy và hết hạn | SIG-05 | TC09 |
| F10 Hoàn tất | SIG-07, FE-07 | TC01, TC02 |
| F11 Nhật ký hash-chain | AUD-01 | TC04 |
| F12 Xác minh | AUD-02, SIG-08, FE-08 | TC03, TC04 |
| F13 Bảng điều khiển | RPT-01, FE-08 | TC22 |
| F14 Quản trị nền tảng | ADM-01, PKI-02 | – |
| F15 Doanh nghiệp thuê | IDN-04, CON-01, FE-02 | TC10, TC14 |
| F16 Thành viên và vai trò | IDN-05, FE-02 | TC20 |
| F17 Gói dịch vụ | SUB-02, FE-03 | – |
| F18 Đăng ký và gia hạn | SUB-03, FE-03 | TC18 |
| F19 Thanh toán | SUB-01, SUB-04, SUB-06, INF-04 | TC16, TC17 |
| F20 Hạn mức | SUB-05 | TC15, TC21 |
| F21 Mẫu hợp đồng | CON-03, FE-04 | – |
| F22 Soạn từ mẫu | CON-04, FE-04 | TC19 |
| F23 Lưu trữ và tra cứu | CON-07, FE-08 | TC14 |
| F24 Thông báo email | NTF-01, NTF-02 | – |
| F25 Thống kê doanh nghiệp | RPT-01 | TC22 |
| F26 Thống kê nền tảng | RPT-02 | TC22 |
| F27 Chữ ký mẫu | SIG-09, FE-09, FE-10 | TC23 |

TC12 (header `X-User-Id` giả mạo) và TC13 (vượt rate limit → 429) đã có trong code khung TrustSign.Api: danh tính chỉ lấy từ JWT, rate limiter có test. Kiểm lại ở QA-01.
