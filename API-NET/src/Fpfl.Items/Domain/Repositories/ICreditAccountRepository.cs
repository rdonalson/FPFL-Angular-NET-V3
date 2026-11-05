using Fpfl.Items.Domain.Entities;

namespace Fpfl.Items.Domain.Repositories;

/// <summary>Cards and loans; each result includes its purchases (with time periods) and payments (with their credit items).</summary>
public interface ICreditAccountRepository
{
    Task<IReadOnlyList<CreditAccount>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<CreditAccount?> FindByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Saves the account and its payments; purchases are added separately.</summary>
    /// <returns>The new account's id.</returns>
    /// <exception cref="Fpfl.Common.Exceptions.DuplicateItemException">The owner already has an account with that name.</exception>
    Task<long> AddAsync(CreditAccount account, CancellationToken cancellationToken);

    /// <summary>Saves the account's fields and its payments (added, changed and removed). Purchases are untouched.</summary>
    Task UpdateAsync(CreditAccount account, CancellationToken cancellationToken);

    /// <returns>Rows deleted (0 or 1). Its purchases and payments go with it.</returns>
    Task<int> DeleteAsync(long id, CancellationToken cancellationToken);

    /// <returns>The new purchase's id.</returns>
    Task<long> AddPurchaseAsync(AccountPurchase purchase, CancellationToken cancellationToken);

    Task UpdatePurchaseAsync(AccountPurchase purchase, CancellationToken cancellationToken);

    /// <returns>Rows deleted (0 or 1).</returns>
    Task<int> DeletePurchaseAsync(long accountId, long purchaseId, CancellationToken cancellationToken);
}
