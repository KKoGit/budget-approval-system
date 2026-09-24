using BudgetApproval.Application.Contracts;
using BudgetApproval.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApproval.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class ReferenceController(ReferenceDataService referenceData, DashboardService dashboard) : ControllerBase
{
    /// <summary>DEMO ONLY: the seeded personas a reviewer can sign in as. Removed when real identity is added.</summary>
    [AllowAnonymous]
    [HttpGet("api/demo/users")]
    public Task<IReadOnlyList<UserDto>> DemoUsers(CancellationToken ct) => referenceData.GetUsersAsync(ct);

    /// <summary>Departments, fiscal years, categories and statuses for filters and forms.</summary>
    [HttpGet("api/lookups")]
    public Task<LookupsDto> Lookups(CancellationToken ct) => referenceData.GetLookupsAsync(ct);

    /// <summary>Allocated, requested, approved, pending and remaining amounts for the chosen scope.</summary>
    [HttpGet("api/dashboard")]
    public Task<DashboardDto> Dashboard([FromQuery] int? fiscalYear, [FromQuery] int? departmentId, CancellationToken ct) =>
        dashboard.GetAsync(fiscalYear, departmentId, ct);
}
