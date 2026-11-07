using System.ComponentModel.DataAnnotations;
using Fpfl.Common.Sanitization;

namespace Fpfl.Api.Dtos.Accounts;

/// <summary>
///     Body of POST /accounts and PUT /accounts/{id}. <c>kind</c> is <c>creditCard</c> (default) or <c>loan</c>;
///     <c>apr</c> defaults to 0 and <c>balanceDate</c> to today; <c>creditLimit</c> is optional (cards only).
///     <c>payments</c> (your credits and debits that pay it) replaces the account's payments on update; purchases are managed under
///     /accounts/{id}/purchases. The owner comes from the access token.
/// </summary>
public sealed record AccountRequest(
    [Required(ErrorMessage = "Account name is required and cannot be blank")]
    [MaxLength(150, ErrorMessage = "Account name must be at most 150 characters")]
    [property: LenientText]
    string Name,
    string? Kind,
    [Required(ErrorMessage = "Opening balance is required")]
    decimal? OpeningBalance,
    DateOnly? BalanceDate,
    decimal? Apr,
    decimal? CreditLimit,
    [MaxLength(20, ErrorMessage = "An account can have at most 20 payments")]
    IReadOnlyList<AccountPaymentRequest>? Payments);

/// <summary>
///     One of your items paying the account each time it occurs. A credit sends <c>amount</c> or <c>percent</c>
///     (not both). A debit (e.g. the card's monthly payment) pays its whole amount, so send neither.
/// </summary>
public sealed record AccountPaymentRequest(
    [Required(ErrorMessage = "Each payment needs a credit or debit")]
    long? ItemId,
    decimal? Amount,
    decimal? Percent);

/// <summary>
///     Body of POST /accounts/{id}/purchases and PUT /accounts/{id}/purchases/{purchaseId}. <c>fkPeriod</c> is
///     1 (One Time, needs <c>beginDate</c>), 2 (Daily) or 3 (Weekly, needs <c>weeklyDow</c> 0–6). With
///     <c>dateRangeReq</c> a recurring purchase runs only from <c>beginDate</c> to <c>endDate</c>.
/// </summary>
public sealed record PurchaseRequest(
    [Required(ErrorMessage = "Purchase name is required and cannot be blank")]
    [MaxLength(150, ErrorMessage = "Purchase name must be at most 150 characters")]
    [property: LenientText]
    string Name,
    [Required(ErrorMessage = "Purchase amount is required and must be a positive currency value")]
    decimal? Amount,
    [Required(ErrorMessage = "Time Period is required (1 = One Time, 2 = Daily, 3 = Weekly)")]
    long? FkPeriod,
    DateOnly? BeginDate,
    DateOnly? EndDate,
    int? WeeklyDow,
    bool DateRangeReq);

/// <summary>A credit card or loan with its purchases and the credits that pay it.</summary>
public sealed record AccountResponse(
    long Id,
    [property: LenientText] string Name,
    string Kind,
    decimal OpeningBalance,
    DateOnly BalanceDate,
    decimal Apr,
    decimal? CreditLimit,
    IReadOnlyList<PurchaseResponse> Purchases,
    IReadOnlyList<AccountPaymentResponse> Payments);

public sealed record PurchaseResponse(
    long Id,
    long AccountId,
    [property: LenientText] string Name,
    decimal Amount,
    long FkPeriod,
    string? Period,
    DateOnly? BeginDate,
    DateOnly? EndDate,
    int? WeeklyDow,
    bool DateRangeReq);

/// <summary>
///     <c>itemTypeId</c> is 1 (credit) or 2 (debit). <c>perOccurrence</c> is how much each occurrence sends: a
///     credit's fixed amount or its percentage, or a debit's whole amount.
/// </summary>
public sealed record AccountPaymentResponse(
    long Id,
    long ItemId,
    long? ItemTypeId,
    [property: LenientText] string? ItemName,
    double? ItemAmount,
    string? ItemPeriod,
    decimal? Amount,
    decimal? Percent,
    decimal PerOccurrence);
