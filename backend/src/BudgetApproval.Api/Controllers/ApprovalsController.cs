using BudgetApproval.Api.Auth;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApproval.Api.Controllers;

/// <summary>Approver-only endpoints. The policy is enforced here and re-checked in the service.</summary>
[ApiController]
[Authorize(Policy = Policies.Approver)]
[Produces("application/json")]
public sealed class ApprovalsController(ApprovalService service) : ControllerBase
{
    /// <summary>Submitted requests awaiting a decision, oldest first.</summary>
    [HttpGet("api/approvals/queue")]
    public Task<IReadOnlyList<ApprovalQueueItemDto>> Queue([FromQuery] int? fiscalYear, [FromQuery] int? departmentId, CancellationToken ct) =>
        service.GetQueueAsync(fiscalYear, departmentId, ct);

    /// <summary>Approves (fully or partially), rejects, or returns a request for revision.</summary>
    /// <remarks>Send the <c>version</c> from the request you reviewed. A 409 means another approver acted first.</remarks>
    [HttpPost("api/budget-requests/{id:int}/decision")]
    [ProducesResponseType(typeof(BudgetRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<BudgetRequestDetailDto> Decide(int id, DecisionDto dto, CancellationToken ct) =>
        service.DecideAsync(id, dto, ct);
}
