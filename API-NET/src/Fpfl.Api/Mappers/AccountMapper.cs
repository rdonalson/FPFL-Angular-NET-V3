using Fpfl.Api.Dtos.Accounts;
using Fpfl.Common.Sanitization;
using Fpfl.Items.Domain;
using Fpfl.Items.Domain.Entities;

namespace Fpfl.Api.Mappers;

/// <summary>Explicit card/loan mapping (no reflection). Outgoing data is sanitized by the response factory.</summary>
public sealed class AccountMapper
{
    public CreditAccountDraft ToDraft(AccountRequest request) => new(
        Sanitizer.Normalize(request.Name),
        request.Kind,
        request.OpeningBalance,
        request.BalanceDate,
        request.Apr,
        request.CreditLimit,
        (request.Payments ?? Array.Empty<AccountPaymentRequest>())
            .Select(p => new PaymentDraft(p.CreditItemId, p.Amount, p.Percent))
            .ToList());

    public PurchaseDraft ToDraft(PurchaseRequest request) => new(
        Sanitizer.Normalize(request.Name),
        request.Amount,
        request.FkPeriod,
        request.BeginDate,
        request.EndDate,
        request.WeeklyDow,
        request.DateRangeReq);

    public static string KindName(AccountKind kind) => kind == AccountKind.Loan ? "loan" : "creditCard";

    public AccountResponse ToResponse(CreditAccount account) => new(
        account.Id,
        Sanitizer.Normalize(account.Name) ?? string.Empty,
        KindName(account.Kind),
        account.OpeningBalance,
        account.BalanceDate,
        account.Apr,
        account.CreditLimit,
        account.Purchases
            .OrderBy(p => p.BeginDate ?? DateOnly.MinValue).ThenBy(p => p.Name).ThenBy(p => p.Id)
            .Select(ToResponse)
            .ToList(),
        account.Payments
            .OrderBy(p => p.CreditItem?.Name)
            .Select(p => new AccountPaymentResponse(
                p.Id,
                p.CreditItemId,
                Sanitizer.Normalize(p.CreditItem?.Name),
                p.CreditItem?.Amount,
                p.CreditItem?.TimePeriod?.Name,
                p.Amount,
                p.Percent,
                PerOccurrence(p)))
            .ToList());

    public PurchaseResponse ToResponse(AccountPurchase purchase) => new(
        purchase.Id,
        purchase.AccountId,
        Sanitizer.Normalize(purchase.Name) ?? string.Empty,
        purchase.Amount,
        purchase.TimePeriodId,
        purchase.TimePeriod?.Name,
        purchase.BeginDate,
        purchase.EndDate,
        purchase.WeeklyDow,
        purchase.DateRangeReq);

    private static decimal PerOccurrence(AccountPayment payment)
    {
        if (payment.Amount is { } amount)
        {
            return amount;
        }

        return payment is { Percent: { } percent, CreditItem.Amount: { } credit } && double.IsFinite(credit)
            ? decimal.Round((decimal)credit * percent / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }
}
