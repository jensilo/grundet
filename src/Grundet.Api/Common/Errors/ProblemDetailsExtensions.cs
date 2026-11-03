using Microsoft.AspNetCore.Mvc;

namespace Grundet.Api.Common.Errors;

public static class ProblemDetailsExtensions
{
    public static ProblemDetails CreateProblem(int status, string type, string title, string detail)
        => new()
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = detail
        };
}
