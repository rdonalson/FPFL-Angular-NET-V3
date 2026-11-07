import { ITEM_TYPE, PERIOD } from '../item-detail/item';
import { makeItem } from '../item-detail/testing-items';
import {
  describePurchase,
  emptyAccountForm,
  emptyPurchaseForm,
  newPayment,
  paymentShare,
  takenElsewhere,
  toAccountForm,
  toAccountRequest,
  toPurchaseRequest,
  validateAccountForm,
  validatePurchaseForm,
} from './accounts.models';
import { visaAccount } from './testing-accounts';

describe('accounts models', () => {
  const paycheck = makeItem(PERIOD.everyTwoWeeks, { id: 20, name: 'Paycheck', amount: 2000 });
  const cardPayment = makeItem(PERIOD.monthly, {
    id: 10,
    name: 'Visa payment',
    amount: 200,
    itemType: { id: ITEM_TYPE.debit, name: 'Debit' },
  });
  const items = new Map([paycheck, cardPayment].map((i) => [i.id, i]));
  const item = (id: number) => items.get(id);
  const valid = () => ({ ...emptyAccountForm(new Date(2026, 10, 5)), name: 'Car Loan', openingBalance: 12000 });

  it('round-trips an account through the form and builds the request', () => {
    const form = toAccountForm(visaAccount());
    expect(form.balanceDate).toEqual(new Date(2026, 9, 17));
    expect(form.payments[0]).toEqual(expect.objectContaining({ itemId: 20, mode: 'percent', percent: 25 }));

    expect(toAccountRequest({ ...form, name: ' Chase Visa ' })).toEqual({
      name: 'Chase Visa',
      kind: 'creditCard',
      openingBalance: 1000,
      balanceDate: '2026-10-17',
      apr: 19.99,
      creditLimit: 5000,
      payments: [{ itemId: 20, amount: null, percent: 25 }],
    });
  });

  it('sends a debit with neither amount nor percentage, and drops the limit for a loan', () => {
    const form = { ...valid(), kind: 'loan' as const, creditLimit: 900, payments: [newPayment({ itemId: 10, mode: 'whole' })] };
    expect(toAccountRequest(form)).toEqual(
      expect.objectContaining({ creditLimit: null, payments: [{ itemId: 10, amount: null, percent: null }] }),
    );
  });

  it('works out each occurrence of a payment', () => {
    expect(paymentShare(newPayment({ mode: 'percent', percent: 33.33 }), paycheck)).toBe(666.6);
    expect(paymentShare(newPayment({ mode: 'amount', amount: 300 }), paycheck)).toBe(300);
    expect(paymentShare(newPayment({ mode: 'whole' }), cardPayment)).toBe(200);
    expect(paymentShare(newPayment(), undefined)).toBe(0);
  });

  it('counts the percentages other accounts take', () => {
    const accounts = [visaAccount(), visaAccount({ id: 2, name: 'Amex' })];
    expect(takenElsewhere(accounts, 1).get(20)).toBe(25);
    expect(takenElsewhere(accounts, null).get(20)).toBe(50);
  });

  it('validates like the API', () => {
    const others = [visaAccount()];
    expect(validateAccountForm(valid(), others, null, item)).toBeNull();
    expect(validateAccountForm({ ...valid(), name: ' ' }, others, null, item)).toContain('name');
    expect(validateAccountForm({ ...valid(), name: 'chase visa' }, others, null, item)).toContain('already have');
    expect(validateAccountForm({ ...valid(), openingBalance: null }, others, null, item)).toContain('balance');
    expect(validateAccountForm({ ...valid(), apr: 120 }, others, null, item)).toContain('APR');

    const pay = (changes: Parameters<typeof newPayment>[0]) => ({ ...valid(), payments: [newPayment(changes)] });
    expect(validateAccountForm(pay({}), others, null, item)).toContain('Choose a credit or debit');
    expect(validateAccountForm(pay({ itemId: 20, percent: 80 }), others, null, item)).toContain('75% is left');
    expect(validateAccountForm(pay({ itemId: 20, percent: 75 }), others, null, item)).toBeNull();
    expect(validateAccountForm(pay({ itemId: 20, mode: 'amount', amount: 2500 }), others, null, item)).toContain('up to');
    expect(validateAccountForm(pay({ itemId: 10, mode: 'whole' }), others, null, item)).toBeNull();
  });

  it('builds purchase requests with only the fields their period uses', () => {
    const oneTime = { ...emptyPurchaseForm(new Date(2026, 10, 5)), name: 'Laptop', amount: 900, endDate: new Date(2026, 11, 1) };
    expect(toPurchaseRequest(oneTime)).toEqual({
      name: 'Laptop',
      amount: 900,
      fkPeriod: PERIOD.oneTime,
      beginDate: '2026-11-05',
      endDate: null,
      weeklyDow: null,
      dateRangeReq: true,
    });

    const weekly = { ...oneTime, name: 'Gas', fkPeriod: PERIOD.weekly, weeklyDow: 5 };
    expect(toPurchaseRequest(weekly)).toEqual(expect.objectContaining({ beginDate: null, weeklyDow: 5, dateRangeReq: false }));
  });

  it('validates purchases', () => {
    const base = { ...emptyPurchaseForm(new Date(2026, 10, 5)), name: 'Gas', amount: 40 };
    expect(validatePurchaseForm(base)).toBeNull();
    expect(validatePurchaseForm({ ...base, beginDate: null })).toContain('date');
    expect(validatePurchaseForm({ ...base, fkPeriod: PERIOD.weekly })).toContain('day of the week');
    expect(
      validatePurchaseForm({ ...base, fkPeriod: PERIOD.daily, dateRangeReq: true, beginDate: new Date(2026, 11, 1), endDate: new Date(2026, 10, 1) }),
    ).toContain('on or before');
  });

  it('describes purchases', () => {
    expect(describePurchase(visaAccount().purchases[0])).toBe('Every Monday');
    expect(describePurchase({ fkPeriod: PERIOD.oneTime, beginDate: '2026-11-05', endDate: null, weeklyDow: null, dateRangeReq: true })).toBe(
      'Once, on Nov 5, 2026',
    );
    expect(
      describePurchase({ fkPeriod: PERIOD.daily, beginDate: '2026-11-01', endDate: '2026-11-30', weeklyDow: null, dateRangeReq: true }),
    ).toBe('Every day, Nov 1, 2026 – Nov 30, 2026');
  });
});
