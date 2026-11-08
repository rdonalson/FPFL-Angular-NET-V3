import { ComponentFixture, TestBed } from '@angular/core/testing';

import { appTestProviders } from '../../../testing/test-providers';
import { DOCS_TABS, Docs, REPO_URL, USER_GUIDE_URL } from './docs';

describe('Docs', () => {
  let fixture: ComponentFixture<Docs>;

  // jsdom has no ResizeObserver, which PrimeNG's tab bar uses (same stand-in as display.spec.ts).
  const realResizeObserver = globalThis.ResizeObserver;
  beforeAll(() => {
    globalThis.ResizeObserver ??= class {
      observe(): void {}
      unobserve(): void {}
      disconnect(): void {}
    } as unknown as typeof ResizeObserver;
  });
  afterAll(() => {
    globalThis.ResizeObserver = realResizeObserver;
  });

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Docs],
      providers: appTestProviders(),
    }).compileComponents();

    fixture = TestBed.createComponent(Docs);
    await fixture.whenStable();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent ?? '';

  it('opens on the overview', () => {
    expect(text()).toContain('FPFL Documentation');
    expect(text()).toContain('What this system does');
    expect(text()).toContain('Where things live');
  });

  it('links the user guide, the API docs and the repository', () => {
    const hrefs = [...el().querySelectorAll<HTMLAnchorElement>('.docs__actions a')].map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual([USER_GUIDE_URL, expect.stringMatching(/\/swagger-ui$/), REPO_URL]);
    expect(USER_GUIDE_URL).toBe('media/fpfl-user-guide.pdf');
  });

  it('has a tab for each section', () => {
    const tabs = [...el().querySelectorAll('p-tab')].map((t) => t.textContent?.trim());
    expect(tabs).toEqual(DOCS_TABS.map((t) => t.label));
  });

  it('switches sections', async () => {
    const tab = (label: string) =>
      [...el().querySelectorAll<HTMLElement>('p-tab')].find((t) => t.textContent?.includes(label))!;

    tab('Ledger Engine').click();
    await fixture.whenStable();
    expect(text()).toContain('How a forecast is built');
    expect(text()).toContain('Nth weekday');
    expect(text()).toContain('PaydownCalculator');
    expect(text()).toContain('AccountLedgerCalculator');

    tab('Architecture').click();
    await fixture.whenStable();
    expect(text()).toContain('Fpfl.Display');
    expect(text()).toContain('Signals and httpResource');

    tab('Data Model').click();
    await fixture.whenStable();
    expect(text()).toContain('DebtAllocation');
    expect(text()).toContain('CreditAccount');

    tab('CI/CD').click();
    await fixture.whenStable();
    expect(text()).toContain('kubectl rollout restart');
  });
});
