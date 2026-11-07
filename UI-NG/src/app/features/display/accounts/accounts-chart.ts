import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ChartModule } from 'primeng/chart';

import { formatDate } from '../../item-detail/schedule';
import { LedgerDay } from '../display.models';
import { AccountLedger, accountColor, accountPayoffStatus, accountsChart, utilization } from './account-ledger.models';

/** The Cards & Loans chart tab: a card per account (owed, purchases, paid, interest, payoff) and every balance over time. */
@Component({
  selector: 'app-accounts-chart',
  imports: [RouterLink, CurrencyPipe, ChartModule],
  templateUrl: './accounts-chart.html',
  styleUrl: '../paydown/paydown.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountsChart {
  readonly accounts = input.required<readonly AccountLedger[]>();
  readonly days = input.required<readonly LedgerDay[]>();

  protected readonly chart = computed(() => accountsChart(this.accounts(), this.days()));
  protected readonly windowEnd = computed(() => this.days().at(-1)?.wDate ?? '');

  protected readonly color = accountColor;
  protected readonly formatDate = formatDate;
  protected readonly status = accountPayoffStatus;
  protected readonly utilization = utilization;
}
