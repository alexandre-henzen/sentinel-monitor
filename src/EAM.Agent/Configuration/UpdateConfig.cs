using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EAM.Agent.Configuration;

public class UpdateConfig
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UpdateConfig> _logger;

    public UpdateConfig(IConfiguration configuration, ILogger<UpdateConfig> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Configurações básicas de update
    public bool AutoUpdate => _configuration.GetValue<bool>("Updates:AutoUpdate", true);
    public bool EnablePreRelease => _configuration.GetValue<bool>("Updates:EnablePreRelease", false);
    public bool RequireSignedUpdates => _configuration.GetValue<bool>("Updates:RequireSignedUpdates", true);
    public int CheckIntervalMinutes => _configuration.GetValue<int>("Updates:CheckIntervalMinutes", 60);
    public int MaxRetryAttempts => _configuration.GetValue<int>("Updates:MaxRetryAttempts", 3);
    public int RetryDelayMinutes => _configuration.GetValue<int>("Updates:RetryDelayMinutes", 15);
    public int DownloadTimeoutMinutes => _configuration.GetValue<int>("Updates:DownloadTimeoutMinutes", 30);
    public int InstallTimeoutMinutes => _configuration.GetValue<int>("Updates:InstallTimeoutMinutes", 10);
    public int BackupRetentionDays => _configuration.GetValue<int>("Updates:BackupRetentionDays", 30);
    public bool RequireUserConsent => _configuration.GetValue<bool>("Updates:RequireUserConsent", false);
    public bool RestartAfterUpdate => _configuration.GetValue<bool>("Updates:RestartAfterUpdate", true);
    public int RestartDelayMinutes => _configuration.GetValue<int>("Updates:RestartDelayMinutes", 5);
    public bool AllowRollback => _configuration.GetValue<bool>("Updates:AllowRollback", true);
    public string TrustedPublisher => _configuration.GetValue<string>("Updates:TrustedPublisher", "EAM Technologies");
    public string UpdateChannel => _configuration.GetValue<string>("Updates:UpdateChannel", "stable");
    public bool EnableTelemetry => _configuration.GetValue<bool>("Updates:EnableTelemetry", true);
    public bool ValidateUpdatePackage => _configuration.GetValue<bool>("Updates:ValidateUpdatePackage", true);
    public long MinimumDiskSpaceMB => _configuration.GetValue<long>("Updates:MinimumDiskSpaceMB", 500);
    public string[] ExcludedVersions => _configuration.GetSection("Updates:ExcludedVersions").Get<string[]>() ?? Array.Empty<string>();
    
    // Configurações de agendamento
    public bool EnableScheduling => _configuration.GetValue<bool>("Updates:Scheduling:Enabled", true);
    public TimeSpan UpdateWindowStart => ParseTimeSpan(_configuration.GetValue<string>("Updates:Scheduling:WindowStart", "02:00"));
    public TimeSpan UpdateWindowEnd => ParseTimeSpan(_configuration.GetValue<string>("Updates:Scheduling:WindowEnd", "06:00"));
    public string[] AllowedDaysOfWeek => _configuration.GetSection("Updates:Scheduling:AllowedDaysOfWeek").Get<string[]>() ?? new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    public bool RespectActiveHours => _configuration.GetValue<bool>("Updates:Scheduling:RespectActiveHours", true);
    public TimeSpan ActiveHoursStart => ParseTimeSpan(_configuration.GetValue<string>("Updates:Scheduling:ActiveHoursStart", "08:00"));
    public TimeSpan ActiveHoursEnd => ParseTimeSpan(_configuration.GetValue<string>("Updates:Scheduling:ActiveHoursEnd", "18:00"));
    public int MaintenanceWindowMinutes => _configuration.GetValue<int>("Updates:Scheduling:MaintenanceWindowMinutes", 120);
    public bool AllowUpdatesDuringBatteryPower => _configuration.GetValue<bool>("Updates:Scheduling:AllowUpdatesDuringBatteryPower", false);
    public int MinimumBatteryLevel => _configuration.GetValue<int>("Updates:Scheduling:MinimumBatteryLevel", 50);
    
    // Configurações de notificação
    public bool EnableNotifications => _configuration.GetValue<bool>("Updates:Notifications:Enabled", true);
    public bool NotifyOnUpdateAvailable => _configuration.GetValue<bool>("Updates:Notifications:OnUpdateAvailable", true);
    public bool NotifyOnUpdateInstalled => _configuration.GetValue<bool>("Updates:Notifications:OnUpdateInstalled", true);
    public bool NotifyOnUpdateFailed => _configuration.GetValue<bool>("Updates:Notifications:OnUpdateFailed", true);
    public bool NotifyBeforeRestart => _configuration.GetValue<bool>("Updates:Notifications:BeforeRestart", true);
    public int NotificationTimeoutMinutes => _configuration.GetValue<int>("Updates:Notifications:TimeoutMinutes", 10);
    public string NotificationSound => _configuration.GetValue<string>("Updates:Notifications:Sound", "Default");
    public bool ShowProgressDialog => _configuration.GetValue<bool>("Updates:Notifications:ShowProgressDialog", false);
    
    // Configurações de proxy
    public bool UseProxy => _configuration.GetValue<bool>("Updates:Proxy:Enabled", false);
    public string ProxyAddress => _configuration.GetValue<string>("Updates:Proxy:Address", "");
    public int ProxyPort => _configuration.GetValue<int>("Updates:Proxy:Port", 8080);
    public bool ProxyRequiresAuth => _configuration.GetValue<bool>("Updates:Proxy:RequiresAuth", false);
    public string ProxyUsername => _configuration.GetValue<string>("Updates:Proxy:Username", "");
    public string ProxyPassword => _configuration.GetValue<string>("Updates:Proxy:Password", "");
    public bool ProxyBypassLocal => _configuration.GetValue<bool>("Updates:Proxy:BypassLocal", true);
    
    // Configurações de logging
    public bool EnableVerboseLogging => _configuration.GetValue<bool>("Updates:Logging:Verbose", false);
    public bool LogToFile => _configuration.GetValue<bool>("Updates:Logging:LogToFile", true);
    public string LogFilePath => _configuration.GetValue<string>("Updates:Logging:FilePath", "%LOCALAPPDATA%\\EAM\\Logs\\updates.log");
    public int LogRetentionDays => _configuration.GetValue<int>("Updates:Logging:RetentionDays", 30);
    public long MaxLogFileSizeMB => _configuration.GetValue<long>("Updates:Logging:MaxFileSizeMB", 10);
    
    // Configurações de rede
    public int NetworkTimeoutSeconds => _configuration.GetValue<int>("Updates:Network:TimeoutSeconds", 30);
    public int NetworkMaxRetries => _configuration.GetValue<int>("Updates:Network:MaxRetries", 3);
    public int NetworkRetryDelaySeconds => _configuration.GetValue<int>("Updates:Network:RetryDelaySeconds", 5);
    public bool NetworkVerifySSL => _configuration.GetValue<bool>("Updates:Network:VerifySSL", true);
    public string NetworkUserAgent => _configuration.GetValue<string>("Updates:Network:UserAgent", "EAM-Agent-Updater/5.0");
    public int NetworkMaxConcurrentDownloads => _configuration.GetValue<int>("Updates:Network:MaxConcurrentDownloads", 1);
    public int NetworkBufferSizeKB => _configuration.GetValue<int>("Updates:Network:BufferSizeKB", 8);
    
    // Configurações de segurança
    public bool AllowUnsignedUpdates => _configuration.GetValue<bool>("Updates:Security:AllowUnsignedUpdates", false);
    public bool AllowDowngradeUpdates => _configuration.GetValue<bool>("Updates:Security:AllowDowngradeUpdates", false);
    public bool ValidateUpdateSource => _configuration.GetValue<bool>("Updates:Security:ValidateUpdateSource", true);
    public string[] AllowedUpdateSources => _configuration.GetSection("Updates:Security:AllowedUpdateSources").Get<string[]>() ?? new[] { "https://updates.eam.local" };
    public bool RequireAdminPrivileges => _configuration.GetValue<bool>("Updates:Security:RequireAdminPrivileges", true);
    public bool EnableUpdateSigning => _configuration.GetValue<bool>("Updates:Security:EnableUpdateSigning", true);
    public string SigningCertificateThumbprint => _configuration.GetValue<string>("Updates:Security:SigningCertificateThumbprint", "");
    public bool AllowBetaUpdates => _configuration.GetValue<bool>("Updates:Security:AllowBetaUpdates", false);
    
    // Métodos auxiliares
    public bool IsWithinUpdateWindow()
    {
        if (!EnableScheduling)
            return true;

        var now = DateTime.Now.TimeOfDay;
        var currentDay = DateTime.Now.DayOfWeek.ToString();
        
        // Verificar se hoje é um dia permitido
        if (!AllowedDaysOfWeek.Contains(currentDay))
        {
            _logger.LogDebug("Atualização não permitida hoje: {Day}", currentDay);
            return false;
        }

        // Verificar janela de atualização
        bool withinWindow;
        if (UpdateWindowStart <= UpdateWindowEnd)
        {
            // Janela normal (ex: 02:00 - 06:00)
            withinWindow = now >= UpdateWindowStart && now <= UpdateWindowEnd;
        }
        else
        {
            // Janela atravessa meia-noite (ex: 22:00 - 06:00)
            withinWindow = now >= UpdateWindowStart || now <= UpdateWindowEnd;
        }

        if (!withinWindow)
        {
            _logger.LogDebug("Fora da janela de atualização: {Now} (Janela: {Start} - {End})", now, UpdateWindowStart, UpdateWindowEnd);
            return false;
        }

        // Verificar horário ativo se configurado
        if (RespectActiveHours)
        {
            bool withinActiveHours;
            if (ActiveHoursStart <= ActiveHoursEnd)
            {
                withinActiveHours = now >= ActiveHoursStart && now <= ActiveHoursEnd;
            }
            else
            {
                withinActiveHours = now >= ActiveHoursStart || now <= ActiveHoursEnd;
            }

            if (withinActiveHours)
            {
                _logger.LogDebug("Dentro do horário ativo: {Now} (Horário ativo: {Start} - {End})", now, ActiveHoursStart, ActiveHoursEnd);
                return false;
            }
        }

        return true;
    }

    public bool IsVersionExcluded(string version)
    {
        return ExcludedVersions.Contains(version, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasSufficientBatteryLevel()
    {
        if (AllowUpdatesDuringBatteryPower)
            return true;

        try
        {
            var powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
            if (powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online)
            {
                return true; // Conectado à energia
            }

            var batteryLevel = (int)(powerStatus.BatteryLifePercent * 100);
            return batteryLevel >= MinimumBatteryLevel;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar nível da bateria");
            return true; // Assumir que está tudo bem se não conseguir verificar
        }
    }

    public bool IsUpdateSourceAllowed(string updateSource)
    {
        if (!ValidateUpdateSource)
            return true;

        return AllowedUpdateSources.Any(source => updateSource.StartsWith(source, StringComparison.OrdinalIgnoreCase));
    }

    public TimeSpan GetNextUpdateCheckInterval()
    {
        var baseInterval = TimeSpan.FromMinutes(CheckIntervalMinutes);
        
        // Adicionar pequena variação para evitar que todos os clientes consultem ao mesmo tempo
        var random = new Random();
        var variation = TimeSpan.FromMinutes(random.Next(-5, 5));
        
        return baseInterval + variation;
    }

    public Dictionary<string, object> GetConfigurationSummary()
    {
        return new Dictionary<string, object>
        {
            ["AutoUpdate"] = AutoUpdate,
            ["EnablePreRelease"] = EnablePreRelease,
            ["CheckIntervalMinutes"] = CheckIntervalMinutes,
            ["UpdateChannel"] = UpdateChannel,
            ["EnableScheduling"] = EnableScheduling,
            ["UpdateWindowStart"] = UpdateWindowStart.ToString(@"hh\:mm"),
            ["UpdateWindowEnd"] = UpdateWindowEnd.ToString(@"hh\:mm"),
            ["AllowedDaysOfWeek"] = string.Join(", ", AllowedDaysOfWeek),
            ["RequireSignedUpdates"] = RequireSignedUpdates,
            ["TrustedPublisher"] = TrustedPublisher,
            ["AllowRollback"] = AllowRollback,
            ["EnableNotifications"] = EnableNotifications,
            ["UseProxy"] = UseProxy,
            ["EnableTelemetry"] = EnableTelemetry
        };
    }

    private TimeSpan ParseTimeSpan(string timeString)
    {
        try
        {
            if (TimeSpan.TryParse(timeString, out var result))
            {
                return result;
            }
            
            // Tentar formato HH:mm
            if (DateTime.TryParseExact(timeString, "HH:mm", null, System.Globalization.DateTimeStyles.None, out var dateTime))
            {
                return dateTime.TimeOfDay;
            }
            
            _logger.LogWarning("Formato de tempo inválido: {TimeString}, usando padrão 02:00", timeString);
            return new TimeSpan(2, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer parse do tempo: {TimeString}", timeString);
            return new TimeSpan(2, 0, 0);
        }
    }

    public void ValidateConfiguration()
    {
        var warnings = new List<string>();
        
        if (CheckIntervalMinutes < 5)
        {
            warnings.Add("Intervalo de verificação muito baixo (< 5 minutos)");
        }
        
        if (MaxRetryAttempts < 1)
        {
            warnings.Add("Número máximo de tentativas deve ser pelo menos 1");
        }
        
        if (DownloadTimeoutMinutes < 1)
        {
            warnings.Add("Timeout de download deve ser pelo menos 1 minuto");
        }
        
        if (RequireSignedUpdates && AllowUnsignedUpdates)
        {
            warnings.Add("Configuração contraditória: RequireSignedUpdates=true e AllowUnsignedUpdates=true");
        }
        
        if (EnableScheduling && AllowedDaysOfWeek.Length == 0)
        {
            warnings.Add("Agendamento habilitado mas nenhum dia da semana foi especificado");
        }
        
        if (UseProxy && string.IsNullOrEmpty(ProxyAddress))
        {
            warnings.Add("Proxy habilitado mas endereço não especificado");
        }
        
        if (warnings.Count > 0)
        {
            _logger.LogWarning("Avisos de configuração de updates:");
            foreach (var warning in warnings)
            {
                _logger.LogWarning("  - {Warning}", warning);
            }
        }
    }
}