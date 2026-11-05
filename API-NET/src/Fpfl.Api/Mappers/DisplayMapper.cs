using Fpfl.Api.Dtos.Display;
using Fpfl.Display.Domain.Accounts;
using Fpfl.Display.Domain.Models;
using Fpfl.Display.Domain.Paydown;

namespace Fpfl.Api.Mappers;

/// <summary>Explicit ledger mapping (no reflection). Outgoing data is sanitized by the response factory.</summary>
public sealed class DisplayMapper
{
    public IReadOnlyList<LedgerDayResponse> ToResponse(IReadOnlyList<LedgerDay> days) =>
        days.Select(ToResponse).ToList();

    public IReadOnlyList<DebtPaydownResponse> ToResponse(IReadOnlyList<DebtPaydown> debts) =>
        debts.Select(ToResponse).ToList();

    public IReadOnlyList<AccountLedgerResponse> ToResponse(IReadOnlyList<AccountLedger> accounts) =>
        accounts.Select(ToResponse).ToList();

    public AccountLedgerResponse ToResponse(AccountLedger account) => new(
        account.AccountId,
        account.Name,
        AccountMapper.KindName(account.Kind),
        account.Apr,
        account.CreditLimit,
        account.OpeningBalance,
        account.BalanceDate,
        account.StartingBalance,
        account.EndingBalance,
        account.TotalPurchases,
        account.TotalPaid,
        account.TotalInterest,
        account.Balances,
        account.Events.Select(e => new AccountEventResponse(
            e.Date,
            e.Kind switch
            {
                AccountEventKind.Purchase => "purchase",
                AccountEventKind.Payment => "payment",
                _ => "interest",
            },
            e.SourceId,
            e.SourceName,
            e.Percent,
            e.Amount,
            e.BalanceAfter)).ToList());

    public DebtPaydownResponse ToResponse(DebtPaydown debt) => new(
        debt.DebtId,
        debt.DebtItemId,
        debt.Name,
        debt.Apr,
        debt.EnteredBalance,
        debt.BalanceDate,
        debt.StartingBalance,
        debt.EndingBalance,
        debt.TotalPaid,
        debt.TotalInterest,
        debt.PayoffDate,
        debt.Balances,
        debt.Events.Select(e => new PaydownEventResponse(
            e.Date,
            e.Kind switch
            {
                PaydownEventKind.Payment => "payment",
                PaydownEventKind.Allocation => "allocation",
                _ => "interest",
            },
            e.SourceItemId,
            e.SourceName,
            e.Percent,
            e.Amount,
            e.BalanceAfter)).ToList());

    public LedgerDayResponse ToResponse(LedgerDay day) => new(
        day.RollupKey,
        day.Year,
        day.Date,
        day.CreditSummary,
        day.DebitSummary,
        day.Net,
        day.RunningTotal,
        day.Entries.Select(entry => new LedgerItemResponse(
            entry.ItemKey,
            entry.ItemId,
            day.RollupKey,
            day.Year,
            entry.OccurrenceDate,
            entry.ItemTypeId,
            entry.ItemTypeName,
            entry.PeriodName,
            entry.Name,
            entry.Amount)).ToList());
}
