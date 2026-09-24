namespace BudgetApproval.Domain.Common;

/// <summary>
/// The person performing an action. The display name is captured at the moment of the action so
/// the audit trail still reads correctly if the user is later renamed or deactivated.
/// </summary>
public sealed record Actor(int UserId, string DisplayName);
