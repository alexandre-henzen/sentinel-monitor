import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

// PrimeNG Components
import { TabViewModule } from 'primeng/tabview';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { CheckboxModule } from 'primeng/checkbox';
import { SliderModule } from 'primeng/slider';
import { TagModule } from 'primeng/tag';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TabViewModule,
    CardModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    DropdownModule,
    CheckboxModule,
    SliderModule,
    TagModule,
    TableModule,
    DialogModule,
    ConfirmDialogModule
  ],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="settings-container">
      <!-- Header -->
      <div class="page-header">
        <h1>Configurações</h1>
        <p>Gerencie as configurações do sistema e scoring</p>
      </div>

      <p-tabView>
        <!-- Scoring Configuration -->
        <p-tabPanel header="Configuração de Scoring">
          <div class="settings-section">
            <p-card header="Regras de Produtividade">
              <div class="grid">
                <div class="col-12 md:col-6">
                  <div class="field">
                    <label for="productiveScore" class="font-medium">Score Produtivo (70-100)</label>
                    <p-slider 
                      [(ngModel)]="productiveScore" 
                      [min]="70" 
                      [max]="100"
                      class="w-full">
                    </p-slider>
                    <div class="text-center mt-2">
                      <p-tag [value]="productiveScore" severity="success"></p-tag>
                    </div>
                  </div>
                  
                  <div class="field">
                    <label for="neutralScore" class="font-medium">Score Neutro (40-69)</label>
                    <p-slider 
                      [(ngModel)]="neutralScore" 
                      [min]="40" 
                      [max]="69"
                      class="w-full">
                    </p-slider>
                    <div class="text-center mt-2">
                      <p-tag [value]="neutralScore" severity="warning"></p-tag>
                    </div>
                  </div>
                  
                  <div class="field">
                    <label for="unproductiveScore" class="font-medium">Score Improdutivo (0-39)</label>
                    <p-slider 
                      [(ngModel)]="unproductiveScore" 
                      [min]="0" 
                      [max]="39"
                      class="w-full">
                    </p-slider>
                    <div class="text-center mt-2">
                      <p-tag [value]="unproductiveScore" severity="danger"></p-tag>
                    </div>
                  </div>
                </div>
                
                <div class="col-12 md:col-6">
                  <div class="field">
                    <label class="font-medium">Configurações de Tempo</label>
                    <div class="grid">
                      <div class="col-12">
                        <label for="idleTimeout">Tempo limite para inatividade (minutos)</label>
                        <p-inputNumber 
                          [(ngModel)]="idleTimeout" 
                          [min]="1" 
                          [max]="60"
                          suffix=" min"
                          class="w-full">
                        </p-inputNumber>
                      </div>
                      <div class="col-12">
                        <label for="screenshotInterval">Intervalo de screenshot (segundos)</label>
                        <p-inputNumber 
                          [(ngModel)]="screenshotInterval" 
                          [min]="30" 
                          [max]="600"
                          suffix=" seg"
                          class="w-full">
                        </p-inputNumber>
                      </div>
                    </div>
                  </div>
                  
                  <div class="field">
                    <label class="font-medium">Opções de Monitoramento</label>
                    <div class="field-checkbox">
                      <p-checkbox 
                        [(ngModel)]="enableScreenshots" 
                        inputId="enableScreenshots"
                        label="Habilitar Screenshots">
                      </p-checkbox>
                    </div>
                    <div class="field-checkbox">
                      <p-checkbox 
                        [(ngModel)]="enableKeyboardTracking" 
                        inputId="enableKeyboardTracking"
                        label="Rastrear Atividade do Teclado">
                      </p-checkbox>
                    </div>
                    <div class="field-checkbox">
                      <p-checkbox 
                        [(ngModel)]="enableMouseTracking" 
                        inputId="enableMouseTracking"
                        label="Rastrear Atividade do Mouse">
                      </p-checkbox>
                    </div>
                  </div>
                </div>
              </div>
              
              <div class="flex justify-content-end gap-2 mt-4">
                <p-button 
                  label="Restaurar Padrões"
                  icon="pi pi-refresh"
                  (onClick)="resetToDefaults()"
                  severity="secondary">
                </p-button>
                <p-button 
                  label="Salvar Configurações"
                  icon="pi pi-check"
                  (onClick)="saveSettings()">
                </p-button>
              </div>
            </p-card>
          </div>
        </p-tabPanel>

        <!-- Application Categories -->
        <p-tabPanel header="Categorias de Aplicações">
          <div class="settings-section">
            <p-card header="Gerenciar Categorias">
              <div class="flex justify-content-between align-items-center mb-4">
                <h3>Categorias de Aplicações</h3>
                <p-button 
                  label="Nova Categoria"
                  icon="pi pi-plus"
                  (onClick)="showAddCategory = true">
                </p-button>
              </div>
              
              <p-table 
                [value]="applicationCategories" 
                [paginator]="true" 
                [rows]="10"
                styleClass="p-datatable-sm">
                <ng-template pTemplate="header">
                  <tr>
                    <th>Nome</th>
                    <th>Aplicações</th>
                    <th>Score Padrão</th>
                    <th>Cor</th>
                    <th>Ações</th>
                  </tr>
                </ng-template>
                <ng-template pTemplate="body" let-category>
                  <tr>
                    <td>{{ category.name }}</td>
                    <td>{{ category.applications.length }} aplicações</td>
                    <td>
                      <p-tag 
                        [value]="category.defaultScore" 
                        [severity]="getScoreSeverity(category.defaultScore)">
                      </p-tag>
                    </td>
                    <td>
                      <div 
                        class="color-indicator" 
                        [style.background-color]="category.color">
                      </div>
                    </td>
                    <td>
                      <p-button 
                        icon="pi pi-pencil" 
                        size="small"
                        severity="info"
                        (onClick)="editCategory(category)"
                        class="mr-2">
                      </p-button>
                      <p-button 
                        icon="pi pi-trash" 
                        size="small"
                        severity="danger"
                        (onClick)="deleteCategory(category)">
                      </p-button>
                    </td>
                  </tr>
                </ng-template>
              </p-table>
            </p-card>
          </div>
        </p-tabPanel>

        <!-- System Settings -->
        <p-tabPanel header="Configurações do Sistema">
          <div class="settings-section">
            <div class="grid">
              <div class="col-12 md:col-6">
                <p-card header="Configurações Gerais">
                  <div class="field">
                    <label for="companyName" class="font-medium">Nome da Empresa</label>
                    <input 
                      type="text" 
                      pInputText 
                      [(ngModel)]="companyName"
                      placeholder="Digite o nome da empresa"
                      class="w-full">
                  </div>
                  
                  <div class="field">
                    <label for="timezone" class="font-medium">Fuso Horário</label>
                    <p-dropdown 
                      [(ngModel)]="timezone" 
                      [options]="timezones" 
                      optionLabel="label" 
                      optionValue="value"
                      placeholder="Selecione o fuso horário"
                      class="w-full">
                    </p-dropdown>
                  </div>
                  
                  <div class="field">
                    <label for="language" class="font-medium">Idioma</label>
                    <p-dropdown 
                      [(ngModel)]="language" 
                      [options]="languages" 
                      optionLabel="label" 
                      optionValue="value"
                      placeholder="Selecione o idioma"
                      class="w-full">
                    </p-dropdown>
                  </div>
                </p-card>
              </div>
              
              <div class="col-12 md:col-6">
                <p-card header="Configurações de Segurança">
                  <div class="field">
                    <label for="sessionTimeout" class="font-medium">Timeout de Sessão (minutos)</label>
                    <p-inputNumber 
                      [(ngModel)]="sessionTimeout" 
                      [min]="5" 
                      [max]="480"
                      suffix=" min"
                      class="w-full">
                    </p-inputNumber>
                  </div>
                  
                  <div class="field">
                    <label class="font-medium">Opções de Segurança</label>
                    <div class="field-checkbox">
                      <p-checkbox 
                        [(ngModel)]="enableTwoFactor" 
                        inputId="enableTwoFactor"
                        label="Habilitar Autenticação de Dois Fatores">
                      </p-checkbox>
                    </div>
                    <div class="field-checkbox">
                      <p-checkbox 
                        [(ngModel)]="enablePasswordExpiry" 
                        inputId="enablePasswordExpiry"
                        label="Expiração de Senha">
                      </p-checkbox>
                    </div>
                    <div class="field-checkbox">
                      <p-checkbox 
                        [(ngModel)]="enableAuditLog" 
                        inputId="enableAuditLog"
                        label="Log de Auditoria">
                      </p-checkbox>
                    </div>
                  </div>
                </p-card>
              </div>
            </div>
            
            <div class="flex justify-content-end gap-2 mt-4">
              <p-button 
                label="Exportar Configurações"
                icon="pi pi-download"
                (onClick)="exportSettings()"
                severity="secondary">
              </p-button>
              <p-button 
                label="Salvar Configurações"
                icon="pi pi-check"
                (onClick)="saveSystemSettings()">
              </p-button>
            </div>
          </div>
        </p-tabPanel>
      </p-tabView>
    </div>

    <!-- Add Category Dialog -->
    <p-dialog 
      [(visible)]="showAddCategory" 
      [modal]="true" 
      [closable]="true"
      [style]="{'width': '500px'}"
      header="Nova Categoria">
      
      <div class="category-form">
        <div class="field">
          <label for="categoryName" class="font-medium">Nome da Categoria</label>
          <input 
            type="text" 
            pInputText 
            [(ngModel)]="newCategory.name"
            placeholder="Digite o nome da categoria"
            class="w-full">
        </div>
        
        <div class="field">
          <label for="categoryScore" class="font-medium">Score Padrão</label>
          <p-inputNumber 
            [(ngModel)]="newCategory.defaultScore" 
            [min]="0" 
            [max]="100"
            class="w-full">
          </p-inputNumber>
        </div>
        
        <div class="field">
          <label for="categoryColor" class="font-medium">Cor</label>
          <input 
            type="color" 
            [(ngModel)]="newCategory.color"
            class="w-full">
        </div>
        
        <div class="field">
          <label for="categoryApps" class="font-medium">Aplicações (separadas por vírgula)</label>
          <textarea 
            pInputTextarea 
            [(ngModel)]="newCategoryApps"
            placeholder="Chrome, Firefox, VSCode..."
            rows="3"
            class="w-full">
          </textarea>
        </div>
      </div>
      
      <ng-template pTemplate="footer">
        <p-button 
          label="Cancelar" 
          icon="pi pi-times" 
          (onClick)="cancelAddCategory()"
          severity="secondary">
        </p-button>
        <p-button 
          label="Salvar" 
          icon="pi pi-check" 
          (onClick)="saveCategory()"
          [disabled]="!newCategory.name">
        </p-button>
      </ng-template>
    </p-dialog>

    <!-- Confirmation Dialog -->
    <p-confirmDialog></p-confirmDialog>
  `,
  styles: [`
    .settings-container {
      padding: 1.5rem;
    }

    .settings-section {
      margin-bottom: 2rem;
    }

    .field {
      margin-bottom: 1rem;
    }

    .field label {
      display: block;
      margin-bottom: 0.5rem;
      font-weight: 500;
    }

    .field-checkbox {
      margin-bottom: 0.5rem;
    }

    .color-indicator {
      width: 24px;
      height: 24px;
      border-radius: 50%;
      border: 1px solid #ccc;
    }

    .category-form {
      padding: 1rem 0;
    }

    @media (max-width: 768px) {
      .settings-container {
        padding: 1rem;
      }
    }
  `]
})
export class SettingsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  
  // Scoring settings
  productiveScore = 80;
  neutralScore = 60;
  unproductiveScore = 30;
  idleTimeout = 10;
  screenshotInterval = 300;
  enableScreenshots = true;
  enableKeyboardTracking = true;
  enableMouseTracking = true;
  
  // System settings
  companyName = 'Minha Empresa';
  timezone = 'America/Sao_Paulo';
  language = 'pt-BR';
  sessionTimeout = 60;
  enableTwoFactor = false;
  enablePasswordExpiry = false;
  enableAuditLog = true;
  
  // Application categories
  applicationCategories = [
    {
      id: 1,
      name: 'Desenvolvimento',
      applications: ['VSCode', 'Visual Studio', 'IntelliJ', 'Eclipse'],
      defaultScore: 90,
      color: '#007bff'
    },
    {
      id: 2,
      name: 'Comunicação',
      applications: ['Teams', 'Slack', 'Discord', 'Skype'],
      defaultScore: 70,
      color: '#28a745'
    },
    {
      id: 3,
      name: 'Navegação',
      applications: ['Chrome', 'Firefox', 'Edge', 'Safari'],
      defaultScore: 50,
      color: '#ffc107'
    },
    {
      id: 4,
      name: 'Entretenimento',
      applications: ['YouTube', 'Netflix', 'Spotify', 'Games'],
      defaultScore: 20,
      color: '#dc3545'
    }
  ];
  
  // Dialog state
  showAddCategory = false;
  newCategory = { name: '', defaultScore: 50, color: '#007bff' };
  newCategoryApps = '';
  
  // Options
  timezones = [
    { label: 'São Paulo (GMT-3)', value: 'America/Sao_Paulo' },
    { label: 'New York (GMT-5)', value: 'America/New_York' },
    { label: 'London (GMT+0)', value: 'Europe/London' },
    { label: 'Tokyo (GMT+9)', value: 'Asia/Tokyo' }
  ];
  
  languages = [
    { label: 'Português (Brasil)', value: 'pt-BR' },
    { label: 'English (US)', value: 'en-US' },
    { label: 'Español', value: 'es-ES' }
  ];

  constructor(
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadSettings();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadSettings() {
    // Load settings from localStorage or API
    const savedSettings = localStorage.getItem('eam-settings');
    if (savedSettings) {
      const settings = JSON.parse(savedSettings);
      Object.assign(this, settings);
    }
  }

  saveSettings() {
    const settings = {
      productiveScore: this.productiveScore,
      neutralScore: this.neutralScore,
      unproductiveScore: this.unproductiveScore,
      idleTimeout: this.idleTimeout,
      screenshotInterval: this.screenshotInterval,
      enableScreenshots: this.enableScreenshots,
      enableKeyboardTracking: this.enableKeyboardTracking,
      enableMouseTracking: this.enableMouseTracking
    };

    localStorage.setItem('eam-settings', JSON.stringify(settings));
    
    this.messageService.add({
      severity: 'success',
      summary: 'Sucesso',
      detail: 'Configurações de scoring salvas com sucesso'
    });
  }

  saveSystemSettings() {
    const settings = {
      companyName: this.companyName,
      timezone: this.timezone,
      language: this.language,
      sessionTimeout: this.sessionTimeout,
      enableTwoFactor: this.enableTwoFactor,
      enablePasswordExpiry: this.enablePasswordExpiry,
      enableAuditLog: this.enableAuditLog
    };

    localStorage.setItem('eam-system-settings', JSON.stringify(settings));
    
    this.messageService.add({
      severity: 'success',
      summary: 'Sucesso',
      detail: 'Configurações do sistema salvas com sucesso'
    });
  }

  resetToDefaults() {
    this.confirmationService.confirm({
      message: 'Tem certeza que deseja restaurar as configurações padrão?',
      header: 'Confirmar Restauração',
      icon: 'pi pi-refresh',
      accept: () => {
        this.productiveScore = 80;
        this.neutralScore = 60;
        this.unproductiveScore = 30;
        this.idleTimeout = 10;
        this.screenshotInterval = 300;
        this.enableScreenshots = true;
        this.enableKeyboardTracking = true;
        this.enableMouseTracking = true;
        
        this.messageService.add({
          severity: 'success',
          summary: 'Sucesso',
          detail: 'Configurações restauradas para os valores padrão'
        });
      }
    });
  }

  editCategory(category: any) {
    this.messageService.add({
      severity: 'info',
      summary: 'Edição',
      detail: 'Funcionalidade de edição será implementada em breve'
    });
  }

  deleteCategory(category: any) {
    this.confirmationService.confirm({
      message: `Tem certeza que deseja excluir a categoria "${category.name}"?`,
      header: 'Confirmar Exclusão',
      icon: 'pi pi-trash',
      accept: () => {
        const index = this.applicationCategories.findIndex(c => c.id === category.id);
        if (index > -1) {
          this.applicationCategories.splice(index, 1);
          this.messageService.add({
            severity: 'success',
            summary: 'Sucesso',
            detail: 'Categoria excluída com sucesso'
          });
        }
      }
    });
  }

  saveCategory() {
    if (!this.newCategory.name) return;

    const applications = this.newCategoryApps
      .split(',')
      .map(app => app.trim())
      .filter(app => app);

    const category = {
      id: Math.max(...this.applicationCategories.map(c => c.id)) + 1,
      name: this.newCategory.name,
      applications,
      defaultScore: this.newCategory.defaultScore,
      color: this.newCategory.color
    };

    this.applicationCategories.push(category);
    this.cancelAddCategory();
    
    this.messageService.add({
      severity: 'success',
      summary: 'Sucesso',
      detail: 'Categoria criada com sucesso'
    });
  }

  cancelAddCategory() {
    this.showAddCategory = false;
    this.newCategory = { name: '', defaultScore: 50, color: '#007bff' };
    this.newCategoryApps = '';
  }

  exportSettings() {
    const settings = {
      scoring: {
        productiveScore: this.productiveScore,
        neutralScore: this.neutralScore,
        unproductiveScore: this.unproductiveScore,
        idleTimeout: this.idleTimeout,
        screenshotInterval: this.screenshotInterval,
        enableScreenshots: this.enableScreenshots,
        enableKeyboardTracking: this.enableKeyboardTracking,
        enableMouseTracking: this.enableMouseTracking
      },
      system: {
        companyName: this.companyName,
        timezone: this.timezone,
        language: this.language,
        sessionTimeout: this.sessionTimeout,
        enableTwoFactor: this.enableTwoFactor,
        enablePasswordExpiry: this.enablePasswordExpiry,
        enableAuditLog: this.enableAuditLog
      },
      categories: this.applicationCategories
    };

    const blob = new Blob([JSON.stringify(settings, null, 2)], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'eam-settings.json';
    link.click();
    window.URL.revokeObjectURL(url);
    
    this.messageService.add({
      severity: 'success',
      summary: 'Sucesso',
      detail: 'Configurações exportadas com sucesso'
    });
  }

  getScoreSeverity(score: number): string {
    if (score >= 70) return 'success';
    if (score >= 40) return 'warning';
    return 'danger';
  }
}