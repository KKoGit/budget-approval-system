# Known limitations and tradeoffs

Every item here was a deliberate choice for a small, reviewable slice. Each notes what it costs and what would change it.

## Tradeoffs made

**SQLite by default, SQL Server for production.**
Reviewers can run the app with no database install. Cost: SQLite has no decimal type, so money is stored as `REAL` in SQLite mode (rounded to cents on read), and dashboard totals are aggregated in memory. Fine for demo data; not acceptable for financial records. SQL Server keeps `decimal(18,2)` and is the production target.

**`EnsureCreated` instead of migrations.**
Zero setup steps and no generated migration files to review. Cost: no schema evolution. Replacing it is the first database task in the roadmap; the model is already migration-ready.

**Denormalised `ApprovedAmount` on the allocation.**
Gives a concurrency token that makes parallel approvals against one department conflict, plus a database check constraint against overspend. Cost: two sources of the same number that must agree; a reconciliation check is on the roadmap. The alternative, a serialisable transaction or application lock around "sum, then approve", is harder to get right across providers.

**Optimistic concurrency with a GUID stamp.**
Works the same on SQLite and SQL Server, and is simple to carry through the API as `version`. Cost: the user whose save loses must reload and redo their action. For a low-contention approval queue that is the right trade; pessimistic locking would add stuck-lock problems.

**Rich domain model over anaemic entities + service rules.**
Rules can't be bypassed and tests need no mocks. Cost: EF Core mapping needs private setters and a backing field for history, which is slightly more ceremony.

**Services return DTOs; repositories return entities.**
A light separation without adding MediatR/CQRS libraries to a small codebase. If read models grow (reporting), move to dedicated query handlers projecting straight to DTOs.

**In-memory dashboard aggregation.**
Provider-agnostic and trivially correct for hundreds of rows. At thousands of requests per year, move to SQL `GROUP BY` on SQL Server.

**Hand-written TypeScript models.**
Small and readable. Cost: they can drift from the API. Generating them from OpenAPI is on the roadmap.

**Department and fiscal year are fixed after creation.**
Changing them would move the request to another allocation mid-flight. Cost: a requester who picked the wrong year creates a new request. Withdrawal (roadmap) makes this tidier.

**Demo authentication.**
Lets a reviewer switch personas instantly. It is fenced off: a startup guard prevents it outside Development, and the identity seam is one handler and one interceptor.

## Known limitations

- No withdrawal, cancellation or reopening of decisions.
- No attachments, notifications or comment threads.
- Allocations are seeded, not editable in the app.
- Single-level approval only; no thresholds or delegation.
- Draft requests don't reserve money; a department can have more drafts than allocation (by design, they are plans).
- No API integration, repository query or Angular unit tests yet (see [testing.md](testing.md)).
- The request list keeps its filters in memory, not in the URL, so a filtered view can't be bookmarked.
- `DaysWaiting` counts calendar days, not business days.
- Seeded dates are relative to first start-up; delete the database file to refresh them.
- Fonts load from Google Fonts; a locked-down network falls back to the system font (self-hosting is a one-line change).
