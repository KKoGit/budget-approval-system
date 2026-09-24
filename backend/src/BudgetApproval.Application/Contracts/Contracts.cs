using System.ComponentModel.DataAnnotations;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Application.Contracts;

// ------------------------------------------------------------------ queries

public sealed record BudgetRequestQuery
{
    public int? FiscalYear { get; init; }
    public int? DepartmentId { get; init; }
    public RequestStatus? Status { get; init; }
    public BudgetCategory? Category { get; init; }
    public string? Search { get; init; }

    /// <summary>One of: updated, submitted, amount, title, department, status. Unknown values fall back to updated.</summary>
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public bool IsDescending => !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, 100);
}

// ------------------------------------------------------------------ commands

public sealed record CreateBudgetRequestDto
{
    /// <summary>Optional; defaults to the requester's own department (the only one they may request for).</summary>
    public int? DepartmentId { get; init; }

    [Range(2000, 2100)] public int FiscalYear { get; init; }
    [Required] public BudgetCategory? Category { get; init; }
    [Required, StringLength(BudgetRequest.TitleMaxLength)] public string Title { get; init; } = string.Empty;

    [Required, StringLength(BudgetRequest.JustificationMaxLength, MinimumLength = BudgetRequest.JustificationMinLength)]
    public string Justification { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "10000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal RequestedAmount { get; init; }
}

public sealed record UpdateBudgetRequestDto
{
    [Required] public BudgetCategory? Category { get; init; }
    [Required, StringLength(BudgetRequest.TitleMaxLength)] public string Title { get; init; } = string.Empty;

    [Required, StringLength(BudgetRequest.JustificationMaxLength, MinimumLength = BudgetRequest.JustificationMinLength)]
    public string Justification { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "10000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal RequestedAmount { get; init; }

    /// <summary>The version the client last saw. Required so an edit never overwrites someone else's change.</summary>
    [Required] public string Version { get; init; } = string.Empty;
}

public sealed record SubmitBudgetRequestDto
{
    [Required] public string Version { get; init; } = string.Empty;
}

public enum DecisionType { Approve, Reject, Return }

public sealed record DecisionDto
{
    [Required] public DecisionType? Decision { get; init; }

    /// <summary>Approve only. Defaults to the full requested amount.</summary>
    public decimal? ApprovedAmount { get; init; }

    [StringLength(BudgetRequest.CommentMaxLength)] public string? Comment { get; init; }

    [Required] public string Version { get; init; } = string.Empty;
}

// ------------------------------------------------------------------ read models

public sealed record BudgetRequestSummaryDto(
    int Id,
    string ReferenceNumber,
    string Title,
    int DepartmentId,
    string DepartmentName,
    int FiscalYear,
    BudgetCategory Category,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    RequestStatus Status,
    string RequestedByName,
    DateTime? SubmittedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AllocationSnapshotDto(decimal Allocated, decimal Approved, decimal Remaining);

public sealed record RequestPermissionsDto(bool CanEdit, bool CanSubmit, bool CanDecide, string? DecisionBlockedReason);

public sealed record AuditEntryDto(
    long Id,
    AuditAction Action,
    RequestStatus? FromStatus,
    RequestStatus ToStatus,
    string ActorName,
    string? Comment,
    string? Changes,
    DateTime OccurredAtUtc);

public sealed record BudgetRequestDetailDto(
    BudgetRequestSummaryDto Summary,
    string Justification,
    int RequestedById,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc,
    string Version,
    RequestPermissionsDto Permissions,
    AllocationSnapshotDto? Allocation,
    IReadOnlyList<AuditEntryDto> History);

public sealed record ApprovalQueueItemDto(
    BudgetRequestSummaryDto Request,
    AllocationSnapshotDto Allocation,
    bool IsOwnRequest,
    bool ExceedsRemaining,
    int DaysWaiting);

public sealed record DepartmentBudgetDto(
    int DepartmentId,
    string Code,
    string Name,
    decimal Allocated,
    decimal Requested,
    decimal Approved,
    decimal Pending,
    decimal Remaining,
    int PendingCount);

public sealed record DashboardDto(
    int? FiscalYear,
    int? DepartmentId,
    decimal Allocated,
    decimal Requested,
    decimal Approved,
    decimal Pending,
    decimal Remaining,
    int PendingCount,
    IReadOnlyDictionary<RequestStatus, int> CountsByStatus,
    IReadOnlyList<DepartmentBudgetDto> Departments);

public sealed record LookupItemDto(int Id, string Code, string Name);

public sealed record LookupsDto(
    IReadOnlyList<LookupItemDto> Departments,
    IReadOnlyList<int> FiscalYears,
    int CurrentFiscalYear,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Statuses);

public sealed record UserDto(
    int Id,
    string DisplayName,
    string Email,
    int DepartmentId,
    string DepartmentName,
    IReadOnlyList<string> Roles);
