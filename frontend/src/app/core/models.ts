// Mirrors the API contracts in BudgetApproval.Application/Contracts. Kept hand-written and small on purpose;
// generating these from the OpenAPI document is on the production roadmap.

export type RequestStatus = 'Draft' | 'Submitted' | 'ReturnedForRevision' | 'Approved' | 'Rejected';
export type BudgetCategory =
  | 'Personnel' | 'Equipment' | 'Software' | 'Training' | 'Travel' | 'Facilities' | 'ProfessionalServices';
export type AuditAction = 'Created' | 'Updated' | 'Submitted' | 'Approved' | 'Rejected' | 'ReturnedForRevision';
export type DecisionType = 'Approve' | 'Reject' | 'Return';
export type Role = 'Requester' | 'Approver';

export interface UserDto {
  id: number;
  displayName: string;
  email: string;
  departmentId: number;
  departmentName: string;
  roles: Role[];
}

export interface LookupItem { id: number; code: string; name: string; }

export interface Lookups {
  departments: LookupItem[];
  fiscalYears: number[];
  currentFiscalYear: number;
  categories: BudgetCategory[];
  statuses: RequestStatus[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface BudgetRequestSummary {
  id: number;
  referenceNumber: string;
  title: string;
  departmentId: number;
  departmentName: string;
  fiscalYear: number;
  category: BudgetCategory;
  requestedAmount: number;
  approvedAmount: number | null;
  status: RequestStatus;
  requestedByName: string;
  submittedAtUtc: string | null;
  updatedAtUtc: string;
}

export interface AllocationSnapshot { allocated: number; approved: number; remaining: number; }

export interface AuditEntry {
  id: number;
  action: AuditAction;
  fromStatus: RequestStatus | null;
  toStatus: RequestStatus;
  actorName: string;
  comment: string | null;
  changes: string | null;
  occurredAtUtc: string;
}

export interface BudgetRequestDetail {
  summary: BudgetRequestSummary;
  justification: string;
  requestedById: number;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  version: string;
  permissions: { canEdit: boolean; canSubmit: boolean; canDecide: boolean; decisionBlockedReason: string | null };
  allocation: AllocationSnapshot | null;
  history: AuditEntry[];
}

export interface ApprovalQueueItem {
  request: BudgetRequestSummary;
  allocation: AllocationSnapshot;
  isOwnRequest: boolean;
  exceedsRemaining: boolean;
  daysWaiting: number;
}

export interface DepartmentBudget {
  departmentId: number;
  code: string;
  name: string;
  allocated: number;
  requested: number;
  approved: number;
  pending: number;
  remaining: number;
  pendingCount: number;
}

export interface Dashboard {
  fiscalYear: number | null;
  departmentId: number | null;
  allocated: number;
  requested: number;
  approved: number;
  pending: number;
  remaining: number;
  pendingCount: number;
  countsByStatus: Record<RequestStatus, number>;
  departments: DepartmentBudget[];
}

export interface RequestQuery {
  fiscalYear?: number | null;
  departmentId?: number | null;
  status?: RequestStatus | null;
  category?: BudgetCategory | null;
  search?: string | null;
  sortBy?: SortField;
  sortDirection?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

export type SortField = 'updated' | 'submitted' | 'amount' | 'title' | 'department' | 'status';

export interface SaveRequestPayload {
  fiscalYear: number;
  category: BudgetCategory;
  title: string;
  justification: string;
  requestedAmount: number;
}

export interface DecisionPayload {
  decision: DecisionType;
  approvedAmount?: number | null;
  comment?: string | null;
  version: string;
}

/** Normalised API error produced by the error interceptor. `code` comes from the API's problem details. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly title: string,
    readonly detail: string,
    readonly fieldErrors: Record<string, string[]> = {}
  ) {
    super(detail);
  }

  get isConflict(): boolean { return this.status === 409; }
}
