import type { ChartData, ChartOptions, TooltipItem } from 'chart.js';

import { formatMoney } from '../chart/chart-data';
import { LedgerDay, shortDay } from '../display.models';

/** One change to a card or loan's balance (API's AccountEventResponse). Purchases and interest are positive, payments negative. */
export interface AccountEvent {
  /** 'yyyy-MM-dd' */
  date: string;
  kind: 'purchase' | 'payment' | 'interest';
  /** The purchase id (purchases) or the paying credit/debit item id (payments). */
  sourceId: number | null;
  sourceName: string | null;
  percent: number | null;
  amount: number;
  balanceAfter: number;
}

/** A card or loan over the forecast window (API's AccountLedgerResponse). */
export interface AccountLedger {
  accountId: number;
  name: string;
  kind: 'creditCard' | 'loan';
  apr: number;
  creditLimit: number | null;
  openingBalance: number;
  balanceDate: string;
  startingBalance: number;
  endingBalance: number;
  totalPurchases: number;
  totalPaid: number;
  totalInterest: number;
  /** May fall after the window; null if not cleared within 30 years. */
  payoffDate: string | null;
  /** Owed at the end of each day of the window. */
  balances: number[];
  events: AccountEvent[];
}

/** One color per account, in a fixed order, so an account keeps its color across chart, ledger and cards. */
export const ACCOUNT_COLORS = ['#0d9488', '#2563eb', '#d97706', '#7c3aed', '#db2777', '#65a30d'] as const;

export function accountColor(index: number): string {
  return ACCOUNT_COLORS[index % ACCOUNT_COLORS.length];
}

/** "Groceries", "25% of Paycheck", "From Paycheck", "Visa payment", "Interest". */
export function describeAccountEvent(event: AccountEvent): string {
  switch (event.kind) {
    case 'purchase':
      return event.sourceName ?? 'Purchase';
    case 'payment':
      return event.percent !== null
        ? `${Number(event.percent.toFixed(2))}% of ${event.sourceName ?? 'a credit'}`
        : `Paid from ${event.sourceName ?? 'an item'}`;
    default:
      return 'Interest';
  }
}

/** Is the payoff inside the window, after it, or not in sight? */
export function accountPayoffStatus(account: AccountLedger, windowEnd: string): 'paid' | 'later' | 'never' {
  if (account.payoffDate === null) {
    return 'never';
  }
  return account.payoffDate <= windowEnd ? 'paid' : 'later';
}

/** Share of the credit limit in use at the end of the window, 0–100+, or null without a limit. */
export function utilization(account: AccountLedger): number | null {
  return account.creditLimit ? Math.round((account.endingBalance / account.creditLimit) * 100) : null;
}

const compactMoney = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', notation: 'compact', maximumFractionDigits: 1 });

/** Every account's balance on one axis, one colored line each. */
export function accountsChart(
  accounts: readonly AccountLedger[],
  days: readonly LedgerDay[],
): { data: ChartData<'line'>; options: ChartOptions<'line'> } {
  return {
    data: {
      labels: days.map((day) => shortDay(day.wDate)),
      datasets: accounts.map((account, index) => ({
        label: account.name,
        data: account.balances,
        borderColor: accountColor(index),
        backgroundColor: accountColor(index),
        borderWidth: 2,
        pointRadius: 0,
        pointHoverRadius: 5,
        pointHoverBorderWidth: 2,
        pointHoverBorderColor: '#ffffff',
        stepped: 'after' as const, // balances change on purchase and payment days and stay flat between them
        fill: false,
      })),
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: {
          display: accounts.length > 1,
          position: 'top',
          align: 'end',
          labels: { boxWidth: 12, boxHeight: 12, color: '#4b5563' },
        },
        tooltip: {
          callbacks: {
            label: (item: TooltipItem<'line'>) => ` ${item.dataset.label}: ${formatMoney(Number(item.raw))} owed`,
          },
        },
      },
      scales: {
        x: { grid: { display: false }, ticks: { color: '#4b5563', autoSkip: true, maxTicksLimit: 8, maxRotation: 0 } },
        y: {
          beginAtZero: true,
          grid: { color: '#eef0f3' },
          border: { display: false },
          ticks: { color: '#4b5563', maxTicksLimit: 6, callback: (value) => compactMoney.format(Number(value)) },
        },
      },
    },
  };
}
