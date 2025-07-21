namespace EAM.Shared.Constants;

/// <summary>
/// Constants for logging across EAM applications
/// </summary>
public static class LoggingConstants
{
    /// <summary>
    /// Service names for logging
    /// </summary>
    public static class ServiceNames
    {
        public const string Agent = "EAM.Agent";
        public const string API = "EAM.API";
        public const string Web = "EAM.Web";
    }

    /// <summary>
    /// Event names for structured logging
    /// </summary>
    public static class EventNames
    {
        // Agent events
        public const string AgentStarted = "AgentStarted";
        public const string AgentStopped = "AgentStopped";
        public const string DataCollected = "DataCollected";
        public const string DataSynced = "DataSynced";
        public const string ScreenshotCaptured = "ScreenshotCaptured";
        public const string HeartbeatSent = "HeartbeatSent";

        // API events
        public const string ApiStarted = "ApiStarted";
        public const string ApiStopped = "ApiStopped";
        public const string RequestReceived = "RequestReceived";
        public const string RequestProcessed = "RequestProcessed";
        public const string DatabaseQuery = "DatabaseQuery";
        public const string CacheHit = "CacheHit";
        public const string CacheMiss = "CacheMiss";

        // Web events
        public const string WebStarted = "WebStarted";
        public const string WebStopped = "WebStopped";
        public const string UserLogin = "UserLogin";
        public const string UserLogout = "UserLogout";
        public const string PageViewed = "PageViewed";
        public const string ReportGenerated = "ReportGenerated";

        // Security events
        public const string SecurityViolation = "SecurityViolation";
        public const string UnauthorizedAccess = "UnauthorizedAccess";
        public const string AuthenticationFailed = "AuthenticationFailed";
        public const string AuthenticationSucceeded = "AuthenticationSucceeded";

        // System events
        public const string SystemError = "SystemError";
        public const string SystemWarning = "SystemWarning";
        public const string ConfigurationChanged = "ConfigurationChanged";
        public const string HealthCheckFailed = "HealthCheckFailed";
        public const string HealthCheckSucceeded = "HealthCheckSucceeded";
    }

    /// <summary>
    /// Log categories for filtering
    /// </summary>
    public static class Categories
    {
        public const string Security = "Security";
        public const string Performance = "Performance";
        public const string Audit = "Audit";
        public const string Business = "Business";
        public const string System = "System";
        public const string Integration = "Integration";
        public const string UserActivity = "UserActivity";
    }

    /// <summary>
    /// Log property names
    /// </summary>
    public static class PropertyNames
    {
        public const string UserId = "UserId";
        public const string UserName = "UserName";
        public const string AgentId = "AgentId";
        public const string SessionId = "SessionId";
        public const string CorrelationId = "CorrelationId";
        public const string RequestId = "RequestId";
        public const string TraceId = "TraceId";
        public const string SpanId = "SpanId";
        public const string Duration = "Duration";
        public const string StatusCode = "StatusCode";
        public const string ErrorCode = "ErrorCode";
        public const string ClientIp = "ClientIp";
        public const string UserAgent = "UserAgent";
        public const string RequestPath = "RequestPath";
        public const string RequestMethod = "RequestMethod";
        public const string ResponseSize = "ResponseSize";
        public const string DatabaseQuery = "DatabaseQuery";
        public const string DatabaseTable = "DatabaseTable";
        public const string CacheKey = "CacheKey";
        public const string ExceptionType = "ExceptionType";
        public const string ExceptionMessage = "ExceptionMessage";
        public const string StackTrace = "StackTrace";
    }

    /// <summary>
    /// Log message templates
    /// </summary>
    public static class MessageTemplates
    {
        // Performance templates
        public const string OperationCompleted = "Operation {OperationName} completed in {Duration}ms";
        public const string QueryExecuted = "Database query executed on {DatabaseTable} in {Duration}ms";
        public const string CacheOperation = "Cache {Operation} for key {CacheKey} in {Duration}ms";

        // Security templates
        public const string AuthenticationAttempt = "Authentication attempt for {UserName} from {ClientIp}";
        public const string AuthorizationFailure = "Authorization failure for {UserName} accessing {Resource}";
        public const string SecurityViolation = "Security violation: {ViolationType} by {UserName} from {ClientIp}";

        // Audit templates
        public const string UserAction = "User {UserName} performed {Action} on {Resource}";
        public const string SystemAction = "System performed {Action} on {Resource}";
        public const string ConfigurationChange = "Configuration {ConfigurationName} changed from {OldValue} to {NewValue} by {UserName}";

        // Error templates
        public const string UnhandledException = "Unhandled exception in {OperationName}: {ExceptionType} - {ExceptionMessage}";
        public const string BusinessError = "Business error in {OperationName}: {ErrorMessage}";
        public const string IntegrationError = "Integration error with {ExternalSystem}: {ErrorMessage}";
    }

    /// <summary>
    /// Log levels for different scenarios
    /// </summary>
    public static class LogLevels
    {
        public const string Trace = "Trace";
        public const string Debug = "Debug";
        public const string Information = "Information";
        public const string Warning = "Warning";
        public const string Error = "Error";
        public const string Critical = "Critical";
    }
}