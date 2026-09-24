using BudgetApproval.Domain.Common;
using BudgetApproval.Domain.Entities;
using static BudgetApproval.UnitTests.TestSupport.TestData;

namespace BudgetApproval.UnitTests.Domain;

public class DepartmentAllocationTests
{
    [Fact]
    public void Approval_cannot_exceed_the_remaining_allocation()
    {
        var allocation = new DepartmentAllocation(FacDepartment, 2026, 500_000m);
        allocation.CommitApproval(460_000m);

        var ex = AssertRule(RuleCodes.AllocationExceeded, () => allocation.CommitApproval(75_000m));
        Assert.Contains("40,000.00", ex.Message);
        Assert.Equal(460_000m, allocation.ApprovedAmount); // unchanged after the failed attempt
    }

    [Fact]
    public void Approval_of_exactly_the_remaining_amount_is_allowed()
    {
        var allocation = new DepartmentAllocation(FacDepartment, 2026, 500_000m);
        allocation.CommitApproval(460_000m);

        allocation.CommitApproval(40_000m);

        Assert.Equal(0m, allocation.RemainingAmount);
    }

    [Fact]
    public void Committing_an_approval_issues_a_new_concurrency_version()
    {
        var allocation = new DepartmentAllocation(FacDepartment, 2026, 500_000m);
        var before = allocation.ConcurrencyStamp;

        allocation.CommitApproval(1_000m);

        Assert.NotEqual(before, allocation.ConcurrencyStamp);
    }
}
