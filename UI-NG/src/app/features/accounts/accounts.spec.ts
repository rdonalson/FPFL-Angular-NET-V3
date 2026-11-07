import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpRequest } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';

import { appTestProviders, authResponse, envelope } from '../../../testing/test-providers';
import { apiUrl } from '../../core/api/api-response';
import { toSession } from '../../core/auth/auth.models';
import { ITEM_TYPE, PERIOD } from '../item-detail/item';
import { makeItem } from '../item-detail/testing-items';
import { AccountsPage } from './accounts';
import { Account } from './accounts.models';
import { visaAccount } from './testing-accounts';

/** The page's protected methods and signals, for driving it without PrimeNG's overlays. */
interface PageApi {
  editing(): Account | 'new' | null;
  confirmDelete(account: Account): void;
  delete(): void;
}

describe('AccountsPage', () => {
  let fixture: ComponentFixture<AccountsPage>;
  let http: HttpTestingController;
  const userId = authResponse().userId;

  const accountsUrl = apiUrl('/accounts');
  const is = (method: string, url: string) => (r: HttpRequest<unknown>) => r.method === method && r.url === url;
  const paycheck = makeItem(PERIOD.everyTwoWeeks, { id: 20, name: 'Paycheck', amount: 2000 });
  const cardPayment = makeItem(PERIOD.monthly, { id: 10, name: 'Visa payment', amount: 200, itemType: { id: ITEM_TYPE.debit, name: 'Debit' } });

  async function create(accounts: Account[] = [visaAccount()]) {
    sessionStorage.clear();
    sessionStorage.setItem('fpfl.session', JSON.stringify(toSession(authResponse())));
    await TestBed.configureTestingModule({ imports: [AccountsPage], providers: appTestProviders() }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AccountsPage);
    fixture.detectChanges();
    TestBed.tick();

    http.expectOne(is('GET', accountsUrl)).flush(envelope(accounts));
    http.expectOne(apiUrl(`/items/${userId}/1`)).flush(envelope([paycheck]));
    http.expectOne(apiUrl(`/items/${userId}/2`)).flush(envelope([cardPayment]));
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  const page = () => fixture.componentInstance as unknown as PageApi;
  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('lists each card with what is owed, what pays it and its purchases', async () => {
    await create();

    expect(text()).toContain('Chase Visa');
    expect(text()).toContain('Credit card');
    expect(text()).toContain('19.99% APR');
    expect(text()).toContain('$5,000 limit');
    expect(text()).toContain('$1,000.00');
    expect(text()).toContain('25%');
    expect(text()).toContain('Paycheck');
    expect(text()).toContain('$500.00');
    expect(text()).toContain('Groceries');
    expect(text()).toContain('Every Monday');
  });

  it('explains how to start when there are none', async () => {
    await create([]);

    expect(text()).toContain('Add a credit card or loan');
  });

  it('opens the editor for a new account', async () => {
    await create([]);

    const add = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Add card or loan'),
    )!;
    add.click();
    fixture.detectChanges();

    expect(page().editing()).toBe('new');
  });

  it('deletes an account and reloads the list', async () => {
    await create();

    page().confirmDelete(visaAccount());
    page().delete();
    http.expectOne(is('DELETE', apiUrl('/accounts/1'))).flush(envelope(null, 200, 'Account deleted successfully'));
    TestBed.tick();
    http.expectOne(is('GET', accountsUrl)).flush(envelope([]));
    await fixture.whenStable();

    expect(text()).toContain('Add a credit card or loan');
  });
});
