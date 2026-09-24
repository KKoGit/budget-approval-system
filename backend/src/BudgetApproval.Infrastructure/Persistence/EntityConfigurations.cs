using BudgetApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApproval.Infrastructure.Persistence;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("Users");
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Roles).HasConversion<int>();
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DepartmentAllocationConfiguration : IEntityTypeConfiguration<DepartmentAllocation>
{
    public void Configure(EntityTypeBuilder<DepartmentAllocation> b)
    {
        b.ToTable("DepartmentAllocations");
        b.HasIndex(x => new { x.DepartmentId, x.FiscalYear }).IsUnique();
        b.Property(x => x.AllocatedAmount).HasPrecision(18, 2);
        b.Property(x => x.ApprovedAmount).HasPrecision(18, 2);
        b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        b.Ignore(x => x.RemainingAmount);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_Allocation_WithinCeiling",
            "[ApprovedAmount] >= 0 AND [ApprovedAmount] <= [AllocatedAmount]"));
    }
}

internal sealed class BudgetRequestConfiguration : IEntityTypeConfiguration<BudgetRequest>
{
    public void Configure(EntityTypeBuilder<BudgetRequest> b)
    {
        b.ToTable("BudgetRequests", t => t.HasCheckConstraint("CK_BudgetRequest_PositiveAmount", "[RequestedAmount] > 0"));
        b.Property(x => x.Title).HasMaxLength(BudgetRequest.TitleMaxLength).IsRequired();
        b.Property(x => x.Justification).HasMaxLength(BudgetRequest.JustificationMaxLength).IsRequired();
        b.Property(x => x.RequestedAmount).HasPrecision(18, 2);
        b.Property(x => x.ApprovedAmount).HasPrecision(18, 2);

        // Enums as readable strings: the database stays self-describing for reporting and support queries.
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);

        b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        b.Ignore(x => x.IsEditable);

        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.BudgetRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Supports the fiscal-year/department/status filters used by every list, the queue and the dashboard.
        b.HasIndex(x => new { x.FiscalYear, x.DepartmentId, x.Status });
        b.HasIndex(x => x.RequestedById);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("AuditEntries");
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(BudgetRequest.CommentMaxLength);
        b.Property(x => x.Changes).HasMaxLength(2000);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BudgetRequestId, x.OccurredAtUtc });
    }
}
