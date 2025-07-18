import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';

// PrimeNG Components
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { ProgressBarModule } from 'primeng/progressbar';

// Services
import { AuthService } from '../../core/services/auth.service';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    CardModule,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    CheckboxModule,
    MessageModule,
    ProgressBarModule
  ],
  providers: [MessageService],
  template: `
    <div class="login-container">
      <div class="login-card">
        <p-card>
          <ng-template pTemplate="header">
            <div class="login-header">
              <img src="assets/logo.png" alt="EAM Logo" class="logo">
              <h1>Employee Activity Monitor</h1>
              <p class="subtitle">Entre com suas credenciais</p>
            </div>
          </ng-template>
          
          <div class="login-content">
            <!-- Loading State -->
            <div *ngIf="isLoading" class="loading-state">
              <p-progressBar mode="indeterminate" [style]="{'height': '6px'}"></p-progressBar>
              <p class="loading-text">Autenticando...</p>
            </div>

            <!-- Login Form -->
            <form *ngIf="!isLoading" (ngSubmit)="login()" #loginForm="ngForm">
              <div class="form-group">
                <label for="username">Usuário</label>
                <input 
                  type="text" 
                  id="username"
                  pInputText 
                  [(ngModel)]="username" 
                  name="username"
                  required
                  placeholder="Digite seu usuário"
                  class="w-full"
                  [class.ng-invalid]="submitted && !username"
                  autocomplete="username">
                <small *ngIf="submitted && !username" class="p-error">
                  Usuário é obrigatório
                </small>
              </div>

              <div class="form-group">
                <label for="password">Senha</label>
                <p-password 
                  [(ngModel)]="password" 
                  name="password"
                  required
                  placeholder="Digite sua senha"
                  styleClass="w-full"
                  [class.ng-invalid]="submitted && !password"
                  [feedback]="false"
                  [toggleMask]="true"
                  autocomplete="current-password">
                </p-password>
                <small *ngIf="submitted && !password" class="p-error">
                  Senha é obrigatória
                </small>
              </div>

              <div class="form-group">
                <p-checkbox 
                  [(ngModel)]="rememberMe" 
                  name="rememberMe"
                  inputId="rememberMe"
                  label="Lembrar de mim">
                </p-checkbox>
              </div>

              <div class="form-group">
                <p-button 
                  type="submit"
                  label="Entrar" 
                  icon="pi pi-sign-in"
                  styleClass="w-full"
                  [loading]="isLoading"
                  [disabled]="!loginForm.valid">
                </p-button>
              </div>

              <div class="form-group">
                <p-button 
                  type="button"
                  label="Entrar com OIDC" 
                  icon="pi pi-external-link"
                  styleClass="w-full"
                  severity="secondary"
                  (onClick)="loginWithOIDC()">
                </p-button>
              </div>
            </form>

            <!-- Error Messages -->
            <div *ngIf="errorMessage" class="error-container">
              <p-message 
                severity="error" 
                [text]="errorMessage"
                [closable]="true"
                (onClose)="clearError()">
              </p-message>
            </div>
          </div>
          
          <ng-template pTemplate="footer">
            <div class="login-footer">
              <p class="version">EAM v5.0.0</p>
              <div class="links">
                <a href="#" class="link">Esqueceu a senha?</a>
                <span class="separator">|</span>
                <a href="#" class="link">Suporte</a>
              </div>
            </div>
          </ng-template>
        </p-card>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      padding: 2rem;
    }

    .login-card {
      width: 100%;
      max-width: 400px;
    }

    .login-header {
      text-align: center;
      padding: 2rem 1rem 1rem;
    }

    .logo {
      height: 60px;
      margin-bottom: 1rem;
    }

    .login-header h1 {
      font-size: 1.8rem;
      font-weight: 600;
      color: #333;
      margin-bottom: 0.5rem;
    }

    .subtitle {
      color: #666;
      font-size: 0.9rem;
      margin: 0;
    }

    .login-content {
      padding: 0 1rem 1rem;
    }

    .form-group {
      margin-bottom: 1.5rem;
    }

    .form-group label {
      display: block;
      margin-bottom: 0.5rem;
      font-weight: 500;
      color: #333;
    }

    .loading-state {
      text-align: center;
      padding: 2rem 0;
    }

    .loading-text {
      margin-top: 1rem;
      color: #666;
    }

    .error-container {
      margin-top: 1rem;
    }

    .login-footer {
      text-align: center;
      padding: 1rem;
      border-top: 1px solid #eee;
      color: #666;
      font-size: 0.85rem;
    }

    .version {
      margin-bottom: 0.5rem;
    }

    .links {
      display: flex;
      justify-content: center;
      gap: 0.5rem;
      align-items: center;
    }

    .link {
      color: #007bff;
      text-decoration: none;
      transition: color 0.2s;
    }

    .link:hover {
      color: #0056b3;
      text-decoration: underline;
    }

    .separator {
      color: #ccc;
    }

    .p-error {
      color: #e74c3c;
      font-size: 0.8rem;
    }

    .ng-invalid.ng-touched {
      border-color: #e74c3c;
    }

    @media (max-width: 768px) {
      .login-container {
        padding: 1rem;
      }
      
      .login-card {
        max-width: 100%;
      }
    }
  `]
})
export class LoginComponent implements OnInit {
  username = '';
  password = '';
  rememberMe = false;
  isLoading = false;
  submitted = false;
  errorMessage = '';

  constructor(
    private authService: AuthService,
    private router: Router,
    private messageService: MessageService
  ) {}

  ngOnInit() {
    // Check if user is already authenticated
    if (this.authService.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    }
  }

  async login() {
    this.submitted = true;
    this.clearError();

    if (!this.username || !this.password) {
      return;
    }

    this.isLoading = true;

    try {
      // For demo purposes, we'll use a simple mock authentication
      // In a real app, this would validate credentials against the backend
      if (this.username === 'admin' && this.password === 'admin') {
        this.messageService.add({
          severity: 'success',
          summary: 'Sucesso',
          detail: 'Login realizado com sucesso'
        });
        
        // Simulate successful authentication
        await new Promise(resolve => setTimeout(resolve, 1000));
        
        this.router.navigate(['/dashboard']);
      } else {
        this.errorMessage = 'Credenciais inválidas. Tente admin/admin para demonstração.';
      }
    } catch (error) {
      console.error('Login error:', error);
      this.errorMessage = 'Erro interno do servidor. Tente novamente mais tarde.';
    } finally {
      this.isLoading = false;
    }
  }

  loginWithOIDC() {
    this.isLoading = true;
    this.clearError();

    try {
      this.authService.login();
    } catch (error) {
      console.error('OIDC login error:', error);
      this.errorMessage = 'Erro ao iniciar autenticação OIDC. Verifique a configuração.';
      this.isLoading = false;
    }
  }

  clearError() {
    this.errorMessage = '';
  }
}