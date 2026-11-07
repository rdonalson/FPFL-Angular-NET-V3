import { HttpResourceRef, httpResource } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { ApiResponse, apiUrl } from '../../../core/api/api-response';
import { AccountLedger } from '../accounts/account-ledger.models';
import { LedgerDay, LedgerRequest } from '../display.models';
import { DebtPaydown } from '../paydown/paydown.models';

/** The forecast ledger (UI-TSX's displayApi). */
@Injectable({ providedIn: 'root' })
export class DisplayApi {
  /**
   * POST /display/ledger whenever `request()` changes; nothing is sent while it returns undefined.
   * Must be called from an injection context.
   */
  ledger(request: () => LedgerRequest | undefined): HttpResourceRef<ApiResponse<LedgerDay[]> | undefined> {
    return httpResource<ApiResponse<LedgerDay[]>>(() => {
      const body = request();
      return body ? { url: apiUrl('/display/ledger'), method: 'POST', body } : undefined;
    });
  }

  /** POST /display/paydown with the same body as the ledger: each tracked debt over the window. */
  paydown(request: () => LedgerRequest | undefined): HttpResourceRef<ApiResponse<DebtPaydown[]> | undefined> {
    return httpResource<ApiResponse<DebtPaydown[]>>(() => {
      const body = request();
      return body ? { url: apiUrl('/display/paydown'), method: 'POST', body } : undefined;
    });
  }

  /** POST /display/accounts with the same body as the ledger: each credit card or loan over the window. */
  accounts(request: () => LedgerRequest | undefined): HttpResourceRef<ApiResponse<AccountLedger[]> | undefined> {
    return httpResource<ApiResponse<AccountLedger[]>>(() => {
      const body = request();
      return body ? { url: apiUrl('/display/accounts'), method: 'POST', body } : undefined;
    });
  }
}
