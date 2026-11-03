using System.Security.Claims;
using Grundet.Api.Common.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Grundet.Api.Features.Feedback;

public static class FeedbackEndpoints
{
    private const long MaxFeedbackRequestBytes = 12 * 1024 * 1024;

    public static void Map(RouteGroupBuilder group)
    {
        var feedback = group.MapGroup("/feedback").RequireAuthorization();

        feedback.MapPost("/", async (
                [FromForm] CreateFeedbackRequest request,
                ClaimsPrincipal principal,
                HttpRequest httpRequest,
                FeedbackService feedbackService,
                CancellationToken cancellationToken) =>
            {
                var submission = await feedbackService.CreateAsync(
                    new CreateFeedbackCommand(
                        principal.GetUserId(),
                        request.Kind,
                        request.Message,
                        request.PagePath,
                        request.UserAgent,
                        request.ViewportWidth,
                        request.ViewportHeight,
                        request.Locale,
                        request.TimeZone,
                        ResolveScreenshots(request, httpRequest)),

                    cancellationToken);

                return Results.Created($"/api/v1/feedback/{submission.Id}", new FeedbackResponse(submission.Id, submission.CreatedAt));
            })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxFeedbackRequestBytes))
            .WithName("CreateFeedback")
            .WithSummary("Submit app feedback")
            .Produces<FeedbackResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static IReadOnlyList<IFormFile> ResolveScreenshots(CreateFeedbackRequest request, HttpRequest httpRequest)
        => request.Screenshots is { Count: > 0 }
            ? request.Screenshots
            : httpRequest.Form.Files.GetFiles("screenshots");
}
