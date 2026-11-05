using Fpfl.Common.Exceptions;
using Fpfl.Display.Domain;
using Fpfl.Display.Domain.Accounts;
using Fpfl.Display.Domain.Models;
using Fpfl.Display.Domain.Paydown;
using Fpfl.Display.Domain.Recurrence;
using Fpfl.Display.Domain.Services;
using Fpfl.Items.Domain.Entities;
using Fpfl.Items.Domain.Repositories;

namespace Fpfl.Display.Application.Services;

/// <summary>
///     Loads the user's items, tracked debts and cards/loans and hands them to <see cref="PaydownCalculator" />,
///     <see cref="AccountLedgerCalculator" /> and <see cref="LedgerBuilder" />. Unlike API-JDK, which built a ledger for whatever userId the request
///     body named, a user can only build their own (admins any), and the range is capped so one request
///     can't ask for centuries of rows.
/// </summary>
public sealed class LedgerReadoutService(
    IItemRepository items, IDebtRepository debts, ICreditAccountRepository? accounts = null) : ILedgerReadoutService
{
    public async Task<IReadOnlyList<LedgerDay>> BuildAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken)
    {
        var (window, userItems, paydown, accountLedgers) = await LoadAsync(userId, start, end, requesterUserId, requesterIsAdmin, cancellationToken);
        return LedgerBuilder.Build(userItems, window, paydown.Adjustments.Concat(accountLedgers.Adjustments));
    }

    public async Task<IReadOnlyList<DebtPaydown>> BuildPaydownAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken)
    {
        var (_, _, paydown, _) = await LoadAsync(userId, start, end, requesterUserId, requesterIsAdmin, cancellationToken);
        return paydown.Debts;
    }

    public async Task<IReadOnlyList<AccountLedger>> BuildAccountsAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken)
    {
        var (_, _, _, accountLedgers) = await LoadAsync(userId, start, end, requesterUserId, requesterIsAdmin, cancellationToken);
        return accountLedgers.Accounts;
    }

    private async Task<(DateWindow Window, IReadOnlyList<Item> Items, PaydownResult Paydown, AccountLedgerResult Accounts)> LoadAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken)
    {
        if (!requesterIsAdmin && userId != requesterUserId)
        {
            throw new ForbiddenOperationException("You can only view your own ledger");
        }

        if (end < start)
        {
            throw new DomainValidationException("Ledger start date must be on or before the end date");
        }

        if (end > start.AddYears(ILedgerReadoutService.MaxYears))
        {
            throw new DomainValidationException($"A ledger can cover at most {ILedgerReadoutService.MaxYears} years");
        }

        var window = new DateWindow(start, end);
        var userItems = await items.ListByUserAsync(userId, cancellationToken);
        var userDebts = await debts.ListByUserAsync(userId, cancellationToken);
        IReadOnlyList<CreditAccount> userAccounts = accounts is null ? [] : await accounts.ListByUserAsync(userId, cancellationToken);

        return (
            window,
            userItems,
            PaydownCalculator.Calculate(userItems, userDebts, window),
            AccountLedgerCalculator.Calculate(userItems, userAccounts, window));
    }
}
