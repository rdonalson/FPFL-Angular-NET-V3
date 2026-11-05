namespace Fpfl.Items.Domain.Entities;

/// <summary>What kind of account a <see cref="CreditAccount" /> is. Stored by name in <c>fpfl.credit_accounts.kind</c>.</summary>
public enum AccountKind
{
    CreditCard,
    Loan,
}

/// <summary>
///     A credit card or loan with its own running balance (<c>fpfl.credit_accounts</c>, see
///     <c>db/002_credit_accounts.sql</c>). Purchases raise the balance; payments from the user's credits
///     lower it and leave the main bank forecast.
/// </summary>
public sealed class CreditAccount
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public AccountKind Kind { get; set; }

    /// <summary>What was owed at the end of <see cref="BalanceDate" />; activity counts from the next day.</summary>
    public decimal OpeningBalance { get; set; }

    public DateOnly BalanceDate { get; set; }

    /// <summary>Annual percentage rate, 0–100; interest is added at each month end.</summary>
    public decimal Apr { get; set; }

    /// <summary>Optional; cards only, for showing available credit.</summary>
    public decimal? CreditLimit { get; set; }

    public List<AccountPurchase> Purchases { get; set; } = [];

    public List<AccountPayment> Payments { get; set; } = [];
}
