namespace Fpfl.Items.Domain.Entities;

/// <summary>
///     One of the user's credits paying a <see cref="CreditAccount" /> (<c>fpfl.account_payments</c>). Each
///     occurrence of the credit sends either <see cref="Amount" /> or <see cref="Percent" /> of the credit
///     (exactly one is set).
/// </summary>
public sealed class AccountPayment
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public long CreditItemId { get; set; }

    public Item? CreditItem { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Percent { get; set; }
}
