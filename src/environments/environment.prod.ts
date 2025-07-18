export const environment = {
  production: true,
  apiUrl: 'https://api.eam.company.com/api/v1',
  auth: {
    issuer: 'https://auth.eam.company.com',
    clientId: 'eam-frontend',
    responseType: 'code',
    scope: 'openid profile email eam-api',
    redirectUri: 'https://eam.company.com/auth/callback',
    postLogoutRedirectUri: 'https://eam.company.com',
    silentRefreshRedirectUri: 'https://eam.company.com/silent-refresh.html'
  },
  features: {
    enableTelemetry: true,
    enableScreenshots: true,
    enableRealTimeUpdates: true,
    maxRetries: 3
  }
};