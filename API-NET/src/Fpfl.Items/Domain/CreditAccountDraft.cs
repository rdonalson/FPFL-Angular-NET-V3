namespace Fpfl.Items.Domain;

/// <summary>What a request asks a card or loan to be, before <see cref="CreditAccountRules" /> checks it.</summary>
/// <param name="Kind"><c>creditCard</c> or <c>loan</c> (any case).</param>
public sealed record CreditAccountDraft(
    string? Name,
    string? Kind,
    decimal? OpeningBalance,
    DateOnly? BalanceDate,
    decimal? Apr,
    decimal? CreditLimit,
    IReadOnlyList<PaymentDraft> Payments);

/// <summary>One of the owner's credits paying the account: a fixed <paramref name="Amount" /> or a <paramref name="Percent" /> (not both).</summary>
public sealed record PaymentDraft(long? CreditItemId, decimal? Amount, decimal? Percent);

/// <summary>What a request asks a purchase to be, before <see cref="PurchaseRules" /> checks it.</summary>
public sealed record PurchaseDraft(
    string? Name,
    decimal? Amount,
    long? TimePeriodId,
    DateOnly? BeginDate,
    DateOnly? EndDate,
    int? WeeklyDow,
    bool DateRangeReq);
