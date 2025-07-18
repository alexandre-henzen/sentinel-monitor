namespace EAM.Shared.Constants;

public static class ApiEndpoints
{
    public const string Base = "/api/v1";
    
    // Authentication
    public const string Auth = $"{Base}/auth";
    public const string AuthLogin = $"{Auth}/login";
    public const string AuthRefresh = $"{Auth}/refresh";
    public const string AuthRevoke = $"{Auth}/revoke";
    
    // Events
    public const string Events = $"{Base}/events";
    public const string EventsBatch = $"{Events}/batch";
    public const string EventsNdjson = $"{Events}/ndjson";
    
    // Screenshots
    public const string Screenshots = $"{Base}/screenshots";
    public const string ScreenshotsUpload = $"{Screenshots}/upload";
    public const string ScreenshotsSignedUrl = $"{Screenshots}/signed-url";
    
    // Agents
    public const string Agents = $"{Base}/agents";
    public const string AgentsRegister = $"{Agents}/register";
    public const string AgentsHeartbeat = $"{Agents}/heartbeat";
    public const string AgentsStatus = $"{Agents}/status";
    
    // Updates
    public const string Updates = $"{Base}/updates";
    public const string UpdatesLatest = $"{Updates}/latest";
    public const string UpdatesCheck = $"{Updates}/check";
    public const string UpdatesDownload = $"{Updates}/download";
    
    // Health
    public const string Health = "/health";
    public const string HealthReady = "/health/ready";
    public const string HealthLive = "/health/live";
}