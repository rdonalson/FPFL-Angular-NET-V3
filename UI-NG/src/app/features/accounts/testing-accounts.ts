import { Account } from './accounts.models';

/** Chase Visa: $1,000 owed as of Oct 17, 2026, a weekly $50 grocery purchase, paid by 25% of the paycheck. */
export function visaAccount(overrides: Partial<Account> = {}): Account {
  return {
    id: 1,
    name: 'Chase Visa',
    kind: 'creditCard',
    openingBalance: 1000,
    balanceDate: '2026-10-17',
    apr: 19.99,
    creditLimit: 5000,
    purchases: [
      {
        id: 5,
        accountId: 1,
        name: 'Groceries',
        amount: 50,
        fkPeriod: 3,
        period: 'Weekly',
        beginDate: null,
        endDate: null,
        weeklyDow: 1,
        dateRangeReq: false,
      },
    ],
    payments: [
      {
        id: 7,
        itemId: 20,
        itemTypeId: 1,
        itemName: 'Paycheck',
        itemAmount: 2000,
        itemPeriod: 'Every Two Weeks',
        amount: null,
        percent: 25,
        perOccurrence: 500,
      },
    ],
    ...overrides,
  };
}
