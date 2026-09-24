using BudgetApproval.Application.Abstractions;
using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.UnitTests.TestSupport;

internal sealed class FakeCurrentUser(int userId, string name, int departmentId, bool requester = true, bool approver = false) : ICurrentUser
{
    public int UserId => userId;
    public string DisplayName => name;
    public int DepartmentId => departmentId;
    public bool IsRequester => requester;
    public bool IsApprover => approver;

    public static FakeCurrentUser Maya => new(1, "Maya Chen", TestData.ItsDepartment);
    public static FakeCurrentUser Marcus => new(6, "Marcus Bell", 6, requester: false, approver: true);
}

internal sealed class FixedClock(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.CompletedTask; }
}

internal sealed class InMemoryRequests : IBudgetRequestRepository
{
    public List<BudgetRequest> Items { get; } = new();
    private int _nextId = 100;

    public Task<BudgetRequest?> GetAsync(int id, bool includeHistory, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

    public Task<PagedResult<BudgetRequest>> SearchAsync(BudgetRequestQuery query, BudgetScope scope, CancellationToken ct)
    {
        var rows = Scoped(scope).ToList();
        return Task.FromResult(new PagedResult<BudgetRequest>(rows, 1, rows.Count, rows.Count));
    }

    public Task<IReadOnlyList<BudgetRequest>> GetPendingAsync(BudgetScope scope, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BudgetRequest>>(Scoped(scope).Where(r => r.Status == RequestStatus.Submitted).ToList());

    public Task<IReadOnlyList<RequestAmountRow>> GetAmountsAsync(BudgetScope scope, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RequestAmountRow>>(Scoped(scope)
            .Select(r => new RequestAmountRow(r.DepartmentId, r.Status, r.RequestedAmount, r.ApprovedAmount)).ToList());

    public void Add(BudgetRequest request)
    {
        if (request.Id == 0) request.WithId(_nextId++);
        Items.Add(request);
    }

    private IEnumerable<BudgetRequest> Scoped(BudgetScope s) =>
        Items.Where(r => (s.FiscalYear is null || r.FiscalYear == s.FiscalYear) && (s.DepartmentId is null || r.DepartmentId == s.DepartmentId));
}

internal sealed class InMemoryAllocations : IAllocationRepository
{
    public List<DepartmentAllocation> Items { get; } = new();

    public Task<DepartmentAllocation?> GetAsync(int departmentId, int fiscalYear, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(a => a.DepartmentId == departmentId && a.FiscalYear == fiscalYear));

    public Task<IReadOnlyList<DepartmentAllocation>> ListAsync(BudgetScope s, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DepartmentAllocation>>(Items
            .Where(a => (s.FiscalYear is null || a.FiscalYear == s.FiscalYear) && (s.DepartmentId is null || a.DepartmentId == s.DepartmentId))
            .ToList());

    public Task<IReadOnlyList<int>> GetFiscalYearsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<int>>(Items.Select(a => a.FiscalYear).Distinct().ToList());
}
