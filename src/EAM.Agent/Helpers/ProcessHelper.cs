using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.ComponentModel;

namespace EAM.Agent.Helpers;

public class ProcessResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ProcessHelper
{
    private readonly ILogger<ProcessHelper> _logger;

    public ProcessHelper(ILogger<ProcessHelper> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessResult> RunMsiInstallerAsync(string installerPath, string arguments = "/quiet /norestart", CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Executando instalador MSI: {InstallerPath} {Arguments}", installerPath, arguments);

            if (!File.Exists(installerPath))
            {
                return new ProcessResult
                {
                    Success = false,
                    ErrorMessage = $"Arquivo instalador não encontrado: {installerPath}",
                    Duration = stopwatch.Elapsed
                };
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{installerPath}\" {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Verb = "runas" // Executar como administrador
            };

            using var process = new Process { StartInfo = processInfo };
            
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger.LogDebug("MSI Output: {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    _logger.LogWarning("MSI Error: {Error}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
            
            stopwatch.Stop();

            var result = new ProcessResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
                Duration = stopwatch.Elapsed
            };

            if (result.Success)
            {
                _logger.LogInformation("Instalador MSI executado com sucesso (código: {ExitCode}, duração: {Duration})", 
                    result.ExitCode, result.Duration);
            }
            else
            {
                result.ErrorMessage = $"Falha na instalação MSI (código: {result.ExitCode})";
                _logger.LogError("Falha na instalação MSI: código {ExitCode}, erro: {Error}", 
                    result.ExitCode, result.StandardError);
            }

            return result;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            _logger.LogWarning("Instalação cancelada pelo usuário (UAC)");
            return new ProcessResult
            {
                Success = false,
                ErrorMessage = "Instalação cancelada pelo usuário",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar instalador MSI");
            return new ProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<ProcessResult> RunCommandAsync(string fileName, string arguments = "", CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Executando comando: {FileName} {Arguments}", fileName, arguments);

            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
            
            stopwatch.Stop();

            var result = new ProcessResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
                Duration = stopwatch.Elapsed
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comando: {FileName} {Arguments}", fileName, arguments);
            return new ProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<bool> IsProcessRunningAsync(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar se processo está em execução: {ProcessName}", processName);
            return false;
        }
    }

    public async Task<bool> KillProcessAsync(string processName, TimeSpan timeout)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            
            if (processes.Length == 0)
            {
                _logger.LogDebug("Processo não encontrado: {ProcessName}", processName);
                return true;
            }

            _logger.LogInformation("Finalizando {Count} processo(s): {ProcessName}", processes.Length, processName);

            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        await process.WaitForExitAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao finalizar processo {ProcessId}: {ProcessName}", process.Id, processName);
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Verificar se todos os processos foram finalizados
            var remaining = Process.GetProcessesByName(processName);
            if (remaining.Length > 0)
            {
                _logger.LogWarning("Ainda existem {Count} processo(s) em execução: {ProcessName}", remaining.Length, processName);
                return false;
            }

            _logger.LogInformation("Processos finalizados com sucesso: {ProcessName}", processName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar processo: {ProcessName}", processName);
            return false;
        }
    }

    public async Task<ProcessResult> StartServiceAsync(string serviceName)
    {
        return await RunCommandAsync("sc.exe", $"start \"{serviceName}\"");
    }

    public async Task<ProcessResult> StopServiceAsync(string serviceName)
    {
        return await RunCommandAsync("sc.exe", $"stop \"{serviceName}\"");
    }

    public async Task<ProcessResult> RestartServiceAsync(string serviceName)
    {
        var stopResult = await StopServiceAsync(serviceName);
        
        if (!stopResult.Success)
        {
            _logger.LogWarning("Falha ao parar o serviço {ServiceName}: {Error}", serviceName, stopResult.ErrorMessage);
        }

        // Aguardar um pouco antes de iniciar novamente
        await Task.Delay(2000);

        return await StartServiceAsync(serviceName);
    }

    public async Task<bool> IsServiceRunningAsync(string serviceName)
    {
        try
        {
            var result = await RunCommandAsync("sc.exe", $"query \"{serviceName}\"");
            return result.Success && result.StandardOutput.Contains("RUNNING");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar status do serviço: {ServiceName}", serviceName);
            return false;
        }
    }

    public async Task<ProcessResult> InstallServiceAsync(string serviceName, string binPath, string displayName = "")
    {
        var arguments = $"create \"{serviceName}\" binPath= \"{binPath}\"";
        
        if (!string.IsNullOrEmpty(displayName))
        {
            arguments += $" DisplayName= \"{displayName}\"";
        }

        return await RunCommandAsync("sc.exe", arguments);
    }

    public async Task<ProcessResult> UninstallServiceAsync(string serviceName)
    {
        return await RunCommandAsync("sc.exe", $"delete \"{serviceName}\"");
    }

    public async Task<bool> WaitForProcessToExitAsync(string processName, TimeSpan timeout)
    {
        var endTime = DateTime.UtcNow + timeout;
        
        while (DateTime.UtcNow < endTime)
        {
            if (!await IsProcessRunningAsync(processName))
            {
                return true;
            }

            await Task.Delay(1000);
        }

        return false;
    }

    public async Task<ProcessResult> RestartComputerAsync(TimeSpan delay)
    {
        var delaySeconds = (int)delay.TotalSeconds;
        return await RunCommandAsync("shutdown.exe", $"/r /t {delaySeconds}");
    }

    public async Task<ProcessResult> CancelRestartAsync()
    {
        return await RunCommandAsync("shutdown.exe", "/a");
    }

    public bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar se está executando como administrador");
            return false;
        }
    }

    public async Task<ProcessResult> ElevateToAdministratorAsync(string applicationPath, string arguments = "")
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = applicationPath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                return new ProcessResult
                {
                    Success = process.ExitCode == 0,
                    ExitCode = process.ExitCode
                };
            }
            else
            {
                return new ProcessResult
                {
                    Success = false,
                    ErrorMessage = "Falha ao iniciar processo com privilégios elevados"
                };
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return new ProcessResult
            {
                Success = false,
                ErrorMessage = "Elevação de privilégios cancelada pelo usuário"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao elevar privilégios");
            return new ProcessResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public int GetCurrentProcessId()
    {
        return Environment.ProcessId;
    }

    public string GetCurrentProcessName()
    {
        return Process.GetCurrentProcess().ProcessName;
    }

    public List<Process> GetProcessesByName(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter processos por nome: {ProcessName}", processName);
            return new List<Process>();
        }
    }
}