import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { appTestProviders, authResponse, envelope } from '../../../../testing/test-providers';
import { apiUrl } from '../../api/api-response';
import { AuthService } from '../../auth/auth';
import { Topbar } from './topbar';

describe('Topbar', () => {
  let fixture: ComponentFixture<Topbar>;
  let http: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Topbar],
      providers: appTestProviders(),
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Topbar);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  async function signInAs(overrides = {}): Promise<void> {
    TestBed.inject(AuthService).login('x@example.com', 'pw').subscribe();
    http.expectOne(apiUrl('/auth/login')).flush(envelope(authResponse(overrides)));
    await fixture.whenStable();
  }

  it('links to the GitHub repo', () => {
    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.topbar__repo');
    expect(link?.href).toContain('github.com/rdonalson/FPFL-Angular-NET-V3');
  });

  it('links the docs page', () => {
    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.topbar__docs');
    expect(link?.getAttribute('href')).toBe('/docs');
  });

  it('shows Guest Login, Login and Register when signed out', () => {
    expect(text()).toContain('Guest Login');
    expect(text()).toContain('Login');
    expect(text()).toContain('Register');
    expect(text()).not.toContain('Logout');
  });

  it('shows the name, Change Password and Logout when signed in', async () => {
    await signInAs();

    expect(text()).toContain('Rick Donalson');
    expect(text()).toContain('Change Password');
    expect(text()).toContain('Logout');
    expect(text()).not.toContain('Guest Login');
  });

  it('hides Change Password for the guest account', async () => {
    await signInAs({ first: 'Guest', last: 'User', isGuest: true });

    expect(text()).toContain('Guest User');
    expect(text()).not.toContain('Change Password');
    expect(text()).toContain('Logout');
  });
});
