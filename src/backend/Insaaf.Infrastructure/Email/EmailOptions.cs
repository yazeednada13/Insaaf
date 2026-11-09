namespace Insaaf.Infrastructure.Email;

/// <summary>
/// <c>Email</c> settings (Phase 11). <c>Provider</c>: <c>file</c> (default; writes to <c>OutboxPath</c>,
/// default <c>%LOCALAPPDATA%\Insaaf\outbox</c>) or <c>smtp</c> (hosting, Phase 12).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "file";

    public string? OutboxPath { get; set; }

    public string From { get; set; } = "no-reply@insaaf.local";

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUserName { get; set; }

    public string? SmtpPassword { get; set; }

    public bool SmtpEnableSsl { get; set; } = true;

    public string ResolveOutbox() =>
        string.IsNullOrWhiteSpace(OutboxPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Insaaf", "outbox")
            : OutboxPath;
}
