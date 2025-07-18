using EAM.PluginSDK;
using EAM.Agent.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace EAM.Agent.Trackers;

public class WindowTracker : ITracker
{
    private readonly ILogger<WindowTracker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ScoringService _scoringService;
    
    private string? _lastWindowTitle;
    private string? _lastProcessName;
    private IntPtr _lastWindowHandle;
    private DateTime _lastCaptureTime;

    public string Name => "WindowTracker";
    public bool IsEnabled => _configuration.GetValue<bool>("Trackers:WindowTracker:Enabled");

    public WindowTracker(ILogger<WindowTracker> logger, IConfiguration configuration, ScoringService scoringService)
    {
        _logger = logger;
        _configuration = configuration;
        _scoringService = scoringService;
        _lastCaptureTime = DateTime.UtcNow;
    }

    public Task InitializeAsync()
    {
        _logger.LogInformation("WindowTracker inicializado");
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync()
    {
        var events = new List<ActivityEvent>();

        try
        {
            var foregroundWindow = Win32Helper.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return events;

            var windowInfo = await GetWindowInfoAsync(foregroundWindow);
            if (windowInfo == null)
                return events;

            // Verificar se houve mudança de janela
            if (HasWindowChanged(foregroundWindow, windowInfo))
            {
                var duration = CalculateDuration();
                var productivityScore = await _scoringService.CalculateProductivityScoreAsync(windowInfo.ProcessName, windowInfo.WindowTitle);

                var activityEvent = new ActivityEvent("WindowFocus")
                {
                    ApplicationName = windowInfo.ProcessName,
                    WindowTitle = windowInfo.WindowTitle,
                    ProcessName = windowInfo.ProcessName,
                    ProcessId = windowInfo.ProcessId,
                    DurationSeconds = duration,
                    ProductivityScore = productivityScore,
                    Timestamp = DateTime.UtcNow
                };

                // Adicionar metadados
                activityEvent.AddMetadata("window_handle", foregroundWindow.ToString());
                activityEvent.AddMetadata("window_class", windowInfo.ClassName ?? "");
                activityEvent.AddMetadata("window_rect", windowInfo.WindowRect);

                events.Add(activityEvent);

                // Atualizar estado interno
                _lastWindowHandle = foregroundWindow;
                _lastWindowTitle = windowInfo.WindowTitle;
                _lastProcessName = windowInfo.ProcessName;
                _lastCaptureTime = DateTime.UtcNow;

                _logger.LogDebug("Janela capturada: {ProcessName} - {WindowTitle}", windowInfo.ProcessName, windowInfo.WindowTitle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro capturando janela em foco");
        }

        return events;
    }

    public Task StopAsync()
    {
        _logger.LogInformation("WindowTracker parado");
        return Task.CompletedTask;
    }

    private async Task<WindowInfo?> GetWindowInfoAsync(IntPtr windowHandle)
    {
        try
        {
            var windowInfo = new WindowInfo();

            // Obter título da janela
            var titleBuilder = new StringBuilder(256);
            Win32Helper.GetWindowText(windowHandle, titleBuilder, titleBuilder.Capacity);
            windowInfo.WindowTitle = titleBuilder.ToString();

            // Obter classe da janela
            var classBuilder = new StringBuilder(256);
            Win32Helper.GetClassName(windowHandle, classBuilder, classBuilder.Capacity);
            windowInfo.ClassName = classBuilder.ToString();

            // Obter informações do processo
            Win32Helper.GetWindowThreadProcessId(windowHandle, out uint processId);
            windowInfo.ProcessId = (int)processId;

            try
            {
                var process = Process.GetProcessById((int)processId);
                windowInfo.ProcessName = process.ProcessName;
                windowInfo.ExecutablePath = process.MainModule?.FileName;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro obtendo informações do processo {ProcessId}", processId);
                windowInfo.ProcessName = "Unknown";
            }

            // Obter retângulo da janela
            if (Win32Helper.GetWindowRect(windowHandle, out var rect))
            {
                windowInfo.WindowRect = new
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Right = rect.Right,
                    Bottom = rect.Bottom,
                    Width = rect.Right - rect.Left,
                    Height = rect.Bottom - rect.Top
                };
            }

            return windowInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro obtendo informações da janela");
            return null;
        }
    }

    private bool HasWindowChanged(IntPtr windowHandle, WindowInfo windowInfo)
    {
        return windowHandle != _lastWindowHandle ||
               windowInfo.WindowTitle != _lastWindowTitle ||
               windowInfo.ProcessName != _lastProcessName;
    }

    private int CalculateDuration()
    {
        var duration = DateTime.UtcNow - _lastCaptureTime;
        return Math.Max(1, (int)duration.TotalSeconds);
    }

    private class WindowInfo
    {
        public string? WindowTitle { get; set; }
        public string? ClassName { get; set; }
        public string? ProcessName { get; set; }
        public int ProcessId { get; set; }
        public string? ExecutablePath { get; set; }
        public object? WindowRect { get; set; }
    }
}