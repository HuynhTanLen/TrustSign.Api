namespace TrustSign.Api.Observability;

public static class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>
    /// Giữ mã theo dõi client gửi tới nếu là GUID hợp lệ, ngược lại sinh mới (NFR13).
    /// Mã được trả về trong response và gắn vào mọi dòng log của request.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        ILogger logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request");

        return app.Use(async (context, next) =>
        {
            string id = Guid.TryParse(context.Request.Headers[HeaderName], out Guid parsed)
                ? parsed.ToString("D")
                : Guid.NewGuid().ToString("D");
            context.TraceIdentifier = id;
            context.Response.Headers[HeaderName] = id;

            using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
            {
                await next(context);
            }
        });
    }
}
