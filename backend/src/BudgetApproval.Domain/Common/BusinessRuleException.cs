namespace BudgetApproval.Domain.Common;

/// <summary>
/// Thrown when an operation would violate a business rule. <see cref="Code"/> is a stable,
/// machine-readable identifier that the API returns to clients (and that tests assert on),
/// so the UI never has to parse message text.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message) : base(message) => Code = code;

    public string Code { get; }
}

public static class RuleCodes
{
    public const string AmountMustBePositive = "AMOUNT_MUST_BE_POSITIVE";
    public const string AmountPrecision = "AMOUNT_PRECISION";
    public const string AmountExceedsPolicyLimit = "AMOUNT_EXCEEDS_POLICY_LIMIT";
    public const string InvalidFiscalYear = "INVALID_FISCAL_YEAR";
    public const string TitleRequired = "TITLE_REQUIRED";
    public const string JustificationTooShort = "JUSTIFICATION_TOO_SHORT";
    public const string RequestNotEditable = "REQUEST_NOT_EDITABLE";
    public const string RequestNotSubmittable = "REQUEST_NOT_SUBMITTABLE";
    public const string RequestNotPending = "REQUEST_NOT_PENDING";
    public const string OnlyRequesterMayModify = "ONLY_REQUESTER_MAY_MODIFY";
    public const string SelfApprovalNotAllowed = "SELF_APPROVAL_NOT_ALLOWED";
    public const string ApprovedAmountInvalid = "APPROVED_AMOUNT_INVALID";
    public const string CommentRequired = "COMMENT_REQUIRED";
    public const string AllocationExceeded = "ALLOCATION_EXCEEDED";
    public const string NoAllocationForFiscalYear = "NO_ALLOCATION_FOR_FISCAL_YEAR";
}
