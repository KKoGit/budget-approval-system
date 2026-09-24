using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Application.Services;
using BudgetApproval.Domain.Common;
using BudgetApproval.Domain.Entities;
using BudgetApproval.UnitTests.TestSupport;
using static BudgetApproval.UnitTests.TestSupport.TestData;

namespace BudgetApproval.UnitTests.Application;

public class ApprovalServiceTests
{
    private readonly InMemoryRequests _requests = new();
    private readonly InMemoryAllocations _allocations = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ApprovalService CreateService(FakeCurrentUser user)
    {
        var clock = new FixedClock(Now.AddDays(1));
        var requestService = new BudgetRequestService(_requests, _allocations, _unitOfWork, user, clock);
        return new ApprovalService(_requests, _allocations, _unitOfWork, user, requestService, clock);
    }

    private BudgetRequest ArrangePending(decimal amount, decimal allocated, decimal alreadyApproved = 0)
    {
        var allocation = new DepartmentAllocation(FacDepartment, 2026, allocated);
        if (alreadyApproved > 0) allocation.CommitApproval(alreadyApproved);
        _allocations.Items.Add(allocation);

        var request = SubmittedRequest(amount, OtherRequester, FacDepartment);
        _requests.Add(request);
        return request;
    }

    private static DecisionDto Approve(BudgetRequest r, decimal? amount = null, string? comment = null) => new()
    {
        Decision = DecisionType.Approve,
        ApprovedAmount = amount,
        Comment = comment,
        Version = r.ConcurrencyStamp.ToString()
    };

    [Fact]
    public async Task Approving_commits_the_amount_against_the_department_allocation()
    {
        var request = ArrangePending(amount: 30_000m, allocated: 500_000m, alreadyApproved: 460_000m);

        var result = await CreateService(FakeCurrentUser.Marcus).DecideAsync(request.Id, Approve(request), default);

        Assert.Equal(RequestStatus.Approved, result.Summary.Status);
        Assert.Equal(10_000m, _allocations.Items.Single().RemainingAmount);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Approval_that_would_exceed_the_allocation_is_refused_and_nothing_is_saved()
    {
        var request = ArrangePending(amount: 75_000m, allocated: 500_000m, alreadyApproved: 460_000m);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(FakeCurrentUser.Marcus).DecideAsync(request.Id, Approve(request), default));

        Assert.Equal(RuleCodes.AllocationExceeded, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task A_partial_approval_within_the_remaining_allocation_succeeds()
    {
        var request = ArrangePending(amount: 75_000m, allocated: 500_000m, alreadyApproved: 460_000m);

        var result = await CreateService(FakeCurrentUser.Marcus)
            .DecideAsync(request.Id, Approve(request, 40_000m, "Temporary patch this year; full replacement in FY2027."), default);

        Assert.Equal(40_000m, result.Summary.ApprovedAmount);
        Assert.Equal(0m, _allocations.Items.Single().RemainingAmount);
    }

    [Fact]
    public async Task A_decision_made_on_a_stale_version_is_rejected_as_a_conflict()
    {
        var request = ArrangePending(amount: 10_000m, allocated: 500_000m);
        var staleVersion = request.ConcurrencyStamp.ToString();

        // Another approver returns the request first…
        request.ReturnForRevision(Approver, "Needs quotes.", Now);

        // …so a decision based on the old screen must not overwrite theirs.
        var dto = new DecisionDto { Decision = DecisionType.Approve, Version = staleVersion };
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            CreateService(FakeCurrentUser.Marcus).DecideAsync(request.Id, dto, default));

        Assert.Equal(RequestStatus.ReturnedForRevision, request.Status);
    }

    [Fact]
    public async Task Users_without_the_approver_role_cannot_decide()
    {
        var request = ArrangePending(amount: 10_000m, allocated: 500_000m);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService(FakeCurrentUser.Maya).DecideAsync(request.Id, Approve(request), default));
    }

    [Fact]
    public async Task The_queue_flags_requests_that_exceed_the_remaining_allocation()
    {
        ArrangePending(amount: 75_000m, allocated: 500_000m, alreadyApproved: 460_000m);

        var queue = await CreateService(FakeCurrentUser.Marcus).GetQueueAsync(2026, null, default);

        var item = Assert.Single(queue);
        Assert.True(item.ExceedsRemaining);
        Assert.Equal(40_000m, item.Allocation.Remaining);
    }
}
