# Assumptions and scope

## Problem statement

Departments of a mid-sized public agency request operating funds during the year. Today requests travel by e-mail and spreadsheet; the Budget Office cannot see how much of each department's allocation is already committed, decisions are hard to trace, and two reviewers occasionally act on the same request. This slice replaces that with one workflow, one source of truth for the numbers, and a complete audit trail.

All organisations, people and figures in the repository are fictional (the "Asencilla Regional Services Agency").

## Users

| Persona | Needs |
|---|---|
| **Requester** (department staff, e.g. Maya Chen, IT) | Draft a request, submit it, see its status and the reasons for any decision, fix and resubmit returned requests |
| **Approver** (Budget Office, e.g. Marcus Bell) | See everything waiting, oldest first; know whether the department can afford it before deciding; approve fully or in part, reject, or return with a reason |
| **Department head with delegated approval** (e.g. Dana Whitfield) | Both of the above, without ever deciding her own requests |

## Assumptions

1. **U.S. federal fiscal calendar.** FY2027 runs 1 October 2026 – 30 September 2027. `FiscalCalendar` is the one place this lives.
2. **Allocations are set elsewhere.** Each department has one ceiling per fiscal year, entered by the Budget Office through another process. This slice reads allocations; it does not edit them.
3. **A request needs an allocation.** A department without an FY2028 allocation cannot raise FY2028 requests; the year is not open for it.
4. **Single-step approval.** Any approver may decide any submitted request except their own. Multi-level or threshold-based routing is on the roadmap.
5. **Partial approval is normal.** Approvers often fund part of a need. A partial approval requires a comment so the requester knows why.
6. **Approved money is committed immediately.** An approval reduces the department's remaining allocation at once; there is no separate obligation step in this slice.
7. **One currency (USD), two decimal places.** Amounts are capped at $10 million per request; larger needs go through capital planning.
8. **Requesters see only their own department.** Approvers see the whole agency.
9. **Department and fiscal year are fixed once a request exists.** Changing either would move the request to a different allocation; the requester creates a new request instead.
10. **Nothing is deleted.** Requests and history are records. Withdrawal/cancellation is on the roadmap.

## In scope

- Five entities: Department, User, DepartmentAllocation, BudgetRequest, AuditEntry
- Five screens: dashboard, request list, create/edit form, request detail with history and decision panel, approval queue (plus a demo persona picker)
- Ten API endpoints with Swagger/OpenAPI
- One complete workflow: draft → submit → approve / reject / return → (edit → resubmit)
- The business rules in [user-stories.md](user-stories.md), enforced on the server and mirrored in the UI
- Seeded demonstration data covering every status and every rule
- Unit tests for the rules; persistence tests for concurrency and audit immutability

## Out of scope (deliberately)

| Not built | Why not now |
|---|---|
| Real identity provider | Needs agency tenant details; the seam (`ICurrentUser`, one interceptor, one auth handler) is in place |
| Allocation management, transfers between departments | A separate workflow with its own approvals |
| Multi-level approval routing, delegation, out-of-office | Needs policy decisions from the Budget Office |
| Attachments (quotes, specifications) | Needs file storage, malware scanning and retention rules |
| Notifications (e-mail, Teams) | Needs integration decisions |
| Reporting/export, fiscal-year close-out | Downstream of a stable data model |
| Withdrawal, cancellation, reopening decisions | Additional states; worth designing with users |

## Success criteria for the slice

- A reviewer can clone, run two commands, and exercise every rule within ten minutes.
- Every figure on the dashboard reconciles with the request list for the same filter.
- No request can change without an audit entry, and no audit entry can change at all.
- Two approvers acting simultaneously can never overwrite each other or overspend an allocation.
