using Fpfl.Display.Domain.Accounts;
using Fpfl.Display.Domain.Recurrence;
using Fpfl.Items.Domain;
using Fpfl.Items.Domain.Entities;

namespace Fpfl.Api.Tests;

/// <summary>
///     Same calendar as <see cref="PaydownCalculatorTests" />: the every-two-weeks Friday paycheck from Oct 2, 2026
///     lands on Oct 2, 16, 30, Nov 13, 27, Dec 11, 25. Balances are entered as of Oct 17.
/// </summary>
public sealed class AccountLedgerCalculatorTests
{
    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    private static readonly DateOnly AsOf = D(10, 17);
    private static readonly DateWindow Quarter = new(D(10, 18), D(12, 31));

    private static Item Paycheck() => new()
    {
        Id = 20,
        Name = "Paycheck",
        Amount = 2000,
        ItemTypeId = ItemRules.Credit,
        TimePeriodId = ItemRules.EveryTwoWeeks,
        EveryOtherWeekDow = 5,
        BeginDate = D(10, 2),
    };

    private static Item CardPayment(double amount = 200) => new()
    {
        Id = 10,
        Name = "Visa payment",
        Amount = amount,
        ItemTypeId = ItemRules.Debit,
        TimePeriodId = ItemRules.Monthly,
        MonthlyDom = 15,
    };

    private static CreditAccount Visa(decimal balance, decimal apr = 0, params AccountPayment[] payments) => new()
    {
        Id = 1,
        Name = "Visa",
        Kind = AccountKind.CreditCard,
        OpeningBalance = balance,
        BalanceDate = AsOf,
        Apr = apr,
        Payments = payments.ToList(),
    };

    private static AccountLedgerResult Run(CreditAccount account, DateWindow window, params Item[] items) =>
        AccountLedgerCalculator.Calculate(items, [account], window);

    [Fact]
    public void A_percentage_of_a_credit_pays_the_account_off_and_leaves_the_bank()
    {
        var result = Run(Visa(1000, 0, new AccountPayment { CreditItemId = 20, Percent = 25 }), Quarter, Paycheck());
        var account = Assert.Single(result.Accounts);

        Assert.Equal(new[] { D(10, 30), D(11, 13) }, account.Events.Select(e => e.Date));
        Assert.Equal(new[] { -500m, -500m }, account.Events.Select(e => e.Amount));
        Assert.Equal(0m, account.EndingBalance);
        Assert.Equal(D(11, 13), account.PayoffDate);
        Assert.Equal(75, account.Balances.Count);

        Assert.Equal(2, result.Adjustments.Count);
        Assert.All(result.Adjustments, a =>
        {
            Assert.Equal("Visa payment", a.Name);
            Assert.Equal("25% of Paycheck", a.Detail);
            Assert.Equal(AccountLedgerCalculator.PaymentTypeName, a.TypeName);
            Assert.Equal(-500m, a.Amount);
        });
    }

    [Fact]
    public void A_fixed_amount_is_cut_to_what_is_owed()
    {
        var result = Run(Visa(700, 0, new AccountPayment { CreditItemId = 20, Amount = 300 }), Quarter, Paycheck());

        Assert.Equal(new[] { -300m, -300m, -100m }, result.Accounts[0].Events.Select(e => e.Amount));
        Assert.Equal(D(11, 27), result.Accounts[0].PayoffDate);
        Assert.Equal(new[] { -300m, -300m, -100m }, result.Adjustments.Select(a => a.Amount));
        Assert.Equal("from Paycheck", result.Adjustments[0].Detail);
    }

    [Fact]
    public void Purchases_raise_the_balance_but_never_touch_the_bank()
    {
        var groceries = new AccountPurchase
        {
            Id = 5, AccountId = 1, Name = "Groceries", Amount = 50, TimePeriodId = ItemRules.Weekly, WeeklyDow = 1,
        };
        var account = Visa(0);
        account.Purchases = [groceries];

        var result = Run(account, new DateWindow(D(10, 18), D(10, 31)));
        var ledger = result.Accounts[0];

        // Mondays Oct 19 and 26.
        Assert.Equal(new[] { 50m, 50m }, ledger.Events.Select(e => e.Amount));
        Assert.All(ledger.Events, e => Assert.Equal(AccountEventKind.Purchase, e.Kind));
        Assert.Equal(100m, ledger.EndingBalance);
        Assert.Equal(100m, ledger.TotalPurchases);
        Assert.Null(ledger.PayoffDate); // purchases every week, nothing pays it
        Assert.Empty(result.Adjustments);
    }

    [Fact]
    public void A_debit_pays_its_whole_amount_without_a_second_bank_deduction()
    {
        var result = Run(Visa(600, 0, new AccountPayment { CreditItemId = 10, Percent = 100 }), Quarter, CardPayment());
        var ledger = result.Accounts[0];

        Assert.Equal(new[] { D(11, 15), D(12, 15) }, ledger.Events.Select(e => e.Date));
        Assert.Equal(200m, ledger.EndingBalance);
        Assert.Equal(D(1, 15, 2027), ledger.PayoffDate); // projected past the window
        Assert.Empty(result.Adjustments); // the debit is already on the main ledger
    }

    [Fact]
    public void Interest_is_added_at_each_month_end()
    {
        var ledger = Run(Visa(1200, apr: 12), new DateWindow(D(10, 18), D(11, 30))).Accounts[0];

        Assert.Equal(new[] { D(10, 31), D(11, 30) }, ledger.Events.Select(e => e.Date));
        Assert.Equal(new[] { 12m, 12.12m }, ledger.Events.Select(e => e.Amount));
        Assert.Equal(1224.12m, ledger.EndingBalance);
        Assert.Null(ledger.PayoffDate);
    }
}
