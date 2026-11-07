import { Route, Routes } from '@angular/router';

import { adminGuard } from './core/auth/admin-guard';
import { authGuard } from './core/auth/auth-guard';
import { ITEM_TYPE } from './features/item-detail/item';

/**
 * Top-level routes. Every page is lazy-loaded with loadComponent (no NgModules).
 * Route `data` is bound to component inputs (withComponentInputBinding), e.g. ItemList's itemType.
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Home | FPFL',
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
  },
  {
    path: 'initial-amount',
    title: 'Initial Amount | FPFL',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/item-detail/initial-amount/initial-amount').then((m) => m.InitialAmountPage),
  },
  ...itemRoutes('credits', ITEM_TYPE.credit, 'Credit'),
  ...itemRoutes('debits', ITEM_TYPE.debit, 'Debit'),
  {
    path: 'accounts',
    title: 'Cards & Loans | FPFL',
    canActivate: [authGuard],
    loadComponent: () => import('./features/accounts/accounts').then((m) => m.AccountsPage),
  },
  {
    path: 'accounts/:id',
    title: 'Card or Loan | FPFL',
    canActivate: [authGuard],
    loadComponent: () => import('./features/accounts/account-detail/account-detail').then((m) => m.AccountDetail),
  },
  {
    path: 'debt-paydown',
    title: 'Debt Paydown | FPFL',
    canActivate: [authGuard],
    loadComponent: () => import('./features/debt-paydown/debt-paydown').then((m) => m.DebtPaydownPage),
  },
  {
    path: 'display',
    title: 'Display | FPFL',
    canActivate: [authGuard],
    loadComponent: () => import('./features/display/display').then((m) => m.DisplayPage),
  },
  {
    path: 'item-types',
    title: 'Item Types | FPFL',
    canActivate: [authGuard],
    loadComponent: () => import('./features/item-types/item-types').then((m) => m.ItemTypes),
  },
  {
    path: 'time-periods',
    title: 'Time Periods | FPFL',
    canActivate: [authGuard],
    loadComponent: () => import('./features/time-periods/time-periods').then((m) => m.TimePeriods),
  },
  {
    path: 'docs',
    title: 'Docs | FPFL',
    loadComponent: () => import('./features/docs/docs').then((m) => m.Docs),
  },
  {
    path: 'status',
    title: 'System Status | FPFL',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/status/status').then((m) => m.Status),
  },
  {
    path: '**',
    title: 'Page not found | FPFL',
    loadComponent: () => import('./core/system/not-found/not-found').then((m) => m.NotFound),
  },
];

/**
 * The four pages for credits or debits: list, period picker, new item for a period, edit item.
 * `itemType` reaches each component as an input; `:periodId` and `:id` do too.
 */
function itemRoutes(path: string, itemType: number, noun: string): Route[] {
  const itemList = () => import('./features/item-detail/item-list/item-list').then((m) => m.ItemList);
  const periodPicker = () => import('./features/item-detail/period-picker/period-picker').then((m) => m.PeriodPicker);
  const itemEdit = () => import('./features/item-detail/item-edit/item-edit').then((m) => m.ItemEdit);
  const shared = { canActivate: [authGuard], data: { itemType } };

  return [
    { path, title: `${noun}s | FPFL`, loadComponent: itemList, ...shared },
    { path: `${path}/new`, title: `New ${noun} | FPFL`, loadComponent: periodPicker, ...shared },
    { path: `${path}/new/:periodId`, title: `New ${noun} | FPFL`, loadComponent: itemEdit, ...shared },
    { path: `${path}/:id/edit`, title: `Edit ${noun} | FPFL`, loadComponent: itemEdit, ...shared },
  ];
}
