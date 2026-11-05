namespace Fpfl.Items.Domain.Entities;

/// <summary>
///     A purchase charged to a <see cref="CreditAccount" /> (<c>fpfl.account_purchases</c>). Scheduled like a
///     debit item, limited for now to One Time, Daily and Weekly (see <see cref="Fpfl.Items.Domain.PurchaseRules" />).
/// </summary>
public sealed class AccountPurchase
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public long TimePeriodId { get; set; }

    public TimePeriod? TimePeriod { get; set; }

    public DateOnly? BeginDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? WeeklyDow { get; set; }

    public bool DateRangeReq { get; set; }
}
