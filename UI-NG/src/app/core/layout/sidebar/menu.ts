import { ADMIN_ROLE } from '../../auth/auth.models';

export interface MenuLink {
  label: string;
  icon: string;
  to: string;
}

/** A titled group of links; `roles` hides the whole group from users without one of them. */
export interface MenuSection {
  label: string;
  roles?: readonly string[];
  items: readonly MenuLink[];
}

/**
 * The sidebar menu (UI-TSX's menuModel). Paths must match app.routes.ts.
 * The Admin section is hidden for non-admins; the API enforces the same rule.
 */
export const MENU: readonly MenuSection[] = [
  {
    label: 'Actions',
    items: [
      { label: 'Home', icon: 'pi pi-home', to: '/' },
      { label: 'Initial Amount', icon: 'pi pi-wallet', to: '/initial-amount' },
      { label: 'Credits', icon: 'pi pi-arrow-down-left', to: '/credits' },
      { label: 'Debits', icon: 'pi pi-arrow-up-right', to: '/debits' },
      { label: 'Cards & Loans', icon: 'pi pi-credit-card', to: '/accounts' },
      { label: 'Debt Paydown', icon: 'pi pi-percentage', to: '/debt-paydown' },
      { label: 'Display', icon: 'pi pi-chart-line', to: '/display' },
      { label: 'Docs', icon: 'pi pi-book', to: '/docs' },
    ],
  },
  {
    label: 'Admin',
    roles: [ADMIN_ROLE],
    items: [
      { label: 'Item Types', icon: 'pi pi-tags', to: '/item-types' },
      { label: 'Time Periods', icon: 'pi pi-calendar', to: '/time-periods' },
      { label: 'System Status', icon: 'pi pi-server', to: '/status' },
    ],
  },
];

/** Drops sections the user has no role for, then any section left empty (UI-TSX's filterMenuByRoles). */
export function filterMenuByRoles(menu: readonly MenuSection[], roles: readonly string[]): MenuSection[] {
  return menu
    .filter((section) => !section.roles?.length || section.roles.some((role) => roles.includes(role)))
    .filter((section) => section.items.length > 0);
}
