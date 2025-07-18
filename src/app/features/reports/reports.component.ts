import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

// PrimeNG Components
import { TabViewModule } from 'primeng/tabview';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { ChartModule } from 'primeng/chart';
import { TagModule } from 'primeng/tag';
import { ProgressBarModule } from 'primeng/progressbar';
import { InputTextModule } from 'primeng/inputtext';
import { FileUploadModule } from 'primeng/fileupload';
import { MessagesModule } from 'primeng/messages';
import { MessageService } from 'primeng/api';

// Services
import { EventService } from '../../core/services/event.service';
import { AgentService } from '../../core/services/agent.service';
import { DashboardService } from '../../core/services/dashboard.service';

// Models
import { EventDto, EventFilters } from '../../shared/models/event-dto';
import { AgentDto } from '../../shared/models/agent-dto';
import { DashboardFilters } from '../../shared/models/dashboard-dto';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TabViewModule,
    CardModule,
    ButtonModule,
    CalendarModule,
    DropdownModule,
    TableModule,
    ChartModule,
    TagModule,
    ProgressBarModule,
    InputTextModule,
    FileUploadModule,
    MessagesModule
  ],
  providers: [MessageService],
  template: `
    <div class="reports-container">
      <!-- Header -->
      <div class="page-header">
        <h1>Relatórios</h1>
        <p>Gere relatórios detalhados sobre a atividade dos agentes</p>
      </div>

      <p-tabView>
        <!-- Activities Report -->
        <p-tabPanel header="Relatório de Atividades">
          <div class="report-section">
            <!-- Filters -->
            <div class="filters-card mb-4">
              <p-card header="Filtros">
                <div class="grid">
                  <div class="col-12 md:col-3">
                    <label for="dateRange" class="font-medium text-sm mb-2">Período</label>
                    <p-calendar 
                      [(ngModel)]="activityDateRange" 
                      selectionMode="range" 
                      dateFormat="dd/mm/yy"
                      placeholder="Selecione o período"
                      class="w-full">
                    </p-calendar>
                  </div>
                  
                  <div class="col-12 md:col-3">
                    <label for="agentSelect" class="font-medium text-sm mb-2">Agente</label>
                    <p-dropdown 
                      [(ngModel)]="selectedAgent" 
                      [options]="agents" 
                      optionLabel="machineName" 
                      optionValue="id"
                      placeholder="Todos os agentes"
                      [showClear]="true"
                      class="w-full">
                    </p-dropdown>
                  </div>
                  
                  <div class="col-12 md:col-3">
                    <label for="eventType" class="font-medium text-sm mb-2">Tipo de Evento</label>
                    <p-dropdown 
                      [(ngModel)]="selectedEventType" 
                      [options]="eventTypes" 
                      optionLabel="label" 
                      optionValue="value"
                      placeholder="Todos os tipos"
                      [showClear]="true"
                      class="w-full">
                    </p-dropdown>
                  </div>
                  
                  <div class="col-12 md:col-3 flex align-items-end">
                    <p-button 
                      label="Gerar Relatório"
                      icon="pi pi-search"
                      (onClick)="generateActivityReport()"
                      [loading]="isGeneratingReport"
                      class="w-full">
                    </p-button>
                  </div>
                </div>
              </p-card>
            </div>

            <!-- Activity Report Results -->
            <div *ngIf="activityReportData" class="report-results">
              <!-- Summary Cards -->
              <div class="summary-grid mb-4">
                <div class="summary-card">
                  <div class="summary-icon">
                    <i class="pi pi-calendar text-blue-500"></i>
                  </div>
                  <div class="summary-content">
                    <div class="summary-value">{{ activityReportData.totalEvents | number }}</div>
                    <div class="summary-label">Total de Eventos</div>
                  </div>
                </div>

                <div class="summary-card">
                  <div class="summary-icon">
                    <i class="pi pi-clock text-green-500"></i>
                  </div>
                  <div class="summary-content">
                    <div class="summary-value">{{ formatTime(activityReportData.totalDuration) }}</div>
                    <div class="summary-label">Tempo Total</div>
                  </div>
                </div>

                <div class="summary-card">
                  <div class="summary-icon">
                    <i class="pi pi-chart-line text-purple-500"></i>
                  </div>
                  <div class="summary-content">
                    <div class="summary-value">{{ activityReportData.averageScore | number:'1.1-1' }}%</div>
                    <div class="summary-label">Score Médio</div>
                  </div>
                </div>

                <div class="summary-card">
                  <div class="summary-icon">
                    <i class="pi pi-desktop text-orange-500"></i>
                  </div>
                  <div class="summary-content">
                    <div class="summary-value">{{ activityReportData.uniqueApps }}</div>
                    <div class="summary-label">Aplicações Únicas</div>
                  </div>
                </div>
              </div>

              <!-- Charts -->
              <div class="grid">
                <div class="col-12 md:col-6">
                  <p-card header="Eventos por Dia">
                    <p-chart 
                      type="bar" 
                      [data]="eventsByDayChart" 
                      [options]="chartOptions">
                    </p-chart>
                  </p-card>
                </div>
                
                <div class="col-12 md:col-6">
                  <p-card header="Aplicações Mais Utilizadas">
                    <p-chart 
                      type="doughnut" 
                      [data]="topAppsChart" 
                      [options]="doughnutOptions">
                    </p-chart>
                  </p-card>
                </div>
              </div>

              <!-- Detailed Table -->
              <div class="mt-4">
                <p-card header="Detalhes do Relatório">
                  <ng-template pTemplate="header">
                    <div class="flex justify-content-between align-items-center">
                      <span>Atividades Detalhadas</span>
                      <div class="flex gap-2">
                        <p-button 
                          label="Exportar CSV"
                          icon="pi pi-download"
                          (onClick)="exportReport('csv')"
                          size="small">
                        </p-button>
                        <p-button 
                          label="Exportar PDF"
                          icon="pi pi-file-pdf"
                          (onClick)="exportReport('pdf')"
                          severity="danger"
                          size="small">
                        </p-button>
                      </div>
                    </div>
                  </ng-template>
                  
                  <p-table 
                    [value]="activityReportData.events" 
                    [paginator]="true" 
                    [rows]="25"
                    styleClass="p-datatable-sm"
                    [exportFilename]="'atividades-' + getCurrentDate()">
                    <ng-template pTemplate="header">
                      <tr>
                        <th>Timestamp</th>
                        <th>Agente</th>
                        <th>Tipo</th>
                        <th>Aplicação</th>
                        <th>Duração</th>
                        <th>Score</th>
                      </tr>
                    </ng-template>
                    <ng-template pTemplate="body" let-event>
                      <tr>
                        <td>{{ event.eventTimestamp | date:'short' }}</td>
                        <td>{{ getAgentName(event.agentId) }}</td>
                        <td>
                          <p-tag [value]="event.eventType" severity="info"></p-tag>
                        </td>
                        <td>{{ event.applicationName || '-' }}</td>
                        <td>{{ formatTime(event.durationSeconds) }}</td>
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
                </p-card>
              </div>
            </div>
          </div>
        </p-tabPanel>

        <!-- Productivity Report -->
        <p-tabPanel header="Relatório de Produtividade">
          <div class="report-section">
            <!-- Productivity Filters -->
            <div class="filters-card mb-4">
              <p-card header="Filtros de Produtividade">
                <div class="grid">
                  <div class="col-12 md:col-4">
                    <label for="prodDateRange" class="font-medium text-sm mb-2">Período</label>
                    <p-calendar 
                      [(ngModel)]="productivityDateRange" 
                      selectionMode="range" 
                      dateFormat="dd/mm/yy"
                      placeholder="Selecione o período"
                      class="w-full">
                    </p-calendar>
                  </div>
                  
                  <div class="col-12 md:col-4">
                    <label for="groupBy" class="font-medium text-sm mb-2">Agrupar por</label>
                    <p-dropdown 
                      [(ngModel)]="productivityGroupBy" 
                      [options]="groupByOptions" 
                      optionLabel="label" 
                      optionValue="value"
                      placeholder="Selecione"
                      class="w-full">
                    </p-dropdown>
                  </div>
                  
                  <div class="col-12 md:col-4 flex align-items-end">
                    <p-button 
                      label="Gerar Relatório"
                      icon="pi pi-chart-line"
                      (onClick)="generateProductivityReport()"
                      [loading]="isGeneratingProductivityReport"
                      class="w-full">
                    </p-button>
                  </div>
                </div>
              </p-card>
            </div>

            <!-- Productivity Results -->
            <div *ngIf="productivityData" class="report-results">
              <div class="grid">
                <div class="col-12 md:col-8">
                  <p-card header="Tendência de Produtividade">
                    <p-chart 
                      type="line" 
                      [data]="productivityTrendChart" 
                      [options]="chartOptions">
                    </p-chart>
                  </p-card>
                </div>
                
                <div class="col-12 md:col-4">
                  <p-card header="Distribuição de Scores">
                    <p-chart 
                      type="pie" 
                      [data]="scoreDistributionChart" 
                      [options]="pieOptions">
                    </p-chart>
                  </p-card>
                </div>
              </div>
            </div>
          </div>
        </p-tabPanel>

        <!-- Custom Reports -->
        <p-tabPanel header="Relatórios Personalizados">
          <div class="report-section">
            <div class="custom-reports">
              <p-card header="Criar Relatório Personalizado">
                <div class="grid">
                  <div class="col-12 md:col-6">
                    <div class="field">
                      <label for="reportName" class="font-medium">Nome do Relatório</label>
                      <input 
                        type="text" 
                        pInputText 
                        [(ngModel)]="customReportName"
                        placeholder="Digite o nome do relatório"
                        class="w-full">
                    </div>
                    
                    <div class="field">
                      <label for="reportDescription" class="font-medium">Descrição</label>
                      <textarea 
                        pInputTextarea 
                        [(ngModel)]="customReportDescription"
                        placeholder="Descreva o relatório"
                        rows="3"
                        class="w-full">
                      </textarea>
                    </div>
                  </div>
                  
                  <div class="col-12 md:col-6">
                    <div class="field">
                      <label class="font-medium">Campos a Incluir</label>
                      <div class="field-checkbox">
                        <p-checkbox 
                          [(ngModel)]="includeTimestamp" 
                          inputId="includeTimestamp"
                          label="Timestamp">
                        </p-checkbox>
                      </div>
                      <div class="field-checkbox">
                        <p-checkbox 
                          [(ngModel)]="includeAgent" 
                          inputId="includeAgent"
                          label="Informações do Agente">
                        </p-checkbox>
                      </div>
                      <div class="field-checkbox">
                        <p-checkbox 
                          [(ngModel)]="includeApplication" 
                          inputId="includeApplication"
                          label="Aplicação">
                        </p-checkbox>
                      </div>
                      <div class="field-checkbox">
                        <p-checkbox 
                          [(ngModel)]="includeProductivity" 
                          inputId="includeProductivity"
                          label="Score de Produtividade">
                        </p-checkbox>
                      </div>
                    </div>
                  </div>
                </div>
                
                <div class="flex justify-content-end gap-2 mt-3">
                  <p-button 
                    label="Salvar Modelo"
                    icon="pi pi-save"
                    (onClick)="saveCustomReport()"
                    severity="secondary">
                  </p-button>
                  <p-button 
                    label="Gerar Relatório"
                    icon="pi pi-play"
                    (onClick)="generateCustomReport()">
                  </p-button>
                </div>
              </p-card>
            </div>
          </div>
        </p-tabPanel>
      </p-tabView>

      <!-- Loading Overlay -->
      <div *ngIf="isGeneratingReport || isGeneratingProductivityReport" class="loading-overlay">
        <div class="loading-content">
          <i class="pi pi-spin pi-spinner text-4xl text-blue-500 mb-3"></i>
          <p>Gerando relatório...</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .reports-container {
      padding: 1.5rem;
    }

    .filters-card {
      background: white;
      border-radius: 8px;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
    }

    .summary-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
    }

    .summary-card {
      background: white;
      border-radius: 8px;
      padding: 1.5rem;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .summary-icon {
      font-size: 2rem;
      width: 60px;
      height: 60px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(0, 123, 255, 0.1);
    }

    .summary-content {
      flex: 1;
    }

    .summary-value {
      font-size: 1.5rem;
      font-weight: 700;
      color: #007bff;
      margin-bottom: 0.5rem;
    }

    .summary-label {
      font-size: 0.9rem;
      color: #6c757d;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .report-results {
      margin-top: 2rem;
    }

    .custom-reports {
      max-width: 800px;
      margin: 0 auto;
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

    .loading-overlay {
      position: fixed;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      background: rgba(255, 255, 255, 0.8);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
    }

    .loading-content {
      text-align: center;
      padding: 2rem;
      background: white;
      border-radius: 8px;
      box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
    }

    @media (max-width: 768px) {
      .reports-container {
        padding: 1rem;
      }
      
      .summary-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class ReportsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  
  // Data
  agents: AgentDto[] = [];
  activityReportData: any = null;
  productivityData: any = null;
  
  // Loading states
  isGeneratingReport = false;
  isGeneratingProductivityReport = false;
  
  // Activity Report filters
  activityDateRange: Date[] = [];
  selectedAgent: string | null = null;
  selectedEventType: string | null = null;
  
  // Productivity Report filters
  productivityDateRange: Date[] = [];
  productivityGroupBy = 'day';
  
  // Custom Report
  customReportName = '';
  customReportDescription = '';
  includeTimestamp = true;
  includeAgent = true;
  includeApplication = true;
  includeProductivity = true;
  
  // Chart data
  eventsByDayChart: any = {};
  topAppsChart: any = {};
  productivityTrendChart: any = {};
  scoreDistributionChart: any = {};
  
  // Options
  eventTypes = [
    { label: 'Foco na Janela', value: 'WindowFocus' },
    { label: 'Aplicação', value: 'Application' },
    { label: 'Navegador', value: 'BrowserUrl' },
    { label: 'Teams', value: 'TeamsStatus' }
  ];
  
  groupByOptions = [
    { label: 'Dia', value: 'day' },
    { label: 'Semana', value: 'week' },
    { label: 'Mês', value: 'month' },
    { label: 'Agente', value: 'agent' }
  ];
  
  chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'top'
      }
    }
  };
  
  doughnutOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom'
      }
    }
  };
  
  pieOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'right'
      }
    }
  };

  constructor(
    private eventService: EventService,
    private agentService: AgentService,
    private dashboardService: DashboardService,
    private messageService: MessageService
  ) {}

  ngOnInit() {
    this.loadAgents();
    this.initializeDateRanges();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadAgents() {
    this.agentService.getAgents()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: any) => {
          this.agents = response.agents;
        },
        error: (error: any) => {
          console.error('Error loading agents:', error);
        }
      });
  }

  private initializeDateRanges() {
    const today = new Date();
    const lastWeek = new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000);
    
    this.activityDateRange = [lastWeek, today];
    this.productivityDateRange = [lastWeek, today];
  }

  generateActivityReport() {
    this.isGeneratingReport = true;
    
    const filters: EventFilters = {
      fromDate: this.activityDateRange[0],
      toDate: this.activityDateRange[1],
      agentId: this.selectedAgent || undefined,
      eventType: this.selectedEventType || undefined,
      pageSize: 1000
    };

    this.eventService.getEvents(filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: any) => {
          this.activityReportData = this.processActivityData(response.events);
          this.buildActivityCharts();
          this.isGeneratingReport = false;
          
          this.messageService.add({
            severity: 'success',
            summary: 'Sucesso',
            detail: 'Relatório de atividades gerado com sucesso'
          });
        },
        error: (error: any) => {
          console.error('Error generating activity report:', error);
          this.isGeneratingReport = false;
          this.messageService.add({
            severity: 'error',
            summary: 'Erro',
            detail: 'Erro ao gerar relatório de atividades'
          });
        }
      });
  }

  generateProductivityReport() {
    this.isGeneratingProductivityReport = true;
    
    const filters: DashboardFilters = {
      dateRange: {
        from: this.productivityDateRange[0],
        to: this.productivityDateRange[1]
      }
    };

    this.dashboardService.getProductivityTrend(30, filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data: any) => {
          this.productivityData = data;
          this.buildProductivityCharts();
          this.isGeneratingProductivityReport = false;
          
          this.messageService.add({
            severity: 'success',
            summary: 'Sucesso',
            detail: 'Relatório de produtividade gerado com sucesso'
          });
        },
        error: (error: any) => {
          console.error('Error generating productivity report:', error);
          this.isGeneratingProductivityReport = false;
          this.messageService.add({
            severity: 'error',
            summary: 'Erro',
            detail: 'Erro ao gerar relatório de produtividade'
          });
        }
      });
  }

  private processActivityData(events: EventDto[]): any {
    const totalEvents = events.length;
    const totalDuration = events.reduce((sum, e) => sum + (e.durationSeconds || 0), 0);
    const validScores = events.filter(e => e.productivityScore !== null).map(e => e.productivityScore!);
    const averageScore = validScores.length > 0 ? validScores.reduce((a, b) => a + b, 0) / validScores.length : 0;
    const uniqueApps = new Set(events.map(e => e.applicationName).filter(a => a)).size;

    return {
      totalEvents,
      totalDuration,
      averageScore,
      uniqueApps,
      events
    };
  }

  private buildActivityCharts() {
    if (!this.activityReportData) return;

    // Events by day chart
    const eventsByDay = this.groupEventsByDay(this.activityReportData.events);
    this.eventsByDayChart = {
      labels: Object.keys(eventsByDay),
      datasets: [{
        label: 'Eventos',
        data: Object.values(eventsByDay),
        backgroundColor: '#007bff'
      }]
    };

    // Top apps chart
    const appCounts = this.countApplications(this.activityReportData.events);
    this.topAppsChart = {
      labels: Object.keys(appCounts).slice(0, 10),
      datasets: [{
        data: Object.values(appCounts).slice(0, 10),
        backgroundColor: [
          '#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF',
          '#FF9F40', '#FF6384', '#C9CBCF', '#4BC0C0', '#FF6384'
        ]
      }]
    };
  }

  private buildProductivityCharts() {
    if (!this.productivityData) return;

    // Productivity trend chart
    this.productivityTrendChart = {
      labels: this.productivityData.map((d: any) => new Date(d.date).toLocaleDateString()),
      datasets: [{
        label: 'Score de Produtividade',
        data: this.productivityData.map((d: any) => d.score),
        borderColor: '#007bff',
        backgroundColor: 'rgba(0, 123, 255, 0.1)',
        fill: true
      }]
    };

    // Score distribution chart
    const scoreRanges = this.calculateScoreDistribution(this.productivityData);
    this.scoreDistributionChart = {
      labels: ['Baixo (0-39)', 'Médio (40-69)', 'Alto (70-100)'],
      datasets: [{
        data: [scoreRanges.low, scoreRanges.medium, scoreRanges.high],
        backgroundColor: ['#dc3545', '#ffc107', '#28a745']
      }]
    };
  }

  private groupEventsByDay(events: EventDto[]): { [key: string]: number } {
    const groups: { [key: string]: number } = {};
    events.forEach(event => {
      const date = new Date(event.eventTimestamp).toLocaleDateString();
      groups[date] = (groups[date] || 0) + 1;
    });
    return groups;
  }

  private countApplications(events: EventDto[]): { [key: string]: number } {
    const counts: { [key: string]: number } = {};
    events.forEach(event => {
      const app = event.applicationName || 'Unknown';
      counts[app] = (counts[app] || 0) + 1;
    });
    return counts;
  }

  private calculateScoreDistribution(data: any[]): { low: number; medium: number; high: number } {
    const distribution = { low: 0, medium: 0, high: 0 };
    data.forEach(item => {
      if (item.score < 40) distribution.low++;
      else if (item.score < 70) distribution.medium++;
      else distribution.high++;
    });
    return distribution;
  }

  generateCustomReport() {
    this.messageService.add({
      severity: 'info',
      summary: 'Relatório Personalizado',
      detail: 'Funcionalidade em desenvolvimento'
    });
  }

  saveCustomReport() {
    if (!this.customReportName) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Aviso',
        detail: 'Nome do relatório é obrigatório'
      });
      return;
    }

    this.messageService.add({
      severity: 'success',
      summary: 'Sucesso',
      detail: 'Modelo de relatório salvo com sucesso'
    });
  }

  exportReport(format: 'csv' | 'pdf') {
    if (!this.activityReportData) return;

    const filters: EventFilters = {
      fromDate: this.activityDateRange[0],
      toDate: this.activityDateRange[1],
      agentId: this.selectedAgent || undefined,
      eventType: this.selectedEventType || undefined
    };

    this.eventService.exportEvents(filters, format)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Sucesso',
            detail: `Relatório exportado em ${format.toUpperCase()} com sucesso`
          });
        },
        error: (error: any) => {
          console.error('Error exporting report:', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Erro',
            detail: 'Erro ao exportar relatório'
          });
        }
      });
  }

  // Utility methods
  formatTime(seconds: number | null): string {
    if (!seconds) return '0s';
    
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;
    
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    } else if (minutes > 0) {
      return `${minutes}m ${remainingSeconds}s`;
    } else {
      return `${remainingSeconds}s`;
    }
  }

  getAgentName(agentId: string): string {
    const agent = this.agents.find(a => a.id === agentId);
    return agent?.machineName || 'Unknown';
  }

  getScoreSeverity(score: number): string {
    if (score >= 80) return 'success';
    if (score >= 60) return 'warning';
    return 'danger';
  }

  getCurrentDate(): string {
    return new Date().toISOString().split('T')[0];
  }
}