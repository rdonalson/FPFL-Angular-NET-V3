using Fpfl.Items.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Fpfl.Items.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Items bounded context, mapped onto the existing
///     <c>fpfl</c> schema in the shared FPFL-V2 PostgreSQL database.
///     The schema is currently owned by API-JDK (Hibernate ddl-auto: update), so this
///     context reads the existing tables and must not be used to generate migrations
///     against that database.
/// </summary>
public sealed class ItemsDbContext(DbContextOptions<ItemsDbContext> options) : DbContext(options)
{
    public const string Schema = "fpfl";

    public DbSet<ItemType> ItemTypes => Set<ItemType>();

    public DbSet<TimePeriod> TimePeriods => Set<TimePeriod>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<Debt> Debts => Set<Debt>();

    public DbSet<DebtAllocation> DebtAllocations => Set<DebtAllocation>();

    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();

    public DbSet<AccountPurchase> AccountPurchases => Set<AccountPurchase>();

    public DbSet<AccountPayment> AccountPayments => Set<AccountPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ItemsDbContext).Assembly);
    }
}
