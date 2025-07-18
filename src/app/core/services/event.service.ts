import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { EventDto, EventResponse, EventFilters } from '../../shared/models/event-dto';

@Injectable({
  providedIn: 'root'
})
export class EventService {
  private readonly baseEndpoint = '/events';

  constructor(private apiService: ApiService) {}

  getEvents(filters?: EventFilters): Observable<EventResponse> {
    return this.apiService.get<EventResponse>(this.baseEndpoint, filters);
  }

  getEventsNdjson(filters?: EventFilters): Observable<EventDto[]> {
    return this.apiService.getNdjson<EventDto>(`${this.baseEndpoint}/ndjson`, filters);
  }

  getEventById(id: string): Observable<EventDto> {
    return this.apiService.get<EventDto>(`${this.baseEndpoint}/${id}`);
  }

  createEvent(event: Partial<EventDto>): Observable<EventDto> {
    return this.apiService.post<EventDto>(this.baseEndpoint, event);
  }

  createEventsBatch(events: Partial<EventDto>[]): Observable<{ created: number; errors: any[] }> {
    return this.apiService.post<{ created: number; errors: any[] }>(`${this.baseEndpoint}/batch`, { events });
  }

  updateEvent(id: string, event: Partial<EventDto>): Observable<EventDto> {
    return this.apiService.put<EventDto>(`${this.baseEndpoint}/${id}`, event);
  }

  deleteEvent(id: string): Observable<void> {
    return this.apiService.delete<void>(`${this.baseEndpoint}/${id}`);
  }

  // Analytics and aggregation methods
  getEventsByDateRange(fromDate: Date, toDate: Date, agentId?: string): Observable<EventDto[]> {
    const filters: EventFilters = {
      fromDate,
      toDate,
      agentId
    };
    return this.getEvents(filters).pipe(
      map(response => response.events)
    );
  }

  getEventsByApplication(applicationName: string, filters?: EventFilters): Observable<EventDto[]> {
    const appFilters = { ...filters, applicationName };
    return this.getEvents(appFilters).pipe(
      map(response => response.events)
    );
  }

  getEventsByAgent(agentId: string, filters?: EventFilters): Observable<EventDto[]> {
    const agentFilters = { ...filters, agentId };
    return this.getEvents(agentFilters).pipe(
      map(response => response.events)
    );
  }

  getEventsByType(eventType: string, filters?: EventFilters): Observable<EventDto[]> {
    const typeFilters = { ...filters, eventType };
    return this.getEvents(typeFilters).pipe(
      map(response => response.events)
    );
  }

  getProductivityScoreRange(minScore: number, maxScore: number, filters?: EventFilters): Observable<EventDto[]> {
    const scoreFilters = { ...filters, minScore, maxScore };
    return this.getEvents(scoreFilters).pipe(
      map(response => response.events)
    );
  }

  // Real-time event stream (if supported by backend)
  getEventStream(filters?: EventFilters): Observable<EventDto> {
    // This would be implemented with Server-Sent Events or WebSockets
    // For now, returning a polling mechanism
    return new Observable(observer => {
      const pollInterval = setInterval(() => {
        this.getEvents({ ...filters, pageSize: 10, currentPage: 1 }).subscribe({
          next: (response) => {
            response.events.forEach(event => observer.next(event));
          },
          error: (error) => observer.error(error)
        });
      }, 5000); // Poll every 5 seconds

      return () => clearInterval(pollInterval);
    });
  }

  // Export methods
  exportEvents(filters?: EventFilters, format: 'csv' | 'json' | 'excel' = 'csv'): Observable<Blob> {
    const exportEndpoint = `${this.baseEndpoint}/export/${format}`;
    return this.apiService.downloadFile(exportEndpoint, `events_export.${format}`);
  }

  // Search methods
  searchEvents(query: string, filters?: EventFilters): Observable<EventDto[]> {
    const searchFilters = { ...filters, search: query };
    return this.getEvents(searchFilters).pipe(
      map(response => response.events)
    );
  }

  // Utility methods
  groupEventsByDate(events: EventDto[]): Map<string, EventDto[]> {
    return events.reduce((groups, event) => {
      const date = new Date(event.eventTimestamp).toDateString();
      if (!groups.has(date)) {
        groups.set(date, []);
      }
      groups.get(date)!.push(event);
      return groups;
    }, new Map<string, EventDto[]>());
  }

  groupEventsByApplication(events: EventDto[]): Map<string, EventDto[]> {
    return events.reduce((groups, event) => {
      const app = event.applicationName || 'Unknown';
      if (!groups.has(app)) {
        groups.set(app, []);
      }
      groups.get(app)!.push(event);
      return groups;
    }, new Map<string, EventDto[]>());
  }

  calculateAverageProductivityScore(events: EventDto[]): number {
    const validScores = events
      .filter(event => event.productivityScore !== null && event.productivityScore !== undefined)
      .map(event => event.productivityScore!);
    
    if (validScores.length === 0) return 0;
    
    const sum = validScores.reduce((acc, score) => acc + score, 0);
    return Math.round((sum / validScores.length) * 100) / 100;
  }
}