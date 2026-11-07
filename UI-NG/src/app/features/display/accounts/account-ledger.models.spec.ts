import { sampleAccount, sampleLedger } from '../testing-ledger';
import { ACCOUNT_COLORS, accountPayoffStatus, accountsChart, describeAccountEvent, utilization } from './account-ledger.models';

describe('account ledger models', () => {
  it('describes each kind of activity', () => {
    const [purchase, payment] = sampleAccount().events;
    expect(describeAccountEvent(purchase)).toBe('Groceries');
    expect(describeAccountEvent(payment)).toBe('25% of Paycheck');
    expect(describeAccountEvent({ ...payment, percent: null })).toBe('Paid from Paycheck');
    expect(describeAccountEvent({ ...payment, kind: 'interest' })).toBe('Interest');
  });

  it('places the payoff and works out the share of the limit', () => {
    expect(accountPayoffStatus(sampleAccount({ payoffDate: '2026-10-18' }), '2026-10-19')).toBe('paid');
    expect(accountPayoffStatus(sampleAccount(), '2026-10-19')).toBe('later');
    expect(accountPayoffStatus(sampleAccount({ payoffDate: null }), '2026-10-19')).toBe('never');
    expect(utilization(sampleAccount())).toBe(9);
    expect(utilization(sampleAccount({ creditLimit: null }))).toBeNull();
  });

  it('charts one stepped line per account with a legend only for several', () => {
    const one = accountsChart([sampleAccount()], sampleLedger());
    expect(one.data.datasets[0].data).toEqual([1000, 425, 425]);
    expect(one.data.datasets[0].borderColor).toBe(ACCOUNT_COLORS[0]);
    expect(one.options.plugins?.legend?.display).toBe(false);

    const two = accountsChart([sampleAccount(), sampleAccount({ accountId: 2, name: 'Car Loan' })], sampleLedger());
    expect(two.data.datasets.map((d) => d.label)).toEqual(['Chase Visa', 'Car Loan']);
    expect(two.options.plugins?.legend?.display).toBe(true);
  });
});
