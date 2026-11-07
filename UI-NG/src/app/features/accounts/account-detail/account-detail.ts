import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TableModule } from 'primeng/table';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { finalize } from 'rxjs';

import { ApiError, errorMessage } from '../../../core/api/api-response';
import { ITEM_TYPE, PERIOD, WEEKDAY_OPTIONS } from '../../item-detail/item';
import { ItemApi } from '../../item-detail/data-access/item-api';
import { formatDate } from '../../item-detail/schedule';
import { AccountEditor } from '../account-editor/account-editor';
import { AccountApi } from '../data-access/account-api';
import {
  Account,
  PURCHASE_PERIODS,
  Purchase,
  PurchaseForm,
  describePurchase,
  emptyPurchaseForm,
  formatPercent,
  kindIcon,
  kindLabel,
  toPurchaseForm,
  toPurchaseRequest,
  validatePurchaseForm,
} from '../accounts.models';

const toOptionalNumber = (value: unknown): number | undefined =>
  value === undefined || value === null || value === '' ? undefined : Number(value);

/** One card or loan (/accounts/:id): its details and payments, and its purchases to add, edit and delete. */
@Component({
  selector: 'app-account-detail',
  imports: [
    RouterLink,
    FormsModule,
    CurrencyPipe,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    SelectButtonModule,
    TableModule,
    ToggleSwitchModule,
    AccountEditor,
  ],
  templateUrl: './account-detail.html',
  styleUrls: ['../../item-types/item-types.scss', '../accounts.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountDetail {
  /** Route parameter. */
  readonly id = input<number | undefined, unknown>(undefined, { transform: toOptionalNumber });

  private readonly api = inject(AccountApi);
  private readonly itemApi = inject(ItemApi);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  protected readonly accountResource = this.api.get(() => this.id());
  /** Every account, for the editor's name and 100% checks. */
  protected readonly accountsResource = this.api.list();
  protected readonly creditsResource = this.itemApi.listForCurrentUser(() => ITEM_TYPE.credit);
  protected readonly debitsResource = this.itemApi.listForCurrentUser(() => ITEM_TYPE.debit);

  protected readonly account = computed(() => (this.accountResource.hasValue() ? (this.accountResource.value()?.data ?? null) : null));
  protected readonly accounts = computed(() => (this.accountsResource.hasValue() ? (this.accountsResource.value()?.data ?? []) : []));
  protected readonly credits = computed(() => (this.creditsResource.hasValue() ? (this.creditsResource.value()?.data ?? []) : []));
  protected readonly debits = computed(() => (this.debitsResource.hasValue() ? (this.debitsResource.value()?.data ?? []) : []));

  protected readonly loadError = computed(() => {
    const error = this.accountResource.error();
    return error
      ? { message: errorMessage(error), correlationId: error instanceof ApiError ? error.correlationId : '' }
      : null;
  });

  protected readonly PERIOD = PERIOD;
  protected readonly periods = [...PURCHASE_PERIODS];
  protected readonly weekdays = [...WEEKDAY_OPTIONS];
  protected readonly debitType = ITEM_TYPE.debit;
  protected readonly formatDate = formatDate;
  protected readonly formatPercent = formatPercent;
  protected readonly kindIcon = kindIcon;
  protected readonly kindLabel = kindLabel;
  protected readonly describePurchase = describePurchase;


  private reload(): void {
    this.accountResource.reload();
    this.accountsResource.reload();
  }

  // ---- account editor -------------------------------------------------------------------------

  protected readonly editingAccount = signal<Account | null>(null);

  protected onAccountSaved(): void {
    this.editingAccount.set(null);
    this.reload();
  }

  // ---- purchase editor ------------------------------------------------------------------------

  /** null = closed; 'new' = adding; a Purchase = editing it. */
  protected readonly editing = signal<Purchase | 'new' | null>(null);
  protected readonly form = signal<PurchaseForm>(emptyPurchaseForm(new Date()));
  protected readonly saving = signal(false);
  protected readonly problem = signal<string | null>(null);

  protected readonly editorTitle = computed(() => {
    const editing = this.editing();
    return editing === 'new' || editing === null ? 'Add a purchase' : `Edit ${editing.name}`;
  });

  protected openNew(): void {
    this.form.set(emptyPurchaseForm(new Date()));
    this.problem.set(null);
    this.editing.set('new');
  }

  protected openEdit(purchase: Purchase): void {
    this.form.set(toPurchaseForm(purchase));
    this.problem.set(null);
    this.editing.set(purchase);
  }

  protected closeEditor(): void {
    if (!this.saving()) {
      this.editing.set(null);
    }
  }

  protected patch(changes: Partial<PurchaseForm>): void {
    this.form.update((form) => ({ ...form, ...changes }));
    this.problem.set(null);
  }

  protected save(): void {
    const editing = this.editing();
    const account = this.account();
    if (!editing || !account) {
      return;
    }

    const problem = validatePurchaseForm(this.form());
    if (problem) {
      this.problem.set(problem);
      return;
    }

    const request = toPurchaseRequest(this.form());
    const call =
      editing === 'new' ? this.api.addPurchase(account.id, request) : this.api.updatePurchase(account.id, editing.id, request);

    this.saving.set(true);
    call.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (response) => {
        this.messages.add({ severity: 'success', summary: response.message, life: 3000 });
        this.editing.set(null);
        this.reload();
      },
      error: (error: unknown) => this.problem.set(errorMessage(error)),
    });
  }

  // ---- delete purchase / account --------------------------------------------------------------

  /** A purchase, or the account itself. */
  protected readonly pendingDelete = signal<Purchase | Account | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  protected isPurchase(target: Purchase | Account): target is Purchase {
    return 'accountId' in target;
  }

  protected confirmDelete(target: Purchase | Account): void {
    this.deleteError.set(null);
    this.pendingDelete.set(target);
  }

  protected cancelDelete(): void {
    if (!this.deleting()) {
      this.pendingDelete.set(null);
    }
  }

  protected delete(): void {
    const target = this.pendingDelete();
    const account = this.account();
    if (!target || !account) {
      return;
    }

    const purchase = this.isPurchase(target);
    const call = purchase ? this.api.removePurchase(account.id, target.id) : this.api.remove(account.id);

    this.deleting.set(true);
    call.pipe(finalize(() => this.deleting.set(false))).subscribe({
      next: (response) => {
        this.messages.add({ severity: 'success', summary: response.message, life: 3000 });
        this.pendingDelete.set(null);
        if (purchase) {
          this.reload();
        } else {
          void this.router.navigate(['/accounts']);
        }
      },
      error: (error: unknown) => this.deleteError.set(errorMessage(error)),
    });
  }
}
