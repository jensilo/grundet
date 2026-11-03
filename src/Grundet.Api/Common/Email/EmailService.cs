using MailKit.Security;
using MimeKit;

namespace Grundet.Api.Common.Email;

public sealed class EmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        string? host = configuration.GetValue<string>("Email:SmtpHost");
        int port = configuration.GetValue<int?>("Email:SmtpPort") ?? 587;
        string? fromAddress = configuration.GetValue<string>("Email:FromAddress");
        string fromName = configuration.GetValue<string>("Email:FromName") ?? "Grundet";
        string? username = configuration.GetValue<string>("Email:SmtpUsername");
        string? password = configuration.GetValue<string>("Email:SmtpPassword");
        bool useSsl = configuration.GetValue<bool?>("Email:SmtpUseSsl") ?? false;

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Email:SmtpHost is not configured.");
        }

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("Email:FromAddress is not configured.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
        var socketOptions = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await smtpClient.ConnectAsync(host, port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(username))
        {
            await smtpClient.AuthenticateAsync(username, password ?? string.Empty, cancellationToken);
        }

        await smtpClient.SendAsync(message, cancellationToken);
        await smtpClient.DisconnectAsync(true, cancellationToken);
    }
}
