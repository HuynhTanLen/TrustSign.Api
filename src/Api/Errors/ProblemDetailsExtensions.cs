using TrustSign.Common;
using Microsoft.AspNetCore.Mvc;

namespace TrustSign.Api.Errors;

public static class ProblemDetailsExtensions
{
    /// <summary>Mọi lỗi trả về theo Problem Details (RFC 9457), kèm mã theo dõi để tra log.</summary>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);

    /// <summary>Đổi lỗi nghiệp vụ thành response: <c>return this.ToProblem(result.Error);</c></summary>
    public static ObjectResult ToProblem(this ControllerBase controller, Error error) =>
        controller.Problem(
            detail: error.Message,
            statusCode: error.Type.ToStatusCode(),
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    public static int ToStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError
    };
}
