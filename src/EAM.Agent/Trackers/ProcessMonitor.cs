using EAM.PluginSDK;
using EAM.Agent.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Management;

namespace EAM.Agent.Trackers;

public class ProcessMonitor : ITracker
{
    private readonly ILogger<ProcessMonitor> _logger;
    private readonly IConfiguration _configuration;
    private readonly ScoringService _scoringService;
    private readonly HashSet<int> _trackedProcesses;
    private readonly Dictionary<int, ProcessInfo> _processCache;
    private DateTime _lastScanTime;

    public string Name => "ProcessMonitor";
    public bool IsEnabled => _configuration.GetValue<bool>("Trackers:ProcessMonitor:Enabled");

    public ProcessMonitor(ILogger<ProcessMonitor> logger, IConfiguration configuration, ScoringService scoringService)
    {
        _logger = logger;
        _configuration = configuration;
        _scoringService = scoringService;
        _trackedProcesses = new HashSet<int>();
        _processCache = new Dictionary<int, ProcessInfo>();
        _lastScanTime = DateTime.UtcNow;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("ProcessMonitor inicializado");
        
        // Carregar processos existentes
        await LoadExistingProcessesAsync();
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync()
    {
        var events = new List<ActivityEvent>();

        try
        {
            var processEvents = await ScanForProcessChangesAsync();
            events.AddRange(processEvents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro monitorando processos");
        }

        return events;
    }

    public Task StopAsync()
    {
        _logger.LogInformation("ProcessMonitor parado");
        return Task.CompletedTask;
    }

    private async Task LoadExistingProcessesAsync()
    {
        try
        {
            var processes = Process.GetProcesses();
            
            foreach (var process in processes)
            {
                try
                {
                    if (ShouldTrackProcess(process))
                    {
                        _trackedProcesses.Add(process.Id);
                        _processCache[process.Id] = await CreateProcessInfoAsync(process);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro carregando processo {ProcessId}", process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }
            
            _logger.LogDebug("Carregados {Count} processos existentes", _trackedProcesses.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro carregando processos existentes");
        }
    }

    private async Task<List<ActivityEvent>> ScanForProcessChangesAsync()
    {
        var events = new List<ActivityEvent>();
        var currentProcesses = new HashSet<int>();

        try
        {
            var processes = Process.GetProcesses();
            
            foreach (var process in processes)
            {
                try
                {
                    if (ShouldTrackProcess(process))
                    {
                        currentProcesses.Add(process.Id);
                        
                        // Verificar se é um novo processo
                        if (!_trackedProcesses.Contains(process.Id))
                        {
                            var processInfo = await CreateProcessInfoAsync(process);
                            var startEvent = await CreateProcessStartEventAsync(processInfo);
                            
                            events.Add(startEvent);
                            _trackedProcesses.Add(process.Id);
                            _processCache[process.Id] = processInfo;
                            
                            _logger.LogDebug("Novo processo detectado: {ProcessName} (PID: {ProcessId})", 
                                processInfo.ProcessName, processInfo.ProcessId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro processando processo {ProcessId}", process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Detectar processos que terminaram
            var terminatedProcesses = _trackedProcesses.Except(currentProcesses).ToList();
            foreach (var terminatedPid in terminatedProcesses)
            {
                if (_processCache.TryGetValue(terminatedPid, out var processInfo))
                {
                    var stopEvent = await CreateProcessStopEventAsync(processInfo);
                    events.Add(stopEvent);
                    
                    _logger.LogDebug("Processo terminado: {ProcessName} (PID: {ProcessId})", 
                        processInfo.ProcessName, processInfo.ProcessId);
                }
                
                _trackedProcesses.Remove(terminatedPid);
                _processCache.Remove(terminatedPid);
            }

            _lastScanTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro escaneando mudanças de processos");
        }

        return events;
    }

    private async Task<ProcessInfo> CreateProcessInfoAsync(Process process)
    {
        var processInfo = new ProcessInfo
        {
            ProcessId = process.Id,
            ProcessName = process.ProcessName,
            StartTime = DateTime.UtcNow
        };

        try
        {
            processInfo.StartTime = process.StartTime;
            processInfo.ExecutablePath = process.MainModule?.FileName;
            processInfo.WorkingDirectory = GetProcessWorkingDirectory(process.Id);
            processInfo.CommandLine = await GetProcessCommandLineAsync(process.Id);
            processInfo.ParentProcessId = GetParentProcessId(process.Id);
            processInfo.WindowTitle = process.MainWindowTitle;
            processInfo.IsVisible = process.MainWindowHandle != IntPtr.Zero;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro obtendo informações detalhadas do processo {ProcessId}", process.Id);
        }

        return processInfo;
    }

    private async Task<ActivityEvent> CreateProcessStartEventAsync(ProcessInfo processInfo)
    {
        var productivityScore = await _scoringService.CalculateProductivityScoreAsync(processInfo.ProcessName);
        
        var startEvent = new ActivityEvent("ProcessStart")
        {
            ProcessName = processInfo.ProcessName,
            ProcessId = processInfo.ProcessId,
            ApplicationName = processInfo.ProcessName,
            WindowTitle = processInfo.WindowTitle,
            ProductivityScore = productivityScore,
            Timestamp = processInfo.StartTime
        };

        // Adicionar metadados
        startEvent.AddMetadata("executable_path", processInfo.ExecutablePath);
        startEvent.AddMetadata("working_directory", processInfo.WorkingDirectory);
        startEvent.AddMetadata("command_line", processInfo.CommandLine);
        startEvent.AddMetadata("parent_process_id", processInfo.ParentProcessId);
        startEvent.AddMetadata("is_visible", processInfo.IsVisible);
        startEvent.AddMetadata("start_time", processInfo.StartTime.ToString("O"));

        return startEvent;
    }

    private async Task<ActivityEvent> CreateProcessStopEventAsync(ProcessInfo processInfo)
    {
        var duration = (DateTime.UtcNow - processInfo.StartTime).TotalSeconds;
        var productivityScore = await _scoringService.CalculateProductivityScoreAsync(processInfo.ProcessName);
        
        var stopEvent = new ActivityEvent("ProcessStop")
        {
            ProcessName = processInfo.ProcessName,
            ProcessId = processInfo.ProcessId,
            ApplicationName = processInfo.ProcessName,
            WindowTitle = processInfo.WindowTitle,
            DurationSeconds = (int)duration,
            ProductivityScore = productivityScore,
            Timestamp = DateTime.UtcNow
        };

        // Adicionar metadados
        stopEvent.AddMetadata("executable_path", processInfo.ExecutablePath);
        stopEvent.AddMetadata("start_time", processInfo.StartTime.ToString("O"));
        stopEvent.AddMetadata("duration_seconds", duration);
        stopEvent.AddMetadata("parent_process_id", processInfo.ParentProcessId);

        return stopEvent;
    }

    private bool ShouldTrackProcess(Process process)
    {
        try
        {
            // Filtrar processos do sistema
            if (IsSystemProcess(process.ProcessName))
                return false;

            // Filtrar processos sem janela principal (serviços, etc.)
            if (process.MainWindowHandle == IntPtr.Zero && string.IsNullOrEmpty(process.MainWindowTitle))
                return false;

            // Filtrar processos com nomes muito curtos (provavelmente sistema)
            if (process.ProcessName.Length < 3)
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsSystemProcess(string processName)
    {
        var systemProcesses = new[]
        {
            "system", "idle", "csrss", "wininit", "winlogon", "services", "lsass",
            "svchost", "spoolsv", "explorer", "dwm", "audiodg", "conhost",
            "sihost", "taskhostw", "searchindexer", "wmiprvse", "dllhost",
            "rundll32", "taskeng", "taskhost", "userinit", "logonui"
        };

        return systemProcesses.Contains(processName.ToLowerInvariant());
    }

    private string? GetProcessWorkingDirectory(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.StartInfo.WorkingDirectory;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetProcessCommandLineAsync(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["CommandLine"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro obtendo command line do processo {ProcessId}", processId);
        }

        return null;
    }

    private int? GetParentProcessId(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["ParentProcessId"] != null)
                {
                    return Convert.ToInt32(obj["ParentProcessId"]);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro obtendo parent process ID do processo {ProcessId}", processId);
        }

        return null;
    }

    private class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string? ExecutablePath { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? CommandLine { get; set; }
        public int? ParentProcessId { get; set; }
        public string? WindowTitle { get; set; }
        public bool IsVisible { get; set; }
    }
}