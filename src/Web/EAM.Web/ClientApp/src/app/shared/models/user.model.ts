export interface User {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  roles: string[];
  permissions: string[];
  isActive: boolean;
  lastLogin?: Date;
  createdAt: Date;
  updatedAt: Date;
  department?: string;
  manager?: string;
  avatar?: string;
}

export interface Agent {
  id: string;
  userId: string;
  userName: string;
  computerName: string;
  ipAddress: string;
  agentVersion: string;
  operatingSystem: string;
  status: AgentStatus;
  lastHeartbeat: Date;
  isOnline: boolean;
  configuration: AgentConfiguration;
  createdAt: Date;
  updatedAt: Date;
}

export enum AgentStatus {
  Online = 'online',
  Offline = 'offline',
  Error = 'error',
  Updating = 'updating',
  Disabled = 'disabled'
}

export interface AgentConfiguration {
  dataSyncInterval: number;
  screenshotInterval: number;
  enableWebTracking: boolean;
  enableTeamsTracking: boolean;
  enableScreenshots: boolean;
  screenshotQuality: string;
  monitoredApplications: string[];
  excludedApplications: string[];
  excludedUrls: string[];
  securitySettings: SecuritySettings;
}

export interface SecuritySettings {
  encryptLocalData: boolean;
  requireHttps: boolean;
  certificateThumbprint?: string;
  maxRetryAttempts: number;
  retryDelay: number;
}

export interface UserPermission {
  id: string;
  name: string;
  description: string;
  category: string;
}

export interface Role {
  id: string;
  name: string;
  description: string;
  permissions: UserPermission[];
  isSystem: boolean;
}

export interface Department {
  id: string;
  name: string;
  description: string;
  managerId?: string;
  users: User[];
}

export interface UserSettings {
  userId: string;
  theme: 'light' | 'dark';
  language: string;
  timezone: string;
  dateFormat: string;
  timeFormat: string;
  dashboardLayout: DashboardLayout;
  notifications: NotificationSettings;
  privacy: PrivacySettings;
}

export interface DashboardLayout {
  widgets: DashboardWidget[];
  columns: number;
  autoRefresh: boolean;
  refreshInterval: number;
}

export interface DashboardWidget {
  id: string;
  type: string;
  title: string;
  position: WidgetPosition;
  size: WidgetSize;
  configuration: any;
}

export interface WidgetPosition {
  x: number;
  y: number;
}

export interface WidgetSize {
  width: number;
  height: number;
}

export interface NotificationSettings {
  emailNotifications: boolean;
  pushNotifications: boolean;
  desktopNotifications: boolean;
  notifyOnAgentOffline: boolean;
  notifyOnLowProductivity: boolean;
  notifyOnSecurityAlerts: boolean;
}

export interface PrivacySettings {
  showDetailedActivities: boolean;
  shareActivityData: boolean;
  allowScreenshotCapture: boolean;
  dataRetentionDays: number;
}

export interface UserProfile {
  user: User;
  settings: UserSettings;
  agents: Agent[];
  recentActivity: any[];
  statistics: UserStatistics;
}

export interface UserStatistics {
  totalActiveTime: number;
  averageProductivityScore: number;
  totalMeetings: number;
  mostUsedApplications: string[];
  mostVisitedWebsites: string[];
  dailyAverages: DailyAverage[];
}

export interface DailyAverage {
  dayOfWeek: number;
  averageActiveTime: number;
  averageProductivityScore: number;
  averageMeetings: number;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  department?: string;
  roles: string[];
  sendWelcomeEmail: boolean;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  email?: string;
  department?: string;
  roles?: string[];
  isActive?: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}