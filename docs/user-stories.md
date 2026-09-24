# User stories and acceptance criteria

Stories are written from the persona's point of view. Acceptance criteria are phrased so each one maps to a test or a manual check; the **Test** line names where it is verified.

---

### US-1 Create a draft request
*As a requester, I want to draft a budget request for my department, so that I can prepare it before anyone reviews it.*

- Given I am a requester, when I enter fiscal year, category, title, justification (20–2,000 characters) and an amount, then a **Draft** is saved for **my** department.
- The amount must be greater than zero, have at most two decimal places, and not exceed $10,000,000.
- I cannot create a request for another department.
- I cannot create a request for a fiscal year my department has no allocation for.
- A "Created" entry appears in the request's history.

**Test:** `BudgetRequestTests.Requested_amount_*`, `A_new_request_is_a_draft_with_a_created_audit_entry`; `BudgetRequestServiceTests.A_requester_*`, `A_request_needs_an_allocation_for_its_fiscal_year`

### US-2 Submit for approval
*As a requester, I want to submit my request, so that an approver can decide on it.*

- Given a Draft or a request Returned for revision that I created, when I submit, its status becomes **Submitted** and it appears in the approval queue.
- Nobody else can submit my request.
- A "Submitted" history entry records the transition.

**Test:** `Only_the_requester_can_edit_or_submit`, `The_full_workflow_leaves_a_complete_ordered_audit_trail`

### US-3 Submitted requests are locked
*As an approver, I want a request to stay unchanged while I review it, so that I approve exactly what I read.*

- Given a request is Submitted, Approved or Rejected, when anyone tries to edit it, the change is refused with `REQUEST_NOT_EDITABLE`.
- The edit screen shows why the form is read-only.
- The request becomes editable again only if an approver returns it for revision.

**Test:** `A_submitted_request_cannot_be_edited`, `A_request_returned_for_revision_can_be_edited_and_the_change_is_audited`

### US-4 Decide a request
*As an approver, I want to approve, reject or return a request, so that departments get a clear answer.*

- Approve: status becomes **Approved**, the approved amount defaults to the requested amount and may be lower (a **partial approval**), never higher and never zero.
- A partial approval, a rejection and a return all require a comment.
- Only Submitted requests can be decided.
- The decision, the approver's name, the time and the comment appear in the history.

**Test:** `Approved_amount_must_be_positive_and_not_exceed_the_request`, `A_partial_approval_requires_a_comment`, `Rejecting_or_returning_requires_a_reason`, `Only_submitted_requests_can_be_decided`

### US-5 No self-approval
*As the Budget Office, I want nobody to decide their own request, so that every approval has a second pair of eyes.*

- Given Dana is both requester and approver, when she opens her own submitted request, the decision panel is replaced by "You created this request, so another approver must decide it."
- The approval queue marks it "Your request".
- The API refuses any decision on it by Dana with `SELF_APPROVAL_NOT_ALLOWED` — approve, reject **and** return.

**Test:** `A_user_cannot_approve_their_own_request`, `Approvers_see_that_they_cannot_decide_their_own_request`

### US-6 Stay within the allocation
*As the Budget Office, I want approvals capped by the department's remaining allocation, so that no department is overcommitted.*

- Given Facilities has $40,000 left in FY2026, when an approver tries to approve the $75,000 roof repair in full, it is refused with `ALLOCATION_EXCEEDED` and the message states the remaining amount.
- Approving $40,000 (with a comment) succeeds and leaves $0 remaining.
- The queue and the detail page warn before the approver tries.

**Test:** `DepartmentAllocationTests.*`, `ApprovalServiceTests.Approval_that_would_exceed_the_allocation_is_refused_and_nothing_is_saved`, `A_partial_approval_within_the_remaining_allocation_succeeds`, `The_queue_flags_requests_that_exceed_the_remaining_allocation`

### US-7 Don't overwrite each other
*As an approver, I want to be told when someone else acted first, so that I never silently undo their decision.*

- Given two approvers open the same request, when the first decides, the second's decision is refused with **409** `CONCURRENCY_CONFLICT` and a "Reload the latest version" link.
- Given two approvers approve **different** requests for the same department at the same moment, and together they would exceed the allocation, the second save is refused rather than overspending.
- An edit made from a stale screen is refused the same way.

**Test:** `A_decision_made_on_a_stale_version_is_rejected_as_a_conflict`, `ConcurrencyAndAuditTests.Two_approvers_deciding_the_same_request_cannot_overwrite_each_other`, `Simultaneous_approvals_of_different_requests_cannot_jointly_exceed_the_allocation`

### US-8 Complete, tamper-proof history
*As an auditor, I want every change recorded permanently, so that I can reconstruct who did what and when.*

- Creation, every edit (with a summary of what changed), submission and every decision produce one history entry with actor, time, from/to status and comment.
- Saving an unchanged form produces no entry.
- Audit entries cannot be modified or deleted through the application.

**Test:** `The_full_workflow_leaves_a_complete_ordered_audit_trail`, `Saving_without_changes_adds_no_audit_entry_and_keeps_the_version`, `Audit_entries_cannot_be_modified_or_deleted`

### US-9 See the money
*As an approver or requester, I want to see allocated, requested, approved and remaining amounts, so that I know what is affordable.*

- The dashboard shows Allocated, Requested (submitted + approved), Approved, Pending and Remaining for the selected fiscal year and department, plus a per-department allocation bar.
- Requesters see only their department; approvers see all departments or one.
- When pending demand exceeds what remains, the bar shows the overflow and approvers see a warning.

**Test:** manual, see the walkthrough in the README; filtering covered by `BudgetScopePolicyTests`

### US-10 Consistent filters
*As any user, I want the fiscal year and department I pick to apply everywhere, so that the numbers on each screen agree.*

- The same scope bar appears on the dashboard, the request list and the approval queue, and the selection carries between them.
- All three API endpoints resolve the scope through `BudgetScopePolicy` and apply it through `QueryScopeExtensions.ApplyScope`.
- A requester asking the API for another department's data is refused (403), not silently redirected.

**Test:** `BudgetScopePolicyTests.*`

### US-11 Find requests
*As any user, I want to filter, search and sort requests, so that I can find what I need quickly.*

- Filter by status and category on top of the fiscal year/department scope.
- Search matches title or justification (case-insensitive), or an exact reference such as `BR-2026-0006`.
- Sort by title, department, amount, status or last change; paging is stable.
- Empty results explain why and offer to clear filters.

**Test:** manual; repository sorting is whitelisted (see `BudgetRequestRepository.ApplySort`)
