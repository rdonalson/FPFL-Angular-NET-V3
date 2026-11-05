using Fpfl.Items.Domain.Entities;

namespace Fpfl.Items.Domain.Services;

/// <summary>A user's credit cards and loans, their purchases, and the credits that pay them.</summary>
public interface ICreditAccountService
{
    Task<IReadOnlyList<CreditAccount>> ListAsync(Guid requesterUserId, CancellationToken cancellationToken);

    /// <exception cref="Fpfl.Common.Exceptions.ItemNotFoundException">Missing, or someone else's (unless admin).</exception>
    Task<CreditAccount> GetAsync(long id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken);

    Task<CreditAccount> CreateAsync(CreditAccountDraft draft, Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Changes the account's fields and payments. Purchases are managed separately.</summary>
    Task<CreditAccount> UpdateAsync(long id, CreditAccountDraft draft, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken);

    /// <summary>Deletes the account with its purchases and payments. The credit items themselves are untouched.</summary>
    Task DeleteAsync(long id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken);

    Task<AccountPurchase> AddPurchaseAsync(long accountId, PurchaseDraft draft, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken);

    Task<AccountPurchase> UpdatePurchaseAsync(long accountId, long purchaseId, PurchaseDraft draft, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken);

    Task DeletePurchaseAsync(long accountId, long purchaseId, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken);
}
