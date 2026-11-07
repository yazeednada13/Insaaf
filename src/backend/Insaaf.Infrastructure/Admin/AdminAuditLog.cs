using Insaaf.Application.Admin;
using Microsoft.Extensions.Logging;

namespace Insaaf.Infrastructure.Admin;

/// <summary>
/// Writes one structured log line per Admin mutation (ADR-017). Only ids, the action, the time and the
/// result are logged; never content text, reasons, emails, names, phone numbers or tokens.
/// </summary>
public sealed class AdminAuditLog : IAdminAuditLog
{
    private readonly ILogger<AdminAuditLog> _logger;

    public AdminAuditLog(ILogger<AdminAuditLog> logger)
    {
        _logger = logger;
    }

    public void Record(AdminAuditEntry entry) =>
        _logger.LogInformation(
            "Admin action {Action} by {AdminId} on {TargetType} {TargetId} at {Timestamp}: {Result}",
            entry.Action,
            entry.AdminId,
            entry.TargetType,
            entry.TargetId,
            entry.Timestamp,
            entry.Result);
}
