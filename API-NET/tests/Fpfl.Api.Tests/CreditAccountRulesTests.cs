using Fpfl.Common.Exceptions;
using Fpfl.Items.Domain;
using Fpfl.Items.Domain.Entities;

namespace Fpfl.Api.Tests;

public sealed class CreditAccountRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);

    private static readonly Dictionary<long, Item> Items = new()
    {
        [10] = new Item { Id = 10, Name = "Visa payment", Amount = 200, ItemTypeId = ItemRules.Debit },
        [20] = new Item { Id = 20, Name = "Paycheck", Amount = 2000, ItemTypeId = ItemRules.Credit },
    };

    private static CreditAccountDraft Draft(string name = "Visa", params PaymentDraft[] payments) =>
        new(name, "creditCard", 1000, null, null, null, payments);

    [Fact]
    public void Defaults_and_rounding()
    {
        var clean = CreditAccountRules.Normalize(Draft(" Visa ", new PaymentDraft(20, null, 33.333m)), Items, [], Today);

        Assert.Equal("Visa", clean.Name);
        Assert.Equal(AccountKind.CreditCard, clean.Kind);
        Assert.Equal(Today, clean.BalanceDate);
        Assert.Equal(33.33m, Assert.Single(clean.Payments).Percent);
    }

    [Fact]
    public void A_debit_pays_its_whole_amount()
    {
        var clean = CreditAccountRules.Normalize(Draft("Visa", new PaymentDraft(10, null, null)), Items, [], Today);
        Assert.Equal(100m, Assert.Single(clean.Payments).Percent);

        Assert.Throws<DomainValidationException>(() =>
            CreditAccountRules.Normalize(Draft("Visa", new PaymentDraft(10, 50, null)), Items, [], Today));
    }

    [Theory]
    [InlineData(20, null, null)]
    [InlineData(20, 100d, 10d)]
    [InlineData(20, 2500d, null)]
    [InlineData(99, 100d, null)]
    public void A_credit_needs_one_sensible_amount_or_percentage(long itemId, double? amount, double? percent)
    {
        var draft = Draft("Visa", new PaymentDraft(itemId, (decimal?)amount, (decimal?)percent));
        Assert.Throws<DomainValidationException>(() => CreditAccountRules.Normalize(draft, Items, [], Today));
    }

    [Fact]
    public void A_credit_cant_send_more_than_100_percent_across_accounts_and_names_are_unique()
    {
        var other = new CreditAccount
        {
            Id = 2, Name = "Car Loan", Payments = [new AccountPayment { CreditItemId = 20, Percent = 80 }],
        };

        Assert.Throws<DomainValidationException>(() =>
            CreditAccountRules.Normalize(Draft("Visa", new PaymentDraft(20, null, 30)), Items, [other], Today));
        Assert.Throws<DuplicateItemException>(() =>
            CreditAccountRules.Normalize(Draft("car loan"), Items, [other], Today));
    }

    [Fact]
    public void Purchases_are_one_time_daily_or_weekly()
    {
        var weekly = PurchaseRules.Normalize(new PurchaseDraft("Gas", 40, ItemRules.Weekly, null, null, 5, false), 1);
        Assert.Equal(5, weekly.WeeklyDow);

        Assert.Throws<DomainValidationException>(() =>
            PurchaseRules.Normalize(new PurchaseDraft("Rent", 900, ItemRules.Monthly, null, null, null, false), 1));
        Assert.Throws<DomainValidationException>(() =>
            PurchaseRules.Normalize(new PurchaseDraft("TV", 500, ItemRules.OneTime, null, null, null, false), 1));
    }
}
