using Fpfl.Common.Sanitization;

namespace Fpfl.Api.Dtos.Display;

/// <summary>
///     One credit card or loan over the requested window (POST /display/accounts, same body as /display/ledger).
///     <c>balances</c> has one value per day of the window.
/// </summary>
public sealed record AccountLedgerResponse(
    long AccountId,
    [property: LenientText] string Name,
    string Kind,
    decimal Apr,
    decimal? CreditLimit,
    decimal OpeningBalance,
    DateOnly BalanceDate,
    decimal StartingBalance,
    decimal EndingBalance,
    decimal TotalPurchases,
    decimal TotalPaid,
    decimal TotalInterest,
    IReadOnlyList<decimal> Balances,
    IReadOnlyList<AccountEventResponse> Events);

/// <summary>
///     <c>kind</c> is <c>purchase</c>, <c>payment</c> or <c>interest</c>. Purchases and interest are positive;
///     payments are negative. <c>sourceId</c> is the purchase id (purchases) or the credit item id (payments).
/// </summary>
public sealed record AccountEventResponse(
    DateOnly Date,
    string Kind,
    long? SourceId,
    [property: LenientText] string? SourceName,
    decimal? Percent,
    decimal Amount,
    decimal BalanceAfter);
