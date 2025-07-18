import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, from } from 'rxjs';
import { OAuthService, AuthConfig } from 'angular-oauth2-oidc';
import { environment } from '../../../environments/environment';

export interface UserInfo {
  sub: string;
  name: string;
  email: string;
  roles: string[];
  permissions: string[];
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private _isAuthenticated = new BehaviorSubject<boolean>(false);
  private _currentUser = new BehaviorSubject<UserInfo | null>(null);
  private _loading = new BehaviorSubject<boolean>(false);

  public isAuthenticated$ = this._isAuthenticated.asObservable();
  public currentUser$ = this._currentUser.asObservable();
  public loading$ = this._loading.asObservable();

  private authConfig: AuthConfig = {
    issuer: environment.auth.issuer,
    redirectUri: environment.auth.redirectUri,
    clientId: environment.auth.clientId,
    responseType: environment.auth.responseType,
    scope: environment.auth.scope,
    showDebugInformation: !environment.production,
    postLogoutRedirectUri: environment.auth.postLogoutRedirectUri,
    silentRefreshRedirectUri: environment.auth.silentRefreshRedirectUri,
    silentRefreshTimeout: 5000,
    timeoutFactor: 0.25,
    sessionChecksEnabled: true,
    clearHashAfterLogin: false,
    nonceStateSeparator: 'semicolon',
    strictDiscoveryDocumentValidation: false
  };

  constructor(
    private oauthService: OAuthService,
    private router: Router
  ) {
    this.configureAuth();
  }

  private configureAuth(): void {
    this.oauthService.configure(this.authConfig);
    this.oauthService.tokenValidationHandler = new NullValidationHandler();
    this.oauthService.setupAutomaticSilentRefresh();
    
    // Token events
    this.oauthService.events.subscribe(event => {
      if (event.type === 'token_received') {
        this.updateAuthState();
      } else if (event.type === 'token_expires') {
        this.handleTokenExpiry();
      }
    });
  }

  async initializeAuth(): Promise<void> {
    this._loading.next(true);
    
    try {
      await this.oauthService.loadDiscoveryDocumentAndTryLogin();
      this.updateAuthState();
      
      if (this.oauthService.hasValidAccessToken()) {
        await this.loadUserProfile();
      }
    } catch (error) {
      console.error('Auth initialization failed:', error);
      this._isAuthenticated.next(false);
      this._currentUser.next(null);
    } finally {
      this._loading.next(false);
    }
  }

  login(): void {
    this.oauthService.initCodeFlow();
  }

  logout(): void {
    this.oauthService.logOut();
    this._isAuthenticated.next(false);
    this._currentUser.next(null);
    this.router.navigate(['/auth/login']);
  }

  isAuthenticated(): boolean {
    return this.oauthService.hasValidAccessToken();
  }

  getAccessToken(): string | null {
    return this.oauthService.getAccessToken();
  }

  getCurrentUser(): UserInfo | null {
    return this._currentUser.value;
  }

  hasRole(role: string): boolean {
    const user = this._currentUser.value;
    return user?.roles?.includes(role) || false;
  }

  hasPermission(permission: string): boolean {
    const user = this._currentUser.value;
    return user?.permissions?.includes(permission) || false;
  }

  async refreshToken(): Promise<void> {
    try {
      await this.oauthService.silentRefresh();
      this.updateAuthState();
    } catch (error) {
      console.error('Token refresh failed:', error);
      this.logout();
    }
  }

  private updateAuthState(): void {
    const isAuthenticated = this.oauthService.hasValidAccessToken();
    this._isAuthenticated.next(isAuthenticated);
    
    if (!isAuthenticated) {
      this._currentUser.next(null);
    }
  }

  private async loadUserProfile(): Promise<void> {
    try {
      const userInfo = await this.oauthService.loadUserProfile();
      const claims = this.oauthService.getIdentityClaims();
      
      if (userInfo && claims) {
        const user: UserInfo = {
          sub: (claims as any).sub,
          name: (claims as any).name || (claims as any).preferred_username,
          email: (claims as any).email,
          roles: (claims as any).roles || [],
          permissions: (claims as any).permissions || []
        };
        
        this._currentUser.next(user);
      }
    } catch (error) {
      console.error('Failed to load user profile:', error);
    }
  }

  private handleTokenExpiry(): void {
    if (this.oauthService.hasValidAccessToken()) {
      this.refreshToken();
    } else {
      this.logout();
    }
  }
}

// Null validation handler for development
class NullValidationHandler {
  validateSignature(): Promise<any> {
    return Promise.resolve(null);
  }
  
  validateAtHash(): Promise<any> {
    return Promise.resolve(null);
  }
}