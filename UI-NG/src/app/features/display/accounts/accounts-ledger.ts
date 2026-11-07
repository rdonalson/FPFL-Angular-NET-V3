import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';

import { longDay } from '../display.models';
import { AccountEvent, AccountLedger, accountColor, describeAccountEvent } from './account-ledger.models';

interface AccountRow {
  key: string;
  accountIndex: number;
  accountName: string;
  event: AccountEvent;
  activity: string;
}

/** The Cards & Loans ledger tab: every purchase, payment and interest charge, with the balance after it. */
@Component({
  selector: 'app-accounts-ledger',
  imports: [FormsModule, CurrencyPipe, SelectModule, TableModule],
  templateUrl: './accounts-ledger.html',
  styleUrl: '../paydown/paydown.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountsLedger {
  readonly accounts = input.required<readonly AccountLedger[]>();

  /** null = every account. */
  protected readonly accountFilter = signal<number | null>(null);

  protected readonly filterOptions = computed(() => [
    { label: 'All cards & loans', value: null },
    ...this.accounts().map((account) => ({ label: account.name, value: account.accountId })),
  ]);

  protected readonly rows = computed<AccountRow[]>(() => {
    const filter = this.accountFilter();
    return this.accounts()
      .flatMap((account, accountIndex) =>
        filter !== null && account.accountId !== filter
          ? []
          : account.events.map((event, i) => ({
              key: `${account.accountId}-${i}`,
              accountIndex,
              accountName: account.name,
              event,
              activity: describeAccountEvent(event),
            })),
      )
      .sort((a, b) => a.event.date.localeCompare(b.event.date) || a.accountIndex - b.accountIndex);
  });

  protected readonly color = accountColor;
  protected readonly longDay = longDay;
  protected readonly abs = Math.abs;
}
