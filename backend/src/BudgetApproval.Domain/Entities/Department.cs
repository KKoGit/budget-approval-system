namespace BudgetApproval.Domain.Entities;

public class Department
{
    private Department() { } // EF Core

    public Department(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public int Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
}
