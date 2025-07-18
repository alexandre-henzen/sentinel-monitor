import { Injectable } from '@angular/core';
import { Observable, timer } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { ApiService } from './api.service';
import { AgentDto, AgentResponse, AgentFilters, AgentMetrics, AgentHeartbeat } from '../../shared/models/agent-dto';

@Injectable({
  providedIn: 'root'
})
export class AgentService {
  private readonly baseEndpoint = '/agents';

  constructor(private apiService: ApiService) {}

  getAgents(filters?: AgentFilters): Observable<AgentResponse> {
    return this.apiService.get<AgentResponse>(this.baseEndpoint, filters);
  }

  getAgentById(id: string): Observable<AgentDto> {
    return this.apiService.get<AgentDto>(`${this.baseEndpoint}/${id}`);
  }

  registerAgent(agent: Partial<AgentDto>): Observable<AgentDto> {
    return this.apiService.post<AgentDto>(`${this.baseEndpoint}/register`, agent);
  }

  updateAgent(id: string, agent: Partial<AgentDto>): Observable<AgentDto> {
    return this.apiService.put<AgentDto>(`${this.baseEndpoint}/${id}`, agent);
  }

  deleteAgent(id: string): Observable<void> {
    return this.apiService.delete<void>(`${this.baseEndpoint}/${id}`);
  }

  updateAgentStatus(id: string, status: string): Observable<AgentDto> {
    return this.apiService.patch<AgentDto>(`${this.baseEndpoint}/${id}/status`, { status });
  }

  sendHeartbeat(heartbeat: AgentHeartbeat): Observable<void> {
    return this.apiService.post<void>(`${this.baseEndpoint}/heartbeat`, heartbeat);
  }

  getAgentMetrics(): Observable<AgentMetrics> {
    return this.apiService.get<AgentMetrics>(`${this.baseEndpoint}/metrics`);
  }

  // Real-time agent monitoring
  getAgentStatusStream(): Observable<AgentDto[]> {
    return timer(0, 30000).pipe( // Poll every 30 seconds
      switchMap(() => this.getAgents())
    ).pipe(
      map(response => response.agents)
    );
  }

  // Utility methods
  getActiveAgents(): Observable<AgentDto[]> {
    return this.getAgents({ status: 'Active' as any }).pipe(
      map(response => response.agents)
    );
  }

  getOfflineAgents(): Observable<AgentDto[]> {
    return this.getAgents({ status: 'Offline' as any }).pipe(
      map(response => response.agents)
    );
  }

  getAgentsByMachine(machineId: string): Observable<AgentDto[]> {
    return this.getAgents({ machineId }).pipe(
      map(response => response.agents)
    );
  }

  getAgentsByUser(userName: string): Observable<AgentDto[]> {
    return this.getAgents({ userName }).pipe(
      map(response => response.agents)
    );
  }

  // Search functionality
  searchAgents(query: string): Observable<AgentDto[]> {
    return this.getAgents().pipe(
      map(response => response.agents.filter(agent => 
        agent.machineName.toLowerCase().includes(query.toLowerCase()) ||
        agent.userName.toLowerCase().includes(query.toLowerCase()) ||
        agent.machineId.toLowerCase().includes(query.toLowerCase())
      ))
    );
  }

  // Health check
  checkAgentHealth(agentId: string): Observable<{ status: string; lastSeen: Date; isHealthy: boolean }> {
    return this.getAgentById(agentId).pipe(
      map(agent => {
        const now = new Date();
        const lastSeen = new Date(agent.lastSeen);
        const timeDiff = now.getTime() - lastSeen.getTime();
        const isHealthy = timeDiff < 5 * 60 * 1000; // 5 minutes threshold
        
        return {
          status: agent.status,
          lastSeen: agent.lastSeen,
          isHealthy
        };
      })
    );
  }

  // Batch operations
  updateMultipleAgentsStatus(agentIds: string[], status: string): Observable<{ updated: number; errors: any[] }> {
    return this.apiService.post<{ updated: number; errors: any[] }>(`${this.baseEndpoint}/batch-update-status`, {
      agentIds,
      status
    });
  }

  deleteMultipleAgents(agentIds: string[]): Observable<{ deleted: number; errors: any[] }> {
    return this.apiService.post<{ deleted: number; errors: any[] }>(`${this.baseEndpoint}/batch-delete`, {
      agentIds
    });
  }

  // Export functionality
  exportAgents(filters?: AgentFilters, format: 'csv' | 'json' | 'excel' = 'csv'): Observable<Blob> {
    const exportEndpoint = `${this.baseEndpoint}/export/${format}`;
    return this.apiService.downloadFile(exportEndpoint, `agents_export.${format}`);
  }

  // Agent configuration
  getAgentConfiguration(agentId: string): Observable<any> {
    return this.apiService.get<any>(`${this.baseEndpoint}/${agentId}/config`);
  }

  updateAgentConfiguration(agentId: string, config: any): Observable<void> {
    return this.apiService.put<void>(`${this.baseEndpoint}/${agentId}/config`, config);
  }

  // Agent updates
  checkForUpdates(agentId: string): Observable<{ hasUpdate: boolean; version: string; downloadUrl?: string }> {
    return this.apiService.get<{ hasUpdate: boolean; version: string; downloadUrl?: string }>(`/updates/check?agentId=${agentId}`);
  }

  getUpdateDownloadUrl(agentId: string, version: string): Observable<{ downloadUrl: string }> {
    return this.apiService.get<{ downloadUrl: string }>(`/updates/download?agentId=${agentId}&version=${version}`);
  }
}