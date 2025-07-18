import { Injectable } from '@angular/core';
import { Observable, combineLatest, map, timer } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';
import { ApiService } from './api.service';
import { EventService } from './event.service';
import { AgentService } from './agent.service';
import { 
  DashboardMetrics, 
  ApplicationMetric, 
  ProductivityTrend, 
  AgentStatusMetric, 
  ActivityTimelineItem,
  DashboardFilters,
  ChartData
} from '../../shared/models/dashboard-dto';
import { EventDto } from '../../shared/models/event-dto';
import { AgentDto } from '../../shared/models/agent-dto';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly baseEndpoint = '/dashboard';

  constructor(
    private apiService: ApiService,
    private eventService: EventService,
    private agentService: AgentService
  ) {}

  getDashboardMetrics(filters?: DashboardFilters): Observable<DashboardMetrics> {
    return this.apiService.get<DashboardMetrics>(`${this.baseEndpoint}/metrics`, filters);
  }

  // Real-time dashboard updates
  getDashboardMetricsStream(filters?: DashboardFilters): Observable<DashboardMetrics> {
    return timer(0, 60000).pipe( // Update every minute
      switchMap(() => this.getDashboardMetrics(filters)),
      catchError((error: any) => {
        console.error('Dashboard metrics error:', error);
        return this.buildDashboardMetrics(filters);
      })
    );
  }

  // Build dashboard metrics from individual services if API endpoint is not available
  private buildDashboardMetrics(filters?: DashboardFilters): Observable<DashboardMetrics> {
    const dateRange = filters?.dateRange || {
      from: new Date(Date.now() - 24 * 60 * 60 * 1000), // Last 24 hours
      to: new Date()
    };

    return combineLatest([
      this.agentService.getAgents(),
      this.eventService.getEventsByDateRange(dateRange.from, dateRange.to),
      this.agentService.getAgentMetrics()
    ]).pipe(
      map(([agentResponse, events, agentMetrics]: [any, any, any]) => {
        return {
          totalAgents: agentResponse.totalCount,
          activeAgents: agentResponse.agents.filter((a: any) => a.status === 'Active').length,
          offlineAgents: agentResponse.agents.filter((a: any) => a.status === 'Offline').length,
          totalEvents: events.length,
          eventsToday: events.filter((e: any) => this.isToday(new Date(e.eventTimestamp))).length,
          averageProductivityScore: this.calculateAverageScore(events),
          topApplications: this.buildTopApplications(events),
          productivityTrend: this.buildProductivityTrend(events),
          agentStatusDistribution: this.buildAgentStatusDistribution(agentResponse.agents),
          activityTimeline: this.buildActivityTimeline(events)
        };
      })
    );
  }

  getTopApplications(limit: number = 10, filters?: DashboardFilters): Observable<ApplicationMetric[]> {
    return this.apiService.get<ApplicationMetric[]>(`${this.baseEndpoint}/top-applications`, {
      limit,
      ...filters
    });
  }

  getProductivityTrend(days: number = 7, filters?: DashboardFilters): Observable<ProductivityTrend[]> {
    return this.apiService.get<ProductivityTrend[]>(`${this.baseEndpoint}/productivity-trend`, {
      days,
      ...filters
    });
  }

  getAgentStatusDistribution(filters?: DashboardFilters): Observable<AgentStatusMetric[]> {
    return this.apiService.get<AgentStatusMetric[]>(`${this.baseEndpoint}/agent-status-distribution`, filters);
  }

  getActivityTimeline(limit: number = 50, filters?: DashboardFilters): Observable<ActivityTimelineItem[]> {
    return this.apiService.get<ActivityTimelineItem[]>(`${this.baseEndpoint}/activity-timeline`, {
      limit,
      ...filters
    });
  }

  // Chart data generators
  getProductivityChartData(filters?: DashboardFilters): Observable<ChartData> {
    return this.getProductivityTrend(7, filters).pipe(
      map((trend: any) => ({
        labels: trend.map((t: any) => new Date(t.date).toLocaleDateString('pt-BR')),
        datasets: [
          {
            label: 'Score de Produtividade',
            data: trend.map((t: any) => t.score),
            borderColor: '#007bff',
            backgroundColor: 'rgba(0, 123, 255, 0.1)',
            fill: true
          },
          {
            label: 'Número de Eventos',
            data: trend.map((t: any) => t.events),
            borderColor: '#28a745',
            backgroundColor: 'rgba(40, 167, 69, 0.1)',
            fill: true
          }
        ]
      }))
    );
  }

  getApplicationUsageChartData(filters?: DashboardFilters): Observable<ChartData> {
    return this.getTopApplications(10, filters).pipe(
      map((apps: any) => ({
        labels: apps.map((app: any) => app.name),
        datasets: [
          {
            label: 'Tempo de Uso (horas)',
            data: apps.map((app: any) => Math.round(app.timeSpent / 3600)), // Convert to hours
            backgroundColor: [
              '#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF',
              '#FF9F40', '#FF6384', '#C9CBCF', '#4BC0C0', '#FF6384'
            ]
          }
        ]
      }))
    );
  }

  getAgentStatusChartData(filters?: DashboardFilters): Observable<ChartData> {
    return this.getAgentStatusDistribution(filters).pipe(
      map((distribution: any) => ({
        labels: distribution.map((d: any) => d.status),
        datasets: [
          {
            label: 'Agentes por Status',
            data: distribution.map((d: any) => d.count),
            backgroundColor: [
              '#28a745', // Active - Green
              '#ffc107', // Inactive - Yellow
              '#dc3545', // Offline - Red
              '#6c757d', // Maintenance - Gray
              '#343a40'  // Disabled - Dark
            ]
          }
        ]
      }))
    );
  }

  // Utility methods
  private isToday(date: Date): boolean {
    const today = new Date();
    return date.toDateString() === today.toDateString();
  }

  private calculateAverageScore(events: EventDto[]): number {
    const validScores = events
      .filter(e => e.productivityScore !== null && e.productivityScore !== undefined)
      .map(e => e.productivityScore!);
    
    if (validScores.length === 0) return 0;
    return Math.round((validScores.reduce((a, b) => a + b, 0) / validScores.length) * 100) / 100;
  }

  private buildTopApplications(events: EventDto[]): ApplicationMetric[] {
    const appMap = new Map<string, {
      usage: number;
      timeSpent: number;
      scores: number[];
    }>();

    events.forEach(event => {
      const app = event.applicationName || 'Unknown';
      const duration = event.durationSeconds || 0;
      const score = event.productivityScore || 0;

      if (!appMap.has(app)) {
        appMap.set(app, { usage: 0, timeSpent: 0, scores: [] });
      }

      const appData = appMap.get(app)!;
      appData.usage++;
      appData.timeSpent += duration;
      appData.scores.push(score);
    });

    const totalUsage = Array.from(appMap.values()).reduce((sum, app) => sum + app.usage, 0);

    return Array.from(appMap.entries())
      .map(([name, data]) => ({
        name,
        usage: data.usage,
        timeSpent: data.timeSpent,
        productivityScore: data.scores.length > 0 ? data.scores.reduce((a, b) => a + b, 0) / data.scores.length : 0,
        percentage: totalUsage > 0 ? (data.usage / totalUsage) * 100 : 0
      }))
      .sort((a, b) => b.usage - a.usage)
      .slice(0, 10);
  }

  private buildProductivityTrend(events: EventDto[]): ProductivityTrend[] {
    const trendMap = new Map<string, { scores: number[]; eventCount: number }>();

    events.forEach(event => {
      const date = new Date(event.eventTimestamp).toDateString();
      const score = event.productivityScore || 0;

      if (!trendMap.has(date)) {
        trendMap.set(date, { scores: [], eventCount: 0 });
      }

      const trendData = trendMap.get(date)!;
      trendData.scores.push(score);
      trendData.eventCount++;
    });

    return Array.from(trendMap.entries())
      .map(([dateStr, data]) => ({
        date: new Date(dateStr),
        score: data.scores.length > 0 ? data.scores.reduce((a, b) => a + b, 0) / data.scores.length : 0,
        events: data.eventCount
      }))
      .sort((a, b) => a.date.getTime() - b.date.getTime());
  }

  private buildAgentStatusDistribution(agents: AgentDto[]): AgentStatusMetric[] {
    const statusMap = new Map<string, number>();

    agents.forEach(agent => {
      const status = agent.status;
      statusMap.set(status, (statusMap.get(status) || 0) + 1);
    });

    const total = agents.length;

    return Array.from(statusMap.entries()).map(([status, count]) => ({
      status,
      count,
      percentage: total > 0 ? (count / total) * 100 : 0
    }));
  }

  private buildActivityTimeline(events: EventDto[]): ActivityTimelineItem[] {
    return events
      .sort((a, b) => new Date(b.eventTimestamp).getTime() - new Date(a.eventTimestamp).getTime())
      .slice(0, 50)
      .map(event => ({
        timestamp: new Date(event.eventTimestamp),
        eventType: event.eventType,
        applicationName: event.applicationName || 'Unknown',
        windowTitle: event.windowTitle || '',
        userName: '', // Would need to join with agent data
        machineName: '', // Would need to join with agent data
        productivityScore: event.productivityScore || 0
      }));
  }
}