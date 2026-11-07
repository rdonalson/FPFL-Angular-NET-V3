import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { appTestProviders, authResponse, envelope } from '../../../testing/test-providers';
import { apiUrl } from '../../core/api/api-response';
import { toSession } from '../../core/auth/auth.models';
import { toIsoDate } from '../item-detail/item-form';
import { DisplayPage } from './display';
import { addDays, addMonths, today } from './display.models';
import { AccountLedger } from './accounts/account-ledger.models';
import { DebtPaydown } from './paydown/paydown.models';
import { makeDay, sampleAccount, sampleLedger, samplePaydown } from './testing-ledger';

describe('DisplayPage', () => {
  let fixture: ComponentFixture<DisplayPage>;
  let http: HttpTestingController;

  // jsdom has neither ResizeObserver (PrimeNG's tab bar uses it) nor a canvas (Chart.js draws on one).
  const realResizeObserver = globalThis.ResizeObserver;
  const realGetContext = HTMLCanvasElement.prototype.getContext;
  beforeAll(() => {
    globalThis.ResizeObserver ??= class {
      observe(): void {}
      unobserve(): void {}
      disconnect(): void {}
    } as unknown as typeof ResizeObserver;
    HTMLCanvasElement.prototype.getContext = (() => null) as unknown as typeof realGetContext;
  });
  afterAll(() => {
    globalThis.ResizeObserver = realResizeObserver;
    HTMLCanvasElement.prototype.getContext = realGetContext;
  });

  async function create() {
    sessionStorage.clear();
    sessionStorage.setItem('fpfl.session', JSON.stringify(toSession(authResponse())));
    await TestBed.configureTestingModule({
      imports: [DisplayPage],
      providers: appTestProviders(),
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DisplayPage);
    fixture.detectChanges();
    TestBed.tick();
  }

  afterEach(() => http.verify());

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const ledgerUrl = apiUrl('/display/ledger');
  const paydownUrl = apiUrl('/display/paydown');

  const accountsUrl = apiUrl('/display/accounts');

  /** Answers the paydown and accounts requests that run alongside every ledger request. */
  const flushPaydown = (debts: DebtPaydown[] = [], accounts: AccountLedger[] = []) => {
    http.expectOne(paydownUrl).flush(envelope(debts));
    http.expectOne(accountsUrl).flush(envelope(accounts));
  };

  it('builds the next 90 days on arrival and shows the summary', async () => {
    await create();

    const request = http.expectOne(ledgerUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      ledgerStartDate: toIsoDate(today()),
      ledgerEndDate: toIsoDate(addDays(today(), 90)),
      includeGrouping: false,
    });

    request.flush(envelope(sampleLedger()));
    flushPaydown();
    await fixture.whenStable();

    expect(text()).toContain('Starting balance');
    expect(text()).toContain('$1,000.00');
    expect(text()).toContain('$2,500.00');
    expect(text()).toContain('-$2,200.00');
    expect(text()).toContain('$1,300.00');
    expect(text()).toContain('1 day with activity');
    expect(text()).not.toContain('drops below');
  });

  it('warns when the balance goes negative', async () => {
    await create();

    http.expectOne(ledgerUrl).flush(
      envelope([makeDay(1, '2026-10-17', 100), makeDay(2, '2026-10-18', -50, [{ name: 'Car', amount: -150 }])]),
    );
    flushPaydown();
    await fixture.whenStable();

    expect(text()).toContain('Your balance drops below $0.00 on Oct 18, 2026.');
  });

  it('re-runs with a preset range', async () => {
    await create();
    http.expectOne(ledgerUrl).flush(envelope(sampleLedger()));
    flushPaydown();
    await fixture.whenStable();

    const presetButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === '1 year',
    )!;
    presetButton.click();
    fixture.detectChanges();
    TestBed.tick();

    const request = http.expectOne(ledgerUrl);
    expect(request.request.body.ledgerStartDate).toBe(toIsoDate(today()));
    expect(request.request.body.ledgerEndDate).toBe(toIsoDate(addMonths(today(), 12)));
    const paydownRequest = http.expectOne(paydownUrl);
    expect(paydownRequest.request.body).toEqual(request.request.body);
    paydownRequest.flush(envelope([]));
    const accountsRequest = http.expectOne(accountsUrl);
    expect(accountsRequest.request.body).toEqual(request.request.body);
    accountsRequest.flush(envelope([]));
    request.flush(envelope(sampleLedger()));
    await fixture.whenStable();
  });

  it('shows the API error', async () => {
    await create();

    http
      .expectOne(ledgerUrl)
      .flush(envelope(null, 400, 'A ledger can cover at most 10 years', 'ref-1'), { status: 400, statusText: 'Bad Request' });
    for (const url of [paydownUrl, accountsUrl]) {
      http.expectOne(url).flush(envelope(null, 400, 'A ledger can cover at most 10 years', 'ref-1'), { status: 400, statusText: 'Bad Request' });
    }
    await fixture.whenStable();

    expect(text()).toContain('A ledger can cover at most 10 years');
    expect(text()).toContain('Reference: ref-1');
  });

  it('adds the Paydown tabs when debts are tracked, and only then', async () => {
    await create();
    http.expectOne(ledgerUrl).flush(envelope(sampleLedger()));
    flushPaydown([samplePaydown()]);
    await fixture.whenStable();

    expect(text()).toContain('Paydown chart');
    expect(text()).toContain('Paydown ledger');
  });

  it('leaves the Paydown tabs out when nothing is tracked', async () => {
    await create();
    http.expectOne(ledgerUrl).flush(envelope(sampleLedger()));
    flushPaydown();
    await fixture.whenStable();

    expect(text()).not.toContain('Paydown chart');
    expect(text()).not.toContain('Cards & loans chart');
  });

  it('adds the Cards & loans tabs when there are accounts', async () => {
    await create();
    http.expectOne(ledgerUrl).flush(envelope(sampleLedger()));
    flushPaydown([], [sampleAccount()]);
    await fixture.whenStable();

    expect(text()).toContain('Cards & loans chart');
    expect(text()).toContain('Cards & loans ledger');
  });
});
