using System.Net;
using Grundet.Api.Data.Entities;
using Grundet.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Xunit;

namespace Grundet.Api.Tests;

[Collection(ApiIntegrationCollection.Name)]
[Trait(TestCategories.Category, TestCategories.DockerRequired)]
public sealed class FeedbackIntegrationTests(ApiIntegrationFixture fixture)
{
    [Fact]
    public async Task CreateFeedback_WithScreenshot_ReencodesStoresFileAndAttachmentRow()
    {
        AuthSession user = await TestApi.SignInAsync(fixture);
        using var form = CreateFeedbackForm();
        form.Add(new ByteArrayContent(CreatePng(64, 32)), "screenshots", "screen.png");

        var response = await user.Client.PostAsync("/api/v1/feedback", form);

        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
        FeedbackSubmission submission = await fixture.WithDbContextAsync(async db =>
            await db.FeedbackSubmissions.SingleAsync(f => f.UserId == user.UserId));
        FeedbackAttachment attachment = await fixture.WithDbContextAsync(async db =>
            await db.FeedbackAttachments.SingleAsync(a => a.FeedbackSubmissionId == submission.Id));
        Assert.Equal("image/jpeg", attachment.ContentType);
        Assert.Equal("screen.png", attachment.OriginalFileName);
        Assert.Equal(64, attachment.Width);
        Assert.Equal(32, attachment.Height);
        Assert.EndsWith(".jpg", attachment.StoragePath, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.FeedbackAttachmentsRoot, attachment.StoragePath)));
    }

    [Fact]
    public async Task CreateFeedback_WithInvalidScreenshot_RejectsAndDoesNotPersistSubmission()
    {
        AuthSession user = await TestApi.SignInAsync(fixture);
        using var form = CreateFeedbackForm();
        form.Add(new ByteArrayContent("not an image"u8.ToArray()), "screenshots", "screen.txt");

        var response = await user.Client.PostAsync("/api/v1/feedback", form);

        await AssertStatusCodeAsync(response, HttpStatusCode.BadRequest);
        int submissionCount = await fixture.WithDbContextAsync(async db =>
            await db.FeedbackSubmissions.CountAsync(f => f.UserId == user.UserId));
        Assert.Equal(0, submissionCount);
    }

    [Fact]
    public async Task CreateFeedback_WithTooManyScreenshots_RejectsAndDoesNotPersistSubmission()
    {
        AuthSession user = await TestApi.SignInAsync(fixture);
        using var form = CreateFeedbackForm();
        for (int i = 0; i < 6; i++)
        {
            form.Add(new ByteArrayContent(CreatePng(8, 8)), "screenshots", $"screen-{i}.png");
        }

        var response = await user.Client.PostAsync("/api/v1/feedback", form);

        await AssertStatusCodeAsync(response, HttpStatusCode.BadRequest);
        int submissionCount = await fixture.WithDbContextAsync(async db =>
            await db.FeedbackSubmissions.CountAsync(f => f.UserId == user.UserId));
        Assert.Equal(0, submissionCount);
    }

    private static async Task AssertStatusCodeAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {expected}, got {response.StatusCode}: {body}");
        }
    }

    private static MultipartFormDataContent CreateFeedbackForm()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Bug"), "kind" },
            { new StringContent("The feedback screenshot upload test message."), "message" },
            { new StringContent("/dashboard?token=secret&tab=home"), "pagePath" },
            { new StringContent("integration-test"), "userAgent" },
            { new StringContent("1024"), "viewportWidth" },
            { new StringContent("768"), "viewportHeight" },
            { new StringContent("en-US"), "locale" },
            { new StringContent("Europe/Stockholm"), "timeZone" }
        };
        return form;
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
