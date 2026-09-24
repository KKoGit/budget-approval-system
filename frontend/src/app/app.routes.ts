import { Routes } from '@angular/router';
import { approverGuard, authGuard, requesterGuard, unsavedChangesGuard } from './core/guards';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in',
    loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivateChild: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Budget overview',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'requests',
        title: 'Budget requests',
        loadComponent: () => import('./features/requests/request-list.component').then(m => m.RequestListComponent)
      },
      {
        path: 'requests/new',
        title: 'New budget request',
        canActivate: [requesterGuard],
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () => import('./features/requests/request-form.component').then(m => m.RequestFormComponent)
      },
      {
        path: 'requests/:id',
        title: 'Budget request',
        loadComponent: () => import('./features/requests/request-detail.component').then(m => m.RequestDetailComponent)
      },
      {
        path: 'requests/:id/edit',
        title: 'Edit budget request',
        canActivate: [requesterGuard],
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () => import('./features/requests/request-form.component').then(m => m.RequestFormComponent)
      },
      {
        path: 'approvals',
        title: 'Approval queue',
        canActivate: [approverGuard],
        loadComponent: () => import('./features/approvals/approval-queue.component').then(m => m.ApprovalQueueComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
