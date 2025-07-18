export interface AgentDto {
  id: string;
  machineId: string;
  machineName: string;
  userName: string;
  osVersion?: string;
  agentVersion?: string;
  lastSeen: Date;
  status: AgentStatus;
  createdAt: Date;
  updatedAt: Date;
}

export interface AgentResponse {
  agents: AgentDto[];
  totalCount: number;
  pageSize: number;
  currentPage: number;
  totalPages: number;
}

export interface AgentFilters {
  machineId?: string;
  machineName?: string;
  userName?: string;
  status?: AgentStatus;
  pageSize?: number;
  currentPage?: number;
}

export enum AgentStatus {
  Active = 'Active',
  Inactive = 'Inactive',
  Offline = 'Offline',
  Maintenance = 'Maintenance',
  Disabled = 'Disabled'
}

export interface AgentMetrics {
  totalAgents: number;
  activeAgents: number;
  offlineAgents: number;
  inactiveAgents: number;
  maintenanceAgents: number;
  disabledAgents: number;
}

export interface AgentHeartbeat {
  agentId: string;
  timestamp: Date;
  systemInfo?: {
    cpuUsage?: number;
    memoryUsage?: number;
    diskUsage?: number;
  };
}