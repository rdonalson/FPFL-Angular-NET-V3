using Fpfl.Common.Exceptions;
using Fpfl.Items.Domain;
using Fpfl.Items.Domain.Entities;
using Fpfl.Items.Domain.Repositories;
using Fpfl.Items.Domain.Services;

namespace Fpfl.Items.Application.Services;

/// <summary>
///     Credit cards and loans. As with items and debts, a user only ever sees and changes their own
///     (someone else's answers 404), the owner comes from the access token, and every save goes through
///     <see cref="CreditAccountRules" /> or <see cref="PurchaseRules" />.
/// </summary>
public sealed class CreditAccountService(ICreditAccountRepository accounts, IItemRepository items, TimeProvider clock)
    : ICreditAccountService
{
    public Task<IReadOnlyList<CreditAccount>> ListAsync(Guid requesterUserId, CancellationToken cancellationToken) =>
        accounts.ListByUserAsync(requesterUserId, cancellationToken);

    public async Task<CreditAccount> GetAsync(long id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new DomainValidationException("Account id must be a positive integer");
        }

        var account = await accounts.FindByIdAsync(id, cancellationToken);
        if (account is null || (!requesterIsAdmin && account.UserId != requesterUserId))
        {
            throw new ItemNotFoundException($"Account {id} not found");
        }

        return account;
    }

    public async Task<CreditAccount> CreateAsync(CreditAccountDraft draft, Guid ownerUserId, CancellationToken cancellationToken)
    {
        var ownerItems = await OwnerItems(ownerUserId, cancellationToken);
        var existing = await accounts.ListByUserAsync(ownerUserId, cancellationToken);
        var clean = CreditAccountRules.Normalize(draft, ownerItems, existing, Today());

        var id = await accounts.AddAsync(Build(0, ownerUserId, clean), cancellationToken);
        return await Reload(id, cancellationToken);
    }

    public async Task<CreditAccount> UpdateAsync(
        long id, CreditAccountDraft draft, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken)
    {
        var current = await GetAsync(id, requesterUserId, requesterIsAdmin, cancellationToken);
        var ownerItems = await OwnerItems(current.UserId, cancellationToken);
        var others = (await accounts.ListByUserAsync(current.UserId, cancellationToken)).Where(a => a.Id != id);
        var clean = CreditAccountRules.Normalize(draft, ownerItems, others, Today());

        await accounts.UpdateAsync(Build(id, current.UserId, clean), cancellationToken);
        return await Reload(id, cancellationToken);
    }

    public async Task DeleteAsync(long id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken)
    {
        await GetAsync(id, requesterUserId, requesterIsAdmin, cancellationToken);

        if (await accounts.DeleteAsync(id, cancellationToken) == 0)
        {
            throw new ItemNotFoundException($"Account {id} not found");
        }
    }

    public async Task<AccountPurchase> AddPurchaseAsync(
        long accountId, PurchaseDraft draft, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken)
    {
        await GetAsync(accountId, requesterUserId, requesterIsAdmin, cancellationToken);
        var purchase = PurchaseRules.Normalize(draft, accountId);

        var purchaseId = await accounts.AddPurchaseAsync(purchase, cancellationToken);
        return await ReloadPurchase(accountId, purchaseId, cancellationToken);
    }

    public async Task<AccountPurchase> UpdatePurchaseAsync(
        long accountId, long purchaseId, PurchaseDraft draft, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken)
    {
        var account = await GetAsync(accountId, requesterUserId, requesterIsAdmin, cancellationToken);
        if (account.Purchases.All(p => p.Id != purchaseId))
        {
            throw new ItemNotFoundException($"Purchase {purchaseId} not found");
        }

        var purchase = PurchaseRules.Normalize(draft, accountId);
        purchase.Id = purchaseId;

        await accounts.UpdatePurchaseAsync(purchase, cancellationToken);
        return await ReloadPurchase(accountId, purchaseId, cancellationToken);
    }

    public async Task DeletePurchaseAsync(
        long accountId, long purchaseId, Guid requesterUserId, bool requesterIsAdmin, CancellationToken cancellationToken)
    {
        await GetAsync(accountId, requesterUserId, requesterIsAdmin, cancellationToken);

        if (await accounts.DeletePurchaseAsync(accountId, purchaseId, cancellationToken) == 0)
        {
            throw new ItemNotFoundException($"Purchase {purchaseId} not found");
        }
    }

    private static CreditAccount Build(long id, Guid owner, CreditAccountRules.Normalized clean) => new()
    {
        Id = id,
        UserId = owner,
        Name = clean.Name,
        Kind = clean.Kind,
        OpeningBalance = clean.OpeningBalance,
        BalanceDate = clean.BalanceDate,
        Apr = clean.Apr,
        CreditLimit = clean.CreditLimit,
        Payments = clean.Payments,
    };

    private async Task<CreditAccount> Reload(long id, CancellationToken cancellationToken) =>
        await accounts.FindByIdAsync(id, cancellationToken) ?? throw new ItemNotFoundException($"Account {id} not found");

    private async Task<AccountPurchase> ReloadPurchase(long accountId, long purchaseId, CancellationToken cancellationToken) =>
        (await Reload(accountId, cancellationToken)).Purchases.FirstOrDefault(p => p.Id == purchaseId)
        ?? throw new ItemNotFoundException($"Purchase {purchaseId} not found");

    private async Task<Dictionary<long, Item>> OwnerItems(Guid userId, CancellationToken cancellationToken) =>
        (await items.ListByUserAsync(userId, cancellationToken)).ToDictionary(i => i.Id);

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
