using Fpfl.Common.Exceptions;
using Fpfl.Items.Domain.Entities;

namespace Fpfl.Items.Domain;

/// <summary>The rules a card or loan and its payments must satisfy, in one place.</summary>
public static class CreditAccountRules
{
    public const int MaxNameLength = 150;
    public const decimal MaxMoney = 1_000_000_000m;
    public const decimal MaxApr = 100m;
    public const int MaxPayments = 20;

    /// <summary>The account's fields once checked, plus its payments.</summary>
    public sealed record Normalized(
        string Name,
        AccountKind Kind,
        decimal OpeningBalance,
        DateOnly BalanceDate,
        decimal Apr,
        decimal? CreditLimit,
        List<AccountPayment> Payments);

    /// <param name="draft">The request.</param>
    /// <param name="ownerItems">Every item the owner has, by id.</param>
    /// <param name="otherAccounts">The owner's other accounts (not this one), for unique names and the per-credit 100% cap.</param>
    /// <param name="today">Used when the draft has no balance date.</param>
    /// <exception cref="DomainValidationException">Any rule is broken; the message says which.</exception>
    public static Normalized Normalize(
        CreditAccountDraft draft,
        IReadOnlyDictionary<long, Item> ownerItems,
        IEnumerable<CreditAccount> otherAccounts,
        DateOnly today)
    {
        var others = otherAccounts.ToList();

        var name = draft.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw Invalid("Account name is required and cannot be blank");
        }

        if (name.Length > MaxNameLength)
        {
            throw Invalid($"Account name must be at most {MaxNameLength} characters");
        }

        if (others.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicateItemException($"You already have an account named \"{name}\"");
        }

        var kind = ParseKind(draft.Kind);

        if (draft.OpeningBalance is not { } balance || balance < 0 || balance > MaxMoney)
        {
            throw Invalid("Opening balance is required and must be between $0 and $1,000,000,000");
        }

        var apr = draft.Apr ?? 0;
        if (apr < 0 || apr > MaxApr)
        {
            throw Invalid("APR must be between 0% and 100%");
        }

        decimal? limit = null;
        if (draft.CreditLimit is { } rawLimit)
        {
            if (kind != AccountKind.CreditCard)
            {
                throw Invalid("Only a credit card has a credit limit");
            }

            if (rawLimit <= 0 || rawLimit > MaxMoney)
            {
                throw Invalid("Credit limit must be more than $0 and at most $1,000,000,000");
            }

            limit = Money(rawLimit);
        }

        return new Normalized(
            name,
            kind,
            Money(balance),
            draft.BalanceDate ?? today,
            decimal.Round(apr, 3),
            limit,
            Payments(draft.Payments, ownerItems, others));
    }

    /// <summary><c>creditCard</c> or <c>loan</c>, any case; defaults to a credit card.</summary>
    public static AccountKind ParseKind(string? kind) => kind?.Trim().ToLowerInvariant() switch
    {
        null or "" or "creditcard" or "credit_card" or "credit card" => AccountKind.CreditCard,
        "loan" => AccountKind.Loan,
        _ => throw Invalid("Kind must be \"creditCard\" or \"loan\""),
    };

    private static List<AccountPayment> Payments(
        IReadOnlyList<PaymentDraft>? drafts, IReadOnlyDictionary<long, Item> ownerItems, List<CreditAccount> others)
    {
        var payments = drafts ?? Array.Empty<PaymentDraft>();
        if (payments.Count > MaxPayments)
        {
            throw Invalid($"An account can have at most {MaxPayments} payments");
        }

        // How much of each credit the owner's other accounts already take as a percentage.
        var taken = others
            .SelectMany(a => a.Payments)
            .Where(p => p.Percent is not null)
            .GroupBy(p => p.CreditItemId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Percent!.Value));

        var result = new List<AccountPayment>();
        foreach (var payment in payments)
        {
            if (payment.CreditItemId is not { } creditId || !ownerItems.TryGetValue(creditId, out var credit))
            {
                throw Invalid("Each payment needs one of your credits or debits");
            }

            if (credit.ItemTypeId is not (ItemRules.Credit or ItemRules.Debit))
            {
                throw Invalid($"\"{credit.Name}\" is not a credit or a debit");
            }

            if (result.Any(p => p.CreditItemId == creditId))
            {
                throw Invalid($"\"{credit.Name}\" pays this account more than once");
            }

            if (credit.ItemTypeId == ItemRules.Debit)
            {
                // A debit (e.g. the card's monthly payment) pays its whole amount; it's stored as 100%.
                if (payment.Amount is not null || payment.Percent is not (null or 100m))
                {
                    throw Invalid($"\"{credit.Name}\" is a debit: it pays its whole amount, so leave amount and percentage empty");
                }

                if (taken.GetValueOrDefault(creditId) > 0)
                {
                    throw Invalid($"\"{credit.Name}\" already pays another account");
                }

                result.Add(new AccountPayment { CreditItemId = creditId, Percent = 100m });
                continue;
            }

            switch (payment)
            {
                case { Amount: not null, Percent: not null }:
                case { Amount: null, Percent: null }:
                    throw Invalid($"The payment from \"{credit.Name}\" needs either an amount or a percentage, not both");

                case { Amount: { } amount }:
                    var creditAmount = credit.Amount is { } a && double.IsFinite(a) ? (decimal)a : 0m;
                    if (amount <= 0 || amount > creditAmount)
                    {
                        throw Invalid($"The payment from \"{credit.Name}\" must be more than $0 and at most its amount ({creditAmount:0.00})");
                    }

                    result.Add(new AccountPayment { CreditItemId = creditId, Amount = Money(amount) });
                    break;

                case { Percent: { } raw }:
                    if (raw <= 0 || raw > 100)
                    {
                        throw Invalid($"The percentage of \"{credit.Name}\" must be more than 0% and at most 100%");
                    }

                    var percent = decimal.Round(raw, 2, MidpointRounding.AwayFromZero);
                    var already = taken.GetValueOrDefault(creditId);
                    if (already + percent > 100)
                    {
                        throw Invalid($"\"{credit.Name}\" already sends {already:0.##}% to other accounts; at most {100 - already:0.##}% is left");
                    }

                    result.Add(new AccountPayment { CreditItemId = creditId, Percent = percent });
                    break;
            }
        }

        return result;
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static DomainValidationException Invalid(string message) => new(message);
}
