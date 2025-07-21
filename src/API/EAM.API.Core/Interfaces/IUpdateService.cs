using EAM.API.Core.Models.Responses;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para serviço de atualizações do agente
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Obtém informações da versão mais recente
    /// </summary>
    /// <param name="currentVersion">Versão atual do agente</param>
    /// <param name="osVersion">Versão do sistema operacional</param>
    /// <param name="architecture">Arquitetura do sistema</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações de atualização</returns>
    Task<UpdateResponse> GetLatestVersionAsync(
        string currentVersion,
        string osVersion,
        string architecture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se uma atualização é obrigatória
    /// </summary>
    /// <param name="currentVersion">Versão atual do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualização é obrigatória</returns>
    Task<bool> IsUpdateRequiredAsync(string currentVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém URL de download para uma versão específica
    /// </summary>
    /// <param name="version">Versão desejada</param>
    /// <param name="osVersion">Versão do sistema operacional</param>
    /// <param name="architecture">Arquitetura do sistema</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL de download</returns>
    Task<string?> GetDownloadUrlAsync(
        string version,
        string osVersion,
        string architecture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra download de uma versão
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="version">Versão baixada</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se registrado com sucesso</returns>
    Task<bool> RegisterDownloadAsync(Guid agentId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra instalação de uma versão
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="version">Versão instalada</param>
    /// <param name="installationResult">Resultado da instalação</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se registrado com sucesso</returns>
    Task<bool> RegisterInstallationAsync(
        Guid agentId,
        string version,
        InstallationResult installationResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas de atualização
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de atualização</returns>
    Task<UpdateStatistics> GetUpdateStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém histórico de versões
    /// </summary>
    /// <param name="includePreRelease">Incluir versões pré-lançamento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de versões</returns>
    Task<IEnumerable<VersionInfo>> GetVersionHistoryAsync(
        bool includePreRelease = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém compatibilidade de uma versão
    /// </summary>
    /// <param name="version">Versão a verificar</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações de compatibilidade</returns>
    Task<CompatibilityInfo> GetVersionCompatibilityAsync(
        string version,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado da instalação
/// </summary>
public class InstallationResult
{
    /// <summary>
    /// Indica se a instalação foi bem-sucedida
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensagem de resultado
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Código de erro (se houver)
    /// </summary>
    public int? ErrorCode { get; set; }

    /// <summary>
    /// Detalhes do erro
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Tempo de instalação
    /// </summary>
    public TimeSpan InstallationTime { get; set; }

    /// <summary>
    /// Indica se foi necessário reiniciar
    /// </summary>
    public bool RequiredRestart { get; set; }

    /// <summary>
    /// Versão anterior
    /// </summary>
    public string? PreviousVersion { get; set; }

    /// <summary>
    /// Timestamp da instalação
    /// </summary>
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Estatísticas de atualização
/// </summary>
public class UpdateStatistics
{
    /// <summary>
    /// Total de atualizações disponíveis
    /// </summary>
    public int TotalUpdatesAvailable { get; set; }

    /// <summary>
    /// Atualizações obrigatórias
    /// </summary>
    public int RequiredUpdates { get; set; }

    /// <summary>
    /// Atualizações opcionais
    /// </summary>
    public int OptionalUpdates { get; set; }

    /// <summary>
    /// Downloads na última semana
    /// </summary>
    public long DownloadsLastWeek { get; set; }

    /// <summary>
    /// Instalações na última semana
    /// </summary>
    public long InstallationsLastWeek { get; set; }

    /// <summary>
    /// Taxa de sucesso de instalação
    /// </summary>
    public decimal InstallationSuccessRate { get; set; }

    /// <summary>
    /// Tempo médio de instalação
    /// </summary>
    public TimeSpan AverageInstallationTime { get; set; }

    /// <summary>
    /// Distribuição de versões
    /// </summary>
    public Dictionary<string, int> VersionDistribution { get; set; } = new();

    /// <summary>
    /// Agentes que precisam de atualização
    /// </summary>
    public int AgentsNeedingUpdate { get; set; }

    /// <summary>
    /// Última versão lançada
    /// </summary>
    public string? LatestVersion { get; set; }

    /// <summary>
    /// Data do último lançamento
    /// </summary>
    public DateTime? LastReleaseDate { get; set; }
}

/// <summary>
/// Informações de versão
/// </summary>
public class VersionInfo
{
    /// <summary>
    /// Número da versão
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Nome da versão
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descrição da versão
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Data de lançamento
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Indica se é pré-lançamento
    /// </summary>
    public bool IsPreRelease { get; set; }

    /// <summary>
    /// Indica se é atualização obrigatória
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Criticidade da atualização
    /// </summary>
    public UpdateCriticality Criticality { get; set; }

    /// <summary>
    /// Notas de versão
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Recursos adicionados
    /// </summary>
    public string[] NewFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Correções de bugs
    /// </summary>
    public string[] BugFixes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Breaking changes
    /// </summary>
    public string[] BreakingChanges { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Tamanho do arquivo em bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Hash do arquivo
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// Compatibilidade
    /// </summary>
    public CompatibilityInfo Compatibility { get; set; } = new();
}