using System.Net;
using System.Net.Mail;
using Insaaf.Application.Auth.Abstractions;
using Microsoft.Extensions.Options;

namespace Insaaf.Infrastructure.Email;

/// <summary>SMTP email for hosting (Phase 12). Credentials come from configuration, never from the repository.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            throw new InvalidOperationException("Email:SmtpHost is not configured.");
        }

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort) { EnableSsl = _options.SmtpEnableSsl };
        if (!string.IsNullOrWhiteSpace(_options.SmtpUserName))
        {
            client.Credentials = new NetworkCredential(_options.SmtpUserName, _options.SmtpPassword);
        }

        using var mail = new MailMessage(_options.From, message.To, message.Subject, message.Body)
        {
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8,
        };
        await client.SendMailAsync(mail, cancellationToken);
    }
}
