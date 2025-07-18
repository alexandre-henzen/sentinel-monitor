export interface DashboardMetrics {
  totalAgents: number;
  activeAgents: number;
  offlineAgents: number;
  totalEvents: number;
  eventsToday: number;
  averageProductivityScore: number;
  topApplications: ApplicationMetric[];
  productivityTrend: ProductivityTrend[];
  agentStatusDistribution: AgentStatusMetric[];
  activityTimeline: ActivityTimelineItem[];
}

export interface ApplicationMetric {
  name: string;
  usage: number;
  productivityScore: number;
  timeSpent: number;
  percentage: number;
}

export interface ProductivityTrend {
  date: Date;
  score: number;
  events: number;
}

export interface AgentStatusMetric {
  status: string;
  count: number;
  percentage: number;
}

export interface ActivityTimelineItem {
  timestamp: Date;
  eventType: string;
  applicationName: string;
  windowTitle: string;
  userName: string;
  machineName: string;
  productivityScore: number;
}

export interface DashboardFilters {
  dateRange: {
    from: Date;
    to: Date;
  };
  agentIds?: string[];
  eventTypes?: string[];
  minProductivityScore?: number;
  maxProductivityScore?: number;
}

export interface ChartData {
  labels: string[];
  datasets: ChartDataset[];
}

export interface ChartDataset {
  label: string;
  data: number[];
  backgroundColor?: string | string[];
  borderColor?: string | string[];
  borderWidth?: number;
  fill?: boolean;
}

export interface TimeSeriesData {
  timestamp: Date;
  value: number;
  label?: string;
}