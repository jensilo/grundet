using Microsoft.AspNetCore.Http;

namespace Grundet.Api.Common.Errors;

public sealed class HttpProblemException(int statusCode, string type, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Type { get; } = type;
    public string Title { get; } = title;
    public string Detail { get; } = detail;

    public static HttpProblemException BadRequest(string type, string detail, string title = "Bad Request")
        => new(StatusCodes.Status400BadRequest, type, title, detail);

    public static HttpProblemException Unauthorized(string type, string detail, string title = "Unauthorized")
        => new(StatusCodes.Status401Unauthorized, type, title, detail);

    public static HttpProblemException Unauthorized(string detail)
        => Unauthorized(ProblemTypes.Unauthorized, detail);

    public static HttpProblemException Forbidden(string type, string detail, string title = "Forbidden")
        => new(StatusCodes.Status403Forbidden, type, title, detail);

    public static HttpProblemException Forbidden(string detail)
        => Forbidden(ProblemTypes.Forbidden, detail);

    public static HttpProblemException Conflict(string type, string detail, string title = "Conflict")
        => new(StatusCodes.Status409Conflict, type, title, detail);

    public static HttpProblemException NotFound(string type, string detail, string title = "Not Found")
        => new(StatusCodes.Status404NotFound, type, title, detail);

    public static HttpProblemException NotFound(string detail)
        => NotFound(ProblemTypes.NotFound, detail);

    public static HttpProblemException TooManyRequests(string type, string detail, string title = "Too Many Requests")
        => new(StatusCodes.Status429TooManyRequests, type, title, detail);

    public static HttpProblemException InternalServerError(
        string type,
        string detail,
        string title = "Internal Server Error")
        => new(StatusCodes.Status500InternalServerError, type, title, detail);
}
