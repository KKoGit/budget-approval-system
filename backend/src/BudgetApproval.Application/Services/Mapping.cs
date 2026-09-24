using BudgetApproval.Application.Contracts;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Application.Services;

internal static class Mapping
{
    public static string ReferenceNumber(BudgetRequest r) => $"BR-{r.FiscalYear}-{r.Id:D4}";

    public static BudgetRequestSummaryDto ToSummary(this BudgetRequest r) => new(
        r.Id,
        ReferenceNumber(r),
        r.Title,
        r.DepartmentId,
        r.Department?.Name ?? string.Empty,
        r.FiscalYear,
        r.Category,
        r.RequestedAmount,
        r.ApprovedAmount,
        r.Status,
        r.RequestedBy?.DisplayName ?? string.Empty,
        r.SubmittedAtUtc,
        r.UpdatedAtUtc);

    public static AuditEntryDto ToDto(this AuditEntry a) => new(
        a.Id, a.Action, a.FromStatus, a.ToStatus, a.ActorName, a.Comment, a.Changes, a.OccurredAtUtc);

    public static AllocationSnapshotDto ToSnapshot(this DepartmentAllocation a) =>
        new(a.AllocatedAmount, a.ApprovedAmount, a.RemainingAmount);
}

/// <summary>
/// The agency follows the U.S. federal fiscal calendar: FY2027 runs 1 Oct 2026 – 30 Sep 2027.
/// </summary>
public static class FiscalCalendar
{
    public static int FiscalYearFor(DateTime date) => date.Month >= 10 ? date.Year + 1 : date.Year;
}
