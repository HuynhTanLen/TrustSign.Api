# Prompt làm giao diện Chữ Tín

Bộ prompt để dán vào công cụ AI làm giao diện (v0, Claude, Cursor…) khi làm các task FE trong [kế hoạch](KE-HOACH.md). Frontend nằm ở repo riêng `D:\KLTN\TrustSign.Frontend`.

## Cách dùng

1. Mở phiên mới, dán **Prompt chung** một lần. Nếu đã có code giao diện kiểu cũ, dán tiếp **Prompt đổi giao diện phần đã làm** trước khi làm màn hình mới.
2. Dán từng **Prompt màn hình** theo thứ tự task (FE-01 → FE-10). Mỗi lần một màn hình; chạy `npm run dev`, bấm thử được rồi mới làm tiếp.
3. Khi backend có endpoint thật, sửa đường dẫn trong `src/lib/api/<module>.ts` cho khớp (endpoint trong prompt chỉ là dự kiến theo SRS).
4. Kết quả AI sinh ra phải được đọc lại trước khi commit: kiểm tra chữ tiếng Việt, phân quyền theo vai trò và không có dữ liệu giả lọt vào bản chính.

## Prompt chung

```text
Bạn đang làm frontend cho "Chữ Tín (TrustSign)": nền tảng SaaS cho thuê dịch vụ quản lý và ký hợp đồng điện tử, dành cho doanh nghiệp vừa và nhỏ ở Việt Nam. Doanh nghiệp đăng ký gói, soạn hợp đồng từ mẫu, gửi cho nhiều bên ký tuần tự, lưu trữ, tra cứu và xác minh hợp đồng đã ký. Toàn bộ chữ trên giao diện bằng tiếng Việt có dấu.

NGƯỜI DÙNG
- Quản trị nền tảng: quản lý gói dịch vụ, doanh nghiệp, người dùng, chứng thư số; xem thống kê toàn nền tảng.
- Chủ doanh nghiệp: tạo doanh nghiệp, chọn và thanh toán gói, mời thành viên, gán vai trò; có mọi quyền của Quản lý hợp đồng.
- Quản lý hợp đồng: tạo mẫu, xem và quản lý mọi hợp đồng của doanh nghiệp, xem thống kê.
- Nhân viên: soạn hợp đồng từ mẫu, gửi ký, chỉ thấy hợp đồng do mình tạo.
- Người ký ngoài: không có tài khoản; mở link trong email, xác thực OTP, xem và ký hoặc từ chối.
- Người xác minh: bất kỳ ai; tải file hoặc nhập mã hợp đồng để kiểm tra tính hợp lệ.
Một tài khoản có thể thuộc nhiều doanh nghiệp; vai trò tính theo doanh nghiệp đang chọn.

KỸ THUẬT (dự án đã có sẵn, không tạo dự án mới)
- Next.js 16 App Router, React 19, TypeScript, Tailwind CSS 4. Next 16 có thay đổi phá vỡ: đọc AGENTS.md và tài liệu trong node_modules/next/dist/docs/ trước khi viết code.
- Chỉ thêm các thư viện: lucide-react (icon), react-hook-form + zod (form và kiểm tra dữ liệu), pdfjs-dist (hiển thị PDF, đặt ô ký), recharts (biểu đồ). Không thêm thư viện UI lớn. Component dùng chung tự viết trong src/components/ui/ (Button, Input, Select, Dialog, Badge, Table, Tabs, Toast, Skeleton, EmptyState).
- Gọi API chỉ qua hàm apiFetch có sẵn trong src/lib/api.ts. Nó đã lo: địa chỉ backend (NEXT_PUBLIC_API_URL, mặc định http://localhost:5050), gắn access token giữ trong bộ nhớ, gửi cookie refresh, và ném ApiError(status, code, correlationId, message) khi lỗi theo Problem Details. Mỗi module có file src/lib/api/<module>.ts chứa type và hàm gọi; endpoint ghi chú "dự kiến".
- Khi API trả 401, gọi POST /api/auth/refresh một lần rồi thử lại; vẫn 401 thì đưa về trang đăng nhập.

BỐ CỤC
- Khu công khai (không cần đăng nhập): trang chủ, bảng giá, đăng nhập, đăng ký, trang ký /sign/[token], trang xác minh /verify. Thanh trên đơn giản có logo và nút Đăng nhập.
- Khu ứng dụng (cần đăng nhập): sidebar trái gồm ô chọn doanh nghiệp đang làm việc và menu Bảng điều khiển, Hợp đồng, Mẫu hợp đồng, Chữ ký của tôi, Gói và thanh toán, Thành viên; thanh trên có tên người dùng và menu (Hồ sơ, Đổi mật khẩu, Đăng xuất). Menu chỉ hiện mục người dùng có quyền. Quản trị nền tảng có khu /admin riêng.
- Trên điện thoại (bề ngang 375px) sidebar thu thành nút menu; bảng chuyển thành danh sách thẻ.

PHONG CÁCH (giống phần mềm quản lý văn phòng thông thường, không giống trang mẫu do AI sinh)
- Font hệ thống, không tải font ngoài, không dùng next/font: font-family: "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif (đều có tiếng Việt). Chữ nội dung 14px, tiêu đề trang 20px, tiêu đề khối 16px, đậm tối đa 600. Không dùng chữ 800–900, không chữ dưới 12px, không tracking-tight.
- Bảng màu cố định, khai báo một lần trong @theme của globals.css và chỉ dùng các biến này; không dùng thẳng màu Tailwind (cyan, teal, indigo, violet, purple, slate-950…):
  --color-primary: #1f5fa8 (nút chính, link, mục menu đang chọn); --color-primary-hover: #194d89
  --color-fg: #1f2328 (chữ chính); --color-muted: #5f6b7a (chữ phụ)
  --color-line: #d0d7de (viền); --color-page: #f5f6f8 (nền trang); --color-surface: #ffffff (thẻ, bảng, form)
  --color-selected: #e8f0fa (nền mục đang chọn, hàng đang chọn)
  --color-danger: #c62828; --color-success: #2e7d32; --color-warning: #b26a00
- Chỉ chế độ sáng, không viết class dark:.
- Hình khối: bo góc 4px cho nút, ô nhập, thẻ, bảng; 6px cho hộp thoại; rounded-full chỉ cho avatar. Tách khối bằng viền 1px màu --color-line, không đổ bóng; chỉ hộp thoại và menu thả xuống có bóng nhẹ.
- Nút cao 36px, một màu đặc, chữ thường. Nút chính nền --color-primary chữ trắng; nút phụ nền trắng viền --color-line; nút nguy hiểm nền --color-danger.
- KHÔNG dùng: gradient (kể cả logo, chữ, nền), backdrop-blur / kính mờ, phát sáng, chữ chuyển màu, nhãn viên thuốc kiểu "Mới", emoji, icon trang trí trước mọi tiêu đề, nền chấm hoặc lưới, hoạt ảnh khi cuộn, thẻ nhấc lên khi rê chuột.
- Icon lucide chỉ dùng khi có chức năng (menu sidebar, nút hành động, trạng thái), cỡ 16px, cùng màu chữ.
- Logo là chữ "Chữ Tín" màu --color-primary, đậm 600; không ô vuông chứa chữ cái, không gradient.
- Mật độ như phần mềm quản lý: bảng có kẻ dòng, hàng cao 40px, đầu bảng nền --color-page chữ 13px đậm 600; form xếp nhãn ở trên ô nhập; nội dung rộng tối đa 1200px; sidebar nền trắng có viền phải, mục đang chọn nền --color-selected chữ --color-primary.
- Trang chủ đơn giản: thanh trên, tên sản phẩm + một câu mô tả, nút Đăng ký và Đăng nhập, ba chức năng chính dạng tiêu đề + một dòng mô tả, chân trang. Không hero khổng lồ, không số liệu ảo, không lời chứng thực khách hàng giả.
- Câu chữ ngắn, rõ, giọng hành chính; không dùng từ quảng cáo ("thế hệ mới", "đột phá", "vượt trội"), không dấu chấm than.
- Badge: nền nhạt, chữ đậm cùng tông, bo 3px, chữ 12px. Trạng thái hợp đồng, màu cố định: Nháp (xám), Chờ ký (vàng), Đang ký (xanh dương), Hoàn tất (xanh lá), Từ chối (đỏ), Đã hủy (xám đậm), Hết hạn (cam).
- Badge thuê bao: Dùng thử (xanh dương), Hiệu lực (xanh lá), Hết hạn (đỏ).

QUY ƯỚC
- Ngày dạng dd/MM/yyyy, giờ HH:mm; tiền dạng 1.000.000 ₫.
- Mọi màn hình có đủ: đang tải (skeleton), rỗng (lời giải thích + nút hành động), lỗi (thông báo thân thiện + "Mã theo dõi: <correlationId>" để báo lỗi).
- Nút gửi form bị khóa khi đang gửi; thông báo thành công bằng toast.
- Truy cập được bằng bàn phím, mọi input có label, ảnh và icon có mô tả cho trình đọc màn hình.
- Không nhúng dữ liệu giả vào component. Nếu cần dữ liệu mẫu để xem giao diện, đặt trong src/lib/mock/ và bật bằng biến NEXT_PUBLIC_USE_MOCK.

BẢN ĐỒ ROUTE
| Đường dẫn | Màn hình | Ai dùng |
| / | Trang chủ giới thiệu | Công khai |
| /pricing | Bảng giá | Công khai |
| /login, /register, /verify-email, /forgot-password, /reset-password | Tài khoản | Công khai |
| /onboarding | Tạo doanh nghiệp đầu tiên | Người dùng mới |
| /app | Bảng điều khiển | Thành viên |
| /app/contracts, /app/contracts/new, /app/contracts/[id] | Hợp đồng | Thành viên |
| /app/templates, /app/templates/[id] | Mẫu hợp đồng | Chủ DN, Quản lý |
| /app/signatures | Chữ ký của tôi | Mọi người dùng |
| /app/billing, /app/billing/checkout/[invoiceCode] | Gói và thanh toán | Chủ DN |
| /app/members | Thành viên và lời mời | Chủ DN |
| /sign/[token] | Ký hợp đồng | Người ký |
| /verify | Xác minh hợp đồng | Công khai |
| /admin, /admin/plans, /admin/tenants, /admin/users, /admin/certificates | Quản trị nền tảng | Quản trị nền tảng |

Chờ tôi gửi yêu cầu từng màn hình. Mỗi lần chỉ làm đúng màn hình được yêu cầu cùng các component nó cần.
```

## Prompt đổi giao diện phần đã làm

Dán sau Prompt chung khi code đã sinh trước đó vẫn theo kiểu cũ (gradient, xanh ngọc, bo tròn lớn, font Geist).

```text
Giao diện hiện tại trông như trang mẫu do AI sinh. Áp lại mục PHONG CÁCH ở trên cho toàn bộ code đang có. Không đổi chức năng, API, route hay câu chữ nghiệp vụ.

1. src/app/layout.tsx: bỏ next/font (Geist, Geist_Mono) và các biến font; body dùng font hệ thống, nền --color-page, chữ --color-fg.
2. src/app/globals.css: khai báo các biến màu trong @theme để dùng được bg-primary, text-muted, border-line, bg-page, bg-surface…; xóa khối prefers-color-scheme dark và --font-geist-*.
3. src/components/ui/: viết lại Button, Input, Badge, Skeleton, Toast, EmptyState theo quy tắc mới (bo 4px, không bóng, nút cao 36px, không focus ring màu cyan mà dùng viền --color-primary).
4. Các trang đã có (/, (auth)/*, /onboarding, layout và trang /app, gateway-status): thay màu cứng bằng biến; bỏ gradient, backdrop-blur, rounded-xl/2xl, shadow-sm/md, nhãn viên thuốc, chữ chuyển màu, ô logo chữ "T"; viết lại trang chủ đúng mô tả trong PHONG CÁCH.
5. Kiểm tra: tìm trong src/ các chuỗi gradient, backdrop-blur, rounded-xl, rounded-2xl, cyan-, teal-, indigo-, violet-, slate-, dark:, font-extrabold, font-bold, text-[11px], text-[10px], Geist. Kết quả phải rỗng (rounded-full cho avatar được giữ). Chạy npm run build không lỗi.

Cuối cùng liệt kê các file đã sửa.
```

## Prompt màn hình

### FE-01 · Tài khoản

```text
Làm các trang tài khoản: /login, /register, /verify-email, /forgot-password, /reset-password, và layout chung của khu ứng dụng (sidebar + thanh trên, chưa cần nội dung menu).

- /register: họ tên (bắt buộc, tối đa 100 ký tự), email, mật khẩu (tối thiểu 8 ký tự, có thanh độ mạnh), nhập lại mật khẩu. Thành công → màn hình "Kiểm tra email để xác minh tài khoản" có nút gửi lại (khóa 60 giây).
- /verify-email?token=...: tự gọi API khi mở; hiện thành công (nút Đăng nhập) hoặc link hết hạn (nút gửi lại).
- /login: email, mật khẩu, nút hiện/ẩn mật khẩu, link Quên mật khẩu. Lỗi sai thông tin hiện chung "Email hoặc mật khẩu không đúng". Tài khoản chưa xác minh hiện nút gửi lại email. Bị khóa tạm thời hiện thời gian còn lại.
- Sau đăng nhập: chưa thuộc doanh nghiệp nào → /onboarding, ngược lại → /app.
- /forgot-password và /reset-password?token=...: form email; form mật khẩu mới + nhập lại.

Endpoint dự kiến: POST /api/auth/register, POST /api/auth/verify-email, POST /api/auth/resend-verification, POST /api/auth/login (trả accessToken, refresh token nằm trong cookie HttpOnly), POST /api/auth/refresh, POST /api/auth/logout, GET /api/auth/me, POST /api/auth/forgot-password, POST /api/auth/reset-password.
```

### FE-02 · Doanh nghiệp và thành viên

```text
Làm /onboarding và /app/members.

- /onboarding: form tạo doanh nghiệp (tên doanh nghiệp, mã số thuế không bắt buộc, địa chỉ, email liên hệ). Thành công → gọi refresh để nhận token có doanh nghiệp mới → /app. Ghi chú: doanh nghiệp mới được tặng gói Dùng thử.
- Ô chọn doanh nghiệp ở sidebar: danh sách doanh nghiệp của tài khoản kèm vai trò; chọn doanh nghiệp khác thì gọi API đổi doanh nghiệp rồi tải lại dữ liệu.
- /app/members (chỉ Chủ doanh nghiệp): bảng thành viên (họ tên, email, vai trò, ngày tham gia), đổi vai trò bằng select (Chủ doanh nghiệp, Quản lý hợp đồng, Nhân viên), xóa thành viên có hộp thoại xác nhận. Không cho tự xóa mình hoặc xóa Chủ doanh nghiệp cuối cùng.
- Nút "Mời thành viên": hộp thoại nhập email + vai trò. Tab "Lời mời đang chờ" có gửi lại và thu hồi.
- Trang chấp nhận lời mời /invitations/[token]: chưa đăng nhập thì mời đăng nhập hoặc đăng ký rồi quay lại; đã đăng nhập thì nút Chấp nhận.
- Người không phải Chủ doanh nghiệp vào /app/members thấy thông báo không có quyền (API trả 403).

Endpoint dự kiến: POST /api/tenants, GET /api/tenants/mine, POST /api/tenants/{id}/switch, GET/PATCH/DELETE /api/tenants/current/members/{userId}, POST /api/tenants/current/invitations, GET /api/tenants/current/invitations, POST /api/invitations/{token}/accept.
```

### FE-03 · Gói dịch vụ và thanh toán SePay

```text
Làm /pricing, /app/billing, /app/billing/checkout/[invoiceCode] và /admin/plans.

- /pricing (công khai): thẻ cho từng gói (tên, giá/kỳ, thời hạn, số hợp đồng gửi ký mỗi tháng, gói Dùng thử đánh dấu "Miễn phí"). Nút "Chọn gói": chưa đăng nhập → đăng ký; đã đăng nhập → tạo hóa đơn rồi sang trang thanh toán.
- /app/billing: thẻ "Gói của tôi" (tên gói, badge trạng thái thuê bao, ngày hết hạn, thanh tiến độ đã dùng / hạn mức trong tháng). Cảnh báo khi còn dưới 7 ngày hoặc đã hết hạn kèm nút Gia hạn. Bảng lịch sử hóa đơn (mã, gói, số tiền, trạng thái Chờ thanh toán / Đã thanh toán / Hết hạn, ngày) có nút Tải PDF cho hóa đơn đã thanh toán.
- /app/billing/checkout/[invoiceCode]: ảnh QR do backend trả về (VietQR của SePay), số tài khoản, ngân hàng, số tiền, nội dung chuyển khoản bắt buộc là mã hóa đơn (nút sao chép từng dòng). Cứ 5 giây hỏi trạng thái hóa đơn; khi Đã thanh toán thì hiện màn hình thành công và nút về /app/billing. Có đồng hồ đếm hạn của hóa đơn.
- /admin/plans (Quản trị nền tảng): bảng gói, tạo và sửa gói (tên, giá, số ngày một kỳ, hạn mức hợp đồng/tháng, cờ dùng thử, đang bán hay ngừng bán).

Endpoint dự kiến: GET /api/subscription/plans, GET /api/subscription/current, POST /api/subscription/invoices (trả invoiceCode, qrImageUrl, bankAccount, bankName, amount, expiresAt), GET /api/subscription/invoices/{code}, GET /api/subscription/invoices, GET /api/subscription/invoices/{code}/pdf, POST/PUT /api/subscription/admin/plans.
```

### FE-04 · Mẫu hợp đồng và soạn từ mẫu

```text
Làm /app/templates, /app/templates/[id] và bước đầu của /app/contracts/new.

- /app/templates: bảng mẫu (tên, số biến, người tạo, ngày sửa), tìm theo tên, nút Tạo mẫu.
- /app/templates/[id]: bên trái là ô soạn nội dung HTML đơn giản (thanh công cụ: đậm, nghiêng, tiêu đề, danh sách, căn giữa) với biến dạng {{ten_bien}}; bên phải là danh sách biến tự phát hiện từ nội dung, mỗi biến có nhãn hiển thị, kiểu (chữ, số, tiền, ngày) và bắt buộc hay không. Nút Xem trước hiện nội dung với giá trị mẫu.
- /app/contracts/new, bước 1 "Nguồn hợp đồng", hai lựa chọn:
  a) Từ mẫu: chọn mẫu → form điền biến (tự sinh từ danh sách biến, kiểm tra theo kiểu) → Xem trước PDF do backend sinh.
  b) Tải PDF lên: kéo thả file (chỉ PDF, tối đa 10 MB), hiện tên file và số trang.
  Sau bước 1 có hợp đồng ở trạng thái Nháp và sang bước 2 (FE-05).

Endpoint dự kiến: GET/POST /api/templates, GET/PUT/DELETE /api/templates/{id}, POST /api/contracts/from-template (templateId, values) trả contractId, POST /api/contracts/upload (multipart) trả contractId, GET /api/contracts/{id}/document (URL tải PDF ngắn hạn).
```

### FE-05 · Người ký, đặt ô ký, gửi ký

```text
Làm bước 2 và 3 của /app/contracts/new (cũng dùng cho hợp đồng Nháp mở lại từ /app/contracts/[id]).

- Bước 2 "Người ký": danh sách tối đa 5 người (họ tên, email, thứ tự ký), kéo để đổi thứ tự, mỗi người một màu riêng. Hạn ký (ngày giờ, phải sau hiện tại).
- Bước 3 "Vị trí ký": hiển thị PDF bằng pdfjs-dist, có chuyển trang và phóng to/thu nhỏ. Chọn người ký ở cột bên rồi kéo một ô chữ ký lên trang; ô mang màu của người đó, kéo để di chuyển, kéo góc để đổi kích thước, nút xóa. Lưu tọa độ theo đơn vị điểm PDF (trang, x, y, rộng, cao) để không lệch khi phóng to. Mỗi người ký phải có ít nhất một ô.
- Nút "Gửi ký": hộp thoại tóm tắt (tên hợp đồng, danh sách người ký theo thứ tự, hạn ký) và lưu ý "Đã gửi thì không sửa được nội dung". Gửi thành công → trang chi tiết hợp đồng.
- Lỗi hết hạn mức (409, mã Quota.Exceeded) hoặc thuê bao hết hạn (403): hộp thoại giải thích, hợp đồng vẫn là Nháp, nút "Nâng gói" dẫn tới /app/billing.

Endpoint dự kiến: PUT /api/contracts/{id}/signers, PUT /api/contracts/{id}/fields, POST /api/contracts/{id}/send.
```

### FE-06 · Trang ký công khai và chi tiết hợp đồng

```text
Làm /sign/[token] (công khai, không cần tài khoản) và /app/contracts/[id].

/sign/[token]:
- Mở link → hiện tên hợp đồng, bên gửi, người ký hiện tại (email che bớt), nút "Gửi mã OTP".
- Nhập OTP 6 số (6 ô, tự nhảy ô, dán được cả chuỗi), hết hạn sau 5 phút có đồng hồ; sai thì báo còn bao nhiêu lần; sai 5 lần thì khóa phiên và mời yêu cầu mã mới.
- Sau OTP: xem PDF, các ô ký của người này được tô nổi. Bấm ô → hộp thoại tạo chữ ký: Vẽ (canvas, nút xóa) hoặc, nếu người ký có tài khoản và đang đăng nhập, "Dùng chữ ký đã lưu".
- Nút "Ký hợp đồng" (khóa sau lần bấm đầu để không ký hai lần) và "Từ chối ký" (bắt buộc nhập lý do).
- Kết quả: đã ký thành công / đã từ chối / chưa tới lượt / link hết hạn hoặc hợp đồng đã hủy, mỗi trường hợp một màn hình rõ ràng.

/app/contracts/[id]:
- Đầu trang: tên, badge trạng thái, người tạo, hạn ký, nút Hủy hợp đồng (khi đang Chờ ký hoặc Đang ký, có xác nhận).
- Danh sách người ký theo thứ tự với trạng thái từng người (Chưa tới lượt, Đang chờ, Đã ký lúc…, Từ chối kèm lý do).
- Dòng thời gian sự kiện (tạo, gửi, mở link, ký, từ chối, hoàn tất…) có giờ.
- Xem PDF hiện tại.

Endpoint dự kiến: GET /api/signing/{token}, POST /api/signing/{token}/otp, POST /api/signing/{token}/verify-otp, POST /api/signing/{token}/sign (kèm idempotencyKey, ảnh chữ ký PNG base64), POST /api/signing/{token}/decline, GET /api/contracts/{id}, GET /api/contracts/{id}/events, POST /api/contracts/{id}/cancel.
```

### FE-07 · Bản đã ký

```text
Bổ sung cho /app/contracts/[id] khi hợp đồng Hoàn tất:
- Nút "Tải bản đã ký" (PDF có chữ ký số).
- Khung "Chữ ký số" liệt kê từng chữ ký: người ký, email, thời điểm ký, đơn vị cấp chứng thư, số serial, hợp lệ hay không; dấu số của hệ thống và dấu thời gian nếu có.
- Hướng dẫn ngắn: muốn Adobe Reader hiện "chữ ký hợp lệ" thì tải và tin cậy Root CA của hệ thống (link tải file .cer).

Endpoint dự kiến: GET /api/contracts/{id}/signed-document, GET /api/contracts/{id}/signatures, GET /api/pki/root-ca.
```

### FE-08 · Xác minh, tra cứu, thống kê, quản trị

```text
Làm /verify, /app/contracts, /app (bảng điều khiển) và khu /admin.

- /verify (công khai): hai tab "Tải file PDF" và "Nhập mã hợp đồng". Kết quả: tổng kết lớn (Hợp lệ / Đã bị thay đổi / Không tìm thấy), danh sách chữ ký với trạng thái từng cái (nguyên vẹn, chứng thư còn hiệu lực hay đã thu hồi), và chuỗi nhật ký dạng dòng thời gian; nếu chuỗi gãy thì tô đỏ đúng sự kiện bị gãy và giải thích.
- /app/contracts: bảng hợp đồng (tên, bên ký, badge trạng thái, người tạo, ngày gửi, hạn ký), bộ lọc theo tên, bên ký, trạng thái (chọn nhiều), khoảng thời gian; phân trang; giữ bộ lọc trên URL. Nhân viên chỉ thấy hợp đồng mình tạo (backend đã lọc, giao diện không cần làm thêm).
- /app: thẻ số liệu (tổng hợp đồng, đang chờ ký, hoàn tất trong tháng, đã dùng / hạn mức), biểu đồ cột số hợp đồng theo tháng (6 tháng), biểu đồ tròn theo trạng thái, danh sách hợp đồng sắp hết hạn ký.
- /admin: thẻ số liệu nền tảng (số doanh nghiệp, thuê bao theo gói, doanh thu tháng), biểu đồ doanh thu 12 tháng. /admin/tenants và /admin/users: bảng có tìm kiếm, khóa / mở khóa có xác nhận và lý do. /admin/certificates: bảng chứng thư (người được cấp, serial, hiệu lực, trạng thái), nút Thu hồi.

Endpoint dự kiến: POST /api/verify/file (multipart), GET /api/verify/{contractCode}, GET /api/contracts?query=&status=&from=&to=&page=, GET /api/stats/tenant, GET /api/admin/stats, GET/POST /api/admin/tenants/{id}/lock|unlock, GET/POST /api/admin/users/{id}/lock|unlock, GET /api/admin/certificates, POST /api/admin/certificates/{serial}/revoke.
```

### FE-09 · Chữ ký của tôi

```text
Làm /app/signatures.

- Lưới thẻ chữ ký đã lưu (ảnh trên nền trắng, nhãn, dấu "Mặc định"), nút đặt mặc định và xóa.
- Nút "Thêm chữ ký" mở hộp thoại 3 tab:
  1) Vẽ: canvas có chọn độ dày nét và màu (đen, xanh dương), nút xóa.
  2) Gõ tên: ô nhập họ tên, chọn 1 trong 4 font chữ viết tay hỗ trợ tiếng Việt, xem trước.
  3) Tải ảnh: chọn ảnh chụp chữ ký trên giấy, chuyển sang bước tách chữ ký (FE-10).
- Mọi kiểu đều lưu thành PNG nền trong suốt.
- Ở trang ký /sign/[token], hộp thoại tạo chữ ký có thêm nút "Dùng chữ ký đã lưu" khi người ký đang đăng nhập.

Endpoint dự kiến: GET /api/signatures, POST /api/signatures (PNG base64, nhãn, kiểu), PUT /api/signatures/{id}/default, DELETE /api/signatures/{id}.
```

### FE-10 · Tách chữ ký từ ảnh chụp

```text
Làm bước "Tách chữ ký" cho tab Tải ảnh ở /app/signatures. Xử lý hoàn toàn trên trình duyệt bằng canvas, không gửi ảnh gốc lên server và không dùng mô hình AI.

Các bước xử lý:
1. Đọc ảnh (JPG, PNG, HEIC nếu trình duyệt hỗ trợ), thu nhỏ cạnh dài về tối đa 1600px, xoay theo EXIF.
2. Chuyển sang ảnh xám.
3. Tìm ngưỡng bằng thuật toán Otsu trên histogram; điểm tối hơn ngưỡng là nét mực.
4. Điểm nền thành trong suốt; điểm mực giữ màu gốc hoặc đổi sang đen / xanh dương theo lựa chọn.
5. Bỏ cụm điểm nhỏ lẻ (nhiễu) dưới vài điểm ảnh.
6. Cắt sát khung bao quanh nét mực, chừa lề 8px.

Giao diện:
- Hai ô xem trước cạnh nhau: ảnh gốc và kết quả trên nền ô caro (để thấy rõ phần trong suốt).
- Thanh trượt "Độ đậm" dịch ngưỡng quanh giá trị Otsu, cập nhật kết quả ngay.
- Công cụ cắt tay khi tự cắt chưa đúng; nút xoay 90 độ.
- Nút "Lưu chữ ký" gửi PNG kết quả như FE-09.
- Đặt thuật toán trong src/lib/signature-extract.ts (hàm thuần, có thể viết test), giao diện trong component riêng.
```
