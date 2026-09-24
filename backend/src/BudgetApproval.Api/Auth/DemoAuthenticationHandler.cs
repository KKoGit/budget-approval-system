using System.Security.Claims;
using System.Text.Encodings.Web;
using BudgetApproval.Application.Abstractions;
using BudgetApproval.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BudgetApproval.Api.Auth;

/// <summary>
/// DEMO ONLY. Authenticates the caller from an <c>X-Demo-User</c> header containing a seeded user id, so a
/// reviewer can switch personas without an identity provider. The user and their roles are always loaded
/// from the database; the header cannot grant a role the user does not have.
/// <para>
/// In production this handler is replaced by OpenID Connect / JWT bearer (e.g. Microsoft Entra ID) and the
/// rest of the application is unchanged, because everything downstream depends only on claims via
/// <see cref="ICurrentUser"/>. Program.cs refuses to start with this scheme outside Development unless
/// explicitly allowed. See docs/security.md.
/// </para>
/// </summary>
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserRepository users)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DemoHeader";
    public const string HeaderName = "X-Demo-User";
    public const string DepartmentClaim = "department_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var raw) || string.IsNullOrWhiteSpace(raw))
            return AuthenticateResult.NoResult();

        if (!int.TryParse(raw.ToString(), out var userId))
            return AuthenticateResult.Fail($"{HeaderName} must be a numeric user id.");

        var user = await users.GetAsync(userId, Context.RequestAborted);
        if (user is null)
            return AuthenticateResult.Fail("Unknown demo user.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(DepartmentClaim, user.DepartmentId.ToString())
        };
        if (user.HasRole(UserRole.Requester)) claims.Add(new Claim(ClaimTypes.Role, nameof(UserRole.Requester)));
        if (user.HasRole(UserRole.Approver)) claims.Add(new Claim(ClaimTypes.Role, nameof(UserRole.Approver)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
