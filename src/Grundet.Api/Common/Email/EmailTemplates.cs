using System.Net;
using System.Text;

namespace Grundet.Api.Common.Email;

public static class EmailTemplates
{
    /// <summary>
    /// Replaces control characters (CR/LF etc.) with spaces so user-supplied values
    /// interpolated into an email subject cannot inject or malform SMTP headers.
    /// </summary>
    public static string SanitizeSubjectLine(string subject)
    {
        var builder = new StringBuilder(subject.Length);
        foreach (char c in subject)
        {
            builder.Append(char.IsControl(c) ? ' ' : c);
        }

        return builder.ToString();
    }

    public static string BuildMagicLinkEmail(string link, int expiryMinutes)
        => $"""
            <p>Use the link below to sign in to Grundet.</p>
            <p><a href="{link}">Sign in to Grundet</a></p>
            <p>This link expires in {expiryMinutes} minutes.</p>
            """;

    public static string BuildInviteEmail(string link, string teamName, string inviterName)
        => $"""
            <p>{WebUtility.HtmlEncode(inviterName)} invited you to join the team {WebUtility.HtmlEncode(teamName)} on Grundet.</p>
            <p><a href="{link}">Accept invitation</a></p>
            <p>If you were not expecting this email, you can ignore it.</p>
            """;

    public static string BuildAlphaWaitlistConfirmationEmail()
        => """
            <p>We received your Grundet alpha request.</p>
            <p>If access opens up, we'll contact you.</p>
            <p>If you did not request access, you can ignore this email.</p>
            """;
}
