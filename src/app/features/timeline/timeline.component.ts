import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

// PrimeNG Components
import { TimelineModule } from 'primeng/timeline';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { SkeletonModule } from 'primeng/skeleton';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';
import { OverlayPanelModule } from 'primeng/overlaypanel';
import { ImageModule } from 'primeng/image';
import { TooltipModule } from 'primeng/tooltip';

// Services
import { EventService } from '../../core/services/event.service';
import { AgentService } from '../../core/services/agent.service';

// Models
import { EventDto, EventFilters, EventType } from '../../shared/models/event-dto';
import { AgentDto } from '../../shared/models/agent-dto';

@Component({
  selector: 'app-timeline',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TimelineModule,
    CardModule,
    TableModule,
    TagModule,
    ButtonModule,
    CalendarModule,
    DropdownModule,
    InputTextModule,
    PaginatorModule,
    SkeletonModule,
    AvatarModule,
    BadgeModule,
    OverlayPanelModule,
    ImageModule,
    TooltipModule
  ],
  template: `
    <div class="timeline-container">
      <!-- Header -->
      <div class="page-header">
        <h1>Timeline de Atividades</h1>
        <p>Visualize e filtre as atividades dos agentes em tempo real</p>
      </div>

      <!-- Filters -->
      <div class="filters-section mb-4">
        <div class="grid">
          <div class="col-12 md:col-3">
            <label for="dateRange" class="font-medium text-sm mb-2">Período</label>
            <p-calendar 
              [(ngModel)]="selectedDateRange" 
              selectionMode="range" 
              dateFormat="dd/mm/yy"
              placeholder="Selecione o período"
              (onSelect)="applyFilters()"
              [readonlyInput]="false">
            </p-calendar>
          </div>
          
          <div class="col-12 md:col-2">
            <label for="agentFilter" class="font-medium text-sm mb-2">Agente</label>
            <p-dropdown 
              [(ngModel)]="selectedAgent" 
              [options]="agents" 
              optionLabel="machineName" 
              optionValue="id"
              placeholder="Todos"
              [showClear]="true"
              (onChange)="applyFilters()">
            </p-dropdown>
          </div>
          
          <div class="col-12 md:col-2">
            <label for="eventType" class="font-medium text-sm mb-2">Tipo</label>
            <p-dropdown 
              [(ngModel)]="selectedEventType" 
              [options]="eventTypes" 
              optionLabel="label" 
              optionValue="value"
              placeholder="Todos"
              [showClear]="true"
              (onChange)="applyFilters()">
            </p-dropdown>
          </div>
          
          <div class="col-12 md:col-2">
            <label for="application" class="font-medium text-sm mb-2">Aplicação</label>
            <p-dropdown 
              [(ngModel)]="selectedApplication" 
              [options]="applications" 
              optionLabel="name" 
              optionValue="name"
              placeholder="Todas"
              [showClear]="true"
              (onChange)="applyFilters()">
            </p-dropdown>
          </div>
          
          <div class="col-12 md:col-2">
            <label for="search" class="font-medium text-sm mb-2">Buscar</label>
            <input 
              type="text" 
              pInputText 
              [(ngModel)]="searchQuery"
              placeholder="Buscar..."
              (keyup.enter)="applyFilters()"
              class="w-full">
          </div>
          
          <div class="col-12 md:col-1 flex align-items-end">
            <p-button 
              icon="pi pi-search" 
              (onClick)="applyFilters()"
              [loading]="isLoading"
              class="w-full">
            </p-button>
          </div>
        </div>
      </div>

      <!-- View Toggle -->
      <div class="view-toggle mb-4">
        <p-button 
          [outlined]="viewMode !== 'timeline'" 
          [raised]="viewMode === 'timeline'"
          label="Timeline" 
          icon="pi pi-clock"
          (onClick)="viewMode = 'timeline'"
          class="mr-2">
        </p-button>
        <p-button 
          [outlined]="viewMode !== 'table'" 
          [raised]="viewMode === 'table'"
          label="Tabela" 
          icon="pi pi-table"
          (onClick)="viewMode = 'table'">
        </p-button>
      </div>

      <!-- Loading State -->
      <div *ngIf="isLoading" class="loading-container">
        <div class="grid">
          <div class="col-12" *ngFor="let item of [1,2,3,4,5]">
            <p-skeleton height="100px" class="mb-3"></p-skeleton>
          </div>
        </div>
      </div>

      <!-- Timeline View -->
      <div *ngIf="!isLoading && viewMode === 'timeline'" class="timeline-view">
        <p-timeline 
          [value]="events" 
          align="alternate"
          styleClass="customized-timeline">
          <ng-template pTemplate="marker" let-event>
            <span class="custom-marker" [style.backgroundColor]="getEventColor(event.eventType)">
              <i [class]="getEventIcon(event.eventType)"></i>
            </span>
          </ng-template>
          <ng-template pTemplate="content" let-event>
            <p-card [style]="{'margin-top': '2rem'}">
              <ng-template pTemplate="header">
                <div class="flex justify-content-between align-items-center">
                  <div>
                    <h4 class="mb-1">{{ event.applicationName || 'Sistema' }}</h4>
                    <small class="text-muted">{{ event.eventTimestamp | date:'medium' }}</small>
                  </div>
                  <div class="flex gap-2">
                    <p-tag 
                      [value]="event.eventType" 
                      [severity]="getEventSeverity(event.eventType)">
                    </p-tag>
                    <p-tag 
                      *ngIf="event.productivityScore !== null && event.productivityScore !== undefined"
                      [value]="event.productivityScore | number:'1.1-1'" 
                      [severity]="getScoreSeverity(event.productivityScore)">
                    </p-tag>
                  </div>
                </div>
              </ng-template>
              
              <div class="event-content">
                <div *ngIf="event.windowTitle" class="mb-2">
                  <strong>Título:</strong> {{ event.windowTitle }}
                </div>
                <div *ngIf="event.url" class="mb-2">
                  <strong>URL:</strong> 
                  <a [href]="event.url" target="_blank" class="text-blue-500">
                    {{ event.url | slice:0:80 }}{{ event.url.length > 80 ? '...' : '' }}
                  </a>
                </div>
                <div *ngIf="event.processName" class="mb-2">
                  <strong>Processo:</strong> {{ event.processName }}
                  <span *ngIf="event.processId" class="text-muted">({{ event.processId }})</span>
                </div>
                <div *ngIf="event.durationSeconds" class="mb-2">
                  <strong>Duração:</strong> {{ formatDuration(event.durationSeconds) }}
                </div>
                <div *ngIf="event.screenshotPath" class="mb-2">
                  <strong>Screenshot:</strong>
                  <p-button 
                    label="Visualizar" 
                    icon="pi pi-eye" 
                    size="small"
                    (onClick)="viewScreenshot(event.screenshotPath)"
                    class="ml-2">
                  </p-button>
                </div>
              </div>
            </p-card>
          </ng-template>
        </p-timeline>
      </div>

      <!-- Table View -->
      <div *ngIf="!isLoading && viewMode === 'table'" class="table-view">
        <p-table 
          [value]="events" 
          [paginator]="true" 
          [rows]="pageSize"
          [totalRecords]="totalRecords"
          [lazy]="true"
          (onLazyLoad)="loadEventsLazy($event)"
          [loading]="isLoading"
          styleClass="p-datatable-sm"
          [globalFilterFields]="['applicationName', 'windowTitle', 'eventType', 'processName']">
          
          <ng-template pTemplate="header">
            <tr>
              <th pSortableColumn="eventTimestamp">
                Timestamp
                <p-sortIcon field="eventTimestamp"></p-sortIcon>
              </th>
              <th pSortableColumn="eventType">
                Tipo
                <p-sortIcon field="eventType"></p-sortIcon>
              </th>
              <th pSortableColumn="applicationName">
                Aplicação
                <p-sortIcon field="applicationName"></p-sortIcon>
              </th>
              <th>Título/URL</th>
              <th>Processo</th>
              <th pSortableColumn="durationSeconds">
                Duração
                <p-sortIcon field="durationSeconds"></p-sortIcon>
              </th>
              <th pSortableColumn="productivityScore">
                Score
                <p-sortIcon field="productivityScore"></p-sortIcon>
              </th>
              <th>Ações</th>
            </tr>
          </ng-template>
          
          <ng-template pTemplate="body" let-event>
            <tr>
              <td>{{ event.eventTimestamp | date:'short' }}</td>
              <td>
                <p-tag 
                  [value]="event.eventType" 
                  [severity]="getEventSeverity(event.eventType)">
                </p-tag>
              </td>
              <td>{{ event.applicationName || '-' }}</td>
              <td>
                <div *ngIf="event.windowTitle" class="mb-1">
                  {{ event.windowTitle | slice:0:50 }}{{ event.windowTitle.length > 50 ? '...' : '' }}
                </div>
                <div *ngIf="event.url" class="text-sm text-muted">
                  {{ event.url | slice:0:40 }}{{ event.url.length > 40 ? '...' : '' }}
                </div>
              </td>
              <td>
                <div *ngIf="event.processName">
                  {{ event.processName }}
                  <span *ngIf="event.processId" class="text-muted text-sm">({{ event.processId }})</span>
                </div>
              </td>
              <td>{{ formatDuration(event.durationSeconds) }}</td>
              <td>
                <p-tag 
                  *ngIf="event.productivityScore !== null && event.productivityScore !== undefined"
                  [value]="event.productivityScore | number:'1.1-1'" 
                  [severity]="getScoreSeverity(event.productivityScore)">
                </p-tag>
              </td>
              <td>
                <p-button 
                  *ngIf="event.screenshotPath"
                  icon="pi pi-eye" 
                  size="small"
                  severity="secondary"
                  (onClick)="viewScreenshot(event.screenshotPath)"
                  pTooltip="Visualizar Screenshot">
                </p-button>
              </td>
            </tr>
          </ng-template>
          
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="8" class="text-center">Nenhuma atividade encontrada</td>
            </tr>
          </ng-template>
        </p-table>
      </div>

      <!-- Pagination for Timeline View -->
      <div *ngIf="!isLoading && viewMode === 'timeline' && totalRecords > pageSize" class="mt-4">
        <p-paginator 
          [first]="first" 
          [rows]="pageSize" 
          [totalRecords]="totalRecords"
          [rowsPerPageOptions]="[10, 25, 50, 100]"
          (onPageChange)="onPageChange($event)">
        </p-paginator>
      </div>

      <!-- Error State -->
      <div *ngIf="error" class="error-container">
        <p-card>
          <div class="text-center">
            <i class="pi pi-exclamation-triangle text-red-500 text-4xl mb-3"></i>
            <h3>Erro ao carregar atividades</h3>
            <p>{{ error }}</p>
            <p-button 
              label="Tentar Novamente" 
              icon="pi pi-refresh" 
              (onClick)="loadEvents()">
            </p-button>
          </div>
        </p-card>
      </div>
    </div>

    <!-- Screenshot Modal -->
    <div *ngIf="selectedScreenshot" class="screenshot-modal" (click)="closeScreenshot()">
      <div class="screenshot-content" (click)="$event.stopPropagation()">
        <img [src]="selectedScreenshot" alt="Screenshot" class="w-full">
        <p-button 
          icon="pi pi-times" 
          severity="secondary"
          (onClick)="closeScreenshot()"
          class="screenshot-close">
        </p-button>
      </div>
    </div>
  `,
  styles: [`
    .timeline-container {
      padding: 1.5rem;
    }

    .filters-section {
      background: white;
      border-radius: 8px;
      padding: 1.5rem;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
    }

    .view-toggle {
      text-align: center;
    }

    .loading-container {
      padding: 2rem;
    }

    .custom-marker {
      display: flex;
      width: 2rem;
      height: 2rem;
      align-items: center;
      justify-content: center;
      color: white;
      border-radius: 50%;
      z-index: 1;
    }

    .customized-timeline .p-timeline-event-content {
      padding: 0 1rem;
    }

    .event-content {
      font-size: 0.9rem;
    }

    .event-content strong {
      font-weight: 600;
    }

    .screenshot-modal {
      position: fixed;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      background: rgba(0, 0, 0, 0.8);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
    }

    .screenshot-content {
      position: relative;
      max-width: 90%;
      max-height: 90%;
      background: white;
      border-radius: 8px;
      padding: 1rem;
    }

    .screenshot-close {
      position: absolute;
      top: 10px;
      right: 10px;
    }

    .error-container {
      padding: 2rem;
    }

    @media (max-width: 768px) {
      .timeline-container {
        padding: 1rem;
      }
      
      .view-toggle {
        text-align: left;
      }
      
      .view-toggle .p-button {
        display: block;
        width: 100%;
        margin-bottom: 0.5rem;
      }
    }
  `]
})
export class TimelineComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  
  events: EventDto[] = [];
  agents: AgentDto[] = [];
  applications: { name: string }[] = [];
  isLoading = false;
  error: string | null = null;
  viewMode: 'timeline' | 'table' = 'timeline';
  
  // Pagination
  first = 0;
  pageSize = 25;
  totalRecords = 0;
  
  // Filters
  selectedDateRange: Date[] = [];
  selectedAgent: string | null = null;
  selectedEventType: string | null = null;
  selectedApplication: string | null = null;
  searchQuery = '';
  
  // Screenshot modal
  selectedScreenshot: string | null = null;
  
  eventTypes = [
    { label: 'Foco na Janela', value: EventType.WindowFocus },
    { label: 'Fechar Janela', value: EventType.WindowClose },
    { label: 'Abrir Janela', value: EventType.WindowOpen },
    { label: 'URL do Navegador', value: EventType.BrowserUrl },
    { label: 'Título do Navegador', value: EventType.BrowserTitle },
    { label: 'Status Teams', value: EventType.TeamsStatus },
    { label: 'Chamada Teams', value: EventType.TeamsCall },
    { label: 'Chat Teams', value: EventType.TeamsChat },
    { label: 'Iniciar Processo', value: EventType.ProcessStart },
    { label: 'Parar Processo', value: EventType.ProcessStop },
    { label: 'Screenshot', value: EventType.Screenshot },
    { label: 'Inativo', value: EventType.Idle },
    { label: 'Ativo', value: EventType.Active },
    { label: 'Atividade do Teclado', value: EventType.KeyboardActivity },
    { label: 'Atividade do Mouse', value: EventType.MouseActivity },
    { label: 'Aplicação', value: EventType.Application },
    { label: 'Evento do Sistema', value: EventType.SystemEvent }
  ];

  constructor(
    private eventService: EventService,
    private agentService: AgentService
  ) {
    this.initializeDateRange();
  }

  ngOnInit() {
    this.loadAgents();
    this.loadApplications();
    this.loadEvents();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeDateRange() {
    const today = new Date();
    const yesterday = new Date(today.getTime() - 24 * 60 * 60 * 1000);
    this.selectedDateRange = [yesterday, today];
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

  private loadApplications() {
    // Load unique applications from recent events
    this.eventService.getEvents({ pageSize: 1000 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: any) => {
          const uniqueApps = new Set<string>();
          response.events.forEach((event: any) => {
            if (event.applicationName) {
              uniqueApps.add(event.applicationName);
            }
          });
          this.applications = Array.from(uniqueApps).map(name => ({ name }));
        },
        error: (error: any) => {
          console.error('Error loading applications:', error);
        }
      });
  }

  loadEvents() {
    this.isLoading = true;
    this.error = null;
    
    const filters = this.buildFilters();
    
    this.eventService.getEvents(filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: any) => {
          this.events = response.events;
          this.totalRecords = response.totalCount;
          this.isLoading = false;
        },
        error: (error: any) => {
          console.error('Error loading events:', error);
          this.error = 'Erro ao carregar eventos';
          this.isLoading = false;
        }
      });
  }

  loadEventsLazy(event: any) {
    this.first = event.first;
    this.pageSize = event.rows;
    this.loadEvents();
  }

  private buildFilters(): EventFilters {
    const filters: EventFilters = {
      pageSize: this.pageSize,
      currentPage: Math.floor(this.first / this.pageSize) + 1
    };

    if (this.selectedDateRange.length === 2) {
      filters.fromDate = this.selectedDateRange[0];
      filters.toDate = this.selectedDateRange[1];
    }

    if (this.selectedAgent) {
      filters.agentId = this.selectedAgent;
    }

    if (this.selectedEventType) {
      filters.eventType = this.selectedEventType;
    }

    if (this.selectedApplication) {
      filters.applicationName = this.selectedApplication;
    }

    return filters;
  }

  // Event handlers
  applyFilters() {
    this.first = 0;
    this.loadEvents();
  }

  onPageChange(event: any) {
    this.first = event.first;
    this.pageSize = event.rows;
    this.loadEvents();
  }

  viewScreenshot(screenshotPath: string) {
    this.selectedScreenshot = screenshotPath;
  }

  closeScreenshot() {
    this.selectedScreenshot = null;
  }

  // Utility methods
  formatDuration(seconds: number | null): string {
    if (!seconds) return '-';
    
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;
    
    if (hours > 0) {
      return `${hours}h ${minutes}m ${remainingSeconds}s`;
    } else if (minutes > 0) {
      return `${minutes}m ${remainingSeconds}s`;
    } else {
      return `${remainingSeconds}s`;
    }
  }

  getEventColor(eventType: string): string {
    const colors: { [key: string]: string } = {
      [EventType.WindowFocus]: '#007bff',
      [EventType.WindowClose]: '#dc3545',
      [EventType.WindowOpen]: '#28a745',
      [EventType.BrowserUrl]: '#ffc107',
      [EventType.BrowserTitle]: '#fd7e14',
      [EventType.TeamsStatus]: '#6f42c1',
      [EventType.TeamsCall]: '#e83e8c',
      [EventType.TeamsChat]: '#20c997',
      [EventType.ProcessStart]: '#17a2b8',
      [EventType.ProcessStop]: '#6c757d',
      [EventType.Screenshot]: '#f8f9fa',
      [EventType.Idle]: '#adb5bd',
      [EventType.Active]: '#28a745',
      [EventType.KeyboardActivity]: '#007bff',
      [EventType.MouseActivity]: '#fd7e14',
      [EventType.Application]: '#6f42c1',
      [EventType.SystemEvent]: '#495057'
    };
    return colors[eventType] || '#6c757d';
  }

  getEventIcon(eventType: string): string {
    const icons: { [key: string]: string } = {
      [EventType.WindowFocus]: 'pi pi-window-maximize',
      [EventType.WindowClose]: 'pi pi-window-minimize',
      [EventType.WindowOpen]: 'pi pi-external-link',
      [EventType.BrowserUrl]: 'pi pi-globe',
      [EventType.BrowserTitle]: 'pi pi-bookmark',
      [EventType.TeamsStatus]: 'pi pi-users',
      [EventType.TeamsCall]: 'pi pi-phone',
      [EventType.TeamsChat]: 'pi pi-comments',
      [EventType.ProcessStart]: 'pi pi-play',
      [EventType.ProcessStop]: 'pi pi-stop',
      [EventType.Screenshot]: 'pi pi-camera',
      [EventType.Idle]: 'pi pi-pause',
      [EventType.Active]: 'pi pi-circle-fill',
      [EventType.KeyboardActivity]: 'pi pi-keyboard',
      [EventType.MouseActivity]: 'pi pi-mouse',
      [EventType.Application]: 'pi pi-desktop',
      [EventType.SystemEvent]: 'pi pi-cog'
    };
    return icons[eventType] || 'pi pi-circle';
  }

  getEventSeverity(eventType: string): string {
    const severities: { [key: string]: string } = {
      [EventType.WindowFocus]: 'info',
      [EventType.WindowClose]: 'danger',
      [EventType.WindowOpen]: 'success',
      [EventType.BrowserUrl]: 'warning',
      [EventType.BrowserTitle]: 'warning',
      [EventType.TeamsStatus]: 'info',
      [EventType.TeamsCall]: 'success',
      [EventType.TeamsChat]: 'info',
      [EventType.ProcessStart]: 'success',
      [EventType.ProcessStop]: 'danger',
      [EventType.Screenshot]: 'secondary',
      [EventType.Idle]: 'secondary',
      [EventType.Active]: 'success',
      [EventType.KeyboardActivity]: 'info',
      [EventType.MouseActivity]: 'info',
      [EventType.Application]: 'info',
      [EventType.SystemEvent]: 'secondary'
    };
    return severities[eventType] || 'secondary';
  }

  getScoreSeverity(score: number): string {
    if (score >= 80) return 'success';
    if (score >= 60) return 'warning';
    return 'danger';
  }
}