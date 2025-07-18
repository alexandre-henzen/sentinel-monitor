export interface EventDto {
  agentId: string;
  eventType: string;
  applicationName?: string;
  windowTitle?: string;
  url?: string;
  processName?: string;
  processId?: number;
  durationSeconds?: number;
  productivityScore?: number;
  eventTimestamp: Date;
  screenshotPath?: string;
  metadata?: Record<string, any>;
  createdAt: Date;
}

export interface EventResponse {
  events: EventDto[];
  totalCount: number;
  pageSize: number;
  currentPage: number;
  totalPages: number;
}

export interface EventFilters {
  agentId?: string;
  eventType?: string;
  applicationName?: string;
  fromDate?: Date;
  toDate?: Date;
  minScore?: number;
  maxScore?: number;
  pageSize?: number;
  currentPage?: number;
}

export enum EventType {
  WindowFocus = 'WindowFocus',
  WindowClose = 'WindowClose',
  WindowOpen = 'WindowOpen',
  BrowserUrl = 'BrowserUrl',
  BrowserTitle = 'BrowserTitle',
  TeamsStatus = 'TeamsStatus',
  TeamsCall = 'TeamsCall',
  TeamsChat = 'TeamsChat',
  ProcessStart = 'ProcessStart',
  ProcessStop = 'ProcessStop',
  Screenshot = 'Screenshot',
  Idle = 'Idle',
  Active = 'Active',
  KeyboardActivity = 'KeyboardActivity',
  MouseActivity = 'MouseActivity',
  Application = 'Application',
  SystemEvent = 'SystemEvent'
}