import { Routes } from '@angular/router';
import { LoginComponent } from './login.component';

export const authRoutes: Routes = [
  {
    path: '',
    redirectTo: '/auth/login',
    pathMatch: 'full'
  },
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: 'callback',
    component: LoginComponent // OAuth callback handling
  }
];