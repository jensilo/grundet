using Grundet.Api.Common.Email;
using Xunit;

namespace Grundet.Api.Tests;

public sealed class EmailTemplatesTests
{
    [Fact]
    public void BuildInviteEmail_IncludesInviterNameTeamNameAndLink()
    {
        string html = EmailTemplates.BuildInviteEmail(
            "https://my.grundet.app/invite/accept?token=abc123",
            "Acme Platform",
            "Jane Doe");

        Assert.Contains("Jane Doe invited you to join the team Acme Platform on Grundet.", html);
        Assert.Contains("<a href=\"https://my.grundet.app/invite/accept?token=abc123\">Accept invitation</a>", html);
        Assert.Contains("If you were not expecting this email, you can ignore it.", html);
    }

    [Fact]
    public void SanitizeSubjectLine_ReplacesControlCharactersWithSpaces()
    {
        string subject = EmailTemplates.SanitizeSubjectLine("Jane\r\nBcc: evil@example.com\tinvited you");

        Assert.Equal("Jane  Bcc: evil@example.com invited you", subject);
    }

    [Fact]
    public void SanitizeSubjectLine_LeavesCleanSubjectsUntouched()
        => Assert.Equal(
            "Jane invited you to Acme on Grundet",
            EmailTemplates.SanitizeSubjectLine("Jane invited you to Acme on Grundet"));

    [Fact]
    public void BuildInviteEmail_HtmlEncodesTeamNameAndInviterName()
    {
        string html = EmailTemplates.BuildInviteEmail(
            "https://my.grundet.app/invite/accept?token=abc123",
            "<b>Evil & Co</b>",
            "<script>alert(1)</script>");

        Assert.DoesNotContain("<b>Evil & Co</b>", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;b&gt;Evil &amp; Co&lt;/b&gt;", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    }
}
