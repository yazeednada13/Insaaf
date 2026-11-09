using System.Text;
using Insaaf.Application.Auth.Abstractions;
using Microsoft.Extensions.Options;

namespace Insaaf.Infrastructure.Email;

/// <summary>
/// Development email (Phase 11): each message becomes a UTF-8 text file in the outbox folder, outside the
/// repository. Nothing leaves the machine.
/// </summary>
public sealed class FileEmailSender : IEmailSender
{
    private readonly string _outbox;

    public FileEmailSender(IOptions<EmailOptions> options)
    {
        _outbox = options.Value.ResolveOutbox();
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_outbox);
        var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt";
        var text = $"To: {message.To}\nSubject: {message.Subject}\n\n{message.Body}\n";
        await File.WriteAllTextAsync(Path.Combine(_outbox, name), text, new UTF8Encoding(false), cancellationToken);
    }
}
