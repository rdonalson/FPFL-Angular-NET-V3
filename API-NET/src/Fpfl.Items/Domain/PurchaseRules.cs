using Fpfl.Common.Exceptions;
using Fpfl.Items.Domain.Entities;

namespace Fpfl.Items.Domain;

/// <summary>
///     The rules a purchase must satisfy. Purchases schedule like items (same period ids and date-range
///     meaning as <see cref="ItemRules" />), limited for now to One Time, Daily and Weekly. Only the fields
///     belonging to the purchase's period are kept.
/// </summary>
public static class PurchaseRules
{
    public static readonly IReadOnlySet<long> SupportedPeriods = new HashSet<long> { ItemRules.OneTime, ItemRules.Daily, ItemRules.Weekly };

    /// <exception cref="DomainValidationException">Any rule is broken; the message says which.</exception>
    public static AccountPurchase Normalize(PurchaseDraft draft, long accountId)
    {
        var name = draft.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw Invalid("Purchase name is required and cannot be blank");
        }

        if (name.Length > CreditAccountRules.MaxNameLength)
        {
            throw Invalid($"Purchase name must be at most {CreditAccountRules.MaxNameLength} characters");
        }

        if (draft.Amount is not { } amount || amount <= 0 || amount > CreditAccountRules.MaxMoney)
        {
            throw Invalid("Purchase amount is required and must be a positive currency value");
        }

        if (draft.TimePeriodId is not { } period || !SupportedPeriods.Contains(period))
        {
            throw Invalid("Purchases can be One Time (1), Daily (2) or Weekly (3)");
        }

        var purchase = new AccountPurchase
        {
            AccountId = accountId,
            Name = name,
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            TimePeriodId = period,
        };

        if (period == ItemRules.Weekly)
        {
            purchase.WeeklyDow = draft.WeeklyDow is >= 0 and <= 6
                ? draft.WeeklyDow
                : throw Invalid("Weekly day of week is required (0 = Sunday … 6 = Saturday)");
        }

        if (period == ItemRules.OneTime)
        {
            // A one-time purchase happens on its begin date; it has no end.
            purchase.BeginDate = draft.BeginDate ?? throw Invalid("A one-time purchase needs its date (beginDate)");
            purchase.DateRangeReq = true;
            return purchase;
        }

        purchase.DateRangeReq = draft.DateRangeReq;
        if (draft.DateRangeReq)
        {
            if (draft.BeginDate is null || draft.EndDate is null)
            {
                throw Invalid("Please select both begin and end dates for the date range");
            }

            if (draft.BeginDate > draft.EndDate)
            {
                throw Invalid("Begin date must be before or equal to end date");
            }

            purchase.BeginDate = draft.BeginDate;
            purchase.EndDate = draft.EndDate;
        }

        return purchase;
    }

    /// <summary>
    ///     The purchase as a debit item, so the ledger can expand its dates with the same recurrence engine
    ///     as everything else. Never saved.
    /// </summary>
    public static Item AsScheduleItem(AccountPurchase purchase) => new()
    {
        Id = purchase.Id,
        Name = purchase.Name,
        Amount = (double)purchase.Amount,
        ItemTypeId = ItemRules.Debit,
        TimePeriodId = purchase.TimePeriodId,
        TimePeriod = purchase.TimePeriod,
        BeginDate = purchase.BeginDate,
        EndDate = purchase.EndDate,
        WeeklyDow = purchase.WeeklyDow,
        DateRangeReq = purchase.DateRangeReq,
    };

    private static DomainValidationException Invalid(string message) => new(message);
}
