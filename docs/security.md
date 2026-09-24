# Security considerations

This is a demonstration slice. The controls below are either **implemented** or explicitly **required before production**. Nothing in the second column is optional for a real deployment.

## Identity and authentication

| Implemented | Required before production |
|---|---|
| `DemoAuthenticationHandler` authenticates from an `X-Demo-User` header holding a seeded user id. Roles and department are always loaded from the database, so the header can't grant a role the user doesn't have. | Replace with OpenID Connect / JWT bearer against the agency identity provider (e.g. Microsoft Entra ID). Roles come from group or app-role claims; department from a directory attribute. |
| The API **refuses to start** with demo authentication outside the Development environment unless `Authentication:AllowDemoAuthOutsideDevelopment=true` is set deliberately. | Remove the demo handler, the `/api/demo/users` endpoint and the persona picker entirely. |
| Everything downstream depends only on claims via `ICurrentUser`. Swapping identity providers touches one handler and one Angular interceptor. | MFA and conditional access enforced by the identity provider; PIV/CAC where agency policy requires it. |

## Authorization

- **Secure by default.** A fallback policy requires an authenticated user on every endpoint; only the demo persona list opts out with `[AllowAnonymous]`.
- **Defence in depth.** Approver endpoints carry `[Authorize(Policy = "Approver")]` **and** the services check the role again. Route guards in Angular are for user experience only; the API never trusts the client.
- **Row-level scoping.** Requesters are pinned to their own department by `BudgetScopePolicy` on every read. A request from another department returns **404, not 403**, so ids can't be probed to discover what exists.
- **Separation of duties.** The domain refuses any decision by the request's author, whatever roles they hold.
- **Ownership.** Only the author can edit or submit a request.

## Input handling

- DTOs are validated with data annotations (400 on malformed input) and the domain re-validates every rule (422), so no code path skips validation.
- EF Core parameterises every query. Sort columns are whitelisted in `BudgetRequestRepository.ApplySort`; user input never becomes a column name. `LIKE` wildcards in search terms are escaped.
- Enum values are bound by name and rejected if unknown.
- Angular escapes all interpolated text by default; the app uses no `innerHTML`.

## Data protection and integrity

- **Audit trail** is append-only in the domain (no mutators) and in persistence (`BudgetDbContext` throws on update/delete of `AuditEntry`). In production also deny `UPDATE`/`DELETE` on `AuditEntries` to the application's database login, and ship entries to the agency's log archive.
- **Integrity constraints** in the database (check constraints, restrictive foreign keys) catch defects the application might miss.
- **Errors** return problem details with a trace id and no stack traces or internal messages; unexpected exceptions are logged server-side.
- The data is budget information, not PII beyond staff names and work e-mails. Classify it under agency policy before production; it may still be controlled unclassified information.

## Transport and hosting (before production)

- HTTPS only with HSTS; the demo runs on `http://localhost` for friction-free review.
- CORS is limited to configured origins (`Cors:AllowedOrigins`); in production, serve the SPA and API from the same origin and remove CORS.
- Security headers: Content-Security-Policy (the app needs only self plus the font host, or self-hosted fonts), `X-Content-Type-Options`, `Referrer-Policy`, `frame-ancestors 'none'`.
- Connection strings and secrets from a managed secret store (e.g. Azure Key Vault) using managed identity, never from `appsettings.json`.
- Rate limiting on write endpoints (`Microsoft.AspNetCore.RateLimiting`).
- Dependency and container scanning in CI; pin and patch .NET, EF Core and Angular on a schedule.
- If the agency requires it: FedRAMP-authorised hosting, an ATO package and continuous monitoring. Accessibility (Section 508) review is part of the definition of done.

## Threats considered

| Threat | Mitigation |
|---|---|
| Approver approves their own request | Domain rule, tested; UI hides the decision panel |
| Requester reads another department's requests | Scope policy on every query; 404 on direct access |
| Two approvers overwrite each other | Version check + concurrency tokens; 409 with reload |
| Parallel approvals overspend an allocation | Allocation row concurrency token + database check constraint |
| History edited to hide a decision | No mutators; `DbContext` guard; database permissions in production |
| Header spoofing of identity | Demo only; startup guard prevents it leaving Development |
| SQL injection via sort/search | Parameterised queries; whitelisted sort; escaped `LIKE` |
