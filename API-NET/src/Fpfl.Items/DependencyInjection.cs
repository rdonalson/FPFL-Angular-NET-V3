using Fpfl.Items.Application.Services;
using Fpfl.Items.Domain.Repositories;
using Fpfl.Items.Domain.Services;
using Fpfl.Items.Infrastructure.Persistence;
using Fpfl.Items.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fpfl.Items;

/// <summary>
///     Registers the Items bounded context: its DbContext, repositories and services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFpflItems(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ItemsDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped<IItemTypeRepository, ItemTypeRepository>();
        services.AddScoped<IItemTypeService, ItemTypeService>();

        services.AddScoped<ITimePeriodRepository, TimePeriodRepository>();
        services.AddScoped<ITimePeriodService, TimePeriodService>();

        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IInitialAmountService, InitialAmountService>();

        services.AddScoped<IDebtRepository, DebtRepository>();
        services.AddScoped<IDebtService, DebtService>();

        services.AddScoped<ICreditAccountRepository, CreditAccountRepository>();
        services.AddScoped<ICreditAccountService, CreditAccountService>();

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
