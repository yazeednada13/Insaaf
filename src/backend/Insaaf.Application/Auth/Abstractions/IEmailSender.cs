namespace Insaaf.Application.Auth.Abstractions;

/// <summary>One plain-text email (Phase 11).</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Sends email (Phase 11: password-reset codes). In Development it writes files to a local outbox; an SMTP
/// sender is selected by configuration for hosting (Phase 12). Message bodies are never logged.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
