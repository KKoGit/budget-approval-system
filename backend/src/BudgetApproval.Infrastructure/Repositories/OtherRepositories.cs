using BudgetApproval.Application.Abstractions;
using BudgetApproval.Application.Common;
using BudgetApproval.Domain.Entities;
using BudgetApproval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApproval.Infrastructure.Repositories;

internal sealed class AllocationRepository(BudgetDbContext db) : IAllocationRepository
{
    public Task<DepartmentAllocation?> GetAsync(int departmentId, int fiscalYear, CancellationToken ct) =>
        db.Allocations.FirstOrDefaultAsync(a => a.DepartmentId == departmentId && a.FiscalYear == fiscalYear, ct);

    public async Task<IReadOnlyList<DepartmentAllocation>> ListAsync(BudgetScope scope, CancellationToken ct) =>
        await db.Allocations.AsNoTracking().Include(a => a.Department).ApplyScope(scope).ToListAsync(ct);

    public async Task<IReadOnlyList<int>> GetFiscalYearsAsync(CancellationToken ct) =>
        await db.Allocations.Select(a => a.FiscalYear).Distinct().OrderByDescending(y => y).ToListAsync(ct);
}

internal sealed class UserRepository(BudgetDbContext db) : IUserRepository
{
    public Task<AppUser?> GetAsync(int id, CancellationToken ct) =>
        db.Users.AsNoTracking().Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().Include(u => u.Department).OrderBy(u => u.DisplayName).ToListAsync(ct);
}

internal sealed class DepartmentRepository(BudgetDbContext db) : IDepartmentRepository
{
    public async Task<IReadOnlyList<Department>> ListAsync(CancellationToken ct) =>
        await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
}

internal sealed class UnitOfWork(BudgetDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "Another approver changed this request or its department's allocation while you were working. " +
                "Reload to see the latest figures, then try again.");
        }
    }
}
