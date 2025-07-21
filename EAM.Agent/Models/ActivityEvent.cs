namespace EAM.Agent.Models
{
    public record ActivityEvent
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
        public required string EventType { get; init; }
        public string MachineName { get; init; } = Environment.MachineName;
        public string UserName { get; init; } = Environment.UserName;
        public string? ProcessName { get; init; }
        public string? WindowTitle { get; init; }
    }
}