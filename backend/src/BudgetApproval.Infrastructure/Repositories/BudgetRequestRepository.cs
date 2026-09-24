using System.Text.RegularExpressions;
using BudgetApproval.Application.Abstractions;
using BudgetApproval.Application.Common;
using BudgetApproval.Application.Contracts;
using BudgetApproval.Domain.Entities;
using BudgetApproval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApproval.Infrastructure.Repositories;

internal sealed partial class BudgetRequestRepository(BudgetDbContext db) : IBudgetRequestRepository
{
    public async Task<BudgetRequest?> GetAsync(int id, bool includeHistory, CancellationToken ct)
    {
        var query = db.BudgetRequests
            .Include(r => r.Department)
            .Include(r => r.RequestedBy)
            .AsQueryable();

        if (includeHistory) query = query.Include(r => r.History);

        return await query.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<PagedResult<BudgetRequest>> SearchAsync(BudgetRequestQuery q, BudgetScope scope, CancellationToken ct)
    {
        var query = db.BudgetRequests
            .AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.RequestedBy)
            .ApplyScope(scope);

        if (q.Status is { } status) query = query.Where(r => r.Status == status);
        if (q.Category is { } category) query = query.Where(r => r.Category == category);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var reference = ReferencePattern().Match(term);
            if (reference.Success && int.TryParse(reference.Groups["id"].Value, out var id))
            {
                query = query.Where(r => r.Id == id);
            }
            else
            {
                // LIKE is case-insensitive on both SQLite (ASCII) and SQL Server's default collation; Contains() is not on SQLite.
                var pattern = $"%{EscapeLike(term)}%";
                query = query.Where(r => EF.Functions.Like(r.Title, pattern, "\\") || EF.Functions.Like(r.Justification, pattern, "\\"));
            }
        }

        var total = await query.CountAsync(ct);
        var items = await ApplySort(query, q.SortBy, q.IsDescending)
            .Skip((q.SafePage - 1) * q.SafePageSize)
            .Take(q.SafePageSize)
            .ToListAsync(ct);

        return new PagedResult<BudgetRequest>(items, q.SafePage, q.SafePageSize, total);
    }

    public async Task<IReadOnlyList<BudgetRequest>> GetPendingAsync(BudgetScope scope, CancellationToken ct) =>
        await db.BudgetRequests
            .AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.RequestedBy)
            .ApplyScope(scope)
            .Where(r => r.Status == RequestStatus.Submitted)
            .OrderBy(r => r.SubmittedAtUtc).ThenBy(r => r.Id) // first in, first reviewed
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RequestAmountRow>> GetAmountsAsync(BudgetScope scope, CancellationToken ct) =>
        await db.BudgetRequests
            .AsNoTracking()
            .ApplyScope(scope)
            .Select(r => new RequestAmountRow(r.DepartmentId, r.Status, r.RequestedAmount, r.ApprovedAmount))
            .ToListAsync(ct);

    public void Add(BudgetRequest request) => db.BudgetRequests.Add(request);

    /// <summary>Whitelisted sort columns; user input never reaches the query as a column name. Id breaks ties so paging is stable.</summary>
    private static IQueryable<BudgetRequest> ApplySort(IQueryable<BudgetRequest> query, string? sortBy, bool desc) =>
        (sortBy?.ToLowerInvariant()) switch
        {
            "amount" => desc ? query.OrderByDescending(r => r.RequestedAmount).ThenByDescending(r => r.Id) : query.OrderBy(r => r.RequestedAmount).ThenBy(r => r.Id),
            "title" => desc ? query.OrderByDescending(r => r.Title).ThenByDescending(r => r.Id) : query.OrderBy(r => r.Title).ThenBy(r => r.Id),
            "department" => desc ? query.OrderByDescending(r => r.Department!.Name).ThenByDescending(r => r.Id) : query.OrderBy(r => r.Department!.Name).ThenBy(r => r.Id),
            "status" => desc ? query.OrderByDescending(r => r.Status).ThenByDescending(r => r.Id) : query.OrderBy(r => r.Status).ThenBy(r => r.Id),
            "submitted" => desc ? query.OrderByDescending(r => r.SubmittedAtUtc).ThenByDescending(r => r.Id) : query.OrderBy(r => r.SubmittedAtUtc).ThenBy(r => r.Id),
            _ => desc ? query.OrderByDescending(r => r.UpdatedAtUtc).ThenByDescending(r => r.Id) : query.OrderBy(r => r.UpdatedAtUtc).ThenBy(r => r.Id)
        };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    [GeneratedRegex(@"^BR-\d{4}-(?<id>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReferencePattern();
}
