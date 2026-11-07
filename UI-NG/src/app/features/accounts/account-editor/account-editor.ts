import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { finalize } from 'rxjs';

import { errorMessage } from '../../../core/api/api-response';
import { ITEM_TYPE, Item } from '../../item-detail/item';
import { describeSchedule } from '../../item-detail/schedule';
import { AccountApi } from '../data-access/account-api';
import {
  Account,
  AccountForm,
  KIND_OPTIONS,
  MAX_PAYMENTS,
  PaymentForm,
  PaymentMode,
  emptyAccountForm,
  formatPercent,
  newPayment,
  paymentShare,
  takenElsewhere,
  toAccountForm,
  toAccountRequest,
  validateAccountForm,
} from '../accounts.models';

interface ItemOption {
  id: number;
  label: string;
  group: string;
}

/**
 * The add / edit dialog for a card or loan: its details and which credits and debits pay it.
 * Open it by setting `account` to 'new' or an Account; it emits `saved` with the result, or `closed`.
 */
@Component({
  selector: 'app-account-editor',
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
  ],
  templateUrl: './account-editor.html',
  styleUrl: '../accounts.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountEditor {
  private readonly api = inject(AccountApi);
  private readonly messages = inject(MessageService);

  /** null = closed. */
  readonly account = input<Account | 'new' | null>(null);
  /** Every account the user has, for unique names and the 100% cap. */
  readonly accounts = input<readonly Account[]>([]);
  readonly credits = input<readonly Item[]>([]);
  readonly debits = input<readonly Item[]>([]);

  readonly saved = output<Account>();
  readonly closed = output<void>();

  protected readonly kinds = [...KIND_OPTIONS];
  protected readonly maxPayments = MAX_PAYMENTS;
  protected readonly formatPercent = formatPercent;
  protected readonly creditModes: { label: string; value: PaymentMode }[] = [
    { label: '%', value: 'percent' },
    { label: '$', value: 'amount' },
  ];

  /** Resets whenever a different account (or 'new') is opened. */
  protected readonly form = linkedSignal<AccountForm>(() => {
    const account = this.account();
    return account && account !== 'new' ? toAccountForm(account) : emptyAccountForm(new Date());
  });

  protected readonly saving = signal(false);
  protected readonly problem = linkedSignal<string | null>(() => (this.account(), null));

  protected readonly editingId = computed(() => {
    const account = this.account();
    return account && account !== 'new' ? account.id : null;
  });

  protected readonly title = computed(() => {
    const account = this.account();
    return account && account !== 'new' ? `Edit ${account.name}` : 'Add a card or loan';
  });

  private readonly itemsById = computed(() => new Map<number, Item>([...this.credits(), ...this.debits()].map((i) => [i.id, i])));

  protected readonly itemOptions = computed<ItemOption[]>(() => [
    ...this.credits().map((i) => ({ id: i.id, label: i.name, group: 'Credits' })),
    ...this.debits().map((i) => ({ id: i.id, label: i.name, group: 'Debits' })),
  ]);

  private readonly elsewhere = computed(() => takenElsewhere(this.accounts(), this.editingId()));

  protected item(id: number | null): Item | undefined {
    return id === null ? undefined : this.itemsById().get(id);
  }

  protected isDebit(id: number | null): boolean {
    return this.item(id)?.itemType?.id === ITEM_TYPE.debit;
  }

  protected schedule(id: number | null): string {
    const item = this.item(id);
    return item ? describeSchedule(item) : '';
  }

  protected share(payment: PaymentForm): number {
    return paymentShare(payment, this.item(payment.itemId));
  }

  protected taken(id: number | null): number {
    return id === null ? 0 : (this.elsewhere().get(id) ?? 0);
  }

  protected patch(changes: Partial<AccountForm>): void {
    this.form.update((form) => ({ ...form, ...changes }));
    this.problem.set(null);
  }

  /** Choosing a debit switches the row to "whole amount"; choosing a credit switches it back. */
  protected chooseItem(key: number, itemId: number | null): void {
    const debit = this.isDebit(itemId);
    this.patchPayment(key, (p) => ({
      ...p,
      itemId,
      mode: debit ? 'whole' : p.mode === 'whole' ? 'percent' : p.mode,
    }));
  }

  protected patchPayment(key: number, change: Partial<PaymentForm> | ((p: PaymentForm) => PaymentForm)): void {
    this.form.update((form) => ({
      ...form,
      payments: form.payments.map((p) => (p.key !== key ? p : typeof change === 'function' ? change(p) : { ...p, ...change })),
    }));
    this.problem.set(null);
  }

  protected addPayment(): void {
    this.form.update((form) => ({ ...form, payments: [...form.payments, newPayment()] }));
  }

  protected removePayment(key: number): void {
    this.form.update((form) => ({ ...form, payments: form.payments.filter((p) => p.key !== key) }));
    this.problem.set(null);
  }

  protected close(): void {
    if (!this.saving()) {
      this.closed.emit();
    }
  }

  protected save(): void {
    const account = this.account();
    if (!account) {
      return;
    }

    const problem = validateAccountForm(this.form(), this.accounts(), this.editingId(), (id) => this.item(id));
    if (problem) {
      this.problem.set(problem);
      return;
    }

    const request = toAccountRequest(this.form());
    const call = account === 'new' ? this.api.create(request) : this.api.update(account.id, request);

    this.saving.set(true);
    call.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (response) => {
        this.messages.add({ severity: 'success', summary: response.message, life: 3000 });
        this.saved.emit(response.data);
      },
      error: (error: unknown) => this.problem.set(errorMessage(error)),
    });
  }
}
