using BudgetApproval.Domain.Common;

namespace BudgetApproval.Application.Abstractions;

/// <summary>The authenticated caller. Implemented by the API from the request's claims.</summary>
public interface ICurrentUser
{
    int UserId { get; }
    string DisplayName { get; }
    int DepartmentId { get; }
    bool IsRequester { get; }
    bool IsApprover { get; }
    Actor Actor => new(UserId, DisplayName);
}
