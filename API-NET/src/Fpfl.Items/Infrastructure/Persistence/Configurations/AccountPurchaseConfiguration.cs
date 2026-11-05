using Fpfl.Items.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fpfl.Items.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AccountPurchase" /> to <c>fpfl.account_purchases</c>.</summary>
public sealed class AccountPurchaseConfiguration : IEntityTypeConfiguration<AccountPurchase>
{
    public void Configure(EntityTypeBuilder<AccountPurchase> builder)
    {
        builder.ToTable("account_purchases", ItemsDbContext.Schema);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(p => p.AccountId).HasColumnName("fk_account").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(p => p.Amount).HasColumnName("amount").HasPrecision(14, 2).IsRequired();
        builder.Property(p => p.TimePeriodId).HasColumnName("fk_time_period").IsRequired();
        builder.HasOne(p => p.TimePeriod).WithMany().HasForeignKey(p => p.TimePeriodId);
        builder.Property(p => p.BeginDate).HasColumnName("begin_date");
        builder.Property(p => p.EndDate).HasColumnName("end_date");
        builder.Property(p => p.WeeklyDow).HasColumnName("weekly_dow");
        builder.Property(p => p.DateRangeReq).HasColumnName("date_range_req").IsRequired();
    }
}
