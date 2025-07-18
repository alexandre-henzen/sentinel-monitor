import { Injectable } from '@angular/core';
import { CanActivate, Router, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { MessageService } from 'primeng/api';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router,
    private messageService: MessageService
  ) {}

  async canActivate(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): Promise<boolean> {
    const isAuthenticated = this.authService.isAuthenticated();
    
    if (!isAuthenticated) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Acesso Negado',
        detail: 'Você precisa estar autenticado para acessar esta página'
      });
      
      this.router.navigate(['/auth/login'], {
        queryParams: { returnUrl: state.url }
      });
      
      return false;
    }

    // Check for required roles if specified
    const requiredRoles = route.data?.['roles'] as string[];
    if (requiredRoles && requiredRoles.length > 0) {
      const hasRequiredRole = requiredRoles.some(role => 
        this.authService.hasRole(role)
      );
      
      if (!hasRequiredRole) {
        this.messageService.add({
          severity: 'error',
          summary: 'Acesso Negado',
          detail: 'Você não tem permissão para acessar esta página'
        });
        
        this.router.navigate(['/dashboard']);
        return false;
      }
    }

    // Check for required permissions if specified
    const requiredPermissions = route.data?.['permissions'] as string[];
    if (requiredPermissions && requiredPermissions.length > 0) {
      const hasRequiredPermission = requiredPermissions.some(permission => 
        this.authService.hasPermission(permission)
      );
      
      if (!hasRequiredPermission) {
        this.messageService.add({
          severity: 'error',
          summary: 'Acesso Negado',
          detail: 'Você não tem permissão para acessar esta página'
        });
        
        this.router.navigate(['/dashboard']);
        return false;
      }
    }

    return true;
  }
}