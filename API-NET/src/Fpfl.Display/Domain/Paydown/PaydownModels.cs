namespace Fpfl.Display.Domain.Paydown;

public enum PaydownEventKind
{
    /// <summary>The debit's own scheduled payment (e.g. the card's monthly payment).</summary>
    Payment,

    /// <summary>A percentage of a credit sent to the debt.</summary>
    Allocation,

    /// <summary>A month's interest added to the balance.</summary>
    Interest,
}

/// <summary>One change to a debt's balance.</summary>
public sealed record PaydownEvent
{
    public required DateOnly Date { get; init; }

    public required PaydownEventKind Kind { get; init; }

    /// <summary>The debit (payments) or credit (allocations) the money came from; null for interest.</summary>
    public long? SourceItemId { get; init; }

    public string? SourceName { get; init; }

    /// <summary>Allocations only: the share of the credit.</summary>
    public decimal? Percent { get; init; }

    /// <summary>Payments and allocations are negative (they reduce what's owed); interest is positive.</summary>
    public required decimal Amount { get; init; }

    public required decimal BalanceAfter { get; init; }
}

/// <summary>One tracked debt over the ledger's window.</summary>
public sealed record DebtPaydown
{
    public required long DebtId { get; init; }

    /// <summary>The debit being paid down; its name names the debt.</summary>
    public required long DebtItemId { get; init; }

    public string? Name { get; init; }

    public required decimal Apr { get; init; }

    /// <summary>What was owed when the user entered it, and as of when.</summary>
    public required decimal EnteredBalance { get; init; }

    public required DateOnly BalanceDate { get; init; }

    /// <summary>Owed at the start of the window (before its first day's activity).</summary>
    public required decimal StartingBalance { get; init; }

    /// <summary>Owed at the end of the window.</summary>
    public required decimal EndingBalance { get; init; }

    /// <summary>Owed at the end of each day of the window, one value per day.</summary>
    public required IReadOnlyList<decimal> Balances { get; init; }

    /// <summary>Within the window, as positive amounts.</summary>
    public required decimal TotalPaid { get; init; }

    public required decimal TotalInterest { get; init; }

    /// <summary>
    ///     The day the balance reaches zero, which may be after the window (projected up to
    ///     <see cref="PaydownCalculator.HorizonYears" /> years out). Null if it never does in that time.
    /// </summary>
    public DateOnly? PayoffDate { get; init; }

    /// <summary>Balance changes within the window, in date order.</summary>
    public required IReadOnlyList<PaydownEvent> Events { get; init; }
}

/// <summary>An extra debit for the main ledger: money a credit sent to a debt.</summary>
public sealed record LedgerAdjustment
{
    public required DateOnly Date { get; init; }

    /// <summary>The debt's debit item.</summary>
    public required long ItemId { get; init; }

    /// <summary>e.g. "Chase Visa paydown".</summary>
    public required string Name { get; init; }

    /// <summary>e.g. "30% of Paycheck".</summary>
    public required string Detail { get; init; }

    /// <summary>Negative.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Item-type name shown on the ledger; null means <see cref="LedgerBuilder.PaydownTypeName" />.</summary>
    public string? TypeName { get; init; }
}

public sealed record PaydownResult(IReadOnlyList<DebtPaydown> Debts, IReadOnlyList<LedgerAdjustment> Adjustments);
