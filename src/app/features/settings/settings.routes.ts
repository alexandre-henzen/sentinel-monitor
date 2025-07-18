import { Routes } from '@angular/router';
import { SettingsComponent } from './settings.component';

export const settingsRoutes: Routes = [
  {
    path: '',
    component: SettingsComponent
  },
  {
    path: 'scoring',
    component: SettingsComponent
  },
  {
    path: 'categories',
    component: SettingsComponent
  },
  {
    path: 'system',
    component: SettingsComponent
  }
];