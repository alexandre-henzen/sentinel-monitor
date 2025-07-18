import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';

// PrimeNG Components
import { CardModule } from 'primeng/card';
import { ChartModule } from 'primeng/chart';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { ProgressBarModule } from 'primeng/progressbar';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';

// Services
import { DashboardService } from '../../core/services/dashboard.service';
import { AgentService } from '../../core/services/agent.service';
import { EventService } from '../../core/services/event.service';

// Models
import { DashboardMetrics, DashboardFilters } from '../../shared/models/dashboard-dto';
import { AgentDto } from '../../shared/models/agent-dto';
import { EventDto } from '../../shared/models/event-dto';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    CardModule,
    ChartModule,
    TableModule,
    TagModule,
    ButtonModule,
    SkeletonModule,
    CalendarModule,
    DropdownModule,
    ProgressBarModule,
    AvatarModule,
    BadgeModule
  ],
  template: `
    <div class="dashboard-container">
      <!-- Header -->
      <div class="page-header">
        <h1>Dashboard</h1>
        <p>Visão geral da atividade dos agentes</p>
      </div>

      <!-- Filters -->
      <div class="filters-section mb-4">
        <div class="flex flex-wrap gap-3">
          <div class="flex-column">
            <label for="dateRange" class="font-medium text-sm mb-2">Período</label>
            <p-calendar 
              [(ngModel)]="selectedDateRange" 
              selectionMode="range" 
              dateFormat="dd/mm/yy"
              placeholder="Selecione o período"
              (onSelect)="onDateRangeChange()"
              [readonlyInput]="false">
            </p-calendar>
          </div>
          
          <div class="flex-column">
            <label for="agentFilter" class="font-medium text-sm mb-2">Agente</label>
            <p-dropdown 
              [(ngModel)]="selectedAgent" 
              [options]="agents" 
              optionLabel="machineName" 
              optionValue="id"
              placeholder="Todos os agentes"
              [showClear]="true"
              (onChange)="onAgentChange()"
              [style]="{'min-width': '200px'}">
            </p-dropdown>
          </div>
          
          <div class="flex align-items-end">
            <p-button 
              label="Atualizar" 
              icon="pi pi-refresh" 
              (onClick)="refreshDashboard()"
              [loading]="isLoading">
            </p-button>
          </div>
        </div>
      </div>

      <!-- Loading State -->
      <div *ngIf="isLoading && !dashboardMetrics" class="loading-container">
        <div class="metrics-grid">
          <p-skeleton height="120px" *ngFor="let item of [1,2,3,4]"></p-skeleton>
        </div>
        <div class="mt-4">
          <p-skeleton height="300px"></p-skeleton>
        </div>
      </div>

      <!-- Dashboard Content -->
      <div *ngIf="dashboardMetrics" class="dashboard-content">
        <!-- Key Metrics -->
        <div class="metrics-grid mb-4">
          <div class="metric-card">
            <div class="metric-icon">
              <i class="pi pi-desktop text-blue-500"></i>
            </div>
            <div class="metric-content">
              <div class="metric-value">{{ dashboardMetrics.totalAgents }}</div>
              <div class="metric-label">Total de Agentes</div>
            </div>
          </div>

          <div class="metric-card">
            <div class="metric-icon">
              <i class="pi pi-circle-fill text-green-500"></i>
            </div>
            <div class="metric-content">
              <div class="metric-value">{{ dashboardMetrics.activeAgents }}</div>
              <div class="metric-label">Agentes Ativos</div>
            </div>
          </div>

          <div class="metric-card">
            <div class="metric-icon">
              <i class="pi pi-calendar text-orange-500"></i>
            </div>
            <div class="metric-content">
              <div class="metric-value">{{ dashboardMetrics.eventsToday | number }}</div>
              <div class="metric-label">Eventos Hoje</div>
            </div>
          </div>

          <div class="metric-card">
            <div class="metric-icon">
              <i class="pi pi-chart-line text-purple-500"></i>
            </div>
            <div class="metric-content">
              <div class="metric-value">{{ dashboardMetrics.averageProductivityScore | number:'1.1-1' }}%</div>
              <div class="metric-label">Score Médio</div>
            </div>
          </div>
        </div>

        <!-- Charts Row -->
        <div class="grid">
          <div class="col-12 md:col-6">
            <p-card header="Tendência de Produtividade" [style]="{'height': '400px'}">
              <p-chart 
                type="line" 
                [data]="productivityChartData" 
                [options]="chartOptions"
                [height]="300">
              </p-chart>
            </p-card>
          </div>

          <div class="col-12 md:col-6">
            <p-card header="Aplicações Mais Utilizadas" [style]="{'height': '400px'}">
              <p-chart 
                type="doughnut" 
                [data]="applicationChartData" 
                [options]="doughnutOptions"
                [height]="300">
              </p-chart>
            </p-card>
          </div>
        </div>

        <!-- Status Distribution -->
        <div class="grid mt-4">
          <div class="col-12 md:col-4">
            <p-card header="Status dos Agentes">
              <p-chart 
                type="pie" 
                [data]="agentStatusChartData" 
                [options]="pieOptions"
                [height]="250">
              </p-chart>
            </p-card>
          </div>

          <div class="col-12 md:col-8">
            <p-card header="Aplicações Populares">
              <p-table 
                [value]="dashboardMetrics.topApplications" 
                [paginator]="false" 
                [rows]="10"
                styleClass="p-datatable-sm">
                <ng-template pTemplate="header">
                  <tr>
                    <th>Aplicação</th>
                    <th>Uso</th>
                    <th>Tempo</th>
                    <th>Score</th>
                    <th>Percentual</th>
                  </tr>
                </ng-template>
                <ng-template pTemplate="body" let-app>
                  <tr>
                    <td>{{ app.name }}</td>
                    <td>{{ app.usage | number }}</td>
                    <td>{{ formatTime(app.timeSpent) }}</td>
                    <td>
                      <p-tag 
                        [value]="app.productivityScore | number:'1.1-1'" 
                        [severity]="getScoreSeverity(app.productivityScore)">
                      </p-tag>
                    </td>
                    <td>
                      <p-progressBar 
                        [value]="app.percentage" 
                        [style]="{'height': '8px'}" 
                        [showValue]="false">
                      </p-progressBar>
                      <small>{{ app.percentage | number:'1.1-1' }}%</small>
                    </td>
                  </tr>
                </ng-template>
              </p-table>
            </p-card>
          </div>
        </div>

        <!-- Recent Activity Timeline -->
        <div class="mt-4">
          <p-card header="Atividade Recente">
            <p-table 
              [value]="dashboardMetrics.activityTimeline" 
              [paginator]="true" 
              [rows]="10"
              styleClass="p-datatable-sm">
              <ng-template pTemplate="header">
                <tr>
                  <th>Timestamp</th>
                  <th>Tipo</th>
                  <th>Aplicação</th>
                  <th>Título</th>
                  <th>Score</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-activity>
                <tr>
                  <td>{{ activity.timestamp | date:'short' }}</td>
                  <td>
                    <p-tag 
                      [value]="activity.eventType" 
                      severity="info">
                    </p-tag>
                  </td>
                  <td>{{ activity.applicationName }}</td>
                  <td>{{ activity.windowTitle | slice:0:50 }}{{ activity.windowTitle?.length > 50 ? '...' : '' }}</td>
                  <td>
                    <p-tag 
                      [value]="activity.productivityScore | number:'1.1-1'" 
                      [severity]="getScoreSeverity(activity.productivityScore)">
                    </p-tag>
                  </td>
                </tr>
              </ng-template>
            </p-table>
          </p-card>
        </div>
      </div>

      <!-- Error State -->
      <div *ngIf="error" class="error-container">
        <p-card>
          <div class="text-center">
            <i class="pi pi-exclamation-triangle text-red-500 text-4xl mb-3"></i>
            <h3>Erro ao carregar dashboard</h3>
            <p>{{ error }}</p>
            <p-button 
              label="Tentar Novamente" 
              icon="pi pi-refresh" 
              (onClick)="refreshDashboard()">
            </p-button>
          </div>
        </p-card>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-container {
      padding: 1.5rem;
    }

    .metrics-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 1.5rem;
      margin-bottom: 2rem;
    }

    .metric-card {
      background: white;
      border-radius: 8px;
      padding: 1.5rem;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .metric-icon {
      font-size: 2rem;
      width: 60px;
      height: 60px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(0, 123, 255, 0.1);
    }

    .metric-content {
      flex: 1;
    }

    .metric-value {
      font-size: 2rem;
      font-weight: 700;
      color: #007bff;
      margin-bottom: 0.5rem;
    }

    .metric-label {
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

    @media (max-width: 768px) {
      .dashboard-container {
        padding: 1rem;
      }
      
      .metrics-grid {
        grid-template-columns: 1fr;
        gap: 1rem;
      }
    }
  `]
})
export class DashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  
  dashboardMetrics: DashboardMetrics | null = null;
  agents: AgentDto[] = [];
  isLoading = false;
  error: string | null = null;
  
  // Filters
  selectedDateRange: Date[] = [];
  selectedAgent: string | null = null;
  
  // Chart data
  productivityChartData: any = {};
  applicationChartData: any = {};
  agentStatusChartData: any = {};
  
  // Chart options
  chartOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      y: {
        beginAtZero: true
      }
    }
  };
  
  doughnutOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom'
      }
    }
  };
  
  pieOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom'
      }
    }
  };

  constructor(
    private dashboardService: DashboardService,
    private agentService: AgentService,
    private eventService: EventService
  ) {
    this.initializeDateRange();
  }

  ngOnInit() {
    this.loadAgents();
    this.loadDashboardData();
    this.setupRealTimeUpdates();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeDateRange() {
    const today = new Date();
    const lastWeek = new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000);
    this.selectedDateRange = [lastWeek, today];
  }

  private loadAgents() {
    this.agentService.getAgents()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.agents = response.agents;
        },
        error: (error) => {
          console.error('Error loading agents:', error);
        }
      });
  }

  private loadDashboardData() {
    this.isLoading = true;
    this.error = null;
    
    const filters = this.buildFilters();
    
    this.dashboardService.getDashboardMetrics(filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (metrics) => {
          this.dashboardMetrics = metrics;
          this.loadChartData(filters);
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Error loading dashboard data:', error);
          this.error = 'Erro ao carregar dados do dashboard';
          this.isLoading = false;
        }
      });
  }

  private loadChartData(filters: DashboardFilters) {
    // Load productivity chart data
    this.dashboardService.getProductivityChartData(filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.productivityChartData = data;
        },
        error: (error) => {
          console.error('Error loading productivity chart data:', error);
        }
      });

    // Load application usage chart data
    this.dashboardService.getApplicationUsageChartData(filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.applicationChartData = data;
        },
        error: (error) => {
          console.error('Error loading application chart data:', error);
        }
      });

    // Load agent status chart data
    this.dashboardService.getAgentStatusChartData(filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.agentStatusChartData = data;
        },
        error: (error) => {
          console.error('Error loading agent status chart data:', error);
        }
      });
  }

  private setupRealTimeUpdates() {
    this.dashboardService.getDashboardMetricsStream(this.buildFilters())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (metrics) => {
          this.dashboardMetrics = metrics;
        },
        error: (error) => {
          console.error('Error in real-time updates:', error);
        }
      });
  }

  private buildFilters(): DashboardFilters {
    const filters: DashboardFilters = {
      dateRange: {
        from: this.selectedDateRange[0] || new Date(Date.now() - 7 * 24 * 60 * 60 * 1000),
        to: this.selectedDateRange[1] || new Date()
      }
    };

    if (this.selectedAgent) {
      filters.agentIds = [this.selectedAgent];
    }

    return filters;
  }

  // Event handlers
  onDateRangeChange() {
    this.loadDashboardData();
  }

  onAgentChange() {
    this.loadDashboardData();
  }

  refreshDashboard() {
    this.loadDashboardData();
  }

  // Utility methods
  formatTime(seconds: number): string {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  }

  getScoreSeverity(score: number): string {
    if (score >= 80) return 'success';
    if (score >= 60) return 'warning';
    return 'danger';
  }
}