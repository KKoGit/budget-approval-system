using BudgetApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BudgetApproval.Infrastructure.Persistence;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<DepartmentAllocation> Allocations => Set<DepartmentAllocation>();
    public DbSet<BudgetRequest> BudgetRequests => Set<BudgetRequest>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BudgetDbContext).Assembly);
        ApplyProviderConventions(modelBuilder);
    }

    /// <summary>
    /// Audit history is append-only. The domain never exposes a way to change an entry; this is the
    /// belt-and-braces check that nobody bypasses the domain through the context.
    /// </summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardAuditImmutability();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAuditImmutability();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void GuardAuditImmutability()
    {
        if (ChangeTracker.Entries<AuditEntry>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit entries are append-only and cannot be modified or deleted.");
    }

    private void ApplyProviderConventions(ModelBuilder modelBuilder)
    {
        // Timestamps are always UTC. SQLite does not store DateTimeKind, so re-stamp it on read.
        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        // SQLite has no decimal type and cannot ORDER BY or compare decimals stored as TEXT. For the
        // reviewer-friendly SQLite mode we store money as REAL; SQL Server keeps decimal(18,2).
        var isSqlite = Database.IsSqlite();
        var money = new ValueConverter<decimal, double>(v => (double)v, v => Math.Round((decimal)v, 2));

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type == typeof(DateTime)) property.SetValueConverter(utc);
            if (type == typeof(decimal) && isSqlite) property.SetValueConverter(money);
        }
    }
}
