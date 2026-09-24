# Budget Planning & Approval — vertical slice

A small, complete budget-request workflow for a fictional public agency, the **Northbridge Regional Services Agency**. Departments draft and submit funding requests; the Budget Office approves (fully or in part), rejects, or returns them; every figure and every change is traceable.

It is intentionally a **polished vertical slice**, not a platform: five entities, five screens, ten API endpoints, one workflow — built the way the full product would be built.

| | |
|---|---|
| **Front end** | Angular 18 · standalone components · signals · reactive forms · route guards · HTTP interceptors |
| **API** | ASP.NET Core 8 Web API · Swagger/OpenAPI · RFC 9457 problem details |
| **Data** | Entity Framework Core 8 · SQLite by default (zero setup) · SQL Server by configuration |
| **Tests** | xUnit · 38 tests covering every business rule, including real-database concurrency tests |

All people, departments and amounts are invented.

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/KKoGit/budget-approval-system?quickstart=1)


---

## Run it in GitHub Codespaces (nothing to install)

Click **Open in GitHub Codespaces** above. GitHub builds a cloud workspace with .NET 8 and Node 20 and installs all dependencies (about two minutes the first time). Then open two terminals:

```bash
# Terminal 1: API
cd backend/src/BudgetApproval.Api && dotnet run

# Terminal 2: web app
cd frontend && npm start
```

The web app opens in a new browser tab when it's ready. Each codespace is a private copy, so your changes don't affect anyone else, and it stops on its own when idle. Codespaces usage counts against the free monthly allowance of whoever opens it.

## Run it locally

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and [Node.js 20+](https://nodejs.org/) (18.19+ works).

```bash
# 1. API — creates and seeds budget-approval.db on first run
cd backend/src/BudgetApproval.Api
dotnet run                      # http://localhost:5080  (Swagger at /swagger)

# 2. Web app — in a second terminal
cd frontend
npm install
npm start                       # http://localhost:4200  (proxies /api to :5080)

# 3. Tests
cd backend
dotnet test
```

Open http://localhost:4200 and pick a person. There is no password; sign-in is simulated so you can switch roles instantly (see [security](docs/security.md)).

**Reset the demo data:** stop the API and delete `backend/src/BudgetApproval.Api/budget-approval.db`.

**Use SQL Server instead:** set `Database:Provider` to `SqlServer` in `appsettings.json` (the LocalDB connection string is already there) and run the API.

**Try the API directly:** in Swagger, click **Authorize** and enter a user id from 1 to 6.

---

## Ten-minute walkthrough

Each step exercises one business rule.

| # | Sign in as | Do this | You'll see |
|---|---|---|---|
| 1 | **Maya Chen** (IT, requester) | *New request* → enter −50 as the amount | Inline validation; the API would also refuse (`AMOUNT_MUST_BE_POSITIVE`) |
| 2 | Maya | Enter a valid request, **Save and submit** | Status *Awaiting decision*; history shows Created → Submitted |
| 3 | Maya | Open it and look for *Edit* | Gone: submitted requests are locked |
| 4 | **Marcus Bell** (Budget Office, approver) | *Overview* | Facilities' bar runs past its end: pending demand exceeds what remains |
| 5 | Marcus | *Approval queue* → *Emergency roof repair* → **Approve** $75,000 | Refused: only $40,000 remains (`ALLOCATION_EXCEEDED`) |
| 6 | Marcus | Change the amount to 40000, add a comment, **Approve in part** | Approved; Facilities now has $0 left |
| 7 | **Dana Whitfield** (R&A, requester *and* approver) | Open her *Survey data collection platform* | No decision panel: "another approver must decide it" |
| 8 | Marcus | Approve Dana's survey request | Allowed — it isn't his |
| 9 | **Priya Natarajan** (Outreach) | Open *Multilingual outreach materials* | The approver's reason; *Edit*, change the amount, **Save and resubmit**; history shows the change |
| 10 | Marcus in two browser windows | Open the same pending request in both; decide in one, then the other | The second gets **"changed by someone else"** with *Reload* — nothing is overwritten |
| 11 | Anyone | Switch fiscal year / department on the Overview, then go to Requests and the Queue | The same selection applies on all three screens |

---

## What's worth looking at

**Business rules live in the domain, not in controllers.** `BudgetRequest` is a small state machine with no public setters; every transition validates its rule, appends an audit entry and issues a new version. There is no code path that changes a request without auditing it. → [`BudgetRequest.cs`](backend/src/BudgetApproval.Domain/Entities/BudgetRequest.cs)

**Concurrency at two levels.** Clients send the `version` they saw, so a decision made on a stale screen is refused (409). Separately, the allocation row carries its own concurrency token, so two approvers approving *different* requests for the same department at the same instant can't jointly overspend — the database refuses the second write. Both are proven against a real database. → [`ApprovalService.DecideAsync`](backend/src/BudgetApproval.Application/Services/ApprovalService.cs), [`ConcurrencyAndAuditTests`](backend/tests/BudgetApproval.UnitTests/Persistence/ConcurrencyAndAuditTests.cs)

**One definition of "FY2026, Facilities".** The dashboard, list and queue resolve filters through one policy (`BudgetScopePolicy`) and translate them to SQL in one place (`ApplyScope`); the UI uses one `ScopeService` and one `ScopeBar`. Requesters are pinned to their own department on the server, not just hidden in the UI. → [`BudgetScope.cs`](backend/src/BudgetApproval.Application/Common/BudgetScope.cs)

**Errors the UI can act on.** Every rule violation returns problem details with a stable code (`SELF_APPROVAL_NOT_ALLOWED`, `ALLOCATION_EXCEEDED`, …). Tests assert on codes, not wording.

**Seed data that obeys the rules.** The seeder calls the same domain methods as the API, so every seeded request has a real history and nothing in the demo is in an impossible state. → [`DbSeeder.cs`](backend/src/BudgetApproval.Infrastructure/Persistence/DbSeeder.cs)

---

## Documentation

| Document | Contents |
|---|---|
| [Architecture](docs/architecture.md) | System diagram, layer responsibilities, workflow state machine, approval sequence, API surface |
| [Assumptions and scope](docs/assumptions-and-scope.md) | Problem, personas, assumptions, what is in and deliberately out |
| [User stories](docs/user-stories.md) | Eleven stories with acceptance criteria, each linked to its tests |
| [Database design](docs/database-design.md) | ER diagram, constraints, indexes, design decisions, dashboard definitions, seed data |
| [Security](docs/security.md) | What is implemented, what is required before production, threats considered |
| [Testing](docs/testing.md) | Test strategy, rule-to-test map, what to automate next |
| [Production roadmap](docs/production-roadmap.md) | Three phases from pilot to reporting, plus team shape |
| [Limitations and tradeoffs](docs/limitations-and-tradeoffs.md) | Every shortcut, what it costs, and what would change it |

---

## Repository layout

```
backend/
  BudgetApproval.sln
  src/
    BudgetApproval.Domain/          entities and business rules (no dependencies)
    BudgetApproval.Application/     use-case services, DTOs, scope policy, repository interfaces
    BudgetApproval.Infrastructure/  EF Core context, mappings, repositories, seed data
    BudgetApproval.Api/             controllers, demo auth, error handling, Swagger
  tests/
    BudgetApproval.UnitTests/       Domain/, Application/, Persistence/
frontend/
  src/app/
    core/                           API client, session, scope, guards, interceptors
    shared/                         scope bar, status badge, allocation bar, toasts
    features/                       dashboard, requests (list/form/detail), approvals
docs/                               the documents listed above
```

## Troubleshooting

- **"Cannot reach the API"** in the web app: the API isn't running on port 5080. Start it first.
- **Port 5080 in use:** change `applicationUrl` in `backend/src/BudgetApproval.Api/Properties/launchSettings.json` and `target` in `frontend/proxy.conf.json`.
- **Only a newer .NET SDK installed:** `global.json` rolls forward, so it builds; running needs the .NET 8 runtime, or change `TargetFramework` to your version in the five `.csproj` files.
- **API refuses to start outside Development:** intentional (demo auth guard). Use `dotnet run`, which sets Development.
