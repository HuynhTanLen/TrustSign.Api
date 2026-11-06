using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TrustSign.Api.Http;
using TrustSign.Api.Observability;
using TrustSign.Api.Security;
using TrustSign.Application.DependencyInjection;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Domain.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDomainRepositories(builder.Configuration.GetConnectionString("TrustSignDb"));
builder.Services.AddApplication();
builder.Services.AddTrustSignJwtAuthentication(builder.Environment);
builder.Services.AddTrustSignRateLimiter();

// AllowCredentials: trình duyệt phải gửi được cookie refresh token (BR10) từ origin của frontend.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

builder.Services.AddMemoryCache();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddApiProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<TrustSignDbContext>(tags: ["ready"]);

// Chạy sau Caddy: lấy IP thật của client cho rate limit và scheme https.
// Chỉ an toàn vì cổng API không mở ra ngoài (docker-compose.prod.yml không publish), mọi request đều qua Caddy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
// Live: tiến trình còn sống. Ready: kết nối được database, tức là nhận được traffic (NFR14).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("AutoMigrate"))
{
    await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
    {
        await scope.ServiceProvider.GetRequiredService<TrustSignDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IAdminSeeder>().SeedAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IPlanSeeder>().SeedDefaultPlansAsync(CancellationToken.None);
    }
}

if (app.Environment.IsProduction())
{
    var storageOpts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContractStorageOptions>>().Value;
    if (string.IsNullOrWhiteSpace(storageOpts.DocumentTokenSecret) ||
        storageOpts.DocumentTokenSecret.Equals("trustsign_dev_document_token_hmac_secret_2026", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "LỖI BẢO MẬT: Chưa cấu hình 'ContractStorage:DocumentTokenSecret' an toàn trên môi trường Production!");
    }
}

app.Run();
