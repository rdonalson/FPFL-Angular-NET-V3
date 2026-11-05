using Fpfl.Items.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fpfl.Items.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CreditAccount" /> to <c>fpfl.credit_accounts</c> (see <c>db/002_credit_accounts.sql</c>).</summary>
public sealed class CreditAccountConfiguration : IEntityTypeConfiguration<CreditAccount>
{
    public void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        builder.ToTable("credit_accounts", ItemsDbContext.Schema);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(a => a.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.OpeningBalance).HasColumnName("opening_balance").HasPrecision(14, 2).IsRequired();
        builder.Property(a => a.BalanceDate).HasColumnName("balance_date").IsRequired();
        builder.Property(a => a.Apr).HasColumnName("apr").HasPrecision(6, 3).IsRequired();
        builder.Property(a => a.CreditLimit).HasColumnName("credit_limit").HasPrecision(14, 2);

        builder.HasMany(a => a.Purchases).WithOne().HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Payments).WithOne().HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
