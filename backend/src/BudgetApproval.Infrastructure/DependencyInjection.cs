using BudgetApproval.Application.Abstractions;
using BudgetApproval.Infrastructure.Persistence;
using BudgetApproval.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetApproval.Infrastructure;

public enum DatabaseProvider { Sqlite, SqlServer }

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, DatabaseProvider provider, string connectionString)
    {
        services.AddDbContext<BudgetDbContext>(options =>
        {
            if (provider == DatabaseProvider.SqlServer)
                options.UseSqlServer(connectionString);
            else
                options.UseSqlite(connectionString);
        });

        services.AddScoped<IBudgetRequestRepository, BudgetRequestRepository>();
        services.AddScoped<IAllocationRepository, AllocationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
