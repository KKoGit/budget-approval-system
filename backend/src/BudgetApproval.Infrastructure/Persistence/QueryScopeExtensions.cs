using BudgetApproval.Application.Common;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Infrastructure.Persistence;

/// <summary>
/// The one and only place fiscal-year and department filters are translated into SQL. The request list,
/// approval queue and dashboard all go through these, so their numbers always reconcile.
/// </summary>
internal static class QueryScopeExtensions
{
    public static IQueryable<BudgetRequest> ApplyScope(this IQueryable<BudgetRequest> query, BudgetScope scope)
    {
        if (scope.FiscalYear is { } fy) query = query.Where(r => r.FiscalYear == fy);
        if (scope.DepartmentId is { } dept) query = query.Where(r => r.DepartmentId == dept);
        return query;
    }

    public static IQueryable<DepartmentAllocation> ApplyScope(this IQueryable<DepartmentAllocation> query, BudgetScope scope)
    {
        if (scope.FiscalYear is { } fy) query = query.Where(a => a.FiscalYear == fy);
        if (scope.DepartmentId is { } dept) query = query.Where(a => a.DepartmentId == dept);
        return query;
    }
}
