using BudgetApproval.Application.Abstractions;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Domain.Entities;

namespace BudgetApproval.Application.Services;

public sealed class ReferenceDataService(
    IDepartmentRepository departments,
    IAllocationRepository allocations,
    IUserRepository users,
    TimeProvider clock)
{
    public async Task<LookupsDto> GetLookupsAsync(CancellationToken ct)
    {
        var depts = await departments.ListAsync(ct);
        var years = await allocations.GetFiscalYearsAsync(ct);

        return new LookupsDto(
            depts.Select(d => new LookupItemDto(d.Id, d.Code, d.Name)).ToList(),
            years,
            FiscalCalendar.FiscalYearFor(clock.GetUtcNow().UtcDateTime),
            Enum.GetNames<BudgetCategory>(),
            Enum.GetNames<RequestStatus>());
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken ct) =>
        (await users.ListAsync(ct)).Select(ToDto).ToList();

    public async Task<UserDto?> GetUserAsync(int id, CancellationToken ct) =>
        await users.GetAsync(id, ct) is { } user ? ToDto(user) : null;

    private static UserDto ToDto(AppUser u) => new(
        u.Id,
        u.DisplayName,
        u.Email,
        u.DepartmentId,
        u.Department?.Name ?? string.Empty,
        Enum.GetValues<UserRole>().Where(r => r != UserRole.None && u.HasRole(r)).Select(r => r.ToString()).ToList());
}
