using Fpfl.Display.Domain.Models;
using Fpfl.Display.Domain.Paydown;
using Fpfl.Display.Domain.Recurrence;
using Fpfl.Items.Application.Services;
using Fpfl.Items.Domain;
using Fpfl.Items.Domain.Entities;

namespace Fpfl.Display.Domain;

/// <summary>
///     Port of the core of API-JDK's <c>LedgerReadoutServiceImpl</c>: expands a user's items into a
///     day-by-day forecast. Pure (no database, no clock), so every rule is unit-tested directly.
/// </summary>
/// <remarks>
///     Money is summed as <see cref="decimal" /> so a long run of small amounts doesn't drift
///     (API-JDK summed doubles, where 0.1 + 0.2 is 0.30000000000000004).
/// </remarks>
public static class LedgerBuilder
{
    /// <summary>Item-type name shown on the main ledger for money sent to a tracked debt.</summary>
    public const string PaydownTypeName = "Debt Paydown";

    /// <param name="adjustments">
    ///     Extra debits that aren't items of their own: money credits send to tracked debts
    ///     (see <see cref="PaydownCalculator" />). Listed after the day's debits.
    /// </param>
    public static IReadOnlyList<LedgerDay> Build(
        IEnumerable<Item> items, DateWindow window, IEnumerable<LedgerAdjustment>? adjustments = null)
    {
        var all = items.ToList();

        // The initial amount (item type 3) is the opening balance, not an occurrence.
        var balance = Money(all.FirstOrDefault(i => i.ItemTypeId == InitialAmountService.InitialAmountType)?.Amount);

        var occurrences = all
            .Where(i => i.ItemTypeId is ItemRules.Credit or ItemRules.Debit)
            .SelectMany(item => RecurrenceEngine.Occurrences(item, window).Select(date => (Date: date, Item: item)))
            .ToLookup(o => o.Date, o => o.Item);

        var extras = (adjustments ?? Enumerable.Empty<LedgerAdjustment>()).Where(a => window.Contains(a.Date)).ToLookup(a => a.Date);

        var days = new List<LedgerDay>(window.Days);
        var rollupKey = 1;

        for (var date = window.Start; date <= window.End; date = date.AddDays(1))
        {
            var day = new LedgerDay { RollupKey = rollupKey++, Date = date };

            // Credits first, then debits, each alphabetically, so a day always lists the same way.
            var itemKey = 1;
            foreach (var item in occurrences[date].OrderBy(i => i.ItemTypeId).ThenBy(i => i.Name).ThenBy(i => i.Id))
            {
                var amount = item.ItemTypeId == ItemRules.Debit ? -Money(item.Amount) : Money(item.Amount);

                if (amount >= 0)
                {
                    day.CreditSummary += amount;
                }
                else
                {
                    day.DebitSummary += amount;
                }

                day.Entries.Add(new LedgerEntry
                {
                    ItemKey = itemKey++,
                    ItemId = item.Id,
                    OccurrenceDate = date,
                    ItemTypeId = item.ItemTypeId,
                    ItemTypeName = item.ItemType?.Name,
                    PeriodName = item.TimePeriod?.Name,
                    Name = item.Name,
                    Amount = amount,
                });
            }

            foreach (var extra in extras[date].OrderBy(a => a.Name).ThenBy(a => a.Detail))
            {
                var amount = -Math.Abs(extra.Amount);
                day.DebitSummary += amount;
                day.Entries.Add(new LedgerEntry
                {
                    ItemKey = itemKey++,
                    ItemId = extra.ItemId,
                    OccurrenceDate = date,
                    ItemTypeId = ItemRules.Debit,
                    ItemTypeName = extra.TypeName ?? PaydownTypeName,
                    PeriodName = extra.Detail,
                    Name = extra.Name,
                    Amount = amount,
                });
            }

            day.Net = day.CreditSummary + day.DebitSummary;
            balance += day.Net;
            day.RunningTotal = balance;
            days.Add(day);
        }

        return days;
    }

    /// <summary>Stored amounts are doubles; bring them over as cents-exact decimals (null counts as zero).</summary>
    private static decimal Money(double? amount) =>
        amount is { } value && double.IsFinite(value) ? decimal.Round((decimal)value, 2, MidpointRounding.AwayFromZero) : 0m;
}
