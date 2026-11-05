using Fpfl.Items.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fpfl.Items.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AccountPayment" /> to <c>fpfl.account_payments</c>.</summary>
public sealed class AccountPaymentConfiguration : IEntityTypeConfiguration<AccountPayment>
{
    public void Configure(EntityTypeBuilder<AccountPayment> builder)
    {
        builder.ToTable("account_payments", ItemsDbContext.Schema);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(p => p.AccountId).HasColumnName("fk_account").IsRequired();
        builder.Property(p => p.CreditItemId).HasColumnName("fk_credit_item").IsRequired();
        builder.HasOne(p => p.CreditItem).WithMany().HasForeignKey(p => p.CreditItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(p => p.Amount).HasColumnName("amount").HasPrecision(14, 2);
        builder.Property(p => p.Percent).HasColumnName("percent").HasPrecision(5, 2);
    }
}
