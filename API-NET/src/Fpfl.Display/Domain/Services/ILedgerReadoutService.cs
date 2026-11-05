using Fpfl.Display.Domain.Accounts;
using Fpfl.Display.Domain.Models;
using Fpfl.Display.Domain.Paydown;

namespace Fpfl.Display.Domain.Services;

/// <summary>Builds a user's forecast ledger and debt paydown. Port of API-JDK's <c>LedgerReadoutService</c>, plus Debt Paydown.</summary>
public interface ILedgerReadoutService
{
    /// <summary>The longest ledger that can be requested in one call.</summary>
    const int MaxYears = 10;

    /// <returns>
    ///     One row per day from <paramref name="start" /> to <paramref name="end" />, inclusive. Money that
    ///     credits send to tracked debts appears as extra "Debt Paydown" debits, and money they send to
    ///     cards and loans as "Card/Loan Payment" debits. Purchases on cards and loans never appear.
    /// </returns>
    /// <exception cref="Fpfl.Common.Exceptions.ForbiddenOperationException">Another user's ledger, and the caller isn't an admin.</exception>
    /// <exception cref="Fpfl.Common.Exceptions.DomainValidationException">The dates are out of order, or span more than <see cref="MaxYears" /> years.</exception>
    Task<IReadOnlyList<LedgerDay>> BuildAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken);

    /// <summary>Each tracked debt's balance, payments and interest over the same window. Same checks as <see cref="BuildAsync" />.</summary>
    Task<IReadOnlyList<DebtPaydown>> BuildPaydownAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken);

    /// <summary>Each credit card or loan's balance, purchases, payments and interest over the same window. Same checks as <see cref="BuildAsync" />.</summary>
    Task<IReadOnlyList<AccountLedger>> BuildAccountsAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken);
}
