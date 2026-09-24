using BudgetApproval.Application.Abstractions;
using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Application.Services;

/// <summary>
/// Dashboard figures. Definitions (also documented in docs/database-design.md):
/// <list type="bullet">
/// <item><b>Allocated</b> – the department ceiling for the fiscal year.</item>
/// <item><b>Requested</b> – active demand: requests that are submitted or approved (their requested amount).
/// Drafts are not yet requests, returned requests are back with the requester, rejected ones are closed.</item>
/// <item><b>Approved</b> – committed against the allocation (authoritative value kept on the allocation row).</item>
/// <item><b>Pending</b> – requested amount of submitted requests awaiting a decision.</item>
/// <item><b>Remaining</b> – Allocated − Approved.</item>
/// </list>
/// </summary>
public sealed class DashboardService(
    IBudgetRequestRepository requests,
    IAllocationRepository allocations,
    IDepartmentRepository departments,
    ICurrentUser currentUser)
{
    public async Task<DashboardDto> GetAsync(int? fiscalYear, int? departmentId, CancellationToken ct)
    {
        var scope = BudgetScopePolicy.Resolve(fiscalYear, departmentId, currentUser);

        var allocationRows = await allocations.ListAsync(scope, ct);
        var amountRows = await requests.GetAmountsAsync(scope, ct);
        var departmentNames = (await departments.ListAsync(ct)).ToDictionary(d => d.Id);

        // Aggregation happens in memory over an already-filtered, narrow projection. At demo scale this keeps the
        // query provider-agnostic (SQLite cannot aggregate decimals natively). See docs/limitations-and-tradeoffs.md.
        var byDepartment = allocationRows
            .GroupBy(a => a.DepartmentId)
            .Select(g =>
            {
                var rows = amountRows.Where(r => r.DepartmentId == g.Key).ToList();
                var allocated = g.Sum(a => a.AllocatedAmount);
                var approved = g.Sum(a => a.ApprovedAmount);
                var pendingRows = rows.Where(r => r.Status == RequestStatus.Submitted).ToList();
                var dept = departmentNames.GetValueOrDefault(g.Key);

                return new DepartmentBudgetDto(
                    g.Key,
                    dept?.Code ?? string.Empty,
                    dept?.Name ?? string.Empty,
                    allocated,
                    Requested: rows.Where(r => r.Status is RequestStatus.Submitted or RequestStatus.Approved).Sum(r => r.RequestedAmount),
                    approved,
                    Pending: pendingRows.Sum(r => r.RequestedAmount),
                    Remaining: allocated - approved,
                    PendingCount: pendingRows.Count);
            })
            .OrderBy(d => d.Name)
            .ToList();

        var counts = Enum.GetValues<RequestStatus>()
            .ToDictionary(s => s, s => amountRows.Count(r => r.Status == s));

        return new DashboardDto(
            scope.FiscalYear,
            scope.DepartmentId,
            Allocated: byDepartment.Sum(d => d.Allocated),
            Requested: byDepartment.Sum(d => d.Requested),
            Approved: byDepartment.Sum(d => d.Approved),
            Pending: byDepartment.Sum(d => d.Pending),
            Remaining: byDepartment.Sum(d => d.Remaining),
            PendingCount: byDepartment.Sum(d => d.PendingCount),
            counts,
            byDepartment);
    }
}
