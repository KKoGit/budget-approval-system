using BudgetApproval.Domain.Common;

namespace BudgetApproval.Domain.Entities;

/// <summary>
/// The ceiling a department may spend in one fiscal year.
/// <para>
/// <see cref="ApprovedAmount"/> is kept on the allocation (rather than summed from requests at approval
/// time) so that committing an approval <em>modifies this row</em>. Because the row carries a concurrency
/// token, two approvers approving different requests for the same department at the same moment cannot
/// jointly exceed the ceiling: the second save fails with a concurrency conflict and is retried against
/// fresh numbers.
/// </para>
/// </summary>
public class DepartmentAllocation
{
    private DepartmentAllocation() { } // EF Core

    public DepartmentAllocation(int departmentId, int fiscalYear, decimal allocatedAmount)
    {
        if (allocatedAmount <= 0)
            throw new BusinessRuleException(RuleCodes.AmountMustBePositive, "An allocation must be greater than zero.");

        DepartmentId = departmentId;
        FiscalYear = fiscalYear;
        AllocatedAmount = allocatedAmount;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public int Id { get; private set; }
    public int DepartmentId { get; private set; }
    public Department? Department { get; private set; }
    public int FiscalYear { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public decimal ApprovedAmount { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public decimal RemainingAmount => AllocatedAmount - ApprovedAmount;

    public void CommitApproval(decimal amount)
    {
        if (amount <= 0)
            throw new BusinessRuleException(RuleCodes.AmountMustBePositive, "An approved amount must be greater than zero.");

        if (amount > RemainingAmount)
            throw new BusinessRuleException(
                RuleCodes.AllocationExceeded,
                $"Approving {Display.Money(amount)} would exceed the FY{FiscalYear} allocation. Only {Display.Money(RemainingAmount)} remains.");

        ApprovedAmount += amount;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
