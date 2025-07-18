import { Routes } from '@angular/router';
import { ReportsComponent } from './reports.component';

export const reportsRoutes: Routes = [
  {
    path: '',
    component: ReportsComponent
  },
  {
    path: 'activities',
    component: ReportsComponent
  },
  {
    path: 'productivity',
    component: ReportsComponent
  },
  {
    path: 'export',
    component: ReportsComponent
  }
];