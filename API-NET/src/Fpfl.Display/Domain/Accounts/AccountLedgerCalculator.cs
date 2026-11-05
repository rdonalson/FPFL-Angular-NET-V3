using System.Globalization;
using Fpfl.Display.Domain.Paydown;
using Fpfl.Display.Domain.Recurrence;
using Fpfl.Items.Domain;
using Fpfl.Items.Domain.Entities;

namespace Fpfl.Display.Domain.Accounts;

/// <summary>
///     Runs each credit card or loan forward day by day. Pure (no database, no clock) so every rule is unit-tested.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>The opening balance is what was owed at the end of its date; activity counts from the next day.</item>
///         <item>Each occurrence of a purchase adds its amount. Purchases never touch the main bank forecast.</item>
///         <item>Each occurrence of a paying credit sends its fixed amount, or its percentage of the credit (rounded to cents).</item>
///         <item>A payment never takes the balance below zero: it's cut to what's owed, and nothing is paid on a zero balance.
///               What is actually paid leaves the bank, as a <see cref="LedgerAdjustment" /> on the main ledger.</item>
///         <item>Within a day, purchases come first (by name), then payments (by credit name).</item>
///         <item>Interest is APR / 12 on the balance at each month end (after that day's activity), while anything is owed.</item>
///     </list>
///     The run starts at the balance date, so a window that starts later still begins from the right balance.
/// </remarks>
public static class AccountLedgerCalculator
{
    /// <summary>Item-type name shown on the main ledger for money sent to a card or loan.</summary>
    public const string PaymentTypeName = "Card/Loan Payment";

    public static AccountLedgerResult Calculate(IEnumerable<Item> items, IEnumerable<CreditAccount> accounts, DateWindow window)
    {
        var byId = items.ToDictionary(i => i.Id);
        var results = new List<AccountLedger>();
        var adjustments = new List<LedgerAdjustment>();

        foreach (var account in accounts.OrderBy(a => a.Name).ThenBy(a => a.Id))
        {
            var (ledger, accountAdjustments) = Run(account, byId, window);
            results.Add(ledger);
            adjustments.AddRange(accountAdjustments);
        }

        return new AccountLedgerResult(results, adjustments.OrderBy(a => a.Date).ThenBy(a => a.Name).ToList());
    }

    private sealed record Scheduled(DateOnly Date, int Order, string SortName, long SourceId, AccountEventKind Kind, decimal Amount, decimal? Percent);

    private static (AccountLedger, List<LedgerAdjustment>) Run(
        CreditAccount account, IReadOnlyDictionary<long, Item> items, DateWindow window)
    {
        var first = account.BalanceDate.AddDays(1);
        var scheduled = new List<Scheduled>();

        if (first <= window.End)
        {
            var simulation = new DateWindow(first, window.End);

            foreach (var purchase in account.Purchases.Where(p => p.Amount > 0))
            {
                scheduled.AddRange(RecurrenceEngine.Occurrences(PurchaseRules.AsScheduleItem(purchase), simulation)
                    .Select(date => new Scheduled(date, 0, purchase.Name, purchase.Id, AccountEventKind.Purchase, Money(purchase.Amount), null)));
            }

            foreach (var payment in account.Payments)
            {
                if (!items.TryGetValue(payment.CreditItemId, out var credit) || credit.ItemTypeId != ItemRules.Credit)
                {
                    continue;
                }

                var amount = payment.Percent is { } percent
                    ? Money(Money(credit.Amount) * Math.Min(percent, 100m) / 100m)
                    : Money(payment.Amount ?? 0);

                if (amount > 0)
                {
                    scheduled.AddRange(RecurrenceEngine.Occurrences(credit, simulation)
                        .Select(date => new Scheduled(date, 1, credit.Name ?? string.Empty, credit.Id, AccountEventKind.Payment, amount, payment.Percent)));
                }
            }
        }

        var byDate = scheduled
            .OrderBy(s => s.Date).ThenBy(s => s.Order).ThenBy(s => s.SortName, StringComparer.Ordinal).ThenBy(s => s.SourceId)
            .ToLookup(s => s.Date);

        var monthlyRate = account.Apr / 100m / 12m;
        var balance = Math.Max(0, account.OpeningBalance);

        var events = new List<AccountEvent>();
        var adjustments = new List<LedgerAdjustment>();
        var balances = new List<decimal>(window.Days);

        // Days of the window on or before the balance date have no history: show the opening balance.
        for (var day = window.Start; day <= window.End && day <= account.BalanceDate; day = day.AddDays(1))
        {
            balances.Add(balance);
        }

        var startingBalance = balance;

        for (var day = first; day <= window.End; day = day.AddDays(1))
        {
            var inWindow = window.Contains(day);
            if (day == window.Start)
            {
                startingBalance = balance;
            }

            foreach (var entry in byDate[day])
            {
                decimal change;
                if (entry.Kind == AccountEventKind.Purchase)
                {
                    change = entry.Amount;
                }
                else
                {
                    var paid = Math.Min(entry.Amount, balance);
                    if (paid <= 0)
                    {
                        continue;
                    }

                    change = -paid;
                    if (inWindow)
                    {
                        adjustments.Add(new LedgerAdjustment
                        {
                            Date = day,
                            ItemId = entry.SourceId,
                            Name = $"{account.Name} payment",
                            Detail = entry.Percent is { } p
                                ? $"{p.ToString("0.##", CultureInfo.InvariantCulture)}% of {entry.SortName}"
                                : $"from {entry.SortName}",
                            Amount = -paid,
                            TypeName = PaymentTypeName,
                        });
                    }
                }

                balance += change;
                if (inWindow)
                {
                    events.Add(new AccountEvent
                    {
                        Date = day,
                        Kind = entry.Kind,
                        SourceId = entry.SourceId,
                        SourceName = entry.SortName,
                        Percent = entry.Percent,
                        Amount = change,
                        BalanceAfter = balance,
                    });
                }
            }

            if (balance > 0 && monthlyRate > 0 && day.Day == DateTime.DaysInMonth(day.Year, day.Month))
            {
                var interest = Money(balance * monthlyRate);
                if (interest > 0)
                {
                    balance += interest;
                    if (inWindow)
                    {
                        events.Add(new AccountEvent { Date = day, Kind = AccountEventKind.Interest, Amount = interest, BalanceAfter = balance });
                    }
                }
            }

            if (inWindow)
            {
                balances.Add(balance);
            }
        }

        var ledger = new AccountLedger
        {
            AccountId = account.Id,
            Name = account.Name,
            Kind = account.Kind,
            Apr = account.Apr,
            CreditLimit = account.CreditLimit,
            OpeningBalance = account.OpeningBalance,
            BalanceDate = account.BalanceDate,
            StartingBalance = startingBalance,
            EndingBalance = balances.Count > 0 ? balances[^1] : startingBalance,
            Balances = balances,
            TotalPurchases = events.Where(e => e.Kind == AccountEventKind.Purchase).Sum(e => e.Amount),
            TotalPaid = -events.Where(e => e.Kind == AccountEventKind.Payment).Sum(e => e.Amount),
            TotalInterest = events.Where(e => e.Kind == AccountEventKind.Interest).Sum(e => e.Amount),
            Events = events,
        };

        return (ledger, adjustments);
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Money(double? amount) =>
        amount is { } value && double.IsFinite(value) && value > 0 ? Money((decimal)value) : 0m;
}
