import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { finalize } from 'rxjs';

import { ApiError, errorMessage } from '../../core/api/api-response';
import { ITEM_TYPE } from '../item-detail/item';
import { ItemApi } from '../item-detail/data-access/item-api';
import { formatDate } from '../item-detail/schedule';
import { AccountEditor } from './account-editor/account-editor';
import { AccountApi } from './data-access/account-api';
import { Account, describePurchase, formatPercent, kindIcon, kindLabel } from './accounts.models';

/**
 * Cards & Loans: every credit card and loan with what's owed, its purchases, and which credits and debits
 * pay it. Purchases are managed on each account's own page; the forecast (Display) charts the balances.
 */
@Component({
  selector: 'app-accounts',
  imports: [RouterLink, CurrencyPipe, ButtonModule, DialogModule, MessageModule, AccountEditor],
  templateUrl: './accounts.html',
  styleUrls: ['../item-types/item-types.scss', './accounts.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountsPage {
  private readonly api = inject(AccountApi);
  private readonly itemApi = inject(ItemApi);
  private readonly messages = inject(MessageService);

  protected readonly accountsResource = this.api.list();
  protected readonly creditsResource = this.itemApi.listForCurrentUser(() => ITEM_TYPE.credit);
  protected readonly debitsResource = this.itemApi.listForCurrentUser(() => ITEM_TYPE.debit);

  /** value() throws while a resource is in an error state, so read it only when there is one. */
  protected readonly accounts = computed(() =>
    this.accountsResource.hasValue() ? (this.accountsResource.value()?.data ?? []) : [],
  );
  protected readonly credits = computed(() => (this.creditsResource.hasValue() ? (this.creditsResource.value()?.data ?? []) : []));
  protected readonly debits = computed(() => (this.debitsResource.hasValue() ? (this.debitsResource.value()?.data ?? []) : []));

  protected readonly loading = computed(() => this.accountsResource.isLoading());
  protected readonly loadError = computed(() => {
    const error = this.accountsResource.error();
    return error
      ? { message: errorMessage(error), correlationId: error instanceof ApiError ? error.correlationId : '' }
      : null;
  });

  protected readonly formatDate = formatDate;
  protected readonly formatPercent = formatPercent;
  protected readonly kindIcon = kindIcon;
  protected readonly kindLabel = kindLabel;
  protected readonly describePurchase = describePurchase;
  protected readonly debitType = ITEM_TYPE.debit;

  /** Purchases listed on a card before "and N more". */
  protected readonly previewCount = 3;

  // ---- editor --------------------------------------------------------------------------------

  protected readonly editing = signal<Account | 'new' | null>(null);

  protected onSaved(): void {
    this.editing.set(null);
    this.accountsResource.reload();
  }

  // ---- delete --------------------------------------------------------------------------------

  protected readonly pendingDelete = signal<Account | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  protected confirmDelete(account: Account): void {
    this.deleteError.set(null);
    this.pendingDelete.set(account);
  }

  protected cancelDelete(): void {
    if (!this.deleting()) {
      this.pendingDelete.set(null);
    }
  }

  protected delete(): void {
    const account = this.pendingDelete();
    if (!account) {
      return;
    }

    this.deleting.set(true);
    this.api
      .remove(account.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: (response) => {
          this.messages.add({ severity: 'success', summary: response.message, life: 3000 });
          this.pendingDelete.set(null);
          this.accountsResource.reload();
        },
        error: (error: unknown) => this.deleteError.set(errorMessage(error)),
      });
  }
}
