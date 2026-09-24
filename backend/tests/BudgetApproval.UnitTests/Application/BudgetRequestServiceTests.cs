using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Application.Services;
using BudgetApproval.Domain.Common;
using BudgetApproval.Domain.Entities;
using BudgetApproval.UnitTests.TestSupport;
using static BudgetApproval.UnitTests.TestSupport.TestData;

namespace BudgetApproval.UnitTests.Application;

public class BudgetRequestServiceTests
{
    private readonly InMemoryRequests _requests = new();
    private readonly InMemoryAllocations _allocations = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private BudgetRequestService CreateService(FakeCurrentUser user) =>
        new(_requests, _allocations, _unitOfWork, user, new FixedClock(Now));

    private static CreateBudgetRequestDto NewRequest(int? departmentId = null, int fiscalYear = 2026) => new()
    {
        DepartmentId = departmentId,
        FiscalYear = fiscalYear,
        Category = BudgetCategory.Software,
        Title = "Monitoring licenses",
        Justification = ValidJustification,
        RequestedAmount = 12_000m
    };

    [Fact]
    public async Task A_requester_creates_a_draft_for_their_own_department()
    {
        _allocations.Items.Add(new DepartmentAllocation(ItsDepartment, 2026, 1_000_000m));

        var created = await CreateService(FakeCurrentUser.Maya).CreateAsync(NewRequest(), default);

        Assert.Equal(RequestStatus.Draft, created.Summary.Status);
        Assert.Equal(ItsDepartment, created.Summary.DepartmentId);
        Assert.True(created.Permissions.CanEdit);
        Assert.Single(created.History);
    }

    [Fact]
    public async Task A_requester_cannot_create_a_request_for_another_department()
    {
        _allocations.Items.Add(new DepartmentAllocation(FacDepartment, 2026, 500_000m));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService(FakeCurrentUser.Maya).CreateAsync(NewRequest(FacDepartment), default));
    }

    [Fact]
    public async Task A_request_needs_an_allocation_for_its_fiscal_year()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(FakeCurrentUser.Maya).CreateAsync(NewRequest(fiscalYear: 2030), default));

        Assert.Equal(RuleCodes.NoAllocationForFiscalYear, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Another_departments_request_is_reported_as_not_found_to_a_requester()
    {
        var facilities = Draft(requester: OtherRequester, departmentId: FacDepartment);
        _requests.Add(facilities);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(FakeCurrentUser.Maya).GetAsync(facilities.Id, default));
    }

    [Fact]
    public async Task Approvers_see_that_they_cannot_decide_their_own_request()
    {
        var dana = new FakeCurrentUser(5, "Dana Whitfield", 5, requester: true, approver: true);
        _allocations.Items.Add(new DepartmentAllocation(5, 2026, 350_000m));
        var own = SubmittedRequest(requester: new BudgetApproval.Domain.Common.Actor(5, "Dana Whitfield"), departmentId: 5);
        _requests.Add(own);

        var detail = await CreateService(dana).GetAsync(own.Id, default);

        Assert.False(detail.Permissions.CanDecide);
        Assert.NotNull(detail.Permissions.DecisionBlockedReason);
    }
}
