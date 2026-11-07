import { HttpClient, HttpResourceRef, httpResource } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiResponse, apiUrl } from '../../../core/api/api-response';
import { Account, AccountRequest, Purchase, PurchaseRequest } from '../accounts.models';

/** Credit cards and loans with their purchases. The API takes the owner from the access token. */
@Injectable({ providedIn: 'root' })
export class AccountApi {
  private readonly http = inject(HttpClient);

  /** The signed-in user's cards and loans. Must be called from an injection context. */
  list(): HttpResourceRef<ApiResponse<Account[]> | undefined> {
    return httpResource<ApiResponse<Account[]>>(() => apiUrl('/accounts'));
  }

  /** One account, or no request while `id` is undefined. Must be called from an injection context. */
  get(id: () => number | undefined): HttpResourceRef<ApiResponse<Account> | undefined> {
    return httpResource<ApiResponse<Account>>(() => {
      const value = id();
      return value ? apiUrl(`/accounts/${value}`) : undefined;
    });
  }

  create(request: AccountRequest): Observable<ApiResponse<Account>> {
    return this.http.post<ApiResponse<Account>>(apiUrl('/accounts'), request);
  }

  update(id: number, request: AccountRequest): Observable<ApiResponse<Account>> {
    return this.http.put<ApiResponse<Account>>(apiUrl(`/accounts/${id}`), request);
  }

  remove(id: number): Observable<ApiResponse<null>> {
    return this.http.delete<ApiResponse<null>>(apiUrl(`/accounts/${id}`));
  }

  addPurchase(accountId: number, request: PurchaseRequest): Observable<ApiResponse<Purchase>> {
    return this.http.post<ApiResponse<Purchase>>(apiUrl(`/accounts/${accountId}/purchases`), request);
  }

  updatePurchase(accountId: number, purchaseId: number, request: PurchaseRequest): Observable<ApiResponse<Purchase>> {
    return this.http.put<ApiResponse<Purchase>>(apiUrl(`/accounts/${accountId}/purchases/${purchaseId}`), request);
  }

  removePurchase(accountId: number, purchaseId: number): Observable<ApiResponse<null>> {
    return this.http.delete<ApiResponse<null>>(apiUrl(`/accounts/${accountId}/purchases/${purchaseId}`));
  }
}
