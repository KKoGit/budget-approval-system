using System.Reflection;
using BudgetApproval.Domain.Common;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.UnitTests.TestSupport;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 9, 1, 14, 0, 0, DateTimeKind.Utc);

    public const int ItsDepartment = 1;
    public const int FacDepartment = 2;

    public static readonly Actor Requester = new(1, "Maya Chen");
    public static readonly Actor OtherRequester = new(2, "Luis Ortega");
    public static readonly Actor Approver = new(6, "Marcus Bell");

    public const string ValidJustification = "Replaces equipment that is out of vendor support.";

    public static BudgetRequest Draft(decimal amount = 10_000m, Actor? requester = null, int departmentId = ItsDepartment, int fiscalYear = 2026) =>
        BudgetRequest.Create(departmentId, fiscalYear, BudgetCategory.Equipment, "Laptop refresh", ValidJustification,
            amount, requester ?? Requester, Now);

    public static BudgetRequest SubmittedRequest(decimal amount = 10_000m, Actor? requester = null, int departmentId = ItsDepartment)
    {
        var r = Draft(amount, requester, departmentId);
        r.Submit(requester ?? Requester, Now.AddMinutes(5));
        return r;
    }

    /// <summary>Assigns a database-generated id for tests that run without a database.</summary>
    public static T WithId<T>(this T entity, int id) where T : class
    {
        typeof(T).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)!.SetValue(entity, id);
        return entity;
    }

    public static BusinessRuleException AssertRule(string expectedCode, Action act)
    {
        var ex = Assert.Throws<BusinessRuleException>(act);
        Assert.Equal(expectedCode, ex.Code);
        return ex;
    }
}
