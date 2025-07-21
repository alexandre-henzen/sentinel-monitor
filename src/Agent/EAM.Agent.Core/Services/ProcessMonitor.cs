using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Management;
using EAM.Agent.Core.Models;
using EAM.Agent.Core.Data;
using ActivityEvent = EAM.Agent.Core.Models.ActivityEvent;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Monitor para processos do sistema (início/fim)
/// </summary>
public class ProcessMonitor : ITracker, IDisposable
{
    private readonly ILogger<ProcessMonitor> _logger;
    private readonly IActivityEventRepository _repository;
    private readonly TrackerConfiguration _configuration;
    private readonly System.Threading.Timer _timer;
    private readonly Guid _agentId;
    private readonly Guid _userId;
    
    private readonly Dictionary<int, ProcessInfo> _runningProcesses = new();
    private readonly HashSet<string> _monitoredProcesses = new();
    private readonly object _lockObject = new object();
    
    private bool _isRunning = false;
    private bool _disposed = false;
    private DateTime _lastScanTime = DateTime.MinValue;

    public ProcessMonitor(
        ILogger<ProcessMonitor> logger,
        IActivityEventRepository repository,
        IOptions<TrackerConfiguration> configuration)
    {
        _logger = logger;
        _repository = repository;
        _configuration = configuration.Value;
        _agentId = Guid.NewGuid();
        _userId = GetCurrentUserId();
        
        InitializeMonitoredProcesses();
        
        _timer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Name => "ProcessMonitor";
    public bool IsEnabled => _configuration.Enabled;
    public int IntervalSeconds => _configuration.IntervalSeconds;

    public event EventHandler<ActivityEvent> EventCaptured;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("ProcessMonitor está desabilitado");
            return;
        }

        _logger.LogInformation("Iniciando ProcessMonitor com intervalo de {IntervalSeconds}s", IntervalSeconds);
        
        // Faz scan inicial
        await InitialProcessScanAsync();
        
        _isRunning = true;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(IntervalSeconds));
        
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parando ProcessMonitor");
        
        _isRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var events = new List<ActivityEvent>();
        
        try
        {
            var currentProcesses = GetCurrentProcesses();
            var currentTime = DateTime.UtcNow;
            
            List<ProcessInfo> newProcessesToMonitor;
            List<ProcessInfo> endedProcessesToMonitor;
            
            lock (_lockObject)
            {
                // Detecta novos processos
                var newProcesses = currentProcesses.Where(p => !_runningProcesses.ContainsKey(p.Key)).ToList();
                
                // Detecta processos finalizados
                var endedProcesses = _runningProcesses.Where(p => !currentProcesses.ContainsKey(p.Key)).ToList();
                
                // Filtra processos que devem ser monitorados
                newProcessesToMonitor = newProcesses
                    .Where(kvp => ShouldMonitorProcess(kvp.Value.ProcessName))
                    .Select(kvp => kvp.Value)
                    .ToList();
                
                endedProcessesToMonitor = endedProcesses
                    .Where(kvp => ShouldMonitorProcess(kvp.Value.ProcessName))
                    .Select(kvp => kvp.Value)
                    .ToList();
                
                // Atualiza lista de processos em execução
                foreach (var kvp in newProcesses)
                {
                    _runningProcesses[kvp.Key] = kvp.Value;
                }
                
                foreach (var kvp in endedProcesses)
                {
                    _runningProcesses.Remove(kvp.Key);
                }
            }
            
            // Processa novos processos (fora do lock)
            foreach (var processInfo in newProcessesToMonitor)
            {
                var startEvent = await CreateProcessStartEvent(processInfo, currentTime, cancellationToken);
                if (startEvent != null)
                {
                    events.Add(startEvent);
                    _logger.LogDebug("Novo processo detectado: {ProcessName} (PID: {ProcessId})",
                        processInfo.ProcessName, processInfo.ProcessId);
                }
            }
            
            // Processa processos finalizados (fora do lock)
            foreach (var processInfo in endedProcessesToMonitor)
            {
                var endEvent = await CreateProcessEndEvent(processInfo, currentTime, cancellationToken);
                if (endEvent != null)
                {
                    events.Add(endEvent);
                    _logger.LogDebug("Processo finalizado: {ProcessName} (PID: {ProcessId})",
                        processInfo.ProcessName, processInfo.ProcessId);
                }
            }
            
            // Monitora processos com alto uso de recursos (fora do lock)
            await MonitorResourceUsage(currentProcesses.Values, currentTime, events, cancellationToken);
            
            _lastScanTime = currentTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar eventos de processo");
        }

        return events;
    }

    private Dictionary<int, ProcessInfo> GetCurrentProcesses()
    {
        var processes = new Dictionary<int, ProcessInfo>();
        
        try
        {
            var systemProcesses = Process.GetProcesses();
            
            foreach (var process in systemProcesses)
            {
                try
                {
                    var processInfo = new ProcessInfo
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        ExecutablePath = GetProcessPath(process),
                        StartTime = GetProcessStartTime(process),
                        WorkingDirectory = GetProcessWorkingDirectory(process),
                        CommandLine = GetProcessCommandLine(process),
                        ParentProcessId = GetParentProcessId(process),
                        User = GetProcessUser(process),
                        Domain = GetProcessDomain(process),
                        MemoryUsage = GetProcessMemoryUsage(process),
                        CpuUsage = GetProcessCpuUsage(process),
                        HandleCount = GetProcessHandleCount(process),
                        ThreadCount = GetProcessThreadCount(process)
                    };
                    
                    processes[process.Id] = processInfo;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao obter informações do processo {ProcessId}", process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter lista de processos");
        }
        
        return processes;
    }

    private async Task InitialProcessScanAsync()
    {
        try
        {
            var currentProcesses = GetCurrentProcesses();
            
            lock (_lockObject)
            {
                _runningProcesses.Clear();
                foreach (var kvp in currentProcesses)
                {
                    _runningProcesses[kvp.Key] = kvp.Value;
                }
            }
            
            _logger.LogDebug("Scan inicial de processos concluído: {Count} processos encontrados", 
                currentProcesses.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no scan inicial de processos");
        }
    }

    private void InitializeMonitoredProcesses()
    {
        // Processos importantes para monitorar
        var defaultProcesses = new[]
        {
            "chrome", "msedge", "firefox", "iexplore", "safari", "opera",
            "teams", "ms-teams", "msteams",
            "outlook", "winword", "excel", "powerpnt", "onenote",
            "notepad", "notepad++", "code", "devenv", "rider",
            "calc", "mspaint", "explorer"
        };
        
        _monitoredProcesses.Clear();
        foreach (var process in defaultProcesses)
        {
            _monitoredProcesses.Add(process.ToLowerInvariant());
        }
        
        // Adiciona processos da configuração
        if (_configuration.Settings.TryGetValue("MonitoredProcesses", out var configProcesses))
        {
            if (configProcesses is IEnumerable<string> processes)
            {
                foreach (var process in processes)
                {
                    _monitoredProcesses.Add(process.ToLowerInvariant());
                }
            }
        }
    }

    private bool ShouldMonitorProcess(string processName)
    {
        if (string.IsNullOrEmpty(processName))
            return false;
        
        var lowerName = processName.ToLowerInvariant();
        
        // Se lista de monitorados está vazia, monitora todos
        if (_monitoredProcesses.Count == 0)
            return true;
        
        // Verifica se está na lista de monitorados
        return _monitoredProcesses.Any(mp => lowerName.Contains(mp));
    }

    private async Task<ActivityEvent?> CreateProcessStartEvent(ProcessInfo processInfo, DateTime currentTime, CancellationToken cancellationToken)
    {
        try
        {
            var activityEvent = new ActivityEvent
            {
                Id = Guid.NewGuid(),
                AgentId = _agentId,
                UserId = _userId,
                Timestamp = currentTime,
                Type = ActivityType.ProcessStart,
                Application = processInfo.ProcessName,
                WindowTitle = processInfo.ProcessName,
                Duration = TimeSpan.Zero,
                IsActive = true,
                CreatedAt = currentTime,
                UpdatedAt = currentTime
            };

            var processEvent = new ProcessEvent
            {
                Id = Guid.NewGuid(),
                ActivityEventId = activityEvent.Id,
                ProcessId = processInfo.ProcessId,
                ProcessName = processInfo.ProcessName,
                ExecutablePath = processInfo.ExecutablePath,
                CommandLine = processInfo.CommandLine,
                WorkingDirectory = processInfo.WorkingDirectory,
                EventType = ProcessEventType.Start,
                ParentProcessId = processInfo.ParentProcessId,
                ParentProcessName = GetProcessNameById(processInfo.ParentProcessId),
                User = processInfo.User,
                Domain = processInfo.Domain,
                MemoryUsage = processInfo.MemoryUsage,
                CpuUsage = processInfo.CpuUsage,
                HandleCount = processInfo.HandleCount,
                ThreadCount = processInfo.ThreadCount,
                StartTime = processInfo.StartTime,
                Timestamp = currentTime
            };

            // Adiciona metadados
            var metadata = new Dictionary<string, object>
            {
                { "ProcessId", processInfo.ProcessId },
                { "ExecutablePath", processInfo.ExecutablePath ?? string.Empty },
                { "StartTime", processInfo.StartTime },
                { "MemoryUsage", processInfo.MemoryUsage },
                { "ThreadCount", processInfo.ThreadCount }
            };

            activityEvent.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);

            // Salva no banco de dados
            await _repository.AddActivityEventAsync(activityEvent, cancellationToken);
            await _repository.AddProcessEventAsync(processEvent, cancellationToken);

            // Dispara evento
            EventCaptured?.Invoke(this, activityEvent);

            return activityEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar evento de início de processo");
            return null;
        }
    }

    private async Task<ActivityEvent?> CreateProcessEndEvent(ProcessInfo processInfo, DateTime currentTime, CancellationToken cancellationToken)
    {
        try
        {
            var duration = currentTime - processInfo.StartTime;
            
            var activityEvent = new ActivityEvent
            {
                Id = Guid.NewGuid(),
                AgentId = _agentId,
                UserId = _userId,
                Timestamp = currentTime,
                Type = ActivityType.ProcessEnd,
                Application = processInfo.ProcessName,
                WindowTitle = processInfo.ProcessName,
                Duration = duration,
                IsActive = false,
                CreatedAt = currentTime,
                UpdatedAt = currentTime
            };

            var processEvent = new ProcessEvent
            {
                Id = Guid.NewGuid(),
                ActivityEventId = activityEvent.Id,
                ProcessId = processInfo.ProcessId,
                ProcessName = processInfo.ProcessName,
                ExecutablePath = processInfo.ExecutablePath,
                CommandLine = processInfo.CommandLine,
                WorkingDirectory = processInfo.WorkingDirectory,
                EventType = ProcessEventType.End,
                ParentProcessId = processInfo.ParentProcessId,
                ParentProcessName = GetProcessNameById(processInfo.ParentProcessId),
                User = processInfo.User,
                Domain = processInfo.Domain,
                MemoryUsage = processInfo.MemoryUsage,
                CpuUsage = processInfo.CpuUsage,
                HandleCount = processInfo.HandleCount,
                ThreadCount = processInfo.ThreadCount,
                StartTime = processInfo.StartTime,
                EndTime = currentTime,
                ExitCode = null, // Não é possível obter após o processo finalizar
                Timestamp = currentTime
            };

            // Adiciona metadados
            var metadata = new Dictionary<string, object>
            {
                { "ProcessId", processInfo.ProcessId },
                { "ExecutablePath", processInfo.ExecutablePath ?? string.Empty },
                { "StartTime", processInfo.StartTime },
                { "EndTime", currentTime },
                { "Duration", duration.TotalSeconds },
                { "MemoryUsage", processInfo.MemoryUsage }
            };

            activityEvent.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);

            // Salva no banco de dados
            await _repository.AddActivityEventAsync(activityEvent, cancellationToken);
            await _repository.AddProcessEventAsync(processEvent, cancellationToken);

            // Dispara evento
            EventCaptured?.Invoke(this, activityEvent);

            return activityEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar evento de fim de processo");
            return null;
        }
    }

    private async Task MonitorResourceUsage(IEnumerable<ProcessInfo> processes, DateTime currentTime, List<ActivityEvent> events, CancellationToken cancellationToken)
    {
        try
        {
            var highMemoryThreshold = 1024 * 1024 * 1024; // 1GB
            var highCpuThreshold = 80.0; // 80%
            
            foreach (var process in processes)
            {
                if (process.MemoryUsage > highMemoryThreshold)
                {
                    var highMemoryEvent = await CreateHighResourceEvent(process, ProcessEventType.HighMemory, currentTime, cancellationToken);
                    if (highMemoryEvent != null)
                    {
                        events.Add(highMemoryEvent);
                    }
                }
                
                if (process.CpuUsage > highCpuThreshold)
                {
                    var highCpuEvent = await CreateHighResourceEvent(process, ProcessEventType.HighCpu, currentTime, cancellationToken);
                    if (highCpuEvent != null)
                    {
                        events.Add(highCpuEvent);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao monitorar uso de recursos");
        }
    }

    private async Task<ActivityEvent?> CreateHighResourceEvent(ProcessInfo processInfo, ProcessEventType eventType, DateTime currentTime, CancellationToken cancellationToken)
    {
        try
        {
            var activityEvent = new ActivityEvent
            {
                Id = Guid.NewGuid(),
                AgentId = _agentId,
                UserId = _userId,
                Timestamp = currentTime,
                Type = eventType == ProcessEventType.HighMemory ? ActivityType.ProcessStart : ActivityType.ProcessStart,
                Application = processInfo.ProcessName,
                WindowTitle = $"{processInfo.ProcessName} - High Resource Usage",
                Duration = TimeSpan.Zero,
                IsActive = true,
                CreatedAt = currentTime,
                UpdatedAt = currentTime
            };

            var processEvent = new ProcessEvent
            {
                Id = Guid.NewGuid(),
                ActivityEventId = activityEvent.Id,
                ProcessId = processInfo.ProcessId,
                ProcessName = processInfo.ProcessName,
                ExecutablePath = processInfo.ExecutablePath,
                EventType = eventType,
                MemoryUsage = processInfo.MemoryUsage,
                CpuUsage = processInfo.CpuUsage,
                HandleCount = processInfo.HandleCount,
                ThreadCount = processInfo.ThreadCount,
                StartTime = processInfo.StartTime,
                Timestamp = currentTime
            };

            // Adiciona metadados
            var metadata = new Dictionary<string, object>
            {
                { "ProcessId", processInfo.ProcessId },
                { "EventType", eventType.ToString() },
                { "MemoryUsage", processInfo.MemoryUsage },
                { "CpuUsage", processInfo.CpuUsage },
                { "Threshold", eventType == ProcessEventType.HighMemory ? "1GB" : "80%" }
            };

            activityEvent.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);

            // Salva no banco de dados
            await _repository.AddActivityEventAsync(activityEvent, cancellationToken);
            await _repository.AddProcessEventAsync(processEvent, cancellationToken);

            return activityEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar evento de alto uso de recursos");
            return null;
        }
    }

    #region Helper Methods

    private string GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private DateTime GetProcessStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }

    private string GetProcessWorkingDirectory(Process process)
    {
        try
        {
            return process.StartInfo.WorkingDirectory ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string GetProcessCommandLine(Process process)
    {
        try
        {
            return process.StartInfo.Arguments ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private int? GetParentProcessId(Process process)
    {
        try
        {
            using var query = new ManagementObjectSearcher($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}");
            using var results = query.Get();
            
            foreach (ManagementObject mo in results)
            {
                return Convert.ToInt32(mo["ParentProcessId"]);
            }
        }
        catch
        {
            // Ignora erro
        }
        return null;
    }

    private string GetProcessUser(Process process)
    {
        try
        {
            return Environment.UserName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string GetProcessDomain(Process process)
    {
        try
        {
            return Environment.UserDomainName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private long GetProcessMemoryUsage(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    private double GetProcessCpuUsage(Process process)
    {
        try
        {
            return process.TotalProcessorTime.TotalMilliseconds;
        }
        catch
        {
            return 0.0;
        }
    }

    private int GetProcessHandleCount(Process process)
    {
        try
        {
            return process.HandleCount;
        }
        catch
        {
            return 0;
        }
    }

    private int GetProcessThreadCount(Process process)
    {
        try
        {
            return process.Threads.Count;
        }
        catch
        {
            return 0;
        }
    }

    private string? GetProcessNameById(int? processId)
    {
        if (!processId.HasValue)
            return null;

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    private async void OnTimerElapsed(object? state)
    {
        if (!_isRunning) return;

        try
        {
            await CaptureAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no timer do ProcessMonitor");
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

    private class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string? ExecutablePath { get; set; }
        public DateTime StartTime { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? CommandLine { get; set; }
        public int? ParentProcessId { get; set; }
        public string? User { get; set; }
        public string? Domain { get; set; }
        public long MemoryUsage { get; set; }
        public double CpuUsage { get; set; }
        public int HandleCount { get; set; }
        public int ThreadCount { get; set; }
    }
}