export const environment = {
  production: false,
  apiUrl: 'https://localhost:5001/api/v1',
  auth: {
    clientId: 'eam-web-client',
    authority: 'https://localhost:5001',
    redirectUri: 'http://localhost:4200/auth/callback',
    postLogoutRedirectUri: 'http://localhost:4200',
    scope: 'openid profile eam-api'
  },
  features: {
    enableTelemetry: true,
    enableRealTimeUpdates: true,
    enableScreenshots: true,
    enableExport: true
  }
};