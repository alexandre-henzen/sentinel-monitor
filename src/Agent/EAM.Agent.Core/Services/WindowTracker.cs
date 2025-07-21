using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EAM.Agent.Core.Models;
using EAM.Agent.Core.Windows;
using EAM.Agent.Core.Data;
using System.Threading;
using ActivityEvent = EAM.Agent.Core.Models.ActivityEvent;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Tracker para captura de mudanças de foco de janelas
/// </summary>
public class WindowTracker : ITracker, IDisposable
{
    private readonly ILogger<WindowTracker> _logger;
    private readonly IActivityEventRepository _repository;
    private readonly TrackerConfiguration _configuration;
    private readonly System.Threading.Timer _timer;
    private readonly Guid _agentId;
    private readonly Guid _userId;
    
    private IntPtr _lastActiveWindow = IntPtr.Zero;
    private string _lastWindowTitle = string.Empty;
    private string _lastProcessName = string.Empty;
    private uint _lastProcessId = 0;
    private DateTime _lastCaptureTime = DateTime.MinValue;
    private bool _isRunning = false;
    private bool _disposed = false;

    public WindowTracker(
        ILogger<WindowTracker> logger,
        IActivityEventRepository repository,
        IOptions<TrackerConfiguration> configuration)
    {
        _logger = logger;
        _repository = repository;
        _configuration = configuration.Value;
        _agentId = Guid.NewGuid(); // Em produção, seria obtido da configuração
        _userId = GetCurrentUserId();
        
        _timer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Name => "WindowTracker";
    public bool IsEnabled => _configuration.Enabled;
    public int IntervalSeconds => _configuration.IntervalSeconds;

    public event EventHandler<ActivityEvent> EventCaptured;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("WindowTracker está desabilitado");
            return;
        }

        _logger.LogInformation("Iniciando WindowTracker com intervalo de {IntervalSeconds}s", IntervalSeconds);
        
        _isRunning = true;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(IntervalSeconds));
        
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parando WindowTracker");
        
        _isRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var events = new List<ActivityEvent>();
        
        try
        {
            var foregroundWindow = WindowsApi.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return events;
            }

            // Verifica se é uma nova janela
            if (foregroundWindow == _lastActiveWindow)
            {
                return events;
            }

            var windowTitle = WindowsApi.GetWindowTitle(foregroundWindow);
            var windowClassName = WindowsApi.GetWindowClassName(foregroundWindow);
            
            WindowsApi.GetWindowThreadProcessId(foregroundWindow, out uint processId);
            var processName = WindowsApi.GetProcessName(processId);
            var processPath = WindowsApi.GetProcessPath(processId);

            // Obtém informações de posição e estado da janela
            var windowPlacement = new WindowsApi.WINDOWPLACEMENT();
            windowPlacement.Length = System.Runtime.InteropServices.Marshal.SizeOf(windowPlacement);
            WindowsApi.GetWindowPlacement(foregroundWindow, ref windowPlacement);

            WindowsApi.GetWindowRect(foregroundWindow, out var windowRect);

            var isVisible = WindowsApi.IsWindowVisible(foregroundWindow);
            var isMinimized = WindowsApi.IsIconic(foregroundWindow);
            var isMaximized = WindowsApi.IsZoomed(foregroundWindow);

            var windowState = isMinimized ? WindowState.Minimized :
                             isMaximized ? WindowState.Maximized :
                             WindowState.Normal;

            var currentTime = DateTime.UtcNow;
            
            // Calcula duração da janela anterior
            var duration = _lastCaptureTime != DateTime.MinValue ? 
                          currentTime - _lastCaptureTime : 
                          TimeSpan.Zero;

            // Cria evento de atividade
            var activityEvent = new ActivityEvent
            {
                Id = Guid.NewGuid(),
                AgentId = _agentId,
                UserId = _userId,
                Timestamp = currentTime,
                Type = ActivityType.WindowFocus,
                Application = processName,
                WindowTitle = windowTitle,
                Duration = duration,
                IsActive = true,
                CreatedAt = currentTime,
                UpdatedAt = currentTime
            };

            // Cria evento específico de janela
            var windowEvent = new WindowEvent
            {
                Id = Guid.NewGuid(),
                ActivityEventId = activityEvent.Id,
                WindowHandle = foregroundWindow,
                WindowClassName = windowClassName,
                ProcessId = (int)processId,
                ProcessName = processName,
                ExecutablePath = processPath,
                State = windowState,
                X = windowRect.Left,
                Y = windowRect.Top,
                Width = windowRect.Right - windowRect.Left,
                Height = windowRect.Bottom - windowRect.Top,
                IsVisible = isVisible,
                IsForeground = true,
                Timestamp = currentTime
            };

            // Adiciona metadados
            var metadata = new Dictionary<string, object>
            {
                { "WindowHandle", foregroundWindow.ToInt64() },
                { "WindowClassName", windowClassName },
                { "ProcessId", processId },
                { "IsVisible", isVisible },
                { "IsMinimized", isMinimized },
                { "IsMaximized", isMaximized },
                { "WindowState", windowState.ToString() }
            };

            activityEvent.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);

            // Salva no banco de dados
            await _repository.AddActivityEventAsync(activityEvent, cancellationToken);
            await _repository.AddWindowEventAsync(windowEvent, cancellationToken);

            events.Add(activityEvent);

            // Atualiza estado interno
            _lastActiveWindow = foregroundWindow;
            _lastWindowTitle = windowTitle;
            _lastProcessName = processName;
            _lastProcessId = processId;
            _lastCaptureTime = currentTime;

            // Dispara evento
            EventCaptured?.Invoke(this, activityEvent);

            _logger.LogDebug("Capturado evento de janela: {ProcessName} - {WindowTitle}", 
                processName, windowTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar evento de janela");
        }

        return events;
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
            _logger.LogError(ex, "Erro no timer do WindowTracker");
        }
    }

    private Guid GetCurrentUserId()
    {
        try
        {
            // Em produção, seria obtido do contexto do usuário Windows
            var userName = Environment.UserName;
            var domainName = Environment.UserDomainName;
            var userIdentifier = $"{domainName}\\{userName}";
            
            // Gera um GUID consistente baseado no identificador do usuário
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
}