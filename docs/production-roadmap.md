# Production roadmap

Ordered by what unblocks a real pilot first. Each phase ends with something a Budget Office user can use.

## Phase 1 — Ready for a pilot (≈ 4–6 weeks)

**Goal:** one department and the Budget Office use it for real FY2027 requests.

- **Identity:** OIDC with the agency identity provider; map app roles to Requester/Approver; remove demo auth, persona picker and `/api/demo/users`.
- **Database:** SQL Server (Azure SQL or on-prem); replace `EnsureCreated` with EF Core migrations applied by the pipeline (`dotnet ef migrations add Initial`), not at app start-up; database login without `UPDATE`/`DELETE` on `AuditEntries`.
- **Hosting:** single origin for SPA and API, HTTPS/HSTS, security headers, secrets from a vault via managed identity.
- **CI/CD:** build, test, dependency scanning, deploy to test and production with approvals.
- **Observability:** structured logs with trace ids (OpenTelemetry), health checks, alerts on 5xx and on 409 spikes.
- **Tests:** API integration tests and a Playwright smoke suite (see [testing.md](testing.md)).
- **Upgrade** to the current .NET LTS and Angular versions before go-live.
- **Accessibility:** Section 508 / WCAG 2.2 AA review of the five screens.

## Phase 2 — Workflow the agency actually runs (≈ 6–8 weeks)

- **Attachments:** vendor quotes and specifications, with blob storage, malware scanning and retention.
- **Notifications:** e-mail/Teams when a request is submitted, decided or returned; daily digest for approvers.
- **Withdraw and cancel** requests; approver can reopen a decision within a window, audited.
- **Allocation administration:** Budget Office sets and adjusts allocations with its own audit trail; mid-year adjustments and transfers.
- **Routing rules:** threshold-based approval (e.g. > $250k needs a second approver), delegation and out-of-office.
- **Comments thread** on a request between requester and approver.

## Phase 3 — Reporting and scale

- **Reporting:** export to Excel/CSV; a read model or reporting views for Power BI.
- **Fiscal-year close-out:** lock a year, carry forward drafts, archive.
- **Reconciliation job:** verify allocation approved totals equal the sum of approved requests; alert on drift.
- **Server-side aggregation** for the dashboard (indexed views or SQL `GROUP BY`) once volumes justify it.
- **Generated API client** for Angular from the OpenAPI document to remove hand-maintained models.
- **Integration** with the financial system of record (obligations, actuals).

## Team and ways of working (suggested)

- Small team: one tech lead, two full-stack engineers, a part-time designer, and a product owner from the Budget Office.
- Two-week iterations with a live demo to Budget Office users; pilot feedback drives Phase 2 order.
- Definition of done: tests for every rule, accessibility checked, docs updated, deployed to test.
- Architecture decisions recorded as short ADRs in `docs/adr/` (the tradeoffs doc is the seed for the first few).
