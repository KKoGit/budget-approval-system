using BudgetApproval.Application.Abstractions;
using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Domain.Common;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Application.Services;

/// <summary>
/// Requester-side use cases. The service owns orchestration and authorization (who may act); the
/// <see cref="BudgetRequest"/> aggregate owns the business rules (what may happen).
/// </summary>
public sealed class BudgetRequestService(
    IBudgetRequestRepository requests,
    IAllocationRepository allocations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<PagedResult<BudgetRequestSummaryDto>> SearchAsync(BudgetRequestQuery query, CancellationToken ct)
    {
        var scope = BudgetScopePolicy.Resolve(query.FiscalYear, query.DepartmentId, currentUser);
        var page = await requests.SearchAsync(query, scope, ct);
        return new PagedResult<BudgetRequestSummaryDto>(
            page.Items.Select(r => r.ToSummary()).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<BudgetRequestDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var request = await LoadVisibleAsync(id, includeHistory: true, ct);
        return await ToDetailAsync(request, ct);
    }

    public async Task<BudgetRequestDetailDto> CreateAsync(CreateBudgetRequestDto dto, CancellationToken ct)
    {
        if (!currentUser.IsRequester)
            throw new ForbiddenAccessException("Only requesters can create budget requests.");

        var departmentId = dto.DepartmentId ?? currentUser.DepartmentId;
        if (departmentId != currentUser.DepartmentId)
            throw new ForbiddenAccessException("You can only create requests for your own department.");

        // A request is only meaningful against an allocation; without one the fiscal year is not open for this department.
        _ = await allocations.GetAsync(departmentId, dto.FiscalYear, ct)
            ?? throw new BusinessRuleException(RuleCodes.NoAllocationForFiscalYear,
                $"Your department has no FY{dto.FiscalYear} allocation yet, so requests for that year cannot be created.");

        var request = BudgetRequest.Create(
            departmentId, dto.FiscalYear, dto.Category!.Value, dto.Title, dto.Justification,
            dto.RequestedAmount, currentUser.Actor, Now);

        requests.Add(request);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(request.Id, ct);
    }

    public async Task<BudgetRequestDetailDto> UpdateAsync(int id, UpdateBudgetRequestDto dto, CancellationToken ct)
    {
        var request = await LoadVisibleAsync(id, includeHistory: false, ct);
        EnsureVersion(request, dto.Version);

        request.UpdateDetails(dto.Category!.Value, dto.Title, dto.Justification, dto.RequestedAmount, currentUser.Actor, Now);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<BudgetRequestDetailDto> SubmitAsync(int id, SubmitBudgetRequestDto dto, CancellationToken ct)
    {
        var request = await LoadVisibleAsync(id, includeHistory: false, ct);
        EnsureVersion(request, dto.Version);

        request.Submit(currentUser.Actor, Now);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    // ------------------------------------------------------------------ shared helpers

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task<BudgetRequest> LoadVisibleAsync(int id, bool includeHistory, CancellationToken ct)
    {
        var request = await requests.GetAsync(id, includeHistory, ct);

        // Report "not found" rather than "forbidden" for other departments' requests so that IDs cannot be probed.
        if (request is null || !BudgetScopePolicy.CanView(currentUser, request.DepartmentId))
            throw new NotFoundException($"Budget request {id} was not found.");

        return request;
    }

    internal static void EnsureVersion(BudgetRequest request, string clientVersion)
    {
        if (!Guid.TryParse(clientVersion, out var expected) || expected != request.ConcurrencyStamp)
            throw new ConcurrencyConflictException(
                "This request was changed by someone else after you opened it. Reload to see the latest version, then try again.");
    }

    private async Task<BudgetRequestDetailDto> ToDetailAsync(BudgetRequest request, CancellationToken ct)
    {
        var isRequester = request.RequestedById == currentUser.UserId;
        var canEdit = isRequester && request.IsEditable;

        string? blockedReason = null;
        var canDecide = false;
        AllocationSnapshotDto? snapshot = null;

        if (currentUser.IsApprover)
        {
            var allocation = await allocations.GetAsync(request.DepartmentId, request.FiscalYear, ct);
            snapshot = allocation?.ToSnapshot();

            if (request.Status != RequestStatus.Submitted) blockedReason = null; // nothing to decide; not an error
            else if (isRequester) blockedReason = "You created this request, so another approver must decide it.";
            else canDecide = true;
        }

        return new BudgetRequestDetailDto(
            request.ToSummary(),
            request.Justification,
            request.RequestedById,
            request.CreatedAtUtc,
            request.DecidedAtUtc,
            request.ConcurrencyStamp.ToString(),
            new RequestPermissionsDto(canEdit, canEdit, canDecide, blockedReason),
            snapshot,
            request.History.OrderByDescending(h => h.OccurredAtUtc).ThenByDescending(h => h.Id).Select(h => h.ToDto()).ToList());
    }
}
