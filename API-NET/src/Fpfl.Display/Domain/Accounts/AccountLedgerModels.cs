using Fpfl.Display.Domain.Paydown;
using Fpfl.Items.Domain.Entities;

namespace Fpfl.Display.Domain.Accounts;

public enum AccountEventKind
{
    /// <summary>A purchase charged to the account (raises the balance).</summary>
    Purchase,

    /// <summary>Money a credit or debit sent to the account (lowers the balance, leaves the bank).</summary>
    Payment,

    /// <summary>A month's interest added to the balance.</summary>
    Interest,
}

/// <summary>One change to an account's balance.</summary>
public sealed record AccountEvent
{
    public required DateOnly Date { get; init; }

    public required AccountEventKind Kind { get; init; }

    /// <summary>The purchase (purchases) or the credit or debit item (payments) behind the change; null for interest.</summary>
    public long? SourceId { get; init; }

    public string? SourceName { get; init; }

    /// <summary>Payments only: the share of the credit, when the payment is a percentage.</summary>
    public decimal? Percent { get; init; }

    /// <summary>Purchases and interest are positive (they raise what's owed); payments are negative.</summary>
    public required decimal Amount { get; init; }

    public required decimal BalanceAfter { get; init; }
}

/// <summary>One card or loan over the ledger's window.</summary>
public sealed record AccountLedger
{
    public required long AccountId { get; init; }

    public required string Name { get; init; }

    public required AccountKind Kind { get; init; }

    public required decimal Apr { get; init; }

    public decimal? CreditLimit { get; init; }

    public required decimal OpeningBalance { get; init; }

    public required DateOnly BalanceDate { get; init; }

    /// <summary>Owed at the start of the window (before its first day's activity).</summary>
    public required decimal StartingBalance { get; init; }

    /// <summary>Owed at the end of the window.</summary>
    public required decimal EndingBalance { get; init; }

    /// <summary>Owed at the end of each day of the window, one value per day.</summary>
    public required IReadOnlyList<decimal> Balances { get; init; }

    /// <summary>Within the window, as positive amounts.</summary>
    public required decimal TotalPurchases { get; init; }

    public required decimal TotalPaid { get; init; }

    public required decimal TotalInterest { get; init; }

    /// <summary>
    ///     The day the balance reaches zero and stays there, which may be after the window (projected up to
    ///     <see cref="AccountLedgerCalculator.HorizonYears" /> years out). Null if it never does in that time.
    /// </summary>
    public DateOnly? PayoffDate { get; init; }

    /// <summary>Balance changes within the window, in date order.</summary>
    public required IReadOnlyList<AccountEvent> Events { get; init; }
}

public sealed record AccountLedgerResult(IReadOnlyList<AccountLedger> Accounts, IReadOnlyList<LedgerAdjustment> Adjustments);
