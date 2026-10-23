# Chữ Tín (TrustSign) – backend

Backend của hệ thống cho thuê dịch vụ quản lý hợp đồng điện tử Chữ Tín (TrustSign), khóa luận tốt nghiệp của Huỳnh Tấn Lên. Đây là một **modular monolith**: một ứng dụng ASP.NET Core, một database PostgreSQL, chia thành bảy module có ranh giới rõ.

Dự án thay cho bản gateway + ba service (repo `TrustSign.Backend`) từ ngày 23/10/2026. Bản cũ chỉ còn để lưu trữ.

Đây là bộ khung: mới có `GET /api/auth/me`, JWKS, health check và token thử ở Development. Các chức năng nghiệp vụ được làm theo bảng task trong kế hoạch khóa luận.

## Tài liệu

- [Kế hoạch thực hiện khóa luận](docs/KE-HOACH.md): task theo tuần, mốc, rủi ro, chuẩn bị bảo vệ.
- [Prompt làm giao diện](docs/UI-PROMPT.md): prompt chung và prompt từng màn hình cho frontend Next.js.

## Cấu trúc

```
src/
  Common/        Result, Error, danh mục lỗi, enum, tên claim. Không phụ thuộc project nào.
  Domain/        Entities, IRepositories, Repositories (DbContext + EF Core), Migrations.
  Application/   Modules/<Module>/ (service, interface, DTO), DependencyInjection.
  Api/           Controllers, Security (JWT, rate limit), Errors (Problem Details), Observability, Program.cs.
tests/
  Api.Tests/     xUnit.
```

Chiều phụ thuộc: `Api → Application → Domain → Common`.

## Bảy module

| Module | Schema | Chức năng |
| --- | --- | --- |
| Identity | `identity` | F01 tài khoản, F15 doanh nghiệp thuê, F16 thành viên và vai trò, cấp token RS256 |
| Subscriptions | `subscription` | F17 gói dịch vụ, F18 thuê bao, F19 thanh toán SePay và hóa đơn, F20 hạn mức |
| Contracts | `contract` | F02–F05 hợp đồng và gửi ký, F09 hủy và hết hạn, F13 bảng điều khiển, F21 mẫu, F22 soạn từ mẫu, F23 tra cứu |
| Signing | `signing` | F06 OTP, F07 ký PAdES, F08 từ chối, F10 hoàn tất, F12 xác minh, F27 chữ ký mẫu |
| Pki | `pki` | Root CA, Issuing CA, cấp và thu hồi chứng thư X.509 (F14) |
| Audit | `audit` | F11 nhật ký hash-chain |
| Notifications | `notification` | F24 email |

Quy ước đặt mã của một module:

- Service, interface và DTO: `src/Application/Modules/<Module>/`; file `<Module>Module.cs` đăng ký DI của module.
- Entity và repository: `src/Domain/Entities/<Module>/`, `src/Domain/IRepositories/<Module>/`, `src/Domain/Repositories/<Module>/`.
- Cấu hình bảng: `src/Domain/Repositories/Configurations/<Module>/`, mỗi bảng khai schema bằng hằng trong `DbSchemas`.
- Controller: `src/Api/Controllers/<Module>/`.
- Module chỉ gọi module khác qua interface công khai của module đó, không đụng thẳng vào entity hay bảng của nhau. Vì cùng một `TrustSignDbContext`, thao tác của hai module (vd gửi ký và trừ hạn mức) chạy chung được một transaction.

## Chạy trên máy

Cần .NET SDK 10.0.400 trở lên và Docker Desktop.

`docker compose up -d` bật PostgreSQL (:5433), MinIO để lưu file (API :9000, console :9001) và Mailpit để xem email (SMTP :1025, giao diện :8025).

```bash
docker compose up -d
```

```bash
dotnet run --project src/Api
```

Ứng dụng nghe ở http://localhost:5050. Lần chạy đầu ở môi trường Development nó tự áp migration và tự sinh khóa ký JWT vào `keys/signing-key.pem` (thư mục `keys/` không được commit).

Trên database mới, log có một dòng `fail` về bảng `__EFMigrationsHistory` chưa tồn tại. Đó là EF Core dò lịch sử migration trước khi tạo bảng, không phải lỗi.

Thử nhanh:

```bash
TOKEN=$(curl -s -X POST http://localhost:5050/api/auth/dev-token | sed 's/.*"accessToken":"\([^"]*\)".*/\1/')
curl -H "Authorization: Bearer $TOKEN" http://localhost:5050/api/auth/me
curl -i http://localhost:5050/api/auth/me
```

Lệnh thứ hai trả 200, lệnh cuối không có token nên trả 401.

## Endpoint

| Phương thức | Đường dẫn | Ghi chú |
| --- | --- | --- |
| GET | `/api/auth/me` | Cần Bearer token |
| GET | `/.well-known/jwks.json` | Public key ký JWT, để bên ngoài tự kiểm token |
| GET | `/health/live`, `/health/ready` | Ready kiểm tra kết nối database |
| POST | `/api/auth/dev-token` | Chỉ có ở Development: cấp token cho tài khoản thử. Xóa `src/Api/Development/` khi đăng nhập đã xong |

Mọi request trừ `/health` bị giới hạn 300 lần đọc và 30 lần ghi mỗi phút cho mỗi người dùng (hoặc IP nếu chưa đăng nhập); vượt thì trả 429.

## Lệnh thường dùng

```bash
dotnet build TrustSign.slnx
```

```bash
dotnet test TrustSign.slnx
```

```bash
dotnet ef migrations add <Tên> --project src/Domain --startup-project src/Api --output-dir Migrations
```

## Cấu hình

| Khóa | Ý nghĩa |
| --- | --- |
| `ConnectionStrings:TrustSignDb` | Chuỗi kết nối PostgreSQL |
| `Jwt:Issuer`, `Jwt:Audience` | Mặc định `trustsign` và `trustsign-clients` |
| `Jwt:AccessTokenMinutes` | Tuổi thọ access token, mặc định 15 |
| `Jwt:PrivateKeyPath` | File PEM chứa private key RSA. Ngoài Development file phải có sẵn, ứng dụng không tự sinh |
| `Cors:AllowedOrigins` | Origin của frontend được phép gọi API kèm cookie; Development là `http://localhost:3000` |

Giá trị trong `appsettings.Development.json` chỉ dành cho máy cá nhân. Môi trường khác ghi đè bằng biến môi trường, xem `.env.example`.
