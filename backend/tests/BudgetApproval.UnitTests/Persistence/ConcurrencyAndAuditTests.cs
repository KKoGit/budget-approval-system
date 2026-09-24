using BudgetApproval.Application.Common;
using BudgetApproval.Domain.Entities;
using BudgetApproval.Infrastructure.Persistence;
using BudgetApproval.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using static BudgetApproval.UnitTests.TestSupport.TestData;

namespace BudgetApproval.UnitTests.Persistence;

/// <summary>
/// These tests use a real EF Core model on in-memory SQLite, because optimistic concurrency is enforced by the
/// database's WHERE clause on the concurrency token — a fake repository could not prove it. Each "approver"
/// gets their own DbContext, exactly as two simultaneous HTTP requests would.
/// </summary>
public sealed class ConcurrencyAndAuditTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BudgetDbContext> _options;
    private readonly int _requestA;
    private readonly int _requestB;

    public ConcurrencyAndAuditTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlite(_connection).Options;

        using var db = NewContext();
        db.Database.EnsureCreated();

        var dept = new Department("FAC", "Facilities & Fleet");
        db.Departments.Add(dept);
        db.SaveChanges();

        var requester = new AppUser("Luis Ortega", "luis@test.example", dept.Id, UserRole.Requester);
        var approverOne = new AppUser("Marcus Bell", "marcus@test.example", dept.Id, UserRole.Approver);
        var approverTwo = new AppUser("Dana Whitfield", "dana@test.example", dept.Id, UserRole.Approver);
        db.Users.AddRange(requester, approverOne, approverTwo);
        db.SaveChanges();

        db.Allocations.Add(new DepartmentAllocation(dept.Id, 2026, 100_000m));

        var a = BudgetRequest.Create(dept.Id, 2026, BudgetCategory.Facilities, "Roof repair", ValidJustification, 60_000m, requester.AsActor(), Now);
        var b = BudgetRequest.Create(dept.Id, 2026, BudgetCategory.Facilities, "HVAC controller", ValidJustification, 60_000m, requester.AsActor(), Now);
        a.Submit(requester.AsActor(), Now);
        b.Submit(requester.AsActor(), Now);
        db.BudgetRequests.AddRange(a, b);
        db.SaveChanges();

        _requestA = a.Id;
        _requestB = b.Id;
    }

    private BudgetDbContext NewContext() => new(_options);

    private static readonly BudgetApproval.Domain.Common.Actor Marcus = new(2, "Marcus Bell");
    private static readonly BudgetApproval.Domain.Common.Actor Dana = new(3, "Dana Whitfield");

    [Fact]
    public async Task Two_approvers_deciding_the_same_request_cannot_overwrite_each_other()
    {
        await using var first = NewContext();
        await using var second = NewContext();

        var seenByFirst = await first.BudgetRequests.SingleAsync(r => r.Id == _requestA);
        var seenBySecond = await second.BudgetRequests.SingleAsync(r => r.Id == _requestA);

        seenByFirst.Reject(Marcus, "Out of scope this year.", Now);
        await new UnitOfWork(first).SaveChangesAsync(default);

        seenBySecond.ReturnForRevision(Dana, "Add quotes.", Now);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(second).SaveChangesAsync(default));

        await using var verify = NewContext();
        Assert.Equal(RequestStatus.Rejected, (await verify.BudgetRequests.SingleAsync(r => r.Id == _requestA)).Status);
    }

    [Fact]
    public async Task Simultaneous_approvals_of_different_requests_cannot_jointly_exceed_the_allocation()
    {
        // Each request (60k) fits the 100k allocation on its own; together they would not.
        await using var first = NewContext();
        await using var second = NewContext();

        // Both approvers open their screens before either commits, so both see 100k remaining.
        var allocationSeenByFirst = await first.Allocations.SingleAsync();
        var allocationSeenBySecond = await second.Allocations.SingleAsync();

        await ApproveAsync(first, _requestA, Marcus, allocationSeenByFirst);
        await new UnitOfWork(first).SaveChangesAsync(default);

        // The second approval passes the in-memory ceiling check (its view is stale)…
        await ApproveAsync(second, _requestB, Dana, allocationSeenBySecond);

        // …but the allocation row's concurrency token has moved on, so the database refuses the write.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(second).SaveChangesAsync(default));

        await using var verify = NewContext();
        Assert.Equal(60_000m, (await verify.Allocations.SingleAsync()).ApprovedAmount);
        Assert.Equal(RequestStatus.Submitted, (await verify.BudgetRequests.SingleAsync(r => r.Id == _requestB)).Status);
    }

    [Fact]
    public async Task Audit_entries_cannot_be_modified_or_deleted()
    {
        await using var db = NewContext();
        var entry = await db.AuditEntries.FirstAsync();

        db.AuditEntries.Remove(entry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static async Task ApproveAsync(BudgetDbContext db, int requestId, BudgetApproval.Domain.Common.Actor approver, DepartmentAllocation allocation)
    {
        var request = await db.BudgetRequests.SingleAsync(r => r.Id == requestId);
        request.Approve(approver, request.RequestedAmount, null, Now);
        allocation.CommitApproval(request.RequestedAmount);
    }

    public void Dispose() => _connection.Dispose();
}
