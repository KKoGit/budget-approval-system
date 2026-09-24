# Testing approach

## What is tested where

```
          ▲  fewer, slower, broader
          │   Manual walkthrough (README)          whole stack, every persona
          │   Persistence tests (3)                real EF Core + SQLite: concurrency, audit immutability
          │   Application service tests (14)       use cases with in-memory fakes: authorization, allocation, versions
          │   Domain tests (21)                    pure business rules, no I/O, milliseconds
          ▼  more, faster, narrower
```

The pyramid follows where the risk is. Almost every rule that matters lives in the domain, so it is tested there directly, fast and without mocks. Services are tested for the things only they do: authorization, orchestration across two aggregates, version checks. Concurrency is tested against a real database because it is the database's `WHERE ConcurrencyStamp = @original` that enforces it; a fake could only pretend.

## Running the tests

```bash
cd backend
dotnet test
```

38 tests (35 domain/service cases including theory rows, plus 3 persistence tests). No external services are needed; the persistence tests use in-memory SQLite.

## Conventions

- **Test names are sentences** that state the rule: `A_user_cannot_approve_their_own_request`. The test list reads as the specification, and each user story in [user-stories.md](user-stories.md) names its tests.
- **Assert on error codes, not messages.** `AssertRule(RuleCodes.AllocationExceeded, …)` — wording can change without breaking tests; the code is the contract with the UI.
- **Fixed clock.** Services take `TimeProvider`; tests pass a `FixedClock`, so nothing depends on the time of day.
- **No mocking library.** Fakes (`InMemoryRequests`, `FakeUnitOfWork`, `FakeCurrentUser`) are a few lines each and make assertions about outcomes, not calls.
- **One reason to fail per test.** Arrange with builders in `TestData`; one behaviour per test.

## Rules and their tests

| Rule | Tests |
|---|---|
| Requested amounts must be positive (and within precision/limit) | `Requested_amount_must_be_positive` (×3), `…fractions_of_a_cent`, `…policy_limit` |
| Submitted requests can't be edited unless returned | `A_submitted_request_cannot_be_edited`, `A_request_returned_for_revision_can_be_edited_and_the_change_is_audited` |
| No self-approval | `A_user_cannot_approve_their_own_request`, `Approvers_see_that_they_cannot_decide_their_own_request` |
| Approval can't exceed allocation | `Approval_cannot_exceed_the_remaining_allocation`, `…exactly_the_remaining_amount_is_allowed`, `Approval_that_would_exceed_the_allocation_is_refused_and_nothing_is_saved`, `A_partial_approval_within_the_remaining_allocation_succeeds` |
| Everything is audited | `A_new_request_is_a_draft_with_a_created_audit_entry`, `The_full_workflow_leaves_a_complete_ordered_audit_trail`, `Saving_without_changes_adds_no_audit_entry_and_keeps_the_version`, `Audit_entries_cannot_be_modified_or_deleted` |
| Concurrency protection | `Every_state_change_issues_a_new_concurrency_version`, `A_decision_made_on_a_stale_version_is_rejected_as_a_conflict`, `Two_approvers_deciding_the_same_request_cannot_overwrite_each_other`, `Simultaneous_approvals_of_different_requests_cannot_jointly_exceed_the_allocation` |
| Consistent fiscal-year/department scope | `BudgetScopePolicyTests` (×3), `Another_departments_request_is_reported_as_not_found_to_a_requester` |

## Not yet automated (next, in this order)

1. **API integration tests** with `WebApplicationFactory<Program>` (the `Program` class is already public for this): status-code mapping, the Approver policy, 404-not-403 scoping, JSON contract snapshots.
2. **Repository query tests** on SQLite: search escaping, reference-number search, whitelisted sorting, stable paging, and that the dashboard totals reconcile with the list for the same scope.
3. **Angular unit tests** (Jest or Karma): `twoDecimalPlaces` validator, `ScopeService` pinning, the error interceptor's mapping of problem details, guards.
4. **End-to-end** (Playwright): the README walkthrough as three scripts — requester happy path, partial approval at the allocation ceiling, and two browser contexts racing to decide the same request.
5. **Accessibility** checks (axe) in the end-to-end run.
6. CI gate: build, test, coverage report on the domain and application projects.
