import { AuditAction, BudgetCategory, RequestStatus } from './models';

export const STATUS_LABELS: Record<RequestStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Awaiting decision',
  ReturnedForRevision: 'Returned for revision',
  Approved: 'Approved',
  Rejected: 'Rejected'
};

export const CATEGORY_LABELS: Record<BudgetCategory, string> = {
  Personnel: 'Personnel',
  Equipment: 'Equipment',
  Software: 'Software',
  Training: 'Training',
  Travel: 'Travel',
  Facilities: 'Facilities',
  ProfessionalServices: 'Professional services'
};

export const ACTION_LABELS: Record<AuditAction, string> = {
  Created: 'Created draft',
  Updated: 'Edited',
  Submitted: 'Submitted for approval',
  Approved: 'Approved',
  Rejected: 'Rejected',
  ReturnedForRevision: 'Returned for revision'
};

export const fiscalYearLabel = (fy: number | null | undefined) => (fy ? `FY${fy}` : 'All years');
