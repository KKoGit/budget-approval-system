using BudgetApproval.Application.Abstractions;

namespace BudgetApproval.Application.Common;

/// <summary>
/// The fiscal-year / department slice a query runs against. Every read path (dashboard, request list,
/// approval queue) resolves its scope through <see cref="BudgetScopePolicy"/> and every repository
/// applies it through a single extension method, so the three screens can never disagree about
/// which rows "FY2026, Facilities" means.
/// </summary>
public sealed record BudgetScope(int? FiscalYear, int? DepartmentId);

public static class BudgetScopePolicy
{
    /// <summary>
    /// Combines the filters a user asked for with what they are allowed to see. Approvers work across
    /// the agency; everyone else is pinned to their own department.
    /// </summary>
    public static BudgetScope Resolve(int? fiscalYear, int? departmentId, ICurrentUser user)
    {
        if (user.IsApprover)
            return new BudgetScope(fiscalYear, departmentId);

        if (departmentId is not null && departmentId != user.DepartmentId)
            throw new ForbiddenAccessException("You can only view budget information for your own department.");

        return new BudgetScope(fiscalYear, user.DepartmentId);
    }

    public static bool CanView(ICurrentUser user, int departmentId) =>
        user.IsApprover || user.DepartmentId == departmentId;
}
