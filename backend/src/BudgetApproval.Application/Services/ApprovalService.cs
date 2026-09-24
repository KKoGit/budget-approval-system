using BudgetApproval.Application.Abstractions;
using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Domain.Common;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Application.Services;

public sealed class ApprovalService(
    IBudgetRequestRepository requests,
    IAllocationRepository allocations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    BudgetRequestService requestService,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<ApprovalQueueItemDto>> GetQueueAsync(int? fiscalYear, int? departmentId, CancellationToken ct)
    {
        EnsureApprover();
        var scope = BudgetScopePolicy.Resolve(fiscalYear, departmentId, currentUser);

        var pending = await requests.GetPendingAsync(scope, ct);
        var allocationLookup = (await allocations.ListAsync(scope, ct))
            .ToDictionary(a => (a.DepartmentId, a.FiscalYear));

        var today = clock.GetUtcNow().UtcDateTime.Date;

        return pending.Select(r =>
        {
            var snapshot = allocationLookup.TryGetValue((r.DepartmentId, r.FiscalYear), out var a)
                ? a.ToSnapshot()
                : new AllocationSnapshotDto(0, 0, 0);

            return new ApprovalQueueItemDto(
                r.ToSummary(),
                snapshot,
                IsOwnRequest: r.RequestedById == currentUser.UserId,
                ExceedsRemaining: r.RequestedAmount > snapshot.Remaining,
                DaysWaiting: r.SubmittedAtUtc is { } submitted ? (int)(today - submitted.Date).TotalDays : 0);
        }).ToList();
    }

    /// <summary>
    /// Records an approver's decision. Concurrency is protected at two levels:
    /// <list type="number">
    /// <item>The client's <c>Version</c> must match the request, so a decision made on a stale screen is refused.</item>
    /// <item>Both the request and the department allocation carry concurrency tokens, so if another approver
    /// commits between our read and our write — on this request, or on a sibling request drawing on the same
    /// allocation — the save fails and nothing is partially applied.</item>
    /// </list>
    /// </summary>
    public async Task<BudgetRequestDetailDto> DecideAsync(int id, DecisionDto dto, CancellationToken ct)
    {
        EnsureApprover();

        var request = await requests.GetAsync(id, includeHistory: false, ct)
            ?? throw new NotFoundException($"Budget request {id} was not found.");

        BudgetRequestService.EnsureVersion(request, dto.Version);

        var now = clock.GetUtcNow().UtcDateTime;
        var approver = currentUser.Actor;

        switch (dto.Decision)
        {
            case DecisionType.Approve:
                var amount = dto.ApprovedAmount ?? request.RequestedAmount;

                // Validate the request-level rules (status, self-approval, amount bounds) before touching the allocation,
                // so the user sees the most relevant error first.
                request.Approve(approver, amount, dto.Comment, now);

                var allocation = await allocations.GetAsync(request.DepartmentId, request.FiscalYear, ct)
                    ?? throw new BusinessRuleException(RuleCodes.NoAllocationForFiscalYear,
                        $"There is no FY{request.FiscalYear} allocation for this department.");
                allocation.CommitApproval(amount);
                break;

            case DecisionType.Reject:
                request.Reject(approver, dto.Comment ?? string.Empty, now);
                break;

            case DecisionType.Return:
                request.ReturnForRevision(approver, dto.Comment ?? string.Empty, now);
                break;

            default:
                throw new BusinessRuleException("DECISION_REQUIRED", "Choose approve, reject or return for revision.");
        }

        // Single unit of work: the request status, the audit entry and the allocation change commit together or not at all.
        await unitOfWork.SaveChangesAsync(ct);

        return await requestService.GetAsync(id, ct);
    }

    private void EnsureApprover()
    {
        if (!currentUser.IsApprover)
            throw new ForbiddenAccessException("Only approvers can review budget requests.");
    }
}
