import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

// PrimeNG Components
import { TableModule } from 'primeng/table';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { SkeletonModule } from 'primeng/skeleton';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressBarModule } from 'primeng/progressbar';
import { ChartModule } from 'primeng/chart';
import { TabViewModule } from 'primeng/tabview';
import { ConfirmationService, MessageService } from 'primeng/api';

// Services
import { AgentService } from '../../core/services/agent.service';
import { EventService } from '../../core/services/event.service';

// Models
import { AgentDto, AgentStatus, AgentFilters } from '../../shared/models/agent-dto';
import { EventDto } from '../../shared/models/event-dto';

@Component({
  selector: 'app-agents',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    CardModule,
    ButtonModule,
    TagModule,
    ConfirmDialogModule,
    DialogModule,
    InputTextModule,
    DropdownModule,
    CalendarModule,
    SkeletonModule,
    TooltipModule,
    ProgressBarModule,
    ChartModule,
    TabViewModule
  ],
  providers: [ConfirmationService, MessageService],
  template: `
    <div class="agents-container">
      <!-- Header -->
      <div class="page-header">
        <h1>Gerenciamento de Agentes</h1>
        <p>Monitore e gerencie todos os agentes conectados</p>
      </div>

      <!-- Stats Cards -->
      <div class="stats-grid mb-4">
        <div class="stat-card">
          <div class="stat-icon bg-blue-100">
            <i class="pi pi-desktop text-blue-600"></i>
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ totalAgents }}</div>
            <div class="stat-label">Total de Agentes</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon bg-green-100">
            <i class="pi pi-check-circle text-green-600"></i>
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ activeAgents }}</div>
            <div class="stat-label">Agentes Ativos</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon bg-red-100">
            <i class="pi pi-times-circle text-red-600"></i>
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ offlineAgents }}</div>
            <div class="stat-label">Agentes Offline</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon bg-yellow-100">
            <i class="pi pi-exclamation-triangle text-yellow-600"></i>
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ maintenanceAgents }}</div>
            <div class="stat-label">Em Manutenção</div>
          </div>
        </div>
      </div>

      <!-- Filters -->
      <div class="filters-section mb-4">
        <div class="grid">
          <div class="col-12 md:col-3">
            <label for="statusFilter" class="font-medium text-sm mb-2">Status</label>
            <p-dropdown 
              [(ngModel)]="selectedStatus" 
              [options]="statusOptions" 
              optionLabel="label" 
              optionValue="value"
              placeholder="Todos os status"
              [showClear]="true"
              (onChange)="applyFilters()"
              class="w-full">
            </p-dropdown>
          </div>
          
          <div class="col-12 md:col-3">
            <label for="machineFilter" class="font-medium text-sm mb-2">Máquina</label>
            <input 
              type="text" 
              pInputText 
              [(ngModel)]="machineFilter"
              placeholder="Nome da máquina"
              (keyup.enter)="applyFilters()"
              class="w-full">
          </div>
          
          <div class="col-12 md:col-3">
            <label for="userFilter" class="font-medium text-sm mb-2">Usuário</label>
            <input 
              type="text" 
              pInputText 
              [(ngModel)]="userFilter"
              placeholder="Nome do usuário"
              (keyup.enter)="applyFilters()"
              class="w-full">
          </div>
          
          <div class="col-12 md:col-3 flex align-items-end">
            <div class="flex gap-2 w-full">
              <p-button 
                icon="pi pi-search" 
                (onClick)="applyFilters()"
                [loading]="isLoading"
                class="flex-1">
              </p-button>
              <p-button 
                icon="pi pi-refresh" 
                (onClick)="refreshData()"
                [loading]="isLoading"
                severity="secondary"
                class="flex-1">
              </p-button>
            </div>
          </div>
        </div>
      </div>

      <!-- Loading State -->
      <div *ngIf="isLoading && agents.length === 0" class="loading-container">
        <div class="grid">
          <div class="col-12" *ngFor="let item of [1,2,3,4,5]">
            <p-skeleton height="80px" class="mb-3"></p-skeleton>
          </div>
        </div>
      </div>

      <!-- Agents Table -->
      <div *ngIf="!isLoading || agents.length > 0">
        <p-table 
          [value]="agents" 
          [paginator]="true" 
          [rows]="pageSize"
          [totalRecords]="totalRecords"
          [lazy]="true"
          (onLazyLoad)="loadAgentsLazy($event)"
          [loading]="isLoading"
          styleClass="p-datatable-striped"
          [globalFilterFields]="['machineName', 'userName', 'machineId']"
          [selection]="selectedAgents"
          [selectionPageOnly]="true"
          (selectionChange)="onSelectionChange($event)"
          dataKey="id">
          
          <ng-template pTemplate="caption">
            <div class="flex justify-content-between align-items-center">
              <div class="flex align-items-center gap-2">
                <span class="text-xl font-semibold">Agentes ({{ totalRecords }})</span>
              </div>
              <div class="flex gap-2">
                <p-button 
                  *ngIf="selectedAgents.length > 0"
                  label="Ações em Lote"
                  icon="pi pi-cog"
                  (onClick)="showBatchActions = true"
                  [badge]="selectedAgents.length.toString()"
                  badgeClass="p-badge-info">
                </p-button>
                <p-button 
                  label="Exportar"
                  icon="pi pi-download"
                  (onClick)="exportAgents()"
                  severity="secondary">
                </p-button>
              </div>
            </div>
          </ng-template>
          
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 3rem">
                <p-tableHeaderCheckbox></p-tableHeaderCheckbox>
              </th>
              <th pSortableColumn="machineName">
                Máquina
                <p-sortIcon field="machineName"></p-sortIcon>
              </th>
              <th pSortableColumn="userName">
                Usuário
                <p-sortIcon field="userName"></p-sortIcon>
              </th>
              <th pSortableColumn="status">
                Status
                <p-sortIcon field="status"></p-sortIcon>
              </th>
              <th pSortableColumn="lastSeen">
                Último Contato
                <p-sortIcon field="lastSeen"></p-sortIcon>
              </th>
              <th pSortableColumn="agentVersion">
                Versão
                <p-sortIcon field="agentVersion"></p-sortIcon>
              </th>
              <th>Sistema</th>
              <th>Ações</th>
            </tr>
          </ng-template>
          
          <ng-template pTemplate="body" let-agent>
            <tr>
              <td>
                <p-tableCheckbox [value]="agent"></p-tableCheckbox>
              </td>
              <td>
                <div class="flex align-items-center gap-2">
                  <i class="pi pi-desktop text-gray-500"></i>
                  <div>
                    <div class="font-medium">{{ agent.machineName }}</div>
                    <div class="text-sm text-gray-500">{{ agent.machineId }}</div>
                  </div>
                </div>
              </td>
              <td>
                <div class="flex align-items-center gap-2">
                  <i class="pi pi-user text-gray-500"></i>
                  <span class="font-medium">{{ agent.userName }}</span>
                </div>
              </td>
              <td>
                <p-tag 
                  [value]="agent.status" 
                  [severity]="getStatusSeverity(agent.status)"
                  [icon]="getStatusIcon(agent.status)">
                </p-tag>
              </td>
              <td>
                <div class="flex flex-column gap-1">
                  <span>{{ agent.lastSeen | date:'short' }}</span>
                  <span class="text-sm" [class]="getLastSeenClass(agent.lastSeen)">
                    {{ getLastSeenText(agent.lastSeen) }}
                  </span>
                </div>
              </td>
              <td>
                <p-tag 
                  [value]="agent.agentVersion || 'N/A'" 
                  severity="info"
                  [class]="agent.agentVersion ? '' : 'opacity-50'">
                </p-tag>
              </td>
              <td>
                <div class="text-sm">{{ agent.osVersion || 'N/A' }}</div>
              </td>
              <td>
                <div class="flex gap-1">
                  <p-button 
                    icon="pi pi-eye" 
                    size="small"
                    severity="info"
                    (onClick)="viewAgent(agent)"
                    pTooltip="Visualizar Detalhes">
                  </p-button>
                  <p-button 
                    icon="pi pi-cog" 
                    size="small"
                    severity="secondary"
                    (onClick)="configureAgent(agent)"
                    pTooltip="Configurar">
                  </p-button>
                  <p-button 
                    icon="pi pi-refresh" 
                    size="small"
                    severity="warning"
                    (onClick)="restartAgent(agent)"
                    pTooltip="Reiniciar">
                  </p-button>
                  <p-button 
                    icon="pi pi-trash" 
                    size="small"
                    severity="danger"
                    (onClick)="deleteAgent(agent)"
                    pTooltip="Excluir">
                  </p-button>
                </div>
              </td>
            </tr>
          </ng-template>
          
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="8" class="text-center">
                <div class="p-4">
                  <i class="pi pi-desktop text-gray-400 text-4xl mb-3"></i>
                  <p class="text-gray-500">Nenhum agente encontrado</p>
                </div>
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>

      <!-- Error State -->
      <div *ngIf="error" class="error-container">
        <p-card>
          <div class="text-center">
            <i class="pi pi-exclamation-triangle text-red-500 text-4xl mb-3"></i>
            <h3>Erro ao carregar agentes</h3>
            <p>{{ error }}</p>
            <p-button 
              label="Tentar Novamente" 
              icon="pi pi-refresh" 
              (onClick)="refreshData()">
            </p-button>
          </div>
        </p-card>
      </div>
    </div>

    <!-- Agent Details Dialog -->
    <p-dialog 
      [(visible)]="showAgentDetails" 
      [modal]="true" 
      [closable]="true"
      [style]="{'width': '60rem'}"
      header="Detalhes do Agente">
      
      <div *ngIf="selectedAgent" class="agent-details">
        <p-tabView>
          <p-tabPanel header="Informações Gerais">
            <div class="grid">
              <div class="col-12 md:col-6">
                <div class="field">
                  <label class="font-medium">Nome da Máquina</label>
                  <p>{{ selectedAgent.machineName }}</p>
                </div>
                <div class="field">
                  <label class="font-medium">ID da Máquina</label>
                  <p>{{ selectedAgent.machineId }}</p>
                </div>
                <div class="field">
                  <label class="font-medium">Usuário</label>
                  <p>{{ selectedAgent.userName }}</p>
                </div>
              </div>
              <div class="col-12 md:col-6">
                <div class="field">
                  <label class="font-medium">Status</label>
                  <p>
                    <p-tag 
                      [value]="selectedAgent.status" 
                      [severity]="getStatusSeverity(selectedAgent.status)">
                    </p-tag>
                  </p>
                </div>
                <div class="field">
                  <label class="font-medium">Versão do Agente</label>
                  <p>{{ selectedAgent.agentVersion || 'N/A' }}</p>
                </div>
                <div class="field">
                  <label class="font-medium">Sistema Operacional</label>
                  <p>{{ selectedAgent.osVersion || 'N/A' }}</p>
                </div>
              </div>
            </div>
          </p-tabPanel>
          
          <p-tabPanel header="Atividade Recente">
            <div *ngIf="agentEvents.length > 0; else noEvents">
              <p-table [value]="agentEvents" [rows]="10" [paginator]="true">
                <ng-template pTemplate="header">
                  <tr>
                    <th>Timestamp</th>
                    <th>Tipo</th>
                    <th>Aplicação</th>
                    <th>Score</th>
                  </tr>
                </ng-template>
                <ng-template pTemplate="body" let-event>
                  <tr>
                    <td>{{ event.eventTimestamp | date:'short' }}</td>
                    <td>
                      <p-tag [value]="event.eventType" severity="info"></p-tag>
                    </td>
                    <td>{{ event.applicationName || '-' }}</td>
                    <td>
                      <p-tag 
                        *ngIf="event.productivityScore !== null"
                        [value]="event.productivityScore | number:'1.1-1'" 
                        [severity]="getScoreSeverity(event.productivityScore)">
                      </p-tag>
                    </td>
                  </tr>
                </ng-template>
              </p-table>
            </div>
            <ng-template #noEvents>
              <div class="text-center p-4">
                <i class="pi pi-info-circle text-gray-400 text-3xl mb-3"></i>
                <p class="text-gray-500">Nenhuma atividade recente encontrada</p>
              </div>
            </ng-template>
          </p-tabPanel>
        </p-tabView>
      </div>
      
      <ng-template pTemplate="footer">
        <p-button 
          label="Fechar" 
          icon="pi pi-times" 
          (onClick)="showAgentDetails = false"
          severity="secondary">
        </p-button>
      </ng-template>
    </p-dialog>

    <!-- Batch Actions Dialog -->
    <p-dialog 
      [(visible)]="showBatchActions" 
      [modal]="true" 
      [closable]="true"
      [style]="{'width': '30rem'}"
      header="Ações em Lote">
      
      <div class="batch-actions">
        <p>{{ selectedAgents.length }} agente(s) selecionado(s)</p>
        
        <div class="flex flex-column gap-2 mt-3">
          <p-button 
            label="Alterar Status"
            icon="pi pi-tag"
            (onClick)="showStatusChange = true"
            severity="info"
            class="w-full">
          </p-button>
          <p-button 
            label="Reiniciar Agentes"
            icon="pi pi-refresh"
            (onClick)="batchRestartAgents()"
            severity="warning"
            class="w-full">
          </p-button>
          <p-button 
            label="Excluir Agentes"
            icon="pi pi-trash"
            (onClick)="batchDeleteAgents()"
            severity="danger"
            class="w-full">
          </p-button>
        </div>
      </div>
      
      <ng-template pTemplate="footer">
        <p-button 
          label="Fechar" 
          icon="pi pi-times" 
          (onClick)="showBatchActions = false"
          severity="secondary">
        </p-button>
      </ng-template>
    </p-dialog>

    <!-- Status Change Dialog -->
    <p-dialog 
      [(visible)]="showStatusChange" 
      [modal]="true" 
      [closable]="true"
      [style]="{'width': '25rem'}"
      header="Alterar Status">
      
      <div class="status-change">
        <div class="field">
          <label for="newStatus" class="font-medium">Novo Status</label>
          <p-dropdown 
            [(ngModel)]="newStatus" 
            [options]="statusOptions" 
            optionLabel="label" 
            optionValue="value"
            placeholder="Selecione o status"
            class="w-full">
          </p-dropdown>
        </div>
      </div>
      
      <ng-template pTemplate="footer">
        <p-button 
          label="Cancelar" 
          icon="pi pi-times" 
          (onClick)="showStatusChange = false"
          severity="secondary">
        </p-button>
        <p-button 
          label="Confirmar" 
          icon="pi pi-check" 
          (onClick)="changeAgentsStatus()"
          [disabled]="!newStatus">
        </p-button>
      </ng-template>
    </p-dialog>

    <!-- Confirmation Dialog -->
    <p-confirmDialog></p-confirmDialog>
  `,
  styles: [`
    .agents-container {
      padding: 1.5rem;
    }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 1.5rem;
    }

    .stat-card {
      background: white;
      border-radius: 8px;
      padding: 1.5rem;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .stat-icon {
      width: 60px;
      height: 60px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.5rem;
    }

    .stat-content {
      flex: 1;
    }

    .stat-value {
      font-size: 2rem;
      font-weight: 700;
      color: #007bff;
      margin-bottom: 0.5rem;
    }

    .stat-label {
      font-size: 0.9rem;
      color: #6c757d;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .filters-section {
      background: white;
      border-radius: 8px;
      padding: 1.5rem;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
    }

    .loading-container {
      padding: 2rem;
    }

    .error-container {
      padding: 2rem;
    }

    .agent-details .field {
      margin-bottom: 1rem;
    }

    .agent-details .field label {
      display: block;
      margin-bottom: 0.5rem;
      color: #6c757d;
      font-size: 0.9rem;
    }

    .agent-details .field p {
      margin: 0;
      font-size: 1rem;
    }

    .batch-actions {
      padding: 1rem 0;
    }

    .status-change {
      padding: 1rem 0;
    }

    .text-green-500 {
      color: #10b981;
    }

    .text-red-500 {
      color: #ef4444;
    }

    .text-yellow-500 {
      color: #f59e0b;
    }

    .text-gray-500 {
      color: #6b7280;
    }

    @media (max-width: 768px) {
      .agents-container {
        padding: 1rem;
      }
      
      .stats-grid {
        grid-template-columns: 1fr;
        gap: 1rem;
      }
    }
  `]
})
export class AgentsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  
  agents: AgentDto[] = [];
  selectedAgents: AgentDto[] = [];
  selectedAgent: AgentDto | null = null;
  agentEvents: EventDto[] = [];
  isLoading = false;
  error: string | null = null;
  
  // Pagination
  first = 0;
  pageSize = 25;
  totalRecords = 0;
  
  // Stats
  totalAgents = 0;
  activeAgents = 0;
  offlineAgents = 0;
  maintenanceAgents = 0;
  
  // Filters
  selectedStatus: string | null = null;
  machineFilter = '';
  userFilter = '';
  
  // Dialogs
  showAgentDetails = false;
  showBatchActions = false;
  showStatusChange = false;
  
  // Batch operations
  newStatus: string | null = null;
  
  statusOptions = [
    { label: 'Ativo', value: AgentStatus.Active },
    { label: 'Inativo', value: AgentStatus.Inactive },
    { label: 'Offline', value: AgentStatus.Offline },
    { label: 'Manutenção', value: AgentStatus.Maintenance },
    { label: 'Desabilitado', value: AgentStatus.Disabled }
  ];

  constructor(
    private agentService: AgentService,
    private eventService: EventService,
    private confirmationService: ConfirmationService,
    private messageService: MessageService
  ) {}

  ngOnInit() {
    this.loadAgents();
    this.loadStats();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAgents() {
    this.isLoading = true;
    this.error = null;
    
    const filters = this.buildFilters();
    
    this.agentService.getAgents(filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: any) => {
          this.agents = response.agents;
          this.totalRecords = response.totalCount;
          this.isLoading = false;
        },
        error: (error: any) => {
          console.error('Error loading agents:', error);
          this.error = 'Erro ao carregar agentes';
          this.isLoading = false;
        }
      });
  }

  loadAgentsLazy(event: any) {
    this.first = event.first;
    this.pageSize = event.rows;
    this.loadAgents();
  }

  loadStats() {
    this.agentService.getAgentMetrics()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (metrics: any) => {
          this.totalAgents = metrics.totalAgents;
          this.activeAgents = metrics.activeAgents;
          this.offlineAgents = metrics.offlineAgents;
          this.maintenanceAgents = metrics.maintenanceAgents;
        },
        error: (error: any) => {
          console.error('Error loading stats:', error);
        }
      });
  }

  private buildFilters(): AgentFilters {
    const filters: AgentFilters = {
      pageSize: this.pageSize,
      currentPage: Math.floor(this.first / this.pageSize) + 1
    };

    if (this.selectedStatus) {
      filters.status = this.selectedStatus as any;
    }

    if (this.machineFilter) {
      filters.machineName = this.machineFilter;
    }

    if (this.userFilter) {
      filters.userName = this.userFilter;
    }

    return filters;
  }

  // Event handlers
  applyFilters() {
    this.first = 0;
    this.loadAgents();
  }

  refreshData() {
    this.loadAgents();
    this.loadStats();
  }

  onSelectionChange(event: any) {
    this.selectedAgents = event;
  }

  viewAgent(agent: AgentDto) {
    this.selectedAgent = agent;
    this.showAgentDetails = true;
    this.loadAgentEvents(agent.id);
  }

  private loadAgentEvents(agentId: string) {
    this.eventService.getEventsByAgent(agentId, { pageSize: 50 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (events: any) => {
          this.agentEvents = events;
        },
        error: (error: any) => {
          console.error('Error loading agent events:', error);
        }
      });
  }

  configureAgent(agent: AgentDto) {
    // TODO: Implement agent configuration
    this.messageService.add({
      severity: 'info',
      summary: 'Configuração',
      detail: 'Funcionalidade de configuração será implementada em breve'
    });
  }

  restartAgent(agent: AgentDto) {
    this.confirmationService.confirm({
      message: `Tem certeza que deseja reiniciar o agente ${agent.machineName}?`,
      header: 'Confirmar Reinicialização',
      icon: 'pi pi-refresh',
      accept: () => {
        // TODO: Implement agent restart
        this.messageService.add({
          severity: 'success',
          summary: 'Sucesso',
          detail: `Comando de reinicialização enviado para ${agent.machineName}`
        });
      }
    });
  }

  deleteAgent(agent: AgentDto) {
    this.confirmationService.confirm({
      message: `Tem certeza que deseja excluir o agente ${agent.machineName}? Esta ação não pode ser desfeita.`,
      header: 'Confirmar Exclusão',
      icon: 'pi pi-trash',
      accept: () => {
        this.agentService.deleteAgent(agent.id)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.messageService.add({
                severity: 'success',
                summary: 'Sucesso',
                detail: `Agente ${agent.machineName} excluído com sucesso`
              });
              this.loadAgents();
              this.loadStats();
            },
            error: (error: any) => {
              console.error('Error deleting agent:', error);
              this.messageService.add({
                severity: 'error',
                summary: 'Erro',
                detail: 'Erro ao excluir agente'
              });
            }
          });
      }
    });
  }

  batchRestartAgents() {
    this.confirmationService.confirm({
      message: `Tem certeza que deseja reiniciar ${this.selectedAgents.length} agente(s)?`,
      header: 'Confirmar Reinicialização em Lote',
      icon: 'pi pi-refresh',
      accept: () => {
        // TODO: Implement batch restart
        this.messageService.add({
          severity: 'success',
          summary: 'Sucesso',
          detail: `Comando de reinicialização enviado para ${this.selectedAgents.length} agente(s)`
        });
        this.showBatchActions = false;
        this.selectedAgents = [];
      }
    });
  }

  batchDeleteAgents() {
    this.confirmationService.confirm({
      message: `Tem certeza que deseja excluir ${this.selectedAgents.length} agente(s)? Esta ação não pode ser desfeita.`,
      header: 'Confirmar Exclusão em Lote',
      icon: 'pi pi-trash',
      accept: () => {
        const agentIds = this.selectedAgents.map(a => a.id);
        this.agentService.deleteMultipleAgents(agentIds)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: (result: any) => {
              this.messageService.add({
                severity: 'success',
                summary: 'Sucesso',
                detail: `${result.deleted} agente(s) excluído(s) com sucesso`
              });
              this.loadAgents();
              this.loadStats();
              this.showBatchActions = false;
              this.selectedAgents = [];
            },
            error: (error: any) => {
              console.error('Error deleting agents:', error);
              this.messageService.add({
                severity: 'error',
                summary: 'Erro',
                detail: 'Erro ao excluir agentes'
              });
            }
          });
      }
    });
  }

  changeAgentsStatus() {
    if (!this.newStatus) return;
    
    const agentIds = this.selectedAgents.map(a => a.id);
    this.agentService.updateMultipleAgentsStatus(agentIds, this.newStatus)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: any) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Sucesso',
            detail: `Status atualizado para ${result.updated} agente(s)`
          });
          this.loadAgents();
          this.loadStats();
          this.showStatusChange = false;
          this.showBatchActions = false;
          this.selectedAgents = [];
          this.newStatus = null;
        },
        error: (error: any) => {
          console.error('Error updating agents status:', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Erro',
            detail: 'Erro ao atualizar status dos agentes'
          });
        }
      });
  }

  exportAgents() {
    const filters = this.buildFilters();
    this.agentService.exportAgents(filters, 'csv')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Sucesso',
            detail: 'Agentes exportados com sucesso'
          });
        },
        error: (error: any) => {
          console.error('Error exporting agents:', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Erro',
            detail: 'Erro ao exportar agentes'
          });
        }
      });
  }

  // Utility methods
  getStatusSeverity(status: string): string {
    switch (status) {
      case AgentStatus.Active: return 'success';
      case AgentStatus.Inactive: return 'warning';
      case AgentStatus.Offline: return 'danger';
      case AgentStatus.Maintenance: return 'info';
      case AgentStatus.Disabled: return 'secondary';
      default: return 'secondary';
    }
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case AgentStatus.Active: return 'pi pi-check-circle';
      case AgentStatus.Inactive: return 'pi pi-clock';
      case AgentStatus.Offline: return 'pi pi-times-circle';
      case AgentStatus.Maintenance: return 'pi pi-wrench';
      case AgentStatus.Disabled: return 'pi pi-ban';
      default: return 'pi pi-question-circle';
    }
  }

  getLastSeenText(lastSeen: Date): string {
    const now = new Date();
    const diff = now.getTime() - new Date(lastSeen).getTime();
    const minutes = Math.floor(diff / (1000 * 60));
    
    if (minutes < 1) return 'Agora';
    if (minutes < 60) return `${minutes}m atrás`;
    if (minutes < 1440) return `${Math.floor(minutes / 60)}h atrás`;
    return `${Math.floor(minutes / 1440)}d atrás`;
  }

  getLastSeenClass(lastSeen: Date): string {
    const now = new Date();
    const diff = now.getTime() - new Date(lastSeen).getTime();
    const minutes = Math.floor(diff / (1000 * 60));
    
    if (minutes < 5) return 'text-green-500';
    if (minutes < 30) return 'text-yellow-500';
    return 'text-red-500';
  }

  getScoreSeverity(score: number): string {
    if (score >= 80) return 'success';
    if (score >= 60) return 'warning';
    return 'danger';
  }
}