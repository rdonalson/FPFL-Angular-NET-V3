using Fpfl.Common.Exceptions;
using Fpfl.Items.Domain.Entities;
using Fpfl.Items.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fpfl.Items.Infrastructure.Persistence.Repositories;

/// <summary>
///     EF Core implementation of <see cref="ICreditAccountRepository" /> over <c>fpfl.credit_accounts</c>,
///     <c>fpfl.account_purchases</c> and <c>fpfl.account_payments</c> (see <c>db/002_credit_accounts.sql</c>).
/// </summary>
public sealed class CreditAccountRepository(ItemsDbContext db) : ICreditAccountRepository
{
    public async Task<IReadOnlyList<CreditAccount>> ListByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return await WithDetails().Where(a => a.UserId == userId).OrderBy(a => a.Name).ThenBy(a => a.Id).ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new RepositoryException($"Database failure while fetching accounts for user {userId}", ex);
        }
    }

    public async Task<CreditAccount?> FindByIdAsync(long id, CancellationToken cancellationToken)
    {
        try
        {
            return await WithDetails().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new RepositoryException($"Database failure while fetching account {id}", ex);
        }
    }

    public async Task<long> AddAsync(CreditAccount account, CancellationToken cancellationToken)
    {
        try
        {
            account.Purchases = [];
            db.CreditAccounts.Add(account);
            await db.SaveChangesAsync(cancellationToken);
            return account.Id;
        }
        catch (Exception ex) when (SqlState(ex) == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateItemException($"You already have an account named \"{account.Name}\"");
        }
        catch (Exception ex) when (SqlState(ex) == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new DomainValidationException("A paying credit no longer exists");
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new RepositoryException("Database failure while saving account", ex);
        }
        finally
        {
            db.ChangeTracker.Clear(); // the context is no-tracking by default; keep it that way
        }
    }

    public async Task UpdateAsync(CreditAccount account, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await db.CreditAccounts.AsTracking().Include(a => a.Payments)
                .FirstOrDefaultAsync(a => a.Id == account.Id, cancellationToken)
                ?? throw new ItemNotFoundException($"Account {account.Id} not found");

            existing.Name = account.Name;
            existing.Kind = account.Kind;
            existing.OpeningBalance = account.OpeningBalance;
            existing.BalanceDate = account.BalanceDate;
            existing.Apr = account.Apr;
            existing.CreditLimit = account.CreditLimit;

            // Diff the payments rather than replace them, so (account, credit) stays unique at every step.
            var wanted = account.Payments.ToDictionary(p => p.CreditItemId);
            foreach (var current in existing.Payments.ToList())
            {
                if (wanted.Remove(current.CreditItemId, out var change))
                {
                    current.Amount = change.Amount;
                    current.Percent = change.Percent;
                }
                else
                {
                    existing.Payments.Remove(current);
                    db.AccountPayments.Remove(current);
                }
            }

            foreach (var added in wanted.Values)
            {
                existing.Payments.Add(new AccountPayment { CreditItemId = added.CreditItemId, Amount = added.Amount, Percent = added.Percent });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (SqlState(ex) == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateItemException($"You already have an account named \"{account.Name}\"");
        }
        catch (Exception ex) when (SqlState(ex) == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new DomainValidationException("A paying credit no longer exists");
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new RepositoryException($"Database failure while saving account {account.Id}", ex);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<int> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        try
        {
            return await db.CreditAccounts.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new RepositoryException($"Database failure while deleting account {id}", ex);
        }
    }

    public async Task<long> AddPurchaseAsync(AccountPurchase purchase, CancellationToken cancellationToken)
    {
        try
        {
            purchase.TimePeriod = null;
            db.AccountPurchases.Add(purchase);
            await db.SaveChangesAsync(cancellationToken);
            return purchase.Id;
        }
        catch (Exception ex) when (SqlState(ex) == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new DomainValidationException("The account or time period no longer exists");
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new RepositoryException("Database failure while saving purchase", ex);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task UpdatePurchaseAsync(AccountPurchase purchase, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await db.AccountPurchases
                .Where(p => p.Id == purchase.Id && p.AccountId == purchase.AccountId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(p => p.Name, purchase.Name)
                    .SetProperty(p => p.Amount, purchase.Amount)
                    .SetProperty(p => p.TimePeriodId, purchase.TimePeriodId)
                    .SetProperty(p => p.BeginDate, purchase.BeginDate)
                    .SetProperty(p => p.EndDate, purchase.EndDate)
                    .SetProperty(p => p.WeeklyDow, purchase.WeeklyDow)
                    .SetProperty(p => p.DateRangeReq, purchase.DateRangeReq), cancellationToken);

            if (updated == 0)
            {
                throw new ItemNotFoundException($"Purchase {purchase.Id} not found");
            }
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new RepositoryException($"Database failure while saving purchase {purchase.Id}", ex);
        }
    }

    public async Task<int> DeletePurchaseAsync(long accountId, long purchaseId, CancellationToken cancellationToken)
    {
        try
        {
            return await db.AccountPurchases.Where(p => p.Id == purchaseId && p.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            throw new RepositoryException($"Database failure while deleting purchase {purchaseId}", ex);
        }
    }

    private IQueryable<CreditAccount> WithDetails() =>
        db.CreditAccounts
            .Include(a => a.Purchases).ThenInclude(p => p.TimePeriod)
            .Include(a => a.Payments).ThenInclude(p => p.CreditItem).ThenInclude(i => i!.TimePeriod)
            .AsSplitQuery();

    private static string? SqlState(Exception ex) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState;

    private static bool IsDatabaseFailure(Exception ex) =>
        ex is not OperationCanceledException and not DomainException;
}
