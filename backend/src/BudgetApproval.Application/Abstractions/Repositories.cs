using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Application.Abstractions;

public interface IBudgetRequestRepository
{
    /// <summary>Loads a tracked request with its department and requester (and optionally its audit history).</summary>
    Task<BudgetRequest?> GetAsync(int id, bool includeHistory, CancellationToken ct);

    Task<PagedResult<BudgetRequest>> SearchAsync(BudgetRequestQuery query, BudgetScope scope, CancellationToken ct);

    /// <summary>Submitted requests in scope, oldest first, for the approval queue.</summary>
    Task<IReadOnlyList<BudgetRequest>> GetPendingAsync(BudgetScope scope, CancellationToken ct);

    /// <summary>Lightweight projection used for dashboard totals.</summary>
    Task<IReadOnlyList<RequestAmountRow>> GetAmountsAsync(BudgetScope scope, CancellationToken ct);

    void Add(BudgetRequest request);
}

public sealed record RequestAmountRow(int DepartmentId, RequestStatus Status, decimal RequestedAmount, decimal? ApprovedAmount);

public interface IAllocationRepository
{
    Task<DepartmentAllocation?> GetAsync(int departmentId, int fiscalYear, CancellationToken ct);
    Task<IReadOnlyList<DepartmentAllocation>> ListAsync(BudgetScope scope, CancellationToken ct);
    Task<IReadOnlyList<int>> GetFiscalYearsAsync(CancellationToken ct);
}

public interface IUserRepository
{
    Task<AppUser?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct);
}

public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> ListAsync(CancellationToken ct);
}

public interface IUnitOfWork
{
    /// <summary>Persists all tracked changes atomically. Throws <see cref="ConcurrencyConflictException"/> on a lost race.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
