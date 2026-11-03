using Microsoft.AspNetCore.Diagnostics;

namespace Grundet.Api.Common.Errors;

public sealed class HttpProblemExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not HttpProblemException httpProblem)
        {
            return false;
        }

        httpContext.Response.StatusCode = httpProblem.StatusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(
            ProblemDetailsExtensions.CreateProblem(
                httpProblem.StatusCode,
                httpProblem.Type,
                httpProblem.Title,
                httpProblem.Detail),
            cancellationToken);

        return true;
    }
}
