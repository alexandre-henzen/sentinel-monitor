import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { PrimeNGConfig } from 'primeng/api';

// PrimeNG Components
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { ProgressBarModule } from 'primeng/progressbar';
import { MenuItem } from 'primeng/api';
import { MessageService } from 'primeng/api';

// Core Services
import { AuthService } from './core/services/auth.service';
import { LoadingService } from './core/services/loading.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    MenubarModule,
    ToastModule,
    ProgressBarModule
  ],
  providers: [MessageService],
  template: `
    <div class="main-container">
      <p-menubar 
        [model]="menuItems" 
        *ngIf="isAuthenticated">
        <ng-template pTemplate="start">
          <img src="assets/logo.png" height="40" class="mr-2" alt="EAM Logo">
        </ng-template>
        <ng-template pTemplate="end">
          <div class="flex align-items-center gap-2">
            <span class="text-sm">{{ currentUser?.name }}</span>
            <i class="pi pi-user"></i>
          </div>
        </ng-template>
      </p-menubar>
      
      <p-progressBar 
        mode="indeterminate" 
        [style]="{'height': '4px'}"
        *ngIf="isLoading$ | async">
      </p-progressBar>
      
      <div class="content-wrapper">
        <router-outlet></router-outlet>
      </div>
    </div>
    
    <p-toast></p-toast>
  `,
  styles: [`
    .main-container {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
    }
    
    .content-wrapper {
      flex: 1;
      padding: 1.5rem;
    }
    
    .p-menubar {
      border-radius: 0;
    }
    
    .p-progressbar {
      height: 4px;
    }
    
    @media (max-width: 768px) {
      .content-wrapper {
        padding: 1rem;
      }
    }
  `]
})
export class AppComponent implements OnInit {
  title = 'EAM - Employee Activity Monitor';
  
  menuItems: MenuItem[] = [];
  isAuthenticated = false;
  currentUser: any = null;
  isLoading$ = this.loadingService.loading$;

  constructor(
    private primengConfig: PrimeNGConfig,
    private authService: AuthService,
    private loadingService: LoadingService,
    private messageService: MessageService
  ) {}

  ngOnInit() {
    this.primengConfig.ripple = true;
    this.initializeAuth();
    this.setupMenu();
  }

  private async initializeAuth() {
    try {
      await this.authService.initializeAuth();
      this.isAuthenticated = this.authService.isAuthenticated();
      this.currentUser = this.authService.getCurrentUser();
    } catch (error) {
      console.error('Auth initialization failed:', error);
      this.messageService.add({
        severity: 'error',
        summary: 'Erro de Autenticação',
        detail: 'Falha ao inicializar a autenticação'
      });
    }
  }

  private setupMenu() {
    this.menuItems = [
      {
        label: 'Dashboard',
        icon: 'pi pi-fw pi-home',
        routerLink: ['/dashboard']
      },
      {
        label: 'Timeline',
        icon: 'pi pi-fw pi-clock',
        routerLink: ['/timeline']
      },
      {
        label: 'Agentes',
        icon: 'pi pi-fw pi-desktop',
        routerLink: ['/agents']
      },
      {
        label: 'Relatórios',
        icon: 'pi pi-fw pi-chart-line',
        items: [
          {
            label: 'Atividades',
            icon: 'pi pi-fw pi-list',
            routerLink: ['/reports/activities']
          },
          {
            label: 'Produtividade',
            icon: 'pi pi-fw pi-chart-bar',
            routerLink: ['/reports/productivity']
          },
          {
            label: 'Exportar',
            icon: 'pi pi-fw pi-download',
            routerLink: ['/reports/export']
          }
        ]
      },
      {
        label: 'Configurações',
        icon: 'pi pi-fw pi-cog',
        items: [
          {
            label: 'Scoring',
            icon: 'pi pi-fw pi-star',
            routerLink: ['/settings/scoring']
          },
          {
            label: 'Categorias',
            icon: 'pi pi-fw pi-tags',
            routerLink: ['/settings/categories']
          },
          {
            label: 'Sistema',
            icon: 'pi pi-fw pi-wrench',
            routerLink: ['/settings/system']
          }
        ]
      },
      {
        label: 'Sair',
        icon: 'pi pi-fw pi-sign-out',
        command: () => this.logout()
      }
    ];
  }

  private logout() {
    this.authService.logout();
  }
}