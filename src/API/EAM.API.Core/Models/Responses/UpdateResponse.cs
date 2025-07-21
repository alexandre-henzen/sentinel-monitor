using System.ComponentModel.DataAnnotations;

namespace EAM.API.Core.Models.Responses;

/// <summary>
/// Resposta com informações de atualização do agente
/// </summary>
public class UpdateResponse
{
    /// <summary>
    /// Versão atual mais recente
    /// </summary>
    [Required]
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>
    /// Versão do agente que fez a requisição
    /// </summary>
    [Required]
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Indica se há atualização disponível
    /// </summary>
    [Required]
    public bool UpdateAvailable { get; set; }

    /// <summary>
    /// Indica se a atualização é obrigatória
    /// </summary>
    [Required]
    public bool IsUpdateRequired { get; set; }

    /// <summary>
    /// Criticidade da atualização
    /// </summary>
    [Required]
    public UpdateCriticality Criticality { get; set; }

    /// <summary>
    /// URL para download da atualização
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Tamanho do arquivo de atualização em bytes
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Hash SHA256 do arquivo de atualização
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// Instruções de instalação
    /// </summary>
    public string? InstallationInstructions { get; set; }

    /// <summary>
    /// Notas de versão
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Data de lançamento da versão
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Data limite para instalação (para atualizações obrigatórias)
    /// </summary>
    public DateTime? InstallationDeadline { get; set; }

    /// <summary>
    /// Compatibilidade com versões anteriores
    /// </summary>
    public CompatibilityInfo Compatibility { get; set; } = new();

    /// <summary>
    /// Pré-requisitos para instalação
    /// </summary>
    public string[] Prerequisites { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Mudanças de breaking changes
    /// </summary>
    public string[] BreakingChanges { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Recursos adicionados
    /// </summary>
    public string[] NewFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Correções de bugs
    /// </summary>
    public string[] BugFixes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Melhorias de performance
    /// </summary>
    public string[] PerformanceImprovements { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Próxima verificação de atualização
    /// </summary>
    public DateTime NextUpdateCheck { get; set; }

    /// <summary>
    /// Configurações de update
    /// </summary>
    public UpdateSettings Settings { get; set; } = new();
}

/// <summary>
/// Criticidade da atualização
/// </summary>
public enum UpdateCriticality
{
    /// <summary>
    /// Atualização opcional
    /// </summary>
    Optional = 0,

    /// <summary>
    /// Atualização recomendada
    /// </summary>
    Recommended = 1,

    /// <summary>
    /// Atualização importante
    /// </summary>
    Important = 2,

    /// <summary>
    /// Atualização crítica
    /// </summary>
    Critical = 3,

    /// <summary>
    /// Atualização de segurança
    /// </summary>
    Security = 4
}

/// <summary>
/// Informações de compatibilidade
/// </summary>
public class CompatibilityInfo
{
    /// <summary>
    /// Versão mínima suportada
    /// </summary>
    public string MinimumVersion { get; set; } = string.Empty;

    /// <summary>
    /// Versões suportadas
    /// </summary>
    public string[] SupportedVersions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Versões descontinuadas
    /// </summary>
    public string[] DeprecatedVersions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Sistemas operacionais suportados
    /// </summary>
    public string[] SupportedOperatingSystems { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Versões do .NET suportadas
    /// </summary>
    public string[] SupportedDotNetVersions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Configurações de atualização
/// </summary>
public class UpdateSettings
{
    /// <summary>
    /// Habilitar atualizações automáticas
    /// </summary>
    public bool EnableAutoUpdate { get; set; } = false;

    /// <summary>
    /// Intervalo de verificação em horas
    /// </summary>
    public int CheckIntervalHours { get; set; } = 24;

    /// <summary>
    /// Horário preferido para instalação
    /// </summary>
    public TimeSpan? PreferredInstallTime { get; set; }

    /// <summary>
    /// Dias da semana para instalação
    /// </summary>
    public DayOfWeek[] InstallationDays { get; set; } = Array.Empty<DayOfWeek>();

    /// <summary>
    /// Permitir reinicialização automática
    /// </summary>
    public bool AllowAutoRestart { get; set; } = false;

    /// <summary>
    /// Tempo de espera antes da reinicialização (minutos)
    /// </summary>
    public int RestartDelayMinutes { get; set; } = 15;

    /// <summary>
    /// Canal de atualização (stable, beta, alpha)
    /// </summary>
    public string UpdateChannel { get; set; } = "stable";
}