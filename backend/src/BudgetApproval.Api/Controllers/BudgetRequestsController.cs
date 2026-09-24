using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApproval.Api.Controllers;

/// <summary>Create, edit, submit and browse budget requests.</summary>
[ApiController]
[Route("api/budget-requests")]
[Produces("application/json")]
public sealed class BudgetRequestsController(BudgetRequestService service) : ControllerBase
{
    /// <summary>Lists requests visible to the caller, filtered, sorted and paged.</summary>
    /// <remarks>Sort by: updated (default), submitted, amount, title, department, status. Search matches title, justification or a reference such as BR-2026-0005.</remarks>
    [HttpGet]
    public Task<PagedResult<BudgetRequestSummaryDto>> Search([FromQuery] BudgetRequestQuery query, CancellationToken ct) =>
        service.SearchAsync(query, ct);

    /// <summary>Gets one request with its permissions and full audit history.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BudgetRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<BudgetRequestDetailDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Creates a draft request for the caller's department.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BudgetRequestDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BudgetRequestDetailDto>> Create(CreateBudgetRequestDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Summary.Id }, created);
    }

    /// <summary>Edits a draft, or a request that was returned for revision.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(BudgetRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<BudgetRequestDetailDto> Update(int id, UpdateBudgetRequestDto dto, CancellationToken ct) =>
        service.UpdateAsync(id, dto, ct);

    /// <summary>Submits a draft or revised request for approval.</summary>
    [HttpPost("{id:int}/submit")]
    [ProducesResponseType(typeof(BudgetRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<BudgetRequestDetailDto> Submit(int id, SubmitBudgetRequestDto dto, CancellationToken ct) =>
        service.SubmitAsync(id, dto, ct);
}
