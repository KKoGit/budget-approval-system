using BudgetApproval.Application.Common;
using BudgetApproval.UnitTests.TestSupport;
using static BudgetApproval.UnitTests.TestSupport.TestData;

namespace BudgetApproval.UnitTests.Application;

public class BudgetScopePolicyTests
{
    [Fact]
    public void A_requester_without_a_department_filter_is_pinned_to_their_own_department()
    {
        var scope = BudgetScopePolicy.Resolve(2026, null, FakeCurrentUser.Maya);

        Assert.Equal(new BudgetScope(2026, ItsDepartment), scope);
    }

    [Fact]
    public void A_requester_asking_for_another_department_is_refused_rather_than_silently_redirected() =>
        Assert.Throws<ForbiddenAccessException>(() => BudgetScopePolicy.Resolve(2026, FacDepartment, FakeCurrentUser.Maya));

    [Fact]
    public void An_approver_can_see_every_department_or_narrow_to_one()
    {
        Assert.Equal(new BudgetScope(2026, null), BudgetScopePolicy.Resolve(2026, null, FakeCurrentUser.Marcus));
        Assert.Equal(new BudgetScope(null, FacDepartment), BudgetScopePolicy.Resolve(null, FacDepartment, FakeCurrentUser.Marcus));
    }
}
