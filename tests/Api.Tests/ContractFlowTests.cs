using System.Security.Cryptography;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Contracts;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories;
using TrustSign.Domain.IRepositories.Contracts;
using TrustSign.Domain.IRepositories.Identity;

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
    private readonly IContractFileStorage _fileStorage;
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
        IOptions<ContractStorageOptions> storageOptions = Options.Create(new ContractStorageOptions
        {
            StorageRootPath = _tempStorageDir,
            PublicApiBaseUrl = "http://localhost:5050",
            DocumentTokenSecret = "test_hmac_secret_key_for_document_tokens_2026",
            DocumentUrlLifetimeMinutes = 30
        });

        IOptions<SmtpOptions> smtpOptions = Options.Create(new SmtpOptions
        {
            FrontendBaseUrl = "http://localhost:3000"
        });

        _fileStorage = new LocalContractFileStorage(storageOptions);
        _templateService = new TemplateService(_store, _store, _clock);
        _contractService = new ContractService(
            _store,
            _store,
            _store,
            _fileStorage,
            _pdfGenerator,
            _quota,
            _store,
            _emails,
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
            Cancel);
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
            Cancel);

        Assert.True(uploaded.IsSuccess);
        Contract? contract = await _store.FindByIdAsync(_tenantA, uploaded.Value.ContractId, Cancel);
        Assert.NotNull(contract);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(validPdf)), contract.OriginalFileHash);
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
            Cancel);

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
            Cancel);

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
            Cancel);

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
            Cancel);

        await _contractService.UploadDraftAsync(
            _tenantA,
            _employee2Id,
            "Hợp đồng của Nhân viên 2",
            validPdf,
            Cancel);

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
        public EmailMessage? LastSent { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancel)
        {
            LastSent = message;
            return Task.CompletedTask;
        }
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

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancel) =>
            Task.FromResult(_contracts.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Contract contract, CancellationToken cancel)
        {
            _contracts.Add(contract);
            return Task.CompletedTask;
        }

        public Task ReplaceSignersAsync(Contract contract, IReadOnlyList<ContractSigner> newSigners, CancellationToken cancel) =>
            Task.CompletedTask;

        // IUserRepository
        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancel) =>
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
