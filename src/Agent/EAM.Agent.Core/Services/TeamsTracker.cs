using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EAM.Agent.Core.Models;
using EAM.Agent.Core.Windows;
using EAM.Agent.Core.Data;
using System.Threading;
using System.Text.RegularExpressions;
using ActivityEvent = EAM.Agent.Core.Models.ActivityEvent;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Tracker para captura de reuniões e atividades do Microsoft Teams
/// </summary>
public class TeamsTracker : ITracker, IDisposable
{
    private readonly ILogger<TeamsTracker> _logger;
    private readonly IActivityEventRepository _repository;
    private readonly TrackerConfiguration _configuration;
    private readonly System.Threading.Timer _timer;
    private readonly Guid _agentId;
    private readonly Guid _userId;
    
    private string _lastMeetingId = string.Empty;
    private TeamsEventType _lastEventType = TeamsEventType.MeetingStart;
    private DateTime _lastCaptureTime = DateTime.MinValue;
    private DateTime _meetingStartTime = DateTime.MinValue;
    private bool _isRunning = false;
    private bool _disposed = false;

    private readonly string[] _teamsProcessNames = { "ms-teams", "teams", "msteams" };
    private readonly string[] _meetingIndicators = 
    {
        "Microsoft Teams meeting",
        "Teams meeting",
        "Reunião do Teams",
        "Meeting",
        "Call",
        "Chamada"
    };

    private readonly string[] _audioVideoIndicators = 
    {
        "Muted",
        "Unmuted",
        "Camera on",
        "Camera off",
        "Sharing",
        "Screen share",
        "Recording"
    };

    public TeamsTracker(
        ILogger<TeamsTracker> logger,
        IActivityEventRepository repository,
        IOptions<TrackerConfiguration> configuration)
    {
        _logger = logger;
        _repository = repository;
        _configuration = configuration.Value;
        _agentId = Guid.NewGuid();
        _userId = GetCurrentUserId();
        
        _timer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Name => "TeamsTracker";
    public bool IsEnabled => _configuration.Enabled;
    public int IntervalSeconds => _configuration.IntervalSeconds;

    public event EventHandler<ActivityEvent> EventCaptured;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("TeamsTracker está desabilitado");
            return;
        }

        _logger.LogInformation("Iniciando TeamsTracker com intervalo de {IntervalSeconds}s", IntervalSeconds);
        
        _isRunning = true;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(IntervalSeconds));
        
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parando TeamsTracker");
        
        _isRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var events = new List<ActivityEvent>();
        
        try
        {
            var teamsWindows = FindTeamsWindows();
            
            foreach (var window in teamsWindows)
            {
                var windowTitle = WindowsApi.GetWindowTitle(window);
                var eventInfo = AnalyzeTeamsWindow(windowTitle);
                
                if (eventInfo != null)
                {
                    var activityEvent = await CreateActivityEvent(eventInfo, windowTitle);
                    var teamsEvent = await CreateTeamsEvent(activityEvent.Id, eventInfo, windowTitle);
                    
                    events.Add(activityEvent);
                    
                    // Salva no banco de dados
                    await _repository.AddActivityEventAsync(activityEvent, cancellationToken);
                    await _repository.AddTeamsEventAsync(teamsEvent, cancellationToken);
                    
                    // Dispara evento
                    EventCaptured?.Invoke(this, activityEvent);
                    
                    _logger.LogDebug("Capturado evento do Teams: {EventType} - {MeetingTitle}", 
                        eventInfo.EventType, eventInfo.MeetingTitle);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar evento do Teams");
        }

        return events;
    }

    private List<IntPtr> FindTeamsWindows()
    {
        var teamsWindows = new List<IntPtr>();
        
        WindowsApi.EnumWindows((hWnd, lParam) =>
        {
            if (WindowsApi.IsWindowVisible(hWnd))
            {
                WindowsApi.GetWindowThreadProcessId(hWnd, out uint processId);
                var processName = WindowsApi.GetProcessName(processId).ToLowerInvariant();
                
                if (_teamsProcessNames.Any(name => processName.Contains(name)))
                {
                    teamsWindows.Add(hWnd);
                }
            }
            return true;
        }, IntPtr.Zero);

        return teamsWindows;
    }

    private TeamsEventInfo? AnalyzeTeamsWindow(string windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        var eventInfo = new TeamsEventInfo();
        
        // Detecta se é uma reunião
        var isMeeting = _meetingIndicators.Any(indicator => 
            windowTitle.Contains(indicator, StringComparison.OrdinalIgnoreCase));
        
        if (!isMeeting)
            return null;

        // Extrai informações da reunião
        eventInfo.MeetingTitle = ExtractMeetingTitle(windowTitle);
        eventInfo.MeetingId = ExtractMeetingId(windowTitle);
        eventInfo.EventType = DetermineEventType(windowTitle);
        eventInfo.IsAudioActive = DetectAudioActivity(windowTitle);
        eventInfo.IsVideoActive = DetectVideoActivity(windowTitle);
        eventInfo.IsScreenSharing = DetectScreenSharing(windowTitle);
        eventInfo.IsRecording = DetectRecording(windowTitle);
        eventInfo.ParticipantCount = EstimateParticipantCount(windowTitle);
        eventInfo.Status = DetermineMeetingStatus(windowTitle);

        return eventInfo;
    }

    private string ExtractMeetingTitle(string windowTitle)
    {
        try
        {
            // Remove indicadores comuns do Teams
            var title = windowTitle;
            var patterns = new[]
            {
                @"Microsoft Teams meeting",
                @"Teams meeting",
                @"Reunião do Teams",
                @"Microsoft Teams",
                @"Teams"
            };

            foreach (var pattern in patterns)
            {
                title = Regex.Replace(title, pattern, "", RegexOptions.IgnoreCase).Trim();
            }

            // Remove caracteres especiais e limpa
            title = Regex.Replace(title, @"[|\-–—]", "").Trim();
            
            return string.IsNullOrEmpty(title) ? "Teams Meeting" : title;
        }
        catch
        {
            return "Teams Meeting";
        }
    }

    private string ExtractMeetingId(string windowTitle)
    {
        try
        {
            // Procura por padrões de ID de reunião
            var patterns = new[]
            {
                @"Meeting ID:\s*(\d+)",
                @"ID:\s*(\d+)",
                @"#(\d+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(windowTitle, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            // Se não encontrar ID, gera um baseado no título
            return GenerateMeetingId(windowTitle);
        }
        catch
        {
            return Guid.NewGuid().ToString("N")[..8];
        }
    }

    private string GenerateMeetingId(string windowTitle)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(windowTitle));
        return Convert.ToHexString(hash)[..8];
    }

    private TeamsEventType DetermineEventType(string windowTitle)
    {
        if (windowTitle.Contains("Recording", StringComparison.OrdinalIgnoreCase))
            return TeamsEventType.RecordingStart;
        
        if (windowTitle.Contains("Sharing", StringComparison.OrdinalIgnoreCase))
            return TeamsEventType.ScreenShareStart;
        
        if (windowTitle.Contains("Muted", StringComparison.OrdinalIgnoreCase))
            return TeamsEventType.AudioToggle;
        
        if (windowTitle.Contains("Camera", StringComparison.OrdinalIgnoreCase))
            return TeamsEventType.VideoToggle;
        
        return TeamsEventType.MeetingStart;
    }

    private bool DetectAudioActivity(string windowTitle)
    {
        return !windowTitle.Contains("Muted", StringComparison.OrdinalIgnoreCase) &&
               !windowTitle.Contains("Silenciado", StringComparison.OrdinalIgnoreCase);
    }

    private bool DetectVideoActivity(string windowTitle)
    {
        return windowTitle.Contains("Camera on", StringComparison.OrdinalIgnoreCase) ||
               windowTitle.Contains("Câmera ligada", StringComparison.OrdinalIgnoreCase);
    }

    private bool DetectScreenSharing(string windowTitle)
    {
        return windowTitle.Contains("Sharing", StringComparison.OrdinalIgnoreCase) ||
               windowTitle.Contains("Compartilhando", StringComparison.OrdinalIgnoreCase);
    }

    private bool DetectRecording(string windowTitle)
    {
        return windowTitle.Contains("Recording", StringComparison.OrdinalIgnoreCase) ||
               windowTitle.Contains("Gravando", StringComparison.OrdinalIgnoreCase);
    }

    private int EstimateParticipantCount(string windowTitle)
    {
        try
        {
            // Procura por padrões como "3 participants" ou "5 pessoas"
            var patterns = new[]
            {
                @"(\d+)\s+participants?",
                @"(\d+)\s+pessoas?",
                @"(\d+)\s+participantes?",
                @"\((\d+)\)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(windowTitle, pattern, RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                {
                    return count;
                }
            }

            return 1; // Pelo menos o usuário atual
        }
        catch
        {
            return 1;
        }
    }

    private MeetingStatus DetermineMeetingStatus(string windowTitle)
    {
        if (windowTitle.Contains("Waiting", StringComparison.OrdinalIgnoreCase) ||
            windowTitle.Contains("Aguardando", StringComparison.OrdinalIgnoreCase))
            return MeetingStatus.Waiting;
        
        if (windowTitle.Contains("Ended", StringComparison.OrdinalIgnoreCase) ||
            windowTitle.Contains("Finalizada", StringComparison.OrdinalIgnoreCase))
            return MeetingStatus.Ended;
        
        return MeetingStatus.InProgress;
    }

    private async Task<ActivityEvent> CreateActivityEvent(TeamsEventInfo eventInfo, string windowTitle)
    {
        var currentTime = DateTime.UtcNow;
        var duration = _lastCaptureTime != DateTime.MinValue ? 
                      currentTime - _lastCaptureTime : 
                      TimeSpan.Zero;

        var activityEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            AgentId = _agentId,
            UserId = _userId,
            Timestamp = currentTime,
            Type = eventInfo.EventType == TeamsEventType.MeetingEnd ? 
                   ActivityType.TeamsCallEnd : 
                   ActivityType.TeamsCall,
            Application = "Microsoft Teams",
            WindowTitle = windowTitle,
            Duration = duration,
            IsActive = true,
            CreatedAt = currentTime,
            UpdatedAt = currentTime
        };

        // Adiciona metadados
        var metadata = new Dictionary<string, object>
        {
            { "MeetingId", eventInfo.MeetingId },
            { "EventType", eventInfo.EventType.ToString() },
            { "IsAudioActive", eventInfo.IsAudioActive },
            { "IsVideoActive", eventInfo.IsVideoActive },
            { "IsScreenSharing", eventInfo.IsScreenSharing },
            { "IsRecording", eventInfo.IsRecording },
            { "ParticipantCount", eventInfo.ParticipantCount },
            { "Status", eventInfo.Status.ToString() }
        };

        activityEvent.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);

        return activityEvent;
    }

    private async Task<TeamsEvent> CreateTeamsEvent(Guid activityEventId, TeamsEventInfo eventInfo, string windowTitle)
    {
        var currentTime = DateTime.UtcNow;
        
        // Calcula duração da reunião
        var durationSeconds = 0;
        if (_meetingStartTime != DateTime.MinValue)
        {
            durationSeconds = (int)(currentTime - _meetingStartTime).TotalSeconds;
        }
        else if (eventInfo.EventType == TeamsEventType.MeetingStart)
        {
            _meetingStartTime = currentTime;
        }

        var teamsEvent = new TeamsEvent
        {
            Id = Guid.NewGuid(),
            ActivityEventId = activityEventId,
            MeetingId = eventInfo.MeetingId,
            MeetingTitle = eventInfo.MeetingTitle,
            EventType = eventInfo.EventType,
            Status = eventInfo.Status,
            IsAudioActive = eventInfo.IsAudioActive,
            IsVideoActive = eventInfo.IsVideoActive,
            IsScreenSharing = eventInfo.IsScreenSharing,
            IsRecording = eventInfo.IsRecording,
            ParticipantCount = eventInfo.ParticipantCount,
            DurationSeconds = durationSeconds,
            StartTime = _meetingStartTime != DateTime.MinValue ? _meetingStartTime : currentTime,
            EndTime = eventInfo.EventType == TeamsEventType.MeetingEnd ? currentTime : null,
            Timestamp = currentTime
        };

        return teamsEvent;
    }

    private async void OnTimerElapsed(object? state)
    {
        if (!_isRunning) return;

        try
        {
            await CaptureAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no timer do TeamsTracker");
        }
    }

    private Guid GetCurrentUserId()
    {
        try
        {
            var userName = Environment.UserName;
            var domainName = Environment.UserDomainName;
            var userIdentifier = $"{domainName}\\{userName}";
            
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userIdentifier));
            return new Guid(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ID do usuário");
            return Guid.NewGuid();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _timer?.Dispose();
                StopAsync().Wait();
            }
            _disposed = true;
        }
    }

    private class TeamsEventInfo
    {
        public string MeetingId { get; set; } = string.Empty;
        public string MeetingTitle { get; set; } = string.Empty;
        public TeamsEventType EventType { get; set; }
        public MeetingStatus Status { get; set; }
        public bool IsAudioActive { get; set; }
        public bool IsVideoActive { get; set; }
        public bool IsScreenSharing { get; set; }
        public bool IsRecording { get; set; }
        public int ParticipantCount { get; set; }
    }
}