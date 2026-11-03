using System.Text.RegularExpressions;
using Grundet.Api.Common.Email;

namespace Grundet.Api.Tests.Infrastructure;

public sealed class TestEmailSink : IEmailService
{
    private readonly object _gate = new();
    private readonly List<CapturedEmail> _messages = [];
    private static readonly Regex TokenRegex = new(@"token=(?<token>[A-Fa-f0-9]+)", RegexOptions.Compiled);
    private static readonly Regex VerifyUrlRegex = new(@"href=""(?<url>https://[^""]+/api/v1/auth/verify[^""]+)""", RegexOptions.Compiled);

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _messages.Add(new CapturedEmail(toEmail, subject, htmlBody, DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _messages.Clear();
        }
    }

    public IReadOnlyList<CapturedEmail> GetMessages(string recipientEmail)
    {
        lock (_gate)
        {
            return _messages
                .Where(message => string.Equals(message.ToEmail, recipientEmail, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public string GetLatestMagicLinkToken(string recipientEmail)
        => GetToken(recipientEmail, "sign-in link");

    public string GetLatestMagicLinkUrl(string recipientEmail)
    {
        CapturedEmail? latest;
        lock (_gate)
        {
            latest = _messages
                .Where(message =>
                    string.Equals(message.ToEmail, recipientEmail, StringComparison.OrdinalIgnoreCase)
                    && message.Subject.Contains("sign-in link", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(message => message.SentAt)
                .FirstOrDefault();
        }

        if (latest is null)
        {
            throw new InvalidOperationException(
                $"No magic-link email captured for recipient '{recipientEmail}'.");
        }

        var match = VerifyUrlRegex.Match(latest.HtmlBody);
        return !match.Success
            ? throw new InvalidOperationException(
                $"No verify URL found in captured email for recipient '{recipientEmail}'.")
            : match.Groups["url"].Value;
    }

    public string GetLatestInviteToken(string recipientEmail)
        => GetToken(recipientEmail, "invited");

    private string GetToken(string recipientEmail, string subjectContains)
    {
        CapturedEmail? latest = null;
        lock (_gate)
        {
            latest = _messages
                .Where(message =>
                    string.Equals(message.ToEmail, recipientEmail, StringComparison.OrdinalIgnoreCase)
                    && message.Subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(message => message.SentAt)
                .FirstOrDefault();
        }

        if (latest is null)
        {
            throw new InvalidOperationException(
                $"No email captured for recipient '{recipientEmail}' with subject containing '{subjectContains}'.");
        }

        var match = TokenRegex.Match(latest.HtmlBody);
        return !match.Success
            ? throw new InvalidOperationException(
                $"No token parameter found in captured email for recipient '{recipientEmail}'.")
            : Uri.UnescapeDataString(match.Groups["token"].Value);
    }
}

public sealed record CapturedEmail(string ToEmail, string Subject, string HtmlBody, DateTime SentAt);
