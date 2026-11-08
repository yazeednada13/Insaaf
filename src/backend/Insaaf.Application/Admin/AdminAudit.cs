namespace Insaaf.Application.Admin;

/// <summary>Outcomes recorded for an Admin action (ADR-017).</summary>
public static class AdminAuditResult
{
    public const string Succeeded = "succeeded";
    public const string NoChange = "no_change";
    public const string NotFound = "not_found";
    public const string Refused = "refused";
    public const string ValidationFailed = "validation_failed";

    /// <summary>The photo is no longer visible, but its files are still being deleted (Phase 9A).</summary>
    public const string RemovedFilePending = "removed_file_pending";
}

/// <summary>
/// One Admin mutation. Ids, the action and the result only: never review or comment text, reasons,
/// emails, names, phone numbers, tokens or request bodies.
/// </summary>
public sealed record AdminAuditEntry(
    string Action,
    Guid AdminId,
    string TargetType,
    Guid TargetId,
    DateTime Timestamp,
    string Result);

/// <summary>Where Admin actions are recorded. Infrastructure writes structured log lines; there is no table.</summary>
public interface IAdminAuditLog
{
    void Record(AdminAuditEntry entry);
}
