using System.Security.Claims;
using BudgetApproval.Application.Abstractions;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Api.Auth;

/// <summary>Adapts the authenticated <see cref="ClaimsPrincipal"/> to the application's <see cref="ICurrentUser"/>.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user
            : throw new InvalidOperationException("No authenticated user is available for this request.");

    public int UserId => int.Parse(Principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public string DisplayName => Principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    public int DepartmentId => int.Parse(Principal.FindFirstValue(DemoAuthenticationHandler.DepartmentClaim)!);
    public bool IsRequester => Principal.IsInRole(nameof(UserRole.Requester));
    public bool IsApprover => Principal.IsInRole(nameof(UserRole.Approver));
}

public static class Policies
{
    public const string Approver = nameof(Approver);
}
