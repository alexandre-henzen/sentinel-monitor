export const environment = {
  production: true,
  apiUrl: '/api/v1',
  auth: {
    clientId: 'eam-web-client',
    authority: 'https://your-domain.com',
    redirectUri: 'https://your-domain.com/auth/callback',
    postLogoutRedirectUri: 'https://your-domain.com',
    scope: 'openid profile eam-api'
  },
  features: {
    enableTelemetry: true,
    enableRealTimeUpdates: true,
    enableScreenshots: true,
    enableExport: true
  }
};