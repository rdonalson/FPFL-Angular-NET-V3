using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Fpfl.Common.Sanitization;

namespace Fpfl.Api.Dtos.Display;

/// <summary>
///     Body of POST /display/ledger; same fields as API-JDK's LedgerRequest. <c>userId</c> is optional and
///     defaults to the caller; naming another user needs the admin role. <c>includeGrouping</c> is accepted
///     for compatibility but, as in API-JDK, not used yet.
/// </summary>
public sealed record LedgerRequest(
    Guid? UserId,
    [Required(ErrorMessage = "Ledger start date is required")]
    DateOnly? LedgerStartDate,
    [Required(ErrorMessage = "Ledger end date is required")]
    DateOnly? LedgerEndDate,
    bool IncludeGrouping = false);

/// <summary>One day of the forecast; same shape as API-JDK's LedgerDto. Debits are negative.</summary>
public sealed record LedgerDayResponse(
    int RollupKey,
    int Year,
    [property: JsonPropertyName("wDate")] DateOnly WDate,
    decimal CreditSummary,
    decimal DebitSummary,
    decimal Net,
    decimal RunningTotal,
    IReadOnlyList<LedgerItemResponse> Items);

/// <summary>
///     One credit or debit on a ledger day; same shape as API-JDK's ItemDto (<c>itemType</c> and
///     <c>period</c> are names), plus <c>itemId</c> for linking back to the item. Amount is signed.
/// </summary>
public sealed record LedgerItemResponse(
    int ItemKey,
    long ItemId,
    int RollupKey,
    int Year,
    DateOnly OccurrenceDate,
    long FkItemType,
    [property: LenientText] string? ItemType, // card/loan payments carry "Card/Loan Payment" here
    [property: LenientText] string? Period, // paydown entries carry "25% of Paycheck" here
    [property: LenientText] string? Name,
    decimal Amount);
