using BudgetApproval.Domain.Common;
using BudgetApproval.Domain.Entities;
using static BudgetApproval.UnitTests.TestSupport.TestData;

namespace BudgetApproval.UnitTests.Domain;

public class BudgetRequestTests
{
    // ---------------------------------------------------------------- creation & validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Requested_amount_must_be_positive(decimal amount) =>
        AssertRule(RuleCodes.AmountMustBePositive, () => Draft(amount));

    [Fact]
    public void Requested_amount_cannot_have_fractions_of_a_cent() =>
        AssertRule(RuleCodes.AmountPrecision, () => Draft(100.005m));

    [Fact]
    public void Requested_amount_cannot_exceed_the_single_request_policy_limit() =>
        AssertRule(RuleCodes.AmountExceedsPolicyLimit, () => Draft(BudgetRequest.MaxRequestAmount + 1));

    [Fact]
    public void A_new_request_is_a_draft_with_a_created_audit_entry()
    {
        var request = Draft(12_500m);

        Assert.Equal(RequestStatus.Draft, request.Status);
        var entry = Assert.Single(request.History);
        Assert.Equal(AuditAction.Created, entry.Action);
        Assert.Null(entry.FromStatus);
        Assert.Equal(RequestStatus.Draft, entry.ToStatus);
        Assert.Equal(Requester.DisplayName, entry.ActorName);
    }

    // ---------------------------------------------------------------- editing

    [Fact]
    public void A_submitted_request_cannot_be_edited()
    {
        var request = SubmittedRequest();

        AssertRule(RuleCodes.RequestNotEditable, () =>
            request.UpdateDetails(BudgetCategory.Equipment, "Changed", ValidJustification, 5_000m, Requester, Now));
    }

    [Fact]
    public void A_request_returned_for_revision_can_be_edited_and_the_change_is_audited()
    {
        var request = SubmittedRequest(10_000m);
        request.ReturnForRevision(Approver, "Attach a vendor quote.", Now.AddHours(1));

        request.UpdateDetails(BudgetCategory.Equipment, "Laptop refresh", ValidJustification, 8_750m, Requester, Now.AddHours(2));

        Assert.Equal(8_750m, request.RequestedAmount);
        var last = request.History.Last();
        Assert.Equal(AuditAction.Updated, last.Action);
        Assert.Contains("$10,000.00 → $8,750.00", last.Changes);
    }

    [Fact]
    public void Saving_without_changes_adds_no_audit_entry_and_keeps_the_version()
    {
        var request = Draft(10_000m);
        var version = request.ConcurrencyStamp;

        request.UpdateDetails(BudgetCategory.Equipment, "  Laptop refresh  ", ValidJustification, 10_000m, Requester, Now);

        Assert.Single(request.History);
        Assert.Equal(version, request.ConcurrencyStamp);
    }

    [Fact]
    public void Only_the_requester_can_edit_or_submit()
    {
        var request = Draft();

        AssertRule(RuleCodes.OnlyRequesterMayModify, () =>
            request.UpdateDetails(BudgetCategory.Travel, "Changed", ValidJustification, 1m, OtherRequester, Now));
        AssertRule(RuleCodes.OnlyRequesterMayModify, () => request.Submit(OtherRequester, Now));
    }

    // ---------------------------------------------------------------- decisions

    [Fact]
    public void A_user_cannot_approve_their_own_request()
    {
        // Dana is both requester and approver; her approver role must not let her approve her own request.
        var dana = new Actor(5, "Dana Whitfield");
        var request = SubmittedRequest(requester: dana);

        AssertRule(RuleCodes.SelfApprovalNotAllowed, () => request.Approve(dana, request.RequestedAmount, null, Now));
        AssertRule(RuleCodes.SelfApprovalNotAllowed, () => request.Reject(dana, "No", Now));
        AssertRule(RuleCodes.SelfApprovalNotAllowed, () => request.ReturnForRevision(dana, "Fix", Now));
    }

    [Fact]
    public void Only_submitted_requests_can_be_decided()
    {
        var draft = Draft();
        AssertRule(RuleCodes.RequestNotPending, () => draft.Approve(Approver, draft.RequestedAmount, null, Now));

        var approved = SubmittedRequest();
        approved.Approve(Approver, approved.RequestedAmount, null, Now);
        AssertRule(RuleCodes.RequestNotPending, () => approved.Reject(Approver, "Changed my mind", Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000.01)]
    public void Approved_amount_must_be_positive_and_not_exceed_the_request(decimal approved)
    {
        var request = SubmittedRequest(10_000m);
        AssertRule(RuleCodes.ApprovedAmountInvalid, () => request.Approve(Approver, approved, "note", Now));
    }

    [Fact]
    public void A_partial_approval_requires_a_comment()
    {
        var request = SubmittedRequest(10_000m);

        AssertRule(RuleCodes.CommentRequired, () => request.Approve(Approver, 6_000m, "   ", Now));

        request.Approve(Approver, 6_000m, "Core scope only.", Now);
        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(6_000m, request.ApprovedAmount);
    }

    [Fact]
    public void Rejecting_or_returning_requires_a_reason()
    {
        AssertRule(RuleCodes.CommentRequired, () => SubmittedRequest().Reject(Approver, "", Now));
        AssertRule(RuleCodes.CommentRequired, () => SubmittedRequest().ReturnForRevision(Approver, " ", Now));
    }

    // ---------------------------------------------------------------- audit & concurrency

    [Fact]
    public void The_full_workflow_leaves_a_complete_ordered_audit_trail()
    {
        var request = Draft(10_000m);
        request.Submit(Requester, Now.AddMinutes(1));
        request.ReturnForRevision(Approver, "Split costs by quarter.", Now.AddMinutes(2));
        request.UpdateDetails(BudgetCategory.Equipment, "Laptop refresh", ValidJustification, 9_000m, Requester, Now.AddMinutes(3));
        request.Submit(Requester, Now.AddMinutes(4));
        request.Approve(Approver, 9_000m, null, Now.AddMinutes(5));

        Assert.Equal(
            new[] { AuditAction.Created, AuditAction.Submitted, AuditAction.ReturnedForRevision, AuditAction.Updated, AuditAction.Submitted, AuditAction.Approved },
            request.History.Select(h => h.Action).ToArray());

        var approval = request.History.Last();
        Assert.Equal(RequestStatus.Submitted, approval.FromStatus);
        Assert.Equal(RequestStatus.Approved, approval.ToStatus);
        Assert.Equal(Approver.DisplayName, approval.ActorName);
    }

    [Fact]
    public void Every_state_change_issues_a_new_concurrency_version()
    {
        var request = Draft();
        var versions = new HashSet<Guid> { request.ConcurrencyStamp };

        request.Submit(Requester, Now);
        Assert.True(versions.Add(request.ConcurrencyStamp));

        request.Approve(Approver, request.RequestedAmount, null, Now);
        Assert.True(versions.Add(request.ConcurrencyStamp));
    }
}
