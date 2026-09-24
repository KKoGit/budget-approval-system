namespace BudgetApproval.Domain.Entities;

/// <summary>
/// An immutable record of something that happened to a budget request. Entries are only created by
/// <see cref="BudgetRequest"/> itself, so a request cannot change without being audited. The
/// persistence layer additionally refuses to update or delete existing entries.
/// </summary>
public class AuditEntry
{
    private AuditEntry() { } // EF Core

    internal AuditEntry(
        AuditAction action,
        RequestStatus? fromStatus,
        RequestStatus toStatus,
        Common.Actor actor,
        string? comment,
        string? changes,
        DateTime occurredAtUtc)
    {
        Action = action;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorUserId = actor.UserId;
        ActorName = actor.DisplayName;
        Comment = comment;
        Changes = changes;
        OccurredAtUtc = occurredAtUtc;
    }

    public long Id { get; private set; }
    public int BudgetRequestId { get; private set; }
    public AuditAction Action { get; private set; }
    public RequestStatus? FromStatus { get; private set; }
    public RequestStatus ToStatus { get; private set; }
    public int ActorUserId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string? Comment { get; private set; }
    public string? Changes { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
}
