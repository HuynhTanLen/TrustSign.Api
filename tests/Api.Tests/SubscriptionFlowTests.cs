using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.Entities.Subscriptions;
using TrustSign.Domain.IRepositories;
using TrustSign.Domain.IRepositories.Identity;
using TrustSign.Domain.IRepositories.Subscriptions;

namespace TrustSign.Api.Tests;

public sealed class SubscriptionFlowTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemorySubscriptionStore _store = new();
    private readonly IOptions<SepayOptions> _sepayOptions = Options.Create(new SepayOptions
    {
        WebhookApiKey = "trustsign_sepay_dev_secret",
        BankAccount = "0359851608",
        BankName = "MBBank",
        BankCode = "MB",
        AccountName = "CHU TIN",
        QrTemplateUrl = "https://qr.sepay.vn/img?acc={acc}&bank={bank}&amount={amount}&des={des}&template=compact"
    });

    private readonly IPlanSeeder _seeder;
    private readonly IPlanService _planService;
    private readonly ISubscriptionProvisioner _provisioner;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ISepayWebhookService _webhookService;
    private readonly IQuotaService _quotaService;
    private readonly IInvoicePdfService _pdfService;

    public SubscriptionFlowTests()
    {
        _seeder = new PlanSeeder(_store, _clock, NullLogger<PlanSeeder>.Instance);
        _planService = new PlanService(_store, _clock);
        _provisioner = new SubscriptionProvisioner(_store, _store, _seeder, NullLogger<SubscriptionProvisioner>.Instance);
        _subscriptionService = new SubscriptionService(_store, _store, _store, _sepayOptions, _clock);
        _webhookService = new SepayWebhookService(_store, _store, _store, _store, _clock, NullLogger<SepayWebhookService>.Instance);
        _quotaService = new QuotaService(_store, _clock);
        _pdfService = new InvoicePdfService(_store, _store);
    }

    [Fact]
    public async Task Seed_default_plans_creates_three_active_plans()
    {
        await _seeder.SeedDefaultPlansAsync(Cancel);

        Result<IReadOnlyList<PlanDto>> plansResult = await _planService.GetPlansAsync(includeInactive: false, Cancel);
        Assert.True(plansResult.IsSuccess);
        IReadOnlyList<PlanDto> plans = plansResult.Value;

        Assert.Equal(3, plans.Count);
        Assert.Contains(plans, p => p.IsTrial && p.Price == 0 && p.MonthlyQuota == 5 && p.DurationDays == 14);
        Assert.Contains(plans, p => !p.IsTrial && p.Price == 199_000 && p.MonthlyQuota == 30 && p.DurationDays == 30);
        Assert.Contains(plans, p => !p.IsTrial && p.Price == 599_000 && p.MonthlyQuota == 150 && p.DurationDays == 30);
    }

    [Fact]
    public async Task Provision_trial_subscription_on_new_tenant_creates_14_day_trial_SUB03()
    {
        Guid tenantId = Guid.NewGuid();

        Result result = await _provisioner.ProvisionTrialSubscriptionAsync(tenantId, _clock.GetUtcNow(), Cancel);
        Assert.True(result.IsSuccess);

        Result<CurrentSubscriptionDto> currentSub = await _subscriptionService.GetCurrentSubscriptionAsync(tenantId, Cancel);
        Assert.True(currentSub.IsSuccess);
        Assert.Equal("Dùng thử", currentSub.Value.PlanName);
        Assert.Equal(SubscriptionStatus.Trial.ToString(), currentSub.Value.Status);
        Assert.Equal(5, currentSub.Value.QuotaLimit);
        Assert.Equal(0, currentSub.Value.QuotaUsed);
        Assert.Equal(_clock.GetUtcNow().AddDays(14), currentSub.Value.ExpiresAt);
    }

    [Fact]
    public async Task Create_invoice_generates_vietqr_url_and_pending_status_SUB04()
    {
        await _seeder.SeedDefaultPlansAsync(Cancel);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        Result<IReadOnlyList<PlanDto>> plans = await _planService.GetPlansAsync(includeInactive: false, Cancel);
        PlanDto basicPlan = plans.Value.First(p => !p.IsTrial && p.Price == 199_000);

        Result<CheckoutDto> checkout = await _subscriptionService.CreateInvoiceAsync(
            tenantId,
            userId,
            new CreateInvoiceRequestDto(basicPlan.Id),
            Cancel);

        Assert.True(checkout.IsSuccess);
        Assert.StartsWith("TS", checkout.Value.InvoiceCode);
        Assert.Equal(199_000, checkout.Value.Amount);
        Assert.Equal("0359851608", checkout.Value.BankAccount);
        Assert.NotNull(checkout.Value.QrImageUrl);
        Assert.Contains("acc=0359851608", checkout.Value.QrImageUrl);
        Assert.Contains("bank=MB", checkout.Value.QrImageUrl);
        Assert.Contains("amount=199000", checkout.Value.QrImageUrl);
        Assert.Contains($"des={checkout.Value.InvoiceCode}", checkout.Value.QrImageUrl);

        Result<InvoiceStatusDto> status = await _subscriptionService.GetInvoiceStatusAsync(tenantId, checkout.Value.InvoiceCode, Cancel);
        Assert.True(status.IsSuccess);
        Assert.Equal(InvoiceStatus.Pending.ToString(), status.Value.Status);
    }

    [Fact]
    public async Task Sepay_webhook_processes_payment_and_activates_subscription_TC16()
    {
        await _seeder.SeedDefaultPlansAsync(Cancel);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        Result<IReadOnlyList<PlanDto>> plans = await _planService.GetPlansAsync(includeInactive: false, Cancel);
        PlanDto basicPlan = plans.Value.First(p => !p.IsTrial && p.Price == 199_000);

        Result<CheckoutDto> checkout = await _subscriptionService.CreateInvoiceAsync(
            tenantId,
            userId,
            new CreateInvoiceRequestDto(basicPlan.Id),
            Cancel);

        long transactionId = 987654321;
        var payload = new SepayWebhookPayload(
            Id: transactionId,
            Gateway: "MBBank",
            TransactionDate: "2026-10-29 09:15:00",
            AccountNumber: "0359851608",
            Code: null,
            Content: $"Thanh toan goi cuoc {checkout.Value.InvoiceCode}",
            TransferType: "in",
            TransferAmount: 199_000,
            Accumulated: 199_000,
            SubAccount: null,
            ReferenceCode: "MB.REF.12345",
            Description: "Chuyen khoan");

        Result webhookResult = await _webhookService.ProcessWebhookAsync(payload, Cancel);
        Assert.True(webhookResult.IsSuccess);

        Result<InvoiceStatusDto> status = await _subscriptionService.GetInvoiceStatusAsync(tenantId, checkout.Value.InvoiceCode, Cancel);
        Assert.True(status.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid.ToString(), status.Value.Status);

        Result<CurrentSubscriptionDto> currentSub = await _subscriptionService.GetCurrentSubscriptionAsync(tenantId, Cancel);
        Assert.True(currentSub.IsSuccess);
        Assert.Equal("Cơ bản", currentSub.Value.PlanName);
        Assert.Equal(SubscriptionStatus.Active.ToString(), currentSub.Value.Status);
        Assert.Equal(30, currentSub.Value.QuotaLimit);
        Assert.Equal(_clock.GetUtcNow().AddDays(30), currentSub.Value.ExpiresAt);
    }

    [Fact]
    public async Task Sepay_webhook_idempotency_duplicate_transaction_is_noop_TC17()
    {
        await _seeder.SeedDefaultPlansAsync(Cancel);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        Result<IReadOnlyList<PlanDto>> plans = await _planService.GetPlansAsync(includeInactive: false, Cancel);
        PlanDto basicPlan = plans.Value.First(p => !p.IsTrial && p.Price == 199_000);

        Result<CheckoutDto> checkout = await _subscriptionService.CreateInvoiceAsync(
            tenantId,
            userId,
            new CreateInvoiceRequestDto(basicPlan.Id),
            Cancel);

        long transactionId = 1122334455;
        var payload = new SepayWebhookPayload(
            Id: transactionId,
            Gateway: "MBBank",
            TransactionDate: "2026-10-29 09:20:00",
            AccountNumber: "0359851608",
            Code: null,
            Content: $"TS thanh toan {checkout.Value.InvoiceCode}",
            TransferType: "in",
            TransferAmount: 199_000,
            Accumulated: 199_000,
            SubAccount: null,
            ReferenceCode: "MB.REF.999",
            Description: "Chuyen khoan");

        // Gửi lần 1: thành công
        Result res1 = await _webhookService.ProcessWebhookAsync(payload, Cancel);
        Assert.True(res1.IsSuccess);

        Result<CurrentSubscriptionDto> subAfterFirst = await _subscriptionService.GetCurrentSubscriptionAsync(tenantId, Cancel);
        DateTimeOffset expiresAtFirst = subAfterFirst.Value.ExpiresAt;

        // Gửi lần 2 cùng TransactionId: trả 200 OK ngay (Idempotent), không cộng thêm ngày hạn
        Result res2 = await _webhookService.ProcessWebhookAsync(payload, Cancel);
        Assert.True(res2.IsSuccess);

        Result<CurrentSubscriptionDto> subAfterSecond = await _subscriptionService.GetCurrentSubscriptionAsync(tenantId, Cancel);
        Assert.Equal(expiresAtFirst, subAfterSecond.Value.ExpiresAt);
    }

    [Fact]
    public async Task Quota_service_blocks_consumption_when_limit_reached_TC15_TC21()
    {
        Guid tenantId = Guid.NewGuid();
        await _provisioner.ProvisionTrialSubscriptionAsync(tenantId, _clock.GetUtcNow(), Cancel);

        // Gói dùng thử có hạn mức 5 lượt ký
        for (int i = 0; i < 5; i++)
        {
            Assert.True(await _quotaService.HasAvailableQuotaAsync(tenantId, Cancel));
            Result consume = await _quotaService.ConsumeQuotaAsync(tenantId, Cancel);
            Assert.True(consume.IsSuccess, $"Lượt ký thứ {i + 1} phải thành công.");
        }

        // Lượt ký thứ 6 vượt hạn mức -> Bị chặn (TC15, TC21)
        Assert.False(await _quotaService.HasAvailableQuotaAsync(tenantId, Cancel));
        Result overLimit = await _quotaService.ConsumeQuotaAsync(tenantId, Cancel);
        Assert.True(overLimit.IsFailure);
        Assert.Equal(SubscriptionErrors.QuotaExceeded.Code, overLimit.Error.Code);
    }

    [Fact]
    public async Task Quota_service_blocks_consumption_when_subscription_expired_TC18()
    {
        Guid tenantId = Guid.NewGuid();
        await _provisioner.ProvisionTrialSubscriptionAsync(tenantId, _clock.GetUtcNow(), Cancel);

        // Tiến thời gian qua 15 ngày (gói dùng thử 14 ngày đã hết hạn)
        _clock.Current = _clock.Current.AddDays(15);

        Assert.False(await _quotaService.HasAvailableQuotaAsync(tenantId, Cancel));
        Result consume = await _quotaService.ConsumeQuotaAsync(tenantId, Cancel);
        Assert.True(consume.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionExpired.Code, consume.Error.Code);
    }

    [Fact]
    public async Task Generate_invoice_receipt_pdf_returns_valid_pdf_SUB06()
    {
        await _seeder.SeedDefaultPlansAsync(Cancel);
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        // Lưu giả lập thông tin tenant
        Tenant tenant = Tenant.Create("Công ty CP Công Nghệ Chữ Tín", "0109998888", "Hà Nội", "contact@chutin.vn", userId, _clock.GetUtcNow());
        await _store.AddTenantAsync(tenant, Cancel);

        Result<IReadOnlyList<PlanDto>> plans = await _planService.GetPlansAsync(includeInactive: false, Cancel);
        PlanDto basicPlan = plans.Value.First(p => !p.IsTrial && p.Price == 199_000);

        Result<CheckoutDto> checkout = await _subscriptionService.CreateInvoiceAsync(
            tenantId,
            userId,
            new CreateInvoiceRequestDto(basicPlan.Id),
            Cancel);

        // Thanh toán hóa đơn qua webhook
        await _webhookService.ProcessWebhookAsync(new SepayWebhookPayload(
            Id: 554433,
            Gateway: "MBBank",
            TransactionDate: "2026-10-29 09:30:00",
            AccountNumber: "0359851608",
            Code: null,
            Content: checkout.Value.InvoiceCode,
            TransferType: "in",
            TransferAmount: 199_000,
            Accumulated: 199_000,
            SubAccount: null,
            ReferenceCode: "MB9988",
            Description: null),
            Cancel);

        Result<byte[]> pdfResult = await _pdfService.GenerateInvoiceReceiptPdfAsync(checkout.Value.InvoiceCode, tenantId, Cancel);
        Assert.True(pdfResult.IsSuccess);
        Assert.NotEmpty(pdfResult.Value);

        // Header PDF chuẩn: 4 bytes đầu là %PDF
        string header = System.Text.Encoding.ASCII.GetString(pdfResult.Value.Take(4).ToArray());
        Assert.Equal("%PDF", header);
    }

    private sealed class FakeClock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = initial;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class InMemorySubscriptionStore :
        IPlanRepository,
        ISubscriptionRepository,
        IInvoiceRepository,
        IUnitOfWork,
        ITenantRepository
    {
        private readonly List<Plan> _plans = [];
        private readonly List<Subscription> _subscriptions = [];
        private readonly List<Invoice> _invoices = [];
        private readonly List<PaymentTransaction> _transactions = [];
        private readonly List<Tenant> _tenants = [];

        // ITenantRepository
        public Task AddTenantAsync(Tenant tenant, CancellationToken cancel = default)
        {
            _tenants.Add(tenant);
            return Task.CompletedTask;
        }

        Task<Tenant?> ITenantRepository.FindByIdAsync(Guid id, CancellationToken cancel) =>
            Task.FromResult(_tenants.FirstOrDefault(t => t.Id == id));

        public Task AddTenantWithOwnerAsync(Tenant tenant, Membership ownerMembership, CancellationToken cancel)
        {
            _tenants.Add(tenant);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserTenantMembership>> GetUserTenantsAsync(Guid userId, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<UserTenantMembership>>([]);

        public Task<Membership?> FindMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancel) =>
            Task.FromResult<Membership?>(null);

        public Task<IReadOnlyList<Membership>> GetMembersAsync(Guid tenantId, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<Membership>>([]);

        public Task AddMembershipAsync(Membership membership, CancellationToken cancel) => Task.CompletedTask;

        public void RemoveMembership(Membership membership) { }

        public Task<TenantInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancel) =>
            Task.FromResult<TenantInvitation?>(null);

        public Task<TenantInvitation?> FindInvitationByTokenHashAsync(string tokenHash, CancellationToken cancel) =>
            Task.FromResult<TenantInvitation?>(null);

        public Task<IReadOnlyList<TenantInvitation>> GetPendingInvitationsAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<TenantInvitation>>([]);

        public Task AddInvitationAsync(TenantInvitation invitation, CancellationToken cancel) => Task.CompletedTask;

        // IUnitOfWork
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancel)
            where TResult : Result => await action();

        // IPlanRepository
        public Task<IReadOnlyList<Plan>> ListAsync(bool includeInactive, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<Plan>>(_plans.Where(p => includeInactive || p.IsActive).ToList());

        Task<Plan?> IPlanRepository.FindByIdAsync(Guid planId, CancellationToken cancel) =>
            Task.FromResult(_plans.FirstOrDefault(p => p.Id == planId));

        public Task<IReadOnlyDictionary<Guid, Plan>> FindByIdsAsync(IReadOnlyCollection<Guid> planIds, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Plan>>(_plans.Where(p => planIds.Contains(p.Id)).ToDictionary(p => p.Id));

        public Task<Plan?> FindActiveTrialAsync(CancellationToken cancel) =>
            Task.FromResult(_plans.FirstOrDefault(p => p.IsTrial && p.IsActive));

        public Task<bool> AnyAsync(CancellationToken cancel) =>
            Task.FromResult(_plans.Count > 0);

        public Task AddAsync(Plan plan, CancellationToken cancel)
        {
            _plans.Add(plan);
            return Task.CompletedTask;
        }

        // ISubscriptionRepository
        public Task<Subscription?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancel) =>
            Task.FromResult(_subscriptions.FirstOrDefault(s => s.TenantId == tenantId));

        public Task AddAsync(Subscription subscription, CancellationToken cancel)
        {
            _subscriptions.Add(subscription);
            return Task.CompletedTask;
        }

        public Task<bool> TryConsumeQuotaAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancel)
        {
            Subscription? sub = _subscriptions.FirstOrDefault(s => s.TenantId == tenantId);
            if (sub is null)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(sub.TryConsumeQuota(now));
        }

        public Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken cancel)
        {
            int count = 0;
            foreach (Subscription s in _subscriptions)
            {
                if (s.MarkExpiredIfDue(now))
                {
                    count++;
                }
            }
            return Task.FromResult(count);
        }

        // IInvoiceRepository
        public Task<Invoice?> FindByCodeAsync(string code, CancellationToken cancel) =>
            Task.FromResult(_invoices.FirstOrDefault(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<Invoice>> ListByTenantAsync(Guid tenantId, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<Invoice>>(_invoices.Where(i => i.TenantId == tenantId).OrderByDescending(i => i.CreatedAt).ToList());

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancel) =>
            Task.FromResult(_invoices.Any(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Invoice invoice, CancellationToken cancel)
        {
            _invoices.Add(invoice);
            return Task.CompletedTask;
        }

        Task<int> IInvoiceRepository.ExpireDueAsync(DateTimeOffset now, CancellationToken cancel)
        {
            int count = 0;
            foreach (Invoice inv in _invoices)
            {
                if (inv.MarkExpiredIfDue(now))
                {
                    count++;
                }
            }
            return Task.FromResult(count);
        }

        public Task<bool> TransactionExistsAsync(long sepayTransactionId, CancellationToken cancel) =>
            Task.FromResult(_transactions.Any(t => t.SepayTransactionId == sepayTransactionId));

        public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancel)
        {
            _transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancel) => Task.CompletedTask;
    }
}
