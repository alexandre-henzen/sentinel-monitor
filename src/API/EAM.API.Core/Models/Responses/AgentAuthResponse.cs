using System.ComponentModel.DataAnnotations;

namespace EAM.API.Core.Models.Responses;

/// <summary>
/// Resposta da autenticação do agente
/// </summary>
public class AgentAuthResponse
{
    /// <summary>
    /// Token JWT para autenticação
    /// </summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do token (sempre "Bearer")
    /// </summary>
    [Required]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Tempo de vida do token em segundos
    /// </summary>
    [Required]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Data de expiração do token
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Escopo do token
    /// </summary>
    [Required]
    public string Scope { get; set; } = "eam.agent";

    /// <summary>
    /// ID único da sessão
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Configurações do agente
    /// </summary>
    public AgentConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// Informações do servidor
    /// </summary>
    public ServerInfo Server { get; set; } = new();

    /// <summary>
    /// Endpoints disponíveis para o agente
    /// </summary>
    public Dictionary<string, string> Endpoints { get; set; } = new();

    /// <summary>
    /// Recursos disponíveis
    /// </summary>
    public string[] AvailableFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Limites de rate limiting
    /// </summary>
    public RateLimitInfo RateLimits { get; set; } = new();

    /// <summary>
    /// Próxima verificação de heartbeat
    /// </summary>
    public DateTime NextHeartbeat { get; set; }

    /// <summary>
    /// Instruções especiais para o agente
    /// </summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// Configuração do agente
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// Intervalo de sincronização de dados (segundos)
    /// </summary>
    public int DataSyncInterval { get; set; } = 60;

    /// <summary>
    /// Intervalo de captura de screenshot (segundos)
    /// </summary>
    public int ScreenshotInterval { get; set; } = 300;

    /// <summary>
    /// Intervalo de heartbeat (segundos)
    /// </summary>
    public int HeartbeatInterval { get; set; } = 30;

    /// <summary>
    /// Habilitar rastreamento web
    /// </summary>
    public bool EnableWebTracking { get; set; } = true;

    /// <summary>
    /// Habilitar rastreamento do Teams
    /// </summary>
    public bool EnableTeamsTracking { get; set; } = true;

    /// <summary>
    /// Habilitar capturas de tela
    /// </summary>
    public bool EnableScreenshots { get; set; } = true;

    /// <summary>
    /// Qualidade das capturas de tela (1-100)
    /// </summary>
    public int ScreenshotQuality { get; set; } = 70;

    /// <summary>
    /// Aplicações a serem monitoradas
    /// </summary>
    public string[] MonitoredApplications { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Aplicações a serem excluídas
    /// </summary>
    public string[] ExcludedApplications { get; set; } = Array.Empty<string>();

    /// <summary>
    /// URLs a serem excluídas
    /// </summary>
    public string[] ExcludedUrls { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Tamanho máximo do lote de eventos
    /// </summary>
    public int MaxBatchSize { get; set; } = 1000;

    /// <summary>
    /// Configurações de segurança
    /// </summary>
    public SecuritySettings Security { get; set; } = new();

    /// <summary>
    /// Configurações de logging
    /// </summary>
    public LoggingSettings Logging { get; set; } = new();
}

/// <summary>
/// Configurações de segurança
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Habilitar criptografia de dados
    /// </summary>
    public bool EnableDataEncryption { get; set; } = true;

    /// <summary>
    /// Habilitar compressão de dados
    /// </summary>
    public bool EnableDataCompression { get; set; } = true;

    /// <summary>
    /// Habilitar verificação de integridade
    /// </summary>
    public bool EnableIntegrityCheck { get; set; } = true;

    /// <summary>
    /// Certificado do servidor (Base64)
    /// </summary>
    public string? ServerCertificate { get; set; }

    /// <summary>
    /// Chave pública para criptografia
    /// </summary>
    public string? PublicKey { get; set; }
}

/// <summary>
/// Configurações de logging
/// </summary>
public class LoggingSettings
{
    /// <summary>
    /// Nível de log mínimo
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Habilitar logs estruturados
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Habilitar upload de logs
    /// </summary>
    public bool EnableLogUpload { get; set; } = false;

    /// <summary>
    /// Intervalo de upload de logs (segundos)
    /// </summary>
    public int LogUploadInterval { get; set; } = 3600;
}

/// <summary>
/// Informações do servidor
/// </summary>
public class ServerInfo
{
    /// <summary>
    /// Versão da API
    /// </summary>
    public string Version { get; set; } = "5.0.0";

    /// <summary>
    /// Ambiente (Development, Production)
    /// </summary>
    public string Environment { get; set; } = "Production";

    /// <summary>
    /// Região do servidor
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Fuso horário do servidor
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Timestamp atual do servidor
    /// </summary>
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Recursos disponíveis
    /// </summary>
    public string[] SupportedFeatures { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Informações de rate limiting
/// </summary>
public class RateLimitInfo
{
    /// <summary>
    /// Limite de requisições por minuto
    /// </summary>
    public int RequestsPerMinute { get; set; } = 100;

    /// <summary>
    /// Limite de eventos por requisição
    /// </summary>
    public int EventsPerRequest { get; set; } = 1000;

    /// <summary>
    /// Limite de uploads por hora
    /// </summary>
    public int UploadsPerHour { get; set; } = 60;

    /// <summary>
    /// Tamanho máximo de upload (bytes)
    /// </summary>
    public long MaxUploadSize { get; set; } = 50_000_000;
}