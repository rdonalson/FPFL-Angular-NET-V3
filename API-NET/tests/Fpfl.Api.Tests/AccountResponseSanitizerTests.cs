using Fpfl.Api.Mappers;
using Fpfl.Common.Sanitization;
using Fpfl.Display.Domain;
using Fpfl.Display.Domain.Accounts;
using Fpfl.Display.Domain.Recurrence;
using Fpfl.Items.Domain;
using Fpfl.Items.Domain.Entities;

using Microsoft.Extensions.Logging.Abstractions;

namespace Fpfl.Api.Tests;

/// <summary>
///     Every response goes through the strict sanitizer on its way out, so the text the Cards &amp; Loans
///     feature generates ("Card/Loan Payment", "25% of Paycheck", …) must be marked lenient where it lands.
/// </summary>
public sealed class AccountResponseSanitizerTests
{
    private readonly Sanitizer _sanitizer = new(NullLogger<Sanitizer>.Instance);

    private static readonly Item Paycheck = new()
    {
        Id = 20,
        Name = "Paycheck",
        Amount = 2000,
        ItemTypeId = ItemRules.Credit,
        ItemType = new ItemType { Id = ItemRules.Credit, Name = "Credit" },
        TimePeriodId = ItemRules.EveryTwoWeeks,
        TimePeriod = new TimePeriod { Id = ItemRules.EveryTwoWeeks, Name = "Every Two Weeks" },
        EveryOtherWeekDow = 5,
        BeginDate = new DateOnly(2026, 10, 2),
    };

    private static CreditAccount Visa() => new()
    {
        Id = 1,
        Name = "Chase Visa",
        OpeningBalance = 1000,
        BalanceDate = new DateOnly(2026, 10, 17),
        Apr = 19.99m,
        Payments = [new AccountPayment { Id = 1, CreditItemId = 20, CreditItem = Paycheck, Percent = 25 }],
        Purchases =
        [
            new AccountPurchase
            {
                Id = 5, AccountId = 1, Name = "Groceries", Amount = 50, TimePeriodId = ItemRules.Weekly, WeeklyDow = 1,
                TimePeriod = new TimePeriod { Id = ItemRules.Weekly, Name = "Weekly" },
            },
        ],
    };

    private static readonly DateWindow Window = new(new DateOnly(2026, 10, 18), new DateOnly(2026, 11, 30));

    [Fact]
    public void The_ledger_with_card_payments_passes_the_sanitizer()
    {
        var accounts = AccountLedgerCalculator.Calculate([Paycheck], [Visa()], Window);
        var days = LedgerBuilder.Build([Paycheck], Window, accounts.Adjustments);
        var response = new DisplayMapper().ToResponse(days);

        Assert.Contains(response.SelectMany(d => d.Items), i => i.ItemType == AccountLedgerCalculator.PaymentTypeName);
        _sanitizer.Sanitize(response);
    }

    [Fact]
    public void The_accounts_ledger_and_account_pass_the_sanitizer()
    {
        var accounts = AccountLedgerCalculator.Calculate([Paycheck], [Visa()], Window);

        _sanitizer.Sanitize(new DisplayMapper().ToResponse(accounts.Accounts));
        _sanitizer.Sanitize(new AccountMapper().ToResponse(Visa()));
    }
}
