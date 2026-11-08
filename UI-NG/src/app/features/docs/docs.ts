import { ChangeDetectionStrategy, Component, VERSION, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TabsModule } from 'primeng/tabs';

import { environment } from '../../../environments/environment';

export const REPO_URL = 'https://github.com/rdonalson/FPFL-Angular-NET';
export const USER_GUIDE_URL = 'media/fpfl-user-guide.pdf';

export type DocsTab = 'overview' | 'architecture' | 'ledger' | 'data' | 'cicd';

export const DOCS_TABS: readonly { value: DocsTab; label: string; icon: string }[] = [
  { value: 'overview', label: 'Overview', icon: 'pi pi-home' },
  { value: 'architecture', label: 'Architecture', icon: 'pi pi-sitemap' },
  { value: 'ledger', label: 'Ledger Engine', icon: 'pi pi-calendar' },
  { value: 'data', label: 'Data Model', icon: 'pi pi-database' },
  { value: 'cicd', label: 'CI/CD', icon: 'pi pi-cloud-upload' },
];

interface Row {
  name: string;
  detail: string;
}

interface LinkRow {
  label: string;
  detail: string;
  href: string;
}

const REPO_LINKS: readonly LinkRow[] = [
  { label: 'Repository root', detail: 'Both applications, the k8s manifests and the CI/CD workflows', href: REPO_URL },
  { label: 'UI-NG', detail: 'This application: Angular 22, standalone and zoneless', href: `${REPO_URL}/tree/master/UI-NG` },
  { label: 'API-NET', detail: '.NET 10 / ASP.NET Core API in bounded-context projects', href: `${REPO_URL}/tree/master/API-NET` },
  { label: 'Kubernetes manifests', detail: 'Deployments, services, ingress and the one-time setup script', href: `${REPO_URL}/tree/master/k8s` },
  {
    label: 'FPFL-React-Java',
    detail: 'The sister project: the same app in React 19 and Java 25 / Spring Boot 4',
    href: 'https://github.com/rdonalson/FPFL-React-Java',
  },
];

const UI_STACK: readonly Row[] = [
  { name: 'Framework', detail: 'Angular 22 + TypeScript: standalone components, zoneless change detection, OnPush' },
  { name: 'State and data', detail: 'Signals and httpResource (no NgRx store)' },
  { name: 'Components', detail: 'PrimeNG 22 with an Aura-based theme, PrimeIcons' },
  { name: 'HTTP', detail: 'Functional interceptors: loading bar, error normalization, auth with silent refresh' },
  { name: 'Routing', detail: 'Lazy-loaded pages (loadComponent) with functional auth and admin guards' },
  { name: 'Charts and export', detail: 'Chart.js through PrimeNG; ExcelJS loaded only when exporting' },
  { name: 'Tests', detail: "Vitest through Angular's @angular/build:unit-test runner" },
];

const API_PROJECTS: readonly Row[] = [
  {
    name: 'Fpfl.Api',
    detail: 'Controllers, DTOs, mappers, the response envelope, global exception handling, OpenAPI, /health and /info',
  },
  { name: 'Fpfl.Items', detail: 'Items, the initial amount, item types, time periods, cards and loans, debts; EF Core persistence' },
  { name: 'Fpfl.Display', detail: 'The forecast: recurrence expanders, the ledger builder, and the card / loan and paydown calculators' },
  { name: 'Fpfl.Auth', detail: 'Users, roles, JWT issue and validation, rotating refresh tokens, BCrypt' },
  { name: 'Fpfl.Common', detail: 'Domain exceptions and the input sanitizer' },
];

const EXPANDERS: readonly { id: number; period: string; pattern: string }[] = [
  { id: 1, period: 'One time', pattern: 'Once, on the begin date' },
  { id: 2, period: 'Daily', pattern: 'Every day in range' },
  { id: 3, period: 'Weekly', pattern: 'Weekly on a chosen weekday' },
  { id: 4, period: 'Every two weeks', pattern: "Every other week, counted from the item's begin date" },
  { id: 5, period: 'Bi-monthly', pattern: 'Two days per month' },
  { id: 6, period: 'Monthly', pattern: "A chosen day (the 31st becomes each month's last day)" },
  { id: 7, period: 'Quarterly', pattern: 'Four month/day pairs a year' },
  { id: 8, period: 'Semi-annual', pattern: 'Two month/day pairs a year' },
  { id: 9, period: 'Annual', pattern: 'Once a year (Feb 29 falls on Feb 28 outside leap years)' },
  { id: 10, period: 'Nth weekday', pattern: 'The 1st–4th or last weekday of the month, e.g. the 2nd Tuesday' },
];

const ENTITIES: readonly { name: string; group: 'Domain' | 'Auth'; detail: string }[] = [
  { name: 'Item', group: 'Domain', detail: 'A credit, debit or initial amount: amount, date bounds and recurrence fields' },
  { name: 'ItemType', group: 'Domain', detail: 'Credit, Debit or Initial Amount' },
  { name: 'TimePeriod', group: 'Domain', detail: 'Which recurrence fields an item uses' },
  { name: 'CreditAccount', group: 'Domain', detail: 'Cards & Loans: a credit card or loan, with balance, as-of date, APR and optional credit limit' },
  { name: 'AccountPurchase', group: 'Domain', detail: 'Cards & Loans: a purchase on a card (one time, daily or weekly)' },
  { name: 'AccountPayment', group: 'Domain', detail: "Cards & Loans: a credit's fixed amount or percentage, or a debit's whole amount, paying an account" },
  { name: 'Debt', group: 'Domain', detail: 'Debt Paydown: a debit being paid down, with balance, APR and as-of date' },
  { name: 'DebtAllocation', group: 'Domain', detail: 'Debt Paydown: a percentage of a credit sent to a debt' },
  { name: 'User', group: 'Auth', detail: 'An application account' },
  { name: 'Role / UserRole', group: 'Auth', detail: 'Role assignments (ROLE_USER, ROLE_ADMIN)' },
  { name: 'RefreshToken', group: 'Auth', detail: 'Single-use refresh tokens, stored only as SHA-256 hashes' },
];

const PIPELINE: readonly Row[] = [
  { name: '1 · Trigger', detail: 'A push to master under that app\'s folder (UI-NG/** or API-NET/**, plus its k8s manifests), or a manual run' },
  { name: '2 · Test', detail: 'API only: dotnet test; a failing test stops the deployment' },
  { name: '3 · Build', detail: 'Docker Buildx builds the multi-stage image and pushes the commit SHA and :latest tags to Azure Container Registry' },
  { name: '4 · Authenticate', detail: 'Azure login with a service principal, then the AKS context for fpfl-cluster' },
  { name: '5 · Apply', detail: 'kubectl apply of the service, deployment and ingress' },
  { name: '6 · Roll out', detail: 'kubectl rollout restart, then rollout status fails the run if the deployment does not become healthy' },
];

/**
 * Public documentation page (UI-TSX's DocsPage): what the system does, how it is built, how the
 * forecast is computed, the data model and the deployment pipeline, with links to the user
 * guide, the code and the API docs.
 */
@Component({
  selector: 'app-docs',
  imports: [RouterLink, TabsModule],
  templateUrl: './docs.html',
  styleUrls: ['../item-types/item-types.scss', './docs.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Docs {
  protected readonly tabs = DOCS_TABS;
  protected readonly tab = signal<DocsTab>('overview');

  protected readonly repoUrl = REPO_URL;
  protected readonly guideUrl = USER_GUIDE_URL;
  protected readonly apiUrl = environment.apiUrl;
  protected readonly swaggerUrl = `${environment.apiUrl}/swagger-ui`;
  protected readonly angularVersion = VERSION.full;
  protected readonly buildMode = environment.production ? 'production' : 'development';

  protected readonly repoLinks = REPO_LINKS;
  protected readonly uiStack = UI_STACK;
  protected readonly apiProjects = API_PROJECTS;
  protected readonly expanders = EXPANDERS;
  protected readonly entities = ENTITIES;
  protected readonly pipeline = PIPELINE;

  protected selectTab(value: string | number | undefined): void {
    if (DOCS_TABS.some((t) => t.value === value)) {
      this.tab.set(value as DocsTab);
    }
  }
}
