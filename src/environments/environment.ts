export const environment = {
  production: false,
  apiUrl: 'https://localhost:7001/api/v1',
  auth: {
    issuer: 'https://localhost:7001',
    clientId: 'eam-frontend',
    responseType: 'code',
    scope: 'openid profile email eam-api',
    redirectUri: 'http://localhost:4200/auth/callback',
    postLogoutRedirectUri: 'http://localhost:4200',
    silentRefreshRedirectUri: 'http://localhost:4200/silent-refresh.html'
  },
  features: {
    enableTelemetry: true,
    enableScreenshots: true,
    enableRealTimeUpdates: true,
    maxRetries: 3
  }
};