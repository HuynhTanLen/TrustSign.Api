using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Media;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Audit;
using TrustSign.Domain.Entities.Contracts;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories;
using TrustSign.Domain.IRepositories.Contracts;
using TrustSign.Domain.IRepositories.Identity;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace TrustSign.Api.Tests;

public sealed class ContractFlowTests : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly string _tempStorageDir = Path.Combine(
        Path.GetTempPath(),
        "trustsign-contract-tests-" + Guid.NewGuid().ToString("N"));

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryContractStore _store = new();
    private readonly ConfigurableQuotaService _quota = new();
    private readonly RecordingEmailSender _emails = new();
    private readonly ToggleIdentityChecker _identity = new();
    private readonly InMemoryAuditLogger _audit;

    // Đủ để qua kiểm tra chữ ký PNG: 8 byte đầu là magic bytes của PNG.
    private static readonly string TestSignaturePng =
        "data:image/png;base64," + Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4]);
    private readonly IMediaStorage _fileStorage;
    private readonly IContractPdfGenerator _pdfGenerator = new ContractPdfGenerator();
    private readonly ITemplateService _templateService;
    private readonly IContractService _contractService;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Guid _employee1Id = Guid.NewGuid();
    private readonly Guid _employee2Id = Guid.NewGuid();

    public ContractFlowTests()
    {
        IOptions<MediaOptions> storageOptions = Options.Create(new MediaOptions
        {
            StorageRootPath = _tempStorageDir,
            PublicApiBaseUrl = "http://localhost:5050",
            TokenSecret = "test_hmac_secret_key_for_document_tokens_2026",
            UrlLifetimeMinutes = 30
        });

        IOptions<SmtpOptions> smtpOptions = Options.Create(new SmtpOptions
        {
            FrontendBaseUrl = "http://localhost:3000"
        });

        _fileStorage = new LocalMediaStorage(storageOptions);
        _audit = new InMemoryAuditLogger(_clock);
        _templateService = new TemplateService(_store, _store, _clock);
        _contractService = new ContractService(
            _store,
            _store,
            _store,
            _identity,
            _fileStorage,
            new HmacMediaSigner(storageOptions),
            _pdfGenerator,
            _quota,
            _store,
            _emails,
            _audit,
            smtpOptions,
            _clock,
            NullLogger<ContractService>.Instance);

        _store.SeedUser(_ownerUserId, "Nguyễn Văn Chủ DN", "owner@chutin.vn");
        _store.SeedUser(_employee1Id, "Trần Thị Nhân Viên 1", "nv1@chutin.vn");
        _store.SeedUser(_employee2Id, "Lê Văn Nhân Viên 2", "nv2@chutin.vn");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempStorageDir))
            {
                Directory.Delete(_tempStorageDir, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup errors
        }
    }

    [Fact]
    public async Task Template_crud_extracts_variables_in_order_of_appearance_CON03()
    {
        Result<TemplateSummaryDto> created = await _templateService.CreateAsync(
            _tenantA,
            _ownerUserId,
            new SaveTemplateRequestDto(
                "Hợp đồng dịch vụ phần mềm",
                "Bên A: {{ten_ben_a}} ký với Bên B: {{ten_ben_b}} (MST: {{ma_so_thue}}). Đại diện {{ten_ben_a}} xác nhận giá trị {{gia_tri_hd}}."),
            Cancel);

        Assert.True(created.IsSuccess);
        Assert.Equal("Hợp đồng dịch vụ phần mềm", created.Value.Name);
        Assert.Equal("Nguyễn Văn Chủ DN", created.Value.AuthorName);
        Assert.Equal(["ten_ben_a", "ten_ben_b", "ma_so_thue", "gia_tri_hd"], created.Value.Variables);

        Result<TemplateSummaryDto> updated = await _templateService.UpdateAsync(
            _tenantA,
            created.Value.Id,
            new SaveTemplateRequestDto(
                "Hợp đồng dịch vụ v2",
                "Đối tác: {{ten_doi_tac}} - Giá trị: {{tong_tien}}"),
            Cancel);

        Assert.True(updated.IsSuccess);
        Assert.Equal(["ten_doi_tac", "tong_tien"], updated.Value.Variables);

        Result deleted = await _templateService.DeleteAsync(_tenantA, created.Value.Id, Cancel);
        Assert.True(deleted.IsSuccess);

        Result<IReadOnlyList<TemplateSummaryDto>> listAfterDelete = await _templateService.ListAsync(_tenantA, Cancel);
        Assert.Empty(listAfterDelete.Value);
    }

    [Fact]
    public async Task Create_contract_is_blocked_until_creator_passes_ekyc()
    {
        Result<TemplateSummaryDto> tpl = await _templateService.CreateAsync(
            _tenantA, _ownerUserId, new SaveTemplateRequestDto("Mẫu", "Bên A: {{ten}}"), Cancel);
        var request = new CreateFromTemplateRequestDto(tpl.Value.Id, "Hợp đồng", new Dictionary<string, string> { ["ten"] = "A" });

        _identity.Verified = false;
        Result<CreatedContractResponseDto> blocked = await _contractService.CreateFromTemplateAsync(_tenantA, _ownerUserId, request, Cancel);
        Assert.Equal(IdentityErrors.IdentityNotVerified, blocked.Error);

        Result<CreatedContractResponseDto> blockedUpload = await _contractService.UploadDraftAsync(
            _tenantA, _ownerUserId, "Hợp đồng", "%PDF-1.4"u8.ToArray(), null, Cancel);
        Assert.Equal(IdentityErrors.IdentityNotVerified, blockedUpload.Error);

        _identity.Verified = true;
        Assert.True((await _contractService.CreateFromTemplateAsync(_tenantA, _ownerUserId, request, Cancel)).IsSuccess);
    }

    [Fact]
    public async Task Create_contract_from_template_renders_vietnamese_pdf_and_computes_sha256_TC19()
    {
        Result<TemplateSummaryDto> tpl = await _templateService.CreateAsync(
            _tenantA,
            _ownerUserId,
            new SaveTemplateRequestDto(
                "Mẫu hợp đồng kinh tế",
                "Bên thuê dịch vụ: {{ten_doi_tac}}\nMã số thuế: {{ma_so_thue}}\nTổng giá trị thanh toán: {{gia_tri_hd}}"),
            Cancel);

        // Thiếu biến -> báo lỗi MissingTemplateVariable
        Result<CreatedContractResponseDto> missingVar = await _contractService.CreateFromTemplateAsync(
            _tenantA,
            _ownerUserId,
            new CreateFromTemplateRequestDto(
                tpl.Value.Id,
                "Hợp đồng thiếu biến",
                new Dictionary<string, string> { ["ten_doi_tac"] = "Công ty TNHH Sao Mai" }),
            Cancel);
        Assert.True(missingVar.IsFailure);
        Assert.Equal(ContractErrors.MissingTemplateVariable.Code, missingVar.Error.Code);

        // Điền đủ biến -> sinh PDF thành công (TC19)
        Result<CreatedContractResponseDto> draft = await _contractService.CreateFromTemplateAsync(
            _tenantA,
            _ownerUserId,
            new CreateFromTemplateRequestDto(
                tpl.Value.Id,
                "Hợp đồng cung cấp Chữ Tín 2026",
                new Dictionary<string, string>
                {
                    ["ten_doi_tac"] = "Công ty Cổ phần Thương mại Đại Việt",
                    ["ma_so_thue"] = "0108889999",
                    ["gia_tri_hd"] = "250.000.000 VND"
                }),
            Cancel);

        Assert.True(draft.IsSuccess);

        Contract? storedContract = await _store.FindByIdAsync(_tenantA, draft.Value.ContractId, Cancel);
        Assert.NotNull(storedContract);
        Assert.Equal(ContractStatus.Draft, storedContract.Status);
        Assert.Equal(64, storedContract.OriginalFileHash.Length);

        byte[]? pdfBytes = await _fileStorage.ReadAsync(storedContract.StorageKey, Cancel);
        Assert.NotNull(pdfBytes);
        Assert.Equal(storedContract.OriginalFileHash, Convert.ToHexStringLower(SHA256.HashData(pdfBytes)));

        // Đọc text từ PDF bằng iText để kiểm chứng nội dung biến tiếng Việt đã điền (TC19)
        using MemoryStream ms = new(pdfBytes);
        using PdfReader reader = new(ms);
        using PdfDocument pdfDoc = new(reader);
        string extractedText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetFirstPage());

        Assert.Contains("0108889999", extractedText);
        Assert.Contains("250.000.000 VND", extractedText);
        Assert.Contains("Đại Việt", extractedText);
    }

    [Fact]
    public async Task Upload_pdf_validates_format_and_stores_sha256_hash_CON02()
    {
        // File không phải PDF -> từ chối
        byte[] fakeBytes = Encoding.UTF8.GetBytes("Not a valid PDF file");
        Result<CreatedContractResponseDto> invalid = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng lỗi",
            fakeBytes,
            null, Cancel);
        Assert.True(invalid.IsFailure);
        Assert.Equal(ContractErrors.InvalidPdfFile.Code, invalid.Error.Code);

        // File PDF hợp lệ -> lưu và tính đúng SHA-256
        byte[] validPdf = _pdfGenerator.RenderTemplateToPdf(
            "Hợp đồng tải lên",
            "HD-2026-0001",
            "Nội dung hợp đồng tải lên",
            _clock.GetUtcNow());

        Result<CreatedContractResponseDto> uploaded = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng tải lên hợp lệ",
            validPdf,
            null, Cancel);

        Assert.True(uploaded.IsSuccess);
        Contract? contract = await _store.FindByIdAsync(_tenantA, uploaded.Value.ContractId, Cancel);
        Assert.NotNull(contract);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(validPdf)), contract.OriginalFileHash);
    }

    [Fact]
    public async Task Create_draft_with_invalid_signer_saves_nothing_and_names_the_recipient()
    {
        byte[] pdf = _pdfGenerator.RenderTemplateToPdf("HĐ", "HD-03", "Nội dung", _clock.GetUtcNow());

        Result<CreatedContractResponseDto> result = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng có email sai",
            pdf,
            [
                new SetSignerItemDto("Nguyễn Văn A", "a@partner.vn", Order: 1),
                new SetSignerItemDto("Trần Thị B", "b @partner.vn", Order: 2)
            ],
            Cancel);

        Assert.True(result.IsFailure);
        Assert.Equal("Contract.InvalidSignerEmail", result.Error.Code);
        Assert.Contains("Người nhận thứ 2", result.Error.Message);

        var list = await _contractService.ListAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, null, null, Cancel);
        Assert.Empty(list.Value);
        Assert.Empty(Directory.Exists(_tempStorageDir)
            ? Directory.GetFiles(_tempStorageDir, "*", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public async Task Create_draft_with_valid_signers_stores_them_in_one_call()
    {
        byte[] pdf = _pdfGenerator.RenderTemplateToPdf("HĐ", "HD-04", "Nội dung", _clock.GetUtcNow());

        Result<CreatedContractResponseDto> result = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng kèm người nhận",
            pdf,
            [
                new SetSignerItemDto("Nguyễn Văn A", "a@partner.vn", Order: 1),
                new SetSignerItemDto("Trần Thị B", "b@partner.vn", Order: 2)
            ],
            Cancel);

        Assert.True(result.IsSuccess);
        Contract? contract = await _store.FindByIdAsync(_tenantA, result.Value.ContractId, Cancel);
        Assert.NotNull(contract);
        Assert.Equal(ContractStatus.Draft, contract.Status);
        Assert.Equal(["a@partner.vn", "b@partner.vn"], contract.Signers.OrderBy(s => s.SignOrder).Select(s => s.Email));
    }

    [Fact]
    public async Task Draft_can_be_cancelled_once()
    {
        byte[] pdf = _pdfGenerator.RenderTemplateToPdf("HĐ nháp", "HD-02", "Nội dung", _clock.GetUtcNow());
        Result<CreatedContractResponseDto> draft = await _contractService.UploadDraftAsync(_tenantA, _ownerUserId, "Hợp đồng nháp", pdf, null, Cancel);

        Assert.True((await _contractService.CancelAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, draft.Value.ContractId, Cancel)).IsSuccess);
        Result again = await _contractService.CancelAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, draft.Value.ContractId, Cancel);
        Assert.Equal(ContractErrors.ContractCannotCancel.Code, again.Error.Code);
    }

    [Fact]
    public async Task Set_signers_and_send_contract_consumes_quota_and_notifies_first_signer_CON05_CON06()
    {
        byte[] validPdf = _pdfGenerator.RenderTemplateToPdf("HĐ 1", "HD-01", "Nội dung", _clock.GetUtcNow());
        Result<CreatedContractResponseDto> draft = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng dịch vụ A-B",
            validPdf,
            null, Cancel);

        // Cấu hình vượt quá 5 người ký -> báo lỗi
        List<SetSignerItemDto> sixSigners = Enumerable.Range(1, 6)
            .Select(i => new SetSignerItemDto($"Người ký {i}", $"s{i}@test.vn", i))
            .ToList();
        Result tooMany = await _contractService.SetSignersAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            draft.Value.ContractId,
            new SetSignersRequestDto(sixSigners),
            Cancel);
        Assert.True(tooMany.IsFailure);
        Assert.Equal(ContractErrors.InvalidSigners.Code, tooMany.Error.Code);

        // Cấu hình 2 người ký hợp lệ
        Result setOk = await _contractService.SetSignersAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            draft.Value.ContractId,
            new SetSignersRequestDto(
            [
                new SetSignerItemDto("Nguyễn Văn A", "a@partner.vn", Order: 1),
                new SetSignerItemDto("Trần Thị B", "b@partner.vn", Order: 2)
            ]),
            Cancel);
        Assert.True(setOk.IsSuccess);

        // Gửi ký: trừ 1 lượt hạn mức, chuyển sang Pending, kích hoạt lượt ký người #1 và gửi email
        DateTimeOffset dueAt = _clock.GetUtcNow().AddDays(7);
        Result sendOk = await _contractService.SendForSigningAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            draft.Value.ContractId,
            new SendContractRequestDto(dueAt),
            Cancel);

        Assert.True(sendOk.IsSuccess);
        Assert.Equal(1, _quota.ConsumedCount);
        Assert.NotNull(_emails.LastSent);
        Assert.Equal("a@partner.vn", _emails.LastSent.To);

        Result<ContractDetailDto> detail = await _contractService.GetByIdAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            draft.Value.ContractId,
            Cancel);

        Assert.True(detail.IsSuccess);
        Assert.Equal(ContractStatus.Pending.ToString(), detail.Value.Status);
        Assert.Equal("Nguyễn Văn A", detail.Value.CurrentSigner);
        Assert.Equal(SignerStatus.Current.ToString(), detail.Value.Signers[0].Status);
        Assert.Equal(SignerStatus.Waiting.ToString(), detail.Value.Signers[1].Status);

        // Gửi lại lời mời: email đi tới người đang tới lượt
        _emails.LastSent = null;
        Result remind = await _contractService.RemindCurrentSignerAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, draft.Value.ContractId, Cancel);
        Assert.True(remind.IsSuccess);
        Assert.Equal("a@partner.vn", _emails.LastSent?.To);
    }

    [Fact]
    public void SetSignersRequestDto_deserializes_from_json_with_string_role()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        string json = "{\"signers\":[{\"fullName\":\"Huỳnh Tấn Huy\",\"email\":\"huynhtanlenlb@gmail.com\",\"order\":1,\"pageNumber\":2,\"boxX\":81,\"boxY\":449,\"boxWidth\":160,\"boxHeight\":64,\"role\":\"Signer\"}]}";

        SetSignersRequestDto? dto = System.Text.Json.JsonSerializer.Deserialize<SetSignersRequestDto>(json, options);

        Assert.NotNull(dto);
        Assert.Single(dto.Signers);
        Assert.Equal("Huỳnh Tấn Huy", dto.Signers[0].FullName);
        Assert.Equal(RecipientRole.Signer, dto.Signers[0].Role);
    }

    [Fact]
    public async Task Send_contract_rolls_back_to_draft_when_quota_exceeded_TC15_TC21()
    {
        byte[] validPdf = _pdfGenerator.RenderTemplateToPdf("HĐ 2", "HD-02", "Nội dung", _clock.GetUtcNow());
        Result<CreatedContractResponseDto> draft = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng vượt hạn mức",
            validPdf,
            null, Cancel);

        await _contractService.SetSignersAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            draft.Value.ContractId,
            new SetSignersRequestDto([new SetSignerItemDto("Lê Văn C", "c@partner.vn", 1)]),
            Cancel);

        // Giả lập hết hạn mức ký trong tháng
        _quota.NextResult = SubscriptionErrors.QuotaExceeded;

        Result sendFail = await _contractService.SendForSigningAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            draft.Value.ContractId,
            new SendContractRequestDto(_clock.GetUtcNow().AddDays(5)),
            Cancel);

        Assert.True(sendFail.IsFailure);
        Assert.Equal(SubscriptionErrors.QuotaExceeded.Code, sendFail.Error.Code);

        // Hợp đồng phải được rollback về trạng thái Draft (CON-06, TC15)
        Result<ContractDetailDto> detail = await _contractService.GetByIdAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            draft.Value.ContractId,
            Cancel);

        Assert.Equal(ContractStatus.Draft.ToString(), detail.Value.Status);
        Assert.Null(detail.Value.SentAt);
    }

    [Fact]
    public async Task Cross_tenant_isolation_blocks_reading_or_modifying_other_tenant_contracts_TC10_TC14()
    {
        byte[] validPdf = _pdfGenerator.RenderTemplateToPdf("HĐ A", "HD-A", "Mật", _clock.GetUtcNow());
        Result<CreatedContractResponseDto> createdInA = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng nội bộ DN A",
            validPdf,
            null, Cancel);

        // Doanh nghiệp B truy cập mã hợp đồng của Doanh nghiệp A -> 404 NotFound (TC10, TC14)
        Result<ContractDetailDto> readFromB = await _contractService.GetByIdAsync(
            _tenantB,
            _ownerUserId,
            TenantRole.TenantOwner,
            createdInA.Value.ContractId,
            Cancel);

        Assert.True(readFromB.IsFailure);
        Assert.Equal(ErrorType.NotFound, readFromB.Error.Type);
        Assert.Equal(ContractErrors.ContractNotFound.Code, readFromB.Error.Code);
    }

    [Fact]
    public async Task Employee_role_only_sees_own_contracts_while_owner_and_manager_see_all_TC20()
    {
        byte[] validPdf = _pdfGenerator.RenderTemplateToPdf("HĐ", "HD-EMP", "Nội dung", _clock.GetUtcNow());

        Result<CreatedContractResponseDto> c1 = await _contractService.UploadDraftAsync(
            _tenantA,
            _employee1Id,
            "Hợp đồng của Nhân viên 1",
            validPdf,
            null, Cancel);

        await _contractService.UploadDraftAsync(
            _tenantA,
            _employee2Id,
            "Hợp đồng của Nhân viên 2",
            validPdf,
            null, Cancel);

        // Nhân viên 1 chỉ thấy đúng 1 hợp đồng do mình tạo
        Result<IReadOnlyList<ContractListItemDto>> emp1List = await _contractService.ListAsync(
            _tenantA,
            _employee1Id,
            TenantRole.Employee,
            query: null,
            status: null,
            Cancel);
        Assert.Single(emp1List.Value);
        Assert.Equal("Hợp đồng của Nhân viên 1", emp1List.Value[0].Title);

        // Nhân viên 2 cố đọc chi tiết hợp đồng của Nhân viên 1 -> 403 Forbidden (TC20)
        Result<ContractDetailDto> emp2ReadEmp1 = await _contractService.GetByIdAsync(
            _tenantA,
            _employee2Id,
            TenantRole.Employee,
            c1.Value.ContractId,
            Cancel);
        Assert.True(emp2ReadEmp1.IsFailure);
        Assert.Equal(ErrorType.Forbidden, emp2ReadEmp1.Error.Type);

        // Chủ DN và Quản lý hợp đồng thấy toàn bộ 2 hợp đồng của DN
        Result<IReadOnlyList<ContractListItemDto>> managerList = await _contractService.ListAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.ContractManager,
            query: null,
            status: null,
            Cancel);
        Assert.Equal(2, managerList.Value.Count);
    }

    [Fact]
    public async Task Contract_without_signer_role_is_rejected_with_validation_error()
    {
        byte[] validPdf = _pdfGenerator.RenderTemplateToPdf("HĐ", "HD-NOSIGNER", "Nội dung", _clock.GetUtcNow());
        Result<CreatedContractResponseDto> draft = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng không có Signer",
            validPdf,
            null, Cancel);

        Result setSignersResult = await _contractService.SetSignersAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            draft.Value.ContractId,
            new SetSignersRequestDto([
                new SetSignerItemDto("Người Duyệt 1", "approver1@domain.vn", 1, Role: RecipientRole.Approver),
                new SetSignerItemDto("Người Nhận Cc", "cc@domain.vn", 2, Role: RecipientRole.Cc)
            ]),
            Cancel);

        Assert.True(setSignersResult.IsFailure);
        Assert.Equal(ContractErrors.AtLeastOneSignerRequired, setSignersResult.Error);
    }

    [Fact]
    public async Task Sequential_flow_signer_approver_signer_advances_turn_and_notifies_cc_on_completion()
    {
        byte[] validPdf = _pdfGenerator.RenderTemplateToPdf("HĐ", "HD-FLOW", "Nội dung", _clock.GetUtcNow());
        Result<CreatedContractResponseDto> draft = await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng luồng phức hợp",
            validPdf,
            null, Cancel);

        Guid contractId = draft.Value.ContractId;

        // Cấu hình: Signer 1 -> Approver 2 -> Signer 3 -> Cc 4
        Result setSigners = await _contractService.SetSignersAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            contractId,
            new SetSignersRequestDto([
                new SetSignerItemDto("Nguyễn Văn Ký 1", "signer1@company.vn", 1, PageNumber: 1, BoxX: 100, BoxY: 100, BoxWidth: 150, BoxHeight: 50, Role: RecipientRole.Signer),
                new SetSignerItemDto("Trần Văn Duyệt 2", "approver2@company.vn", 2, Role: RecipientRole.Approver, Title: "Trưởng phòng"),
                new SetSignerItemDto("Lê Văn Ký 3", "signer3@company.vn", 3, PageNumber: 1, BoxX: 300, BoxY: 100, BoxWidth: 150, BoxHeight: 50, Role: RecipientRole.Signer, Organization: "Đối tác X", Title: "Giám đốc"),
                new SetSignerItemDto("Phạm Văn Theo Dõi 4", "cc4@company.vn", 4, Role: RecipientRole.Cc)
            ]),
            Cancel);
        Assert.True(setSigners.IsSuccess);

        // Gửi ký -> kích hoạt lượt #1 (Signer 1), gửi email cho Signer 1
        Result send = await _contractService.SendForSigningAsync(
            _tenantA,
            _ownerUserId,
            TenantRole.TenantOwner,
            contractId,
            new SendContractRequestDto(_clock.GetUtcNow().AddDays(3)),
            Cancel);
        Assert.True(send.IsSuccess);

        Result<ContractDetailDto> detail1 = await _contractService.GetByIdAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, contractId, Cancel);
        Assert.Equal(ContractStatus.Pending.ToString(), detail1.Value.Status);
        Assert.Equal(SignerStatus.Current.ToString(), detail1.Value.Signers[0].Status);
        Assert.Equal(SignerStatus.Waiting.ToString(), detail1.Value.Signers[1].Status);
        Assert.Equal(SignerStatus.Waiting.ToString(), detail1.Value.Signers[2].Status);
        Assert.Equal(SignerStatus.Waiting.ToString(), detail1.Value.Signers[3].Status);

        // Lấy danh sách ID signer từ database
        Contract contract = (await _store.FindByIdAsync(_tenantA, contractId, Cancel))!;
        ContractSigner s1 = contract.Signers.First(s => s.SignOrder == 1);
        ContractSigner s2 = contract.Signers.First(s => s.SignOrder == 2);
        ContractSigner s3 = contract.Signers.First(s => s.SignOrder == 3);
        ContractSigner s4 = contract.Signers.First(s => s.SignOrder == 4);

        // Lượt 1: Signer 1 ký -> chuyển lượt sang Approver 2
        Result noSignature = await _contractService.ProcessSignOrApproveAsync(contractId, s1.Id, null, Cancel);
        Assert.Equal(SigningErrors.SignatureImageInvalid, noSignature.Error);

        Result sign1 = await _contractService.ProcessSignOrApproveAsync(contractId, s1.Id, TestSignaturePng, Cancel);
        Assert.True(sign1.IsSuccess);
        Assert.NotNull(await _fileStorage.ReadAsync(s1.SignatureStorageKey!, Cancel));

        // Bấm ký lần 2: thành công nhưng không thêm sự kiện hay chuyển lượt (TC07)
        int eventsBefore = _audit.Events.Count;
        Result sign1Again = await _contractService.ProcessSignOrApproveAsync(contractId, s1.Id, TestSignaturePng, Cancel);
        Assert.True(sign1Again.IsSuccess);
        Assert.Equal(eventsBefore, _audit.Events.Count);

        Result<ContractDetailDto> detail2 = await _contractService.GetByIdAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, contractId, Cancel);
        Assert.Equal(ContractStatus.Signing.ToString(), detail2.Value.Status);
        Assert.Equal(SignerStatus.Signed.ToString(), detail2.Value.Signers[0].Status);
        Assert.Equal(SignerStatus.Current.ToString(), detail2.Value.Signers[1].Status);
        Assert.Equal(SignerStatus.Waiting.ToString(), detail2.Value.Signers[2].Status);

        // Lượt 2: Approver 2 duyệt -> chuyển lượt sang Signer 3
        Result approve2 = await _contractService.ProcessSignOrApproveAsync(contractId, s2.Id, null, Cancel);
        Assert.True(approve2.IsSuccess);

        Result<ContractDetailDto> detail3 = await _contractService.GetByIdAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, contractId, Cancel);
        Assert.Equal(SignerStatus.Signed.ToString(), detail3.Value.Signers[1].Status);
        Assert.Equal(SignerStatus.Current.ToString(), detail3.Value.Signers[2].Status);

        // Lượt 3: Signer 3 ký -> người ký/duyệt cuối cùng -> Hợp đồng hoàn tất (Completed), gửi email cho người nhận Cc (s4)
        Result sign3 = await _contractService.ProcessSignOrApproveAsync(contractId, s3.Id, TestSignaturePng, Cancel);
        Assert.True(sign3.IsSuccess);

        Result<ContractDetailDto> detailDone = await _contractService.GetByIdAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, contractId, Cancel);
        Assert.Equal(ContractStatus.Completed.ToString(), detailDone.Value.Status);
        Assert.Equal(SignerStatus.Signed.ToString(), detailDone.Value.Signers[2].Status);

        // Kiểm tra email gửi cho người nhận Cc
        Assert.NotNull(_emails.LastSent);
        Assert.Equal("cc4@company.vn", _emails.LastSent!.To);
        Assert.Contains("Bản sao hợp đồng hoàn tất", _emails.LastSent!.Subject);

        // Dòng thời gian đọc từ chuỗi audit của chính hợp đồng này (AUD-01)
        Assert.Equal(
            ["Khởi tạo hợp đồng nháp", "Gửi hợp đồng đi ký", "Ký hợp đồng", "Duyệt hợp đồng", "Ký hợp đồng", "Hoàn tất hợp đồng"],
            detailDone.Value.Events.Select(e => e.Title));
        Assert.True(AuditEvent.VerifyChain(await _audit.ListForResourceAsync(_tenantA, "Contract", contractId.ToString(), Cancel)));
    }

    [Fact]
    public async Task Coordinator_names_partner_signer_who_gets_invited_and_timeline_shows_coordination()
    {
        byte[] pdf = _pdfGenerator.RenderTemplateToPdf("HĐ", "HD-COORD", "Nội dung", _clock.GetUtcNow());
        Guid contractId = (await _contractService.UploadDraftAsync(_tenantA, _ownerUserId, "HĐ điều phối", pdf, [
            new SetSignerItemDto("Giám đốc A", "gd@a.vn", 1, PageNumber: 1, BoxX: 60, BoxY: 60, BoxWidth: 150, BoxHeight: 50),
            new SetSignerItemDto("Đầu mối B", "tu@b.vn", 2, PageNumber: 1, BoxX: 350, BoxY: 60, BoxWidth: 150, BoxHeight: 50, Role: RecipientRole.Coordinator),
        ], Cancel)).Value.ContractId;
        Assert.True((await _contractService.SendForSigningAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, contractId, new SendContractRequestDto(_clock.GetUtcNow().AddDays(3)), Cancel)).IsSuccess);

        Contract contract = (await _store.FindByIdAsync(_tenantA, contractId, Cancel))!;
        Assert.True((await _contractService.ProcessSignOrApproveAsync(contractId, contract.Signers[0].Id, TestSignaturePng, Cancel)).IsSuccess);
        Assert.Equal("tu@b.vn", _emails.LastSent!.To);
        Assert.Contains("Mời điều phối", _emails.LastSent.Subject);

        Guid coordinatorId = contract.Signers.Single(s => s.Role == RecipientRole.Coordinator).Id;
        IReadOnlyList<SetSignerItemDto> partySide = [
            new SetSignerItemDto("Pháp chế B", "lan@b.vn", Role: RecipientRole.Approver),
            new SetSignerItemDto("Giám đốc B", "minh@b.vn", Role: RecipientRole.Signer),
        ];
        Assert.True((await _contractService.CoordinateAsync(contractId, coordinatorId, partySide, Cancel)).IsSuccess);
        Assert.Equal("lan@b.vn", _emails.LastSent!.To);
        Assert.Contains("Mời duyệt", _emails.LastSent.Subject);

        // Gửi lại lần hai (mất phản hồi) không chèn thêm người.
        Assert.True((await _contractService.CoordinateAsync(contractId, coordinatorId, partySide, Cancel)).IsSuccess);
        Assert.Equal(4, contract.Signers.Count);

        Result<ContractDetailDto> detail = await _contractService.GetByIdAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, contractId, Cancel);
        Assert.Equal(["Khởi tạo hợp đồng nháp", "Gửi hợp đồng đi ký", "Ký hợp đồng", "Điều phối người ký"], detail.Value.Events.Select(e => e.Title));
        Assert.Equal(["Signer", "Coordinator", "Approver", "Signer"], detail.Value.Signers.Select(s => s.Role));
    }

    [Fact]
    public async Task Decline_stops_flow_logs_event_and_emails_creator()
    {
        byte[] pdf = _pdfGenerator.RenderTemplateToPdf("HĐ", "HD-DECLINE", "Nội dung", _clock.GetUtcNow());
        Guid contractId = (await _contractService.UploadDraftAsync(
            _tenantA,
            _ownerUserId,
            "Hợp đồng bị từ chối",
            pdf,
            [new SetSignerItemDto("Nguyễn Văn Ký", "signer@company.vn", 1)],
            Cancel)).Value.ContractId;
        await _contractService.SendForSigningAsync(
            _tenantA, _ownerUserId, TenantRole.TenantOwner, contractId,
            new SendContractRequestDto(_clock.GetUtcNow().AddDays(3)), Cancel);
        Guid signerId = (await _store.FindByIdAsync(_tenantA, contractId, Cancel))!.Signers[0].Id;

        Result decline = await _contractService.DeclineAsync(contractId, signerId, "Sai giá trị hợp đồng", Cancel);

        Assert.True(decline.IsSuccess);
        Assert.Equal("owner@chutin.vn", _emails.LastSent!.To);
        Assert.Contains("bị từ chối", _emails.LastSent!.Subject);
        ContractDetailDto detail = (await _contractService.GetByIdAsync(_tenantA, _ownerUserId, TenantRole.TenantOwner, contractId, Cancel)).Value;
        Assert.Equal(ContractStatus.Declined.ToString(), detail.Status);
        Assert.Equal("Từ chối ký", detail.Events[^1].Title);
        Assert.Contains("Sai giá trị hợp đồng", detail.Events[^1].Detail);
    }


    private sealed class FakeClock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = initial;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class ConfigurableQuotaService : IQuotaService
    {
        public Result NextResult { get; set; } = Result.Success();
        public int ConsumedCount { get; private set; }

        public Task<bool> HasAvailableQuotaAsync(Guid tenantId, CancellationToken cancel = default) =>
            Task.FromResult(NextResult.IsSuccess);

        public Task<Result> ConsumeQuotaAsync(Guid tenantId, CancellationToken cancel = default)
        {
            if (NextResult.IsSuccess)
            {
                ConsumedCount++;
            }

            return Task.FromResult(NextResult);
        }
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public EmailMessage? LastSent { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancel)
        {
            LastSent = message;
            return Task.CompletedTask;
        }
    }

    private sealed class ToggleIdentityChecker : TrustSign.Application.Modules.Identity.IIdentityVerifiedChecker
    {
        public bool Verified { get; set; } = true;

        public Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancel) => Task.FromResult(Verified);
    }

    private sealed class InMemoryContractStore :
        IContractTemplateRepository,
        IContractRepository,
        IUserRepository,
        IUnitOfWork
    {
        private readonly List<ContractTemplate> _templates = [];
        private readonly List<Contract> _contracts = [];
        private readonly List<User> _users = [];

        public void SeedUser(Guid id, string fullName, string email)
        {
            User user = User.Register(email, fullName, "hash", DateTimeOffset.UtcNow);
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
            _users.Add(user);
        }

        // IUnitOfWork: Nếu lỗi nghiệp vụ, khôi phục trạng thái Draft như rollback DB
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancel)
            where TResult : Result
        {
            var snapshots = _contracts.ToDictionary(
                c => c.Id,
                c => (c.Status, c.SentAt, c.DueAt));

            TResult res = await action();
            if (res.IsFailure)
            {
                foreach (Contract c in _contracts)
                {
                    if (snapshots.TryGetValue(c.Id, out var snap))
                    {
                        typeof(Contract).GetProperty(nameof(Contract.Status))!.SetValue(c, snap.Status);
                        typeof(Contract).GetProperty(nameof(Contract.SentAt))!.SetValue(c, snap.SentAt);
                        typeof(Contract).GetProperty(nameof(Contract.DueAt))!.SetValue(c, snap.DueAt);
                    }
                }
            }

            return res;
        }

        // IContractTemplateRepository
        Task<IReadOnlyList<ContractTemplate>> IContractTemplateRepository.ListByTenantAsync(Guid tenantId, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<ContractTemplate>>(
                _templates.Where(t => t.TenantId == tenantId).OrderByDescending(t => t.UpdatedAt).ToList());

        Task<ContractTemplate?> IContractTemplateRepository.FindByIdAsync(Guid tenantId, Guid templateId, CancellationToken cancel) =>
            Task.FromResult(_templates.FirstOrDefault(t => t.Id == templateId && t.TenantId == tenantId));

        public Task AddAsync(ContractTemplate template, CancellationToken cancel)
        {
            _templates.Add(template);
            return Task.CompletedTask;
        }

        public void Remove(ContractTemplate template) => _templates.Remove(template);

        // IContractRepository
        public Task<IReadOnlyList<Contract>> ListByTenantAsync(
            Guid tenantId,
            Guid? createdByUserId,
            string? query,
            ContractStatus? status,
            CancellationToken cancel)
        {
            IEnumerable<Contract> q = _contracts.Where(c => c.TenantId == tenantId);
            if (createdByUserId.HasValue)
            {
                q = q.Where(c => c.CreatedByUserId == createdByUserId.Value);
            }

            if (status.HasValue)
            {
                q = q.Where(c => c.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                string term = query.Trim();
                q = q.Where(c =>
                    c.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Signers.Any(s =>
                        s.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        s.Email.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            return Task.FromResult<IReadOnlyList<Contract>>(q.OrderByDescending(c => c.CreatedAt).ToList());
        }

        public Task<Contract?> FindByIdAsync(Guid tenantId, Guid contractId, CancellationToken cancel) =>
            Task.FromResult(_contracts.FirstOrDefault(c => c.Id == contractId && c.TenantId == tenantId));

        Task<Contract?> IContractRepository.FindByIdAsync(Guid contractId, CancellationToken cancel) =>
            Task.FromResult(_contracts.FirstOrDefault(c => c.Id == contractId));

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancel) =>
            Task.FromResult(_contracts.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Contract contract, CancellationToken cancel)
        {
            _contracts.Add(contract);
            return Task.CompletedTask;
        }

        public Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken cancel)
        {
            int count = 0;
            foreach (var c in _contracts.Where(c => (c.Status == ContractStatus.Pending || c.Status == ContractStatus.Signing) && c.DueAt != null && c.DueAt <= now))
            {
                c.MarkExpiredIfDue(now);
                count++;
            }
            return Task.FromResult(count);
        }

        public Task ReplaceSignersAsync(Contract contract, IReadOnlyList<ContractSigner> newSigners, CancellationToken cancel) =>
            Task.CompletedTask;

        // IUserRepository
        Task<User?> IUserRepository.FindByIdAsync(Guid id, CancellationToken cancel) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyDictionary<Guid, User>>(_users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id));

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancel) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Email == User.NormalizeEmail(email)));

        public Task AddAsync(User user, CancellationToken cancel)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancel) => Task.CompletedTask;
    }
}
