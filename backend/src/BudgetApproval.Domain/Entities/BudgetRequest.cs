using System.Globalization;
using System.Text;
using BudgetApproval.Domain.Common;

namespace BudgetApproval.Domain.Entities;

/// <summary>
/// Aggregate root for the approval workflow.
/// <code>
///   Draft ──submit──► Submitted ──approve──► Approved
///     ▲                  │  │
///     │                  │  └──reject──────► Rejected
///     │ (edit)           └──return──► ReturnedForRevision ──submit──► Submitted
/// </code>
/// Every state-changing method validates its rule, applies the change, appends an <see cref="AuditEntry"/>
/// and renews <see cref="ConcurrencyStamp"/>. There are no public setters, so there is no path that
/// changes a request without passing through a rule and leaving an audit record.
/// </summary>
public class BudgetRequest
{
    public const int TitleMaxLength = 120;
    public const int JustificationMinLength = 20;
    public const int JustificationMaxLength = 2000;
    public const int CommentMaxLength = 1000;

    /// <summary>Single-request ceiling. Anything larger belongs in a capital-planning process, not this workflow.</summary>
    public const decimal MaxRequestAmount = 10_000_000m;

    private readonly List<AuditEntry> _history = new();

    private BudgetRequest() { } // EF Core

    public int Id { get; private set; }
    public int DepartmentId { get; private set; }
    public Department? Department { get; private set; }
    public int FiscalYear { get; private set; }
    public BudgetCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Justification { get; private set; } = string.Empty;
    public decimal RequestedAmount { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public RequestStatus Status { get; private set; }
    public int RequestedById { get; private set; }
    public AppUser? RequestedBy { get; private set; }
    public int? DecidedById { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }

    /// <summary>Optimistic-concurrency token. Clients echo it back; a mismatch means someone else changed the request.</summary>
    public Guid ConcurrencyStamp { get; private set; }

    public IReadOnlyCollection<AuditEntry> History => _history.AsReadOnly();

    public bool IsEditable => Status is RequestStatus.Draft or RequestStatus.ReturnedForRevision;

    public static BudgetRequest Create(
        int departmentId,
        int fiscalYear,
        BudgetCategory category,
        string title,
        string justification,
        decimal requestedAmount,
        Actor requester,
        DateTime nowUtc)
    {
        ValidateFiscalYear(fiscalYear);
        var (cleanTitle, cleanJustification) = ValidateDetails(title, justification, requestedAmount);

        var request = new BudgetRequest
        {
            DepartmentId = departmentId,
            FiscalYear = fiscalYear,
            Category = category,
            Title = cleanTitle,
            Justification = cleanJustification,
            RequestedAmount = requestedAmount,
            Status = RequestStatus.Draft,
            RequestedById = requester.UserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyStamp = Guid.NewGuid()
        };

        request.Record(AuditAction.Created, null, requester, null,
            $"Requested {Display.Money(requestedAmount)} for {Display.Category(category)}", nowUtc);
        return request;
    }

    public void UpdateDetails(
        BudgetCategory category,
        string title,
        string justification,
        decimal requestedAmount,
        Actor actor,
        DateTime nowUtc)
    {
        EnsureRequester(actor);
        if (!IsEditable)
            throw new BusinessRuleException(RuleCodes.RequestNotEditable,
                $"A request that is {Describe(Status)} cannot be edited. Only drafts and requests returned for revision can change.");

        var (cleanTitle, cleanJustification) = ValidateDetails(title, justification, requestedAmount);

        var changes = new StringBuilder();
        if (category != Category) Append(changes, $"Category: {Display.Category(Category)} → {Display.Category(category)}");
        if (cleanTitle != Title) Append(changes, $"Title: \"{Title}\" → \"{cleanTitle}\"");
        if (requestedAmount != RequestedAmount) Append(changes, $"Amount: {Display.Money(RequestedAmount)} → {Display.Money(requestedAmount)}");
        if (cleanJustification != Justification) Append(changes, "Justification revised");

        if (changes.Length == 0) return; // Nothing changed: no audit noise, no new version.

        Category = category;
        Title = cleanTitle;
        Justification = cleanJustification;
        RequestedAmount = requestedAmount;
        UpdatedAtUtc = nowUtc;
        Record(AuditAction.Updated, Status, actor, null, changes.ToString(), nowUtc);
    }

    public void Submit(Actor actor, DateTime nowUtc)
    {
        EnsureRequester(actor);
        if (!IsEditable)
            throw new BusinessRuleException(RuleCodes.RequestNotSubmittable,
                $"A request that is {Describe(Status)} cannot be submitted.");

        var from = Status;
        Status = RequestStatus.Submitted;
        SubmittedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Record(AuditAction.Submitted, from, actor, null, null, nowUtc);
    }

    /// <summary>
    /// Approves the request for <paramref name="approvedAmount"/>, which may be less than requested
    /// (a partial approval) but then requires an explanatory comment. Checking the department's
    /// remaining allocation is the caller's job, because it involves a second aggregate
    /// (<see cref="DepartmentAllocation.CommitApproval"/>).
    /// </summary>
    public void Approve(Actor approver, decimal approvedAmount, string? comment, DateTime nowUtc)
    {
        EnsurePendingDecisionBy(approver);

        if (approvedAmount <= 0 || approvedAmount > RequestedAmount)
            throw new BusinessRuleException(RuleCodes.ApprovedAmountInvalid,
                $"The approved amount must be greater than zero and no more than the requested {Display.Money(RequestedAmount)}.");

        var cleanComment = NormalizeComment(comment);
        if (approvedAmount < RequestedAmount && cleanComment is null)
            throw new BusinessRuleException(RuleCodes.CommentRequired,
                "Explain a partial approval so the requester knows why the amount was reduced.");

        ApprovedAmount = approvedAmount;
        Decide(RequestStatus.Approved, AuditAction.Approved, approver, cleanComment,
            approvedAmount == RequestedAmount ? null : $"Approved {Display.Money(approvedAmount)} of {Display.Money(RequestedAmount)}",
            nowUtc);
    }

    public void Reject(Actor approver, string comment, DateTime nowUtc)
    {
        EnsurePendingDecisionBy(approver);
        Decide(RequestStatus.Rejected, AuditAction.Rejected, approver,
            RequireComment(comment, "Give a reason for rejecting the request."), null, nowUtc);
    }

    public void ReturnForRevision(Actor approver, string comment, DateTime nowUtc)
    {
        EnsurePendingDecisionBy(approver);
        Decide(RequestStatus.ReturnedForRevision, AuditAction.ReturnedForRevision, approver,
            RequireComment(comment, "Say what needs to change before the request is resubmitted."), null, nowUtc);
    }

    // ---------------------------------------------------------------- helpers

    private void Decide(RequestStatus to, AuditAction action, Actor approver, string? comment, string? changes, DateTime nowUtc)
    {
        var from = Status;
        Status = to;
        DecidedById = approver.UserId;
        DecidedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Record(action, from, approver, comment, changes, nowUtc);
    }

    private void Record(AuditAction action, RequestStatus? from, Actor actor, string? comment, string? changes, DateTime nowUtc)
    {
        _history.Add(new AuditEntry(action, from, Status, actor, comment, changes, nowUtc));
        ConcurrencyStamp = Guid.NewGuid();
    }

    private void EnsureRequester(Actor actor)
    {
        if (actor.UserId != RequestedById)
            throw new BusinessRuleException(RuleCodes.OnlyRequesterMayModify,
                "Only the person who created this request can change or submit it.");
    }

    private void EnsurePendingDecisionBy(Actor approver)
    {
        if (Status != RequestStatus.Submitted)
            throw new BusinessRuleException(RuleCodes.RequestNotPending,
                $"This request is {Describe(Status)}, so there is no decision to make.");

        if (approver.UserId == RequestedById)
            throw new BusinessRuleException(RuleCodes.SelfApprovalNotAllowed,
                "You cannot decide on a request you created. Another approver must review it.");
    }

    private static void ValidateFiscalYear(int fiscalYear)
    {
        if (fiscalYear is < 2000 or > 2100)
            throw new BusinessRuleException(RuleCodes.InvalidFiscalYear, "Choose a valid fiscal year.");
    }

    private static (string Title, string Justification) ValidateDetails(string title, string justification, decimal amount)
    {
        var cleanTitle = (title ?? string.Empty).Trim();
        if (cleanTitle.Length == 0 || cleanTitle.Length > TitleMaxLength)
            throw new BusinessRuleException(RuleCodes.TitleRequired,
                $"Enter a title of up to {TitleMaxLength} characters.");

        var cleanJustification = (justification ?? string.Empty).Trim();
        if (cleanJustification.Length < JustificationMinLength || cleanJustification.Length > JustificationMaxLength)
            throw new BusinessRuleException(RuleCodes.JustificationTooShort,
                $"Write a justification between {JustificationMinLength} and {JustificationMaxLength} characters.");

        if (amount <= 0)
            throw new BusinessRuleException(RuleCodes.AmountMustBePositive, "The requested amount must be greater than zero.");

        if (amount > MaxRequestAmount)
            throw new BusinessRuleException(RuleCodes.AmountExceedsPolicyLimit,
                Invariant($"A single request cannot exceed ${MaxRequestAmount:N0}. Split the need or use capital planning."));

        if (decimal.Round(amount, 2) != amount)
            throw new BusinessRuleException(RuleCodes.AmountPrecision, "Amounts can have at most two decimal places.");

        return (cleanTitle, cleanJustification);
    }

    private static string? NormalizeComment(string? comment)
    {
        var clean = comment?.Trim();
        if (string.IsNullOrEmpty(clean)) return null;
        return clean.Length > CommentMaxLength ? clean[..CommentMaxLength] : clean;
    }

    private static string RequireComment(string? comment, string message) =>
        NormalizeComment(comment) ?? throw new BusinessRuleException(RuleCodes.CommentRequired, message);

    private static void Append(StringBuilder sb, string change)
    {
        if (sb.Length > 0) sb.Append("; ");
        sb.Append(change);
    }

    private static string Describe(RequestStatus status) => status switch
    {
        RequestStatus.ReturnedForRevision => "returned for revision",
        _ => status.ToString().ToLowerInvariant()
    };

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
