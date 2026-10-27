using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using TrustSign.Api.Errors;
using TrustSign.Api.Observability;
using TrustSign.Api.Security;
using TrustSign.Application.DependencyInjection;
using TrustSign.Application.Modules.Identity;
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

builder.Services.AddControllers();
builder.Services.AddApiProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<TrustSignDbContext>(tags: ["ready"]);

var app = builder.Build();

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

if (app.Environment.IsDevelopment())
{
    await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
    {
        await scope.ServiceProvider.GetRequiredService<TrustSignDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IAdminSeeder>().SeedAsync(CancellationToken.None);
    }
}

app.Run();
