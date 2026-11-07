import { AccountLedger } from './accounts/account-ledger.models';
import { LedgerDay, LedgerItem } from './display.models';
import { DebtPaydown } from './paydown/paydown.models';

/** A ledger day for specs; amounts are derived from `items` unless given. */
export function makeDay(rollupKey: number, wDate: string, runningTotal: number, items: Partial<LedgerItem>[] = []): LedgerDay {
  const full: LedgerItem[] = items.map((item, index) => ({
    itemKey: index + 1,
    itemId: 100 + index,
    rollupKey,
    year: Number(wDate.slice(0, 4)),
    occurrenceDate: wDate,
    fkItemType: (item.amount ?? 0) < 0 ? 2 : 1,
    itemType: (item.amount ?? 0) < 0 ? 'Debit' : 'Credit',
    period: 'Monthly',
    name: 'Item',
    amount: 0,
    ...item,
  }));
  const creditSummary = full.filter((i) => i.amount > 0).reduce((sum, i) => sum + i.amount, 0);
  const debitSummary = full.filter((i) => i.amount < 0).reduce((sum, i) => sum + i.amount, 0);
  return {
    rollupKey,
    year: Number(wDate.slice(0, 4)),
    wDate,
    creditSummary,
    debitSummary,
    net: creditSummary + debitSummary,
    runningTotal,
    items: full,
  };
}

/** Oct 17–19, 2026 from a $1,000 start: paycheck and rent on the 18th, nothing else. */
export function sampleLedger(): LedgerDay[] {
  return [
    makeDay(1, '2026-10-17', 1000),
    makeDay(2, '2026-10-18', 1300, [
      { name: 'Paycheck', amount: 2500, period: 'Every Two Weeks' },
      { name: 'Rent', amount: -2200, period: 'Monthly' },
    ]),
    makeDay(3, '2026-10-19', 1300),
  ];
}

/**
 * Chase Visa over the same three days as sampleLedger(): $1,000 owed, a $200 scheduled payment and
 * 25% of the $2,500 paycheck ($625) on the 18th, leaving $175 (paid off the next month).
 */
export function samplePaydown(overrides: Partial<DebtPaydown> = {}): DebtPaydown {
  return {
    debtId: 1,
    itemId: 10,
    name: 'Chase Visa',
    apr: 0,
    enteredBalance: 1000,
    balanceDate: '2026-10-16',
    startingBalance: 1000,
    endingBalance: 175,
    totalPaid: 825,
    totalInterest: 0,
    payoffDate: '2026-11-18',
    balances: [1000, 175, 175],
    events: [
      { date: '2026-10-18', kind: 'payment', sourceItemId: 10, sourceName: 'Chase Visa', percent: null, amount: -200, balanceAfter: 800 },
      { date: '2026-10-18', kind: 'allocation', sourceItemId: 20, sourceName: 'Paycheck', percent: 25, amount: -625, balanceAfter: 175 },
    ],
    ...overrides,
  };
}

/**
 * Chase Visa over the same three days as sampleLedger(): $1,000 owed, a $50 purchase and 25% of the
 * $2,500 paycheck ($625) on the 18th, leaving $425.
 */
export function sampleAccount(overrides: Partial<AccountLedger> = {}): AccountLedger {
  return {
    accountId: 1,
    name: 'Chase Visa',
    kind: 'creditCard',
    apr: 0,
    creditLimit: 5000,
    openingBalance: 1000,
    balanceDate: '2026-10-16',
    startingBalance: 1000,
    endingBalance: 425,
    totalPurchases: 50,
    totalPaid: 625,
    totalInterest: 0,
    payoffDate: '2026-11-01',
    balances: [1000, 425, 425],
    events: [
      { date: '2026-10-18', kind: 'purchase', sourceId: 5, sourceName: 'Groceries', percent: null, amount: 50, balanceAfter: 1050 },
      { date: '2026-10-18', kind: 'payment', sourceId: 20, sourceName: 'Paycheck', percent: 25, amount: -625, balanceAfter: 425 },
    ],
    ...overrides,
  };
}
