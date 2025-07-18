using EAM.PluginSDK;
using EAM.Agent.Helpers;
using EAM.Agent.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EAM.Agent.Trackers;

public class TeamsTracker : ITracker
{
    private readonly ILogger<TeamsTracker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ScoringService _scoringService;
    
    private string? _lastTeamsStatus;
    private string? _lastMeetingInfo;
    private DateTime _lastCaptureTime;

    public string Name => "TeamsTracker";
    public bool IsEnabled => _configuration.GetValue<bool>("Trackers:TeamsTracker:Enabled");

    public TeamsTracker(ILogger<TeamsTracker> logger, IConfiguration configuration, ScoringService scoringService)
    {
        _logger = logger;
        _configuration = configuration;
        _scoringService = scoringService;
        _lastCaptureTime = DateTime.UtcNow;
    }

    public Task InitializeAsync()
    {
        _logger.LogInformation("TeamsTracker inicializado");
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync()
    {
        var events = new List<ActivityEvent>();

        try
        {
            var teamsEvents = await CaptureTeamsEventsAsync();
            events.AddRange(teamsEvents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro capturando eventos do Teams");
        }

        return events;
    }

    public Task StopAsync()
    {
        _logger.LogInformation("TeamsTracker parado");
        return Task.CompletedTask;
    }

    private async Task<List<ActivityEvent>> CaptureTeamsEventsAsync()
    {
        var events = new List<ActivityEvent>();

        try
        {
            // Procurar por janelas do Teams
            var teamsWindows = Win32Helper.GetWindowsByProcessName("ms-teams");
            
            // Fallback para Teams clássico
            if (!teamsWindows.Any())
            {
                teamsWindows = Win32Helper.GetWindowsByProcessName("teams");
            }

            foreach (var windowHandle in teamsWindows)
            {
                var teamsInfo = await GetTeamsInfoAsync(windowHandle);
                if (teamsInfo == null) continue;

                var hasStatusChanged = _lastTeamsStatus != teamsInfo.Status;
                var hasMeetingChanged = _lastMeetingInfo != teamsInfo.MeetingInfo;

                if (hasStatusChanged || hasMeetingChanged)
                {
                    var productivityScore = await _scoringService.CalculateProductivityScoreAsync("teams", teamsInfo.WindowTitle);

                    var eventType = DetermineEventType(teamsInfo);
                    var teamsEvent = new ActivityEvent(eventType)
                    {
                        ApplicationName = "Microsoft Teams",
                        WindowTitle = teamsInfo.WindowTitle,
                        ProcessName = "ms-teams",
                        ProcessId = teamsInfo.ProcessId,
                        ProductivityScore = productivityScore,
                        Timestamp = DateTime.UtcNow
                    };

                    // Adicionar metadados específicos do Teams
                    teamsEvent.AddMetadata("teams_status", teamsInfo.Status);
                    teamsEvent.AddMetadata("meeting_info", teamsInfo.MeetingInfo);
                    teamsEvent.AddMetadata("is_in_meeting", teamsInfo.IsInMeeting);
                    teamsEvent.AddMetadata("is_presenting", teamsInfo.IsPresenting);
                    teamsEvent.AddMetadata("is_sharing_screen", teamsInfo.IsSharingScreen);
                    teamsEvent.AddMetadata("participant_count", teamsInfo.ParticipantCount);
                    teamsEvent.AddMetadata("meeting_duration", teamsInfo.MeetingDuration);
                    teamsEvent.AddMetadata("window_handle", windowHandle.ToString());

                    events.Add(teamsEvent);

                    // Atualizar cache
                    _lastTeamsStatus = teamsInfo.Status;
                    _lastMeetingInfo = teamsInfo.MeetingInfo;
                    _lastCaptureTime = DateTime.UtcNow;

                    _logger.LogDebug("Teams event capturado: {EventType} - {Status}", eventType, teamsInfo.Status);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro capturando eventos do Teams");
        }

        return events;
    }

    private async Task<TeamsInfo?> GetTeamsInfoAsync(IntPtr windowHandle)
    {
        try
        {
            var title = Win32Helper.GetWindowText(windowHandle);
            var processId = Win32Helper.GetWindowProcessId(windowHandle);

            var teamsInfo = new TeamsInfo
            {
                WindowTitle = title,
                ProcessId = (int)processId,
                WindowHandle = windowHandle
            };

            // Analisar título para extrair informações
            AnalyzeTeamsTitle(teamsInfo, title);

            // Verificar status através de outras janelas do Teams
            await AnalyzeTeamsStatusAsync(teamsInfo);

            return teamsInfo;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro obtendo informações do Teams");
            return null;
        }
    }

    private void AnalyzeTeamsTitle(TeamsInfo teamsInfo, string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            teamsInfo.Status = "Unknown";
            return;
        }

        var lowerTitle = title.ToLowerInvariant();

        // Detectar se está em reunião
        if (lowerTitle.Contains("meeting") || lowerTitle.Contains("call") || lowerTitle.Contains("reunião"))
        {
            teamsInfo.IsInMeeting = true;
            teamsInfo.Status = "In Meeting";
            teamsInfo.MeetingInfo = ExtractMeetingInfo(title);
        }
        else if (lowerTitle.Contains("presenting") || lowerTitle.Contains("apresentando"))
        {
            teamsInfo.IsPresenting = true;
            teamsInfo.Status = "Presenting";
        }
        else if (lowerTitle.Contains("sharing") || lowerTitle.Contains("compartilhando"))
        {
            teamsInfo.IsSharingScreen = true;
            teamsInfo.Status = "Sharing Screen";
        }
        else if (lowerTitle.Contains("chat") || lowerTitle.Contains("conversation"))
        {
            teamsInfo.Status = "Chatting";
        }
        else if (lowerTitle.Contains("teams") && lowerTitle.Contains("microsoft"))
        {
            teamsInfo.Status = "Active";
        }
        else
        {
            teamsInfo.Status = "Unknown";
        }

        // Extrair contagem de participantes
        var participantMatch = Regex.Match(title, @"(\d+)\s+(participant|people|person)", RegexOptions.IgnoreCase);
        if (participantMatch.Success)
        {
            if (int.TryParse(participantMatch.Groups[1].Value, out var count))
            {
                teamsInfo.ParticipantCount = count;
            }
        }
    }

    private async Task AnalyzeTeamsStatusAsync(TeamsInfo teamsInfo)
    {
        try
        {
            // Procurar por janelas específicas do Teams que indicam status
            var allTeamsWindows = Win32Helper.GetWindowsByProcessName("ms-teams");
            
            foreach (var window in allTeamsWindows)
            {
                var windowTitle = Win32Helper.GetWindowText(window);
                var className = Win32Helper.GetClassName(window);

                // Verificar por indicadores de reunião
                if (windowTitle.Contains("Microsoft Teams Call") || 
                    windowTitle.Contains("Microsoft Teams Meeting"))
                {
                    teamsInfo.IsInMeeting = true;
                    teamsInfo.Status = "In Meeting";
                }

                // Verificar por janelas de notificação
                if (className.Contains("TeamsWebView") || className.Contains("Chrome_WidgetWin"))
                {
                    // Analisar conteúdo da notificação se possível
                    if (windowTitle.Contains("incoming call") || windowTitle.Contains("chamada"))
                    {
                        teamsInfo.Status = "Incoming Call";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro analisando status do Teams");
        }
    }

    private string ExtractMeetingInfo(string title)
    {
        // Extrair informações de reunião do título
        var meetingPatterns = new[]
        {
            @"Meeting with (.+?)(?:\s-\s|\s\|\s|$)",
            @"(.+?)\s-\s(?:Microsoft Teams|Teams)",
            @"Call with (.+?)(?:\s-\s|\s\|\s|$)"
        };

        foreach (var pattern in meetingPatterns)
        {
            var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return title;
    }

    private string DetermineEventType(TeamsInfo teamsInfo)
    {
        if (teamsInfo.IsInMeeting)
            return "TeamsCall";
        
        if (teamsInfo.IsPresenting)
            return "TeamsPresenting";
        
        if (teamsInfo.IsSharingScreen)
            return "TeamsScreenShare";
        
        if (teamsInfo.Status == "Chatting")
            return "TeamsChat";
        
        return "TeamsStatus";
    }

    private class TeamsInfo
    {
        public string WindowTitle { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public IntPtr WindowHandle { get; set; }
        public string Status { get; set; } = "Unknown";
        public string MeetingInfo { get; set; } = string.Empty;
        public bool IsInMeeting { get; set; }
        public bool IsPresenting { get; set; }
        public bool IsSharingScreen { get; set; }
        public int ParticipantCount { get; set; }
        public TimeSpan MeetingDuration { get; set; }
    }
}