import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import {
  ApprovalQueueItem, BudgetRequestDetail, BudgetRequestSummary, Dashboard, DecisionPayload, Lookups,
  PagedResult, RequestQuery, SaveRequestPayload, UserDto
} from './models';

/** Typed client for the Budget Approval API. One method per endpoint; no business logic lives here. */
@Injectable({ providedIn: 'root' })
export class BudgetApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';
  private lookups$?: Observable<Lookups>;

  demoUsers(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(`${this.base}/demo/users`);
  }

  /** Reference data rarely changes, so it is fetched once per session and shared. */
  lookups(): Observable<Lookups> {
    return (this.lookups$ ??= this.http.get<Lookups>(`${this.base}/lookups`).pipe(shareReplay({ bufferSize: 1, refCount: false })));
  }

  clearCache(): void {
    this.lookups$ = undefined;
  }

  dashboard(fiscalYear: number | null, departmentId: number | null): Observable<Dashboard> {
    return this.http.get<Dashboard>(`${this.base}/dashboard`, { params: toParams({ fiscalYear, departmentId }) });
  }

  searchRequests(query: RequestQuery): Observable<PagedResult<BudgetRequestSummary>> {
    return this.http.get<PagedResult<BudgetRequestSummary>>(`${this.base}/budget-requests`, { params: toParams(query) });
  }

  getRequest(id: number): Observable<BudgetRequestDetail> {
    return this.http.get<BudgetRequestDetail>(`${this.base}/budget-requests/${id}`);
  }

  createRequest(payload: SaveRequestPayload): Observable<BudgetRequestDetail> {
    return this.http.post<BudgetRequestDetail>(`${this.base}/budget-requests`, payload);
  }

  updateRequest(id: number, payload: Omit<SaveRequestPayload, 'fiscalYear'>, version: string): Observable<BudgetRequestDetail> {
    return this.http.put<BudgetRequestDetail>(`${this.base}/budget-requests/${id}`, { ...payload, version });
  }

  submitRequest(id: number, version: string): Observable<BudgetRequestDetail> {
    return this.http.post<BudgetRequestDetail>(`${this.base}/budget-requests/${id}/submit`, { version });
  }

  approvalQueue(fiscalYear: number | null, departmentId: number | null): Observable<ApprovalQueueItem[]> {
    return this.http.get<ApprovalQueueItem[]>(`${this.base}/approvals/queue`, { params: toParams({ fiscalYear, departmentId }) });
  }

  decide(id: number, payload: DecisionPayload): Observable<BudgetRequestDetail> {
    return this.http.post<BudgetRequestDetail>(`${this.base}/budget-requests/${id}/decision`, payload);
  }
}

/** Drops null/undefined/empty values so the API sees "no filter" rather than "filter by empty". */
function toParams(values: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value));
  }
  return params;
}
