import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

import { AuthService } from '../../auth/auth';
import { AuthUi } from '../../auth/auth-ui';
import { Layout } from '../layout';

/**
 * Top bar: menu button (signed in), avatar + app title (links home), GitHub link, and the auth controls
 * (Guest Login / Login / Register, or the user's name / Change Password / Logout).
 * Same controls as UI-TSX's AppTopMenu; Change Password is hidden for the guest.
 */
@Component({
  selector: 'app-topbar',
  imports: [RouterLink, ButtonModule],
  templateUrl: './topbar.html',
  styleUrl: './topbar.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Topbar {
  protected readonly auth = inject(AuthService);
  protected readonly ui = inject(AuthUi);
  protected readonly layout = inject(Layout);

  protected readonly title = 'Financial Planner Forecast Ledger';
  protected readonly repoUrl = 'https://github.com/rdonalson/FPFL-Angular-NET-V3';
}
