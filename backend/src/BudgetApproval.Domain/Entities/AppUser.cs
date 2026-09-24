using BudgetApproval.Domain.Common;

namespace BudgetApproval.Domain.Entities;

public class AppUser
{
    private AppUser() { } // EF Core

    public AppUser(string displayName, string email, int departmentId, UserRole roles)
    {
        DisplayName = displayName;
        Email = email;
        DepartmentId = departmentId;
        Roles = roles;
    }

    public int Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public int DepartmentId { get; private set; }
    public Department? Department { get; private set; }
    public UserRole Roles { get; private set; }

    public bool HasRole(UserRole role) => (Roles & role) == role;

    public Actor AsActor() => new(Id, DisplayName);
}
