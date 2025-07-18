import { Routes } from '@angular/router';
import { AuthGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/dashboard',
    pathMatch: 'full'
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.authRoutes)
  },
  {
    path: 'dashboard',
    canActivate: [AuthGuard],
    loadChildren: () => import('./features/dashboard/dashboard.routes').then(m => m.dashboardRoutes)
  },
  {
    path: 'timeline',
    canActivate: [AuthGuard],
    loadChildren: () => import('./features/timeline/timeline.routes').then(m => m.timelineRoutes)
  },
  {
    path: 'agents',
    canActivate: [AuthGuard],
    loadChildren: () => import('./features/agents/agents.routes').then(m => m.agentsRoutes)
  },
  {
    path: 'reports',
    canActivate: [AuthGuard],
    loadChildren: () => import('./features/reports/reports.routes').then(m => m.reportsRoutes)
  },
  {
    path: 'settings',
    canActivate: [AuthGuard],
    loadChildren: () => import('./features/settings/settings.routes').then(m => m.settingsRoutes)
  },
  {
    path: '**',
    redirectTo: '/dashboard'
  }
];