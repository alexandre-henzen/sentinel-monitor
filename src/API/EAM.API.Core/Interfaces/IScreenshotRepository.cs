using EAM.API.Core.Entities;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para repositório de screenshots
/// </summary>
public interface IScreenshotRepository
{
    /// <summary>
    /// Obtém um screenshot por ID
    /// </summary>
    /// <param name="id">ID do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Screenshot encontrado ou null</returns>
    Task<Screenshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém screenshots por agente e período
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots</returns>
    Task<IEnumerable<Screenshot>> GetByAgentAndPeriodAsync(
        Guid agentId,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém screenshots por usuário e período
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots</returns>
    Task<IEnumerable<Screenshot>> GetByUserAndPeriodAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém screenshots por status de processamento
    /// </summary>
    /// <param name="status">Status do processamento</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots</returns>
    Task<IEnumerable<Screenshot>> GetByProcessingStatusAsync(
        ScreenshotProcessingStatus status,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém screenshots não processados
    /// </summary>
    /// <param name="maxRetryCount">Número máximo de tentativas</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots pendentes</returns>
    Task<IEnumerable<Screenshot>> GetPendingProcessingAsync(
        int maxRetryCount = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém screenshots expirados
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots expirados</returns>
    Task<IEnumerable<Screenshot>> GetExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um novo screenshot
    /// </summary>
    /// <param name="screenshot">Dados do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Screenshot criado</returns>
    Task<Screenshot> CreateAsync(Screenshot screenshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza um screenshot existente
    /// </summary>
    /// <param name="screenshot">Dados do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Screenshot atualizado</returns>
    Task<Screenshot> UpdateAsync(Screenshot screenshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o status de processamento
    /// </summary>
    /// <param name="screenshotId">ID do screenshot</param>
    /// <param name="status">Novo status</param>
    /// <param name="errorMessage">Mensagem de erro (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    Task<bool> UpdateProcessingStatusAsync(
        Guid screenshotId,
        ScreenshotProcessingStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca screenshot como uploadado
    /// </summary>
    /// <param name="screenshotId">ID do screenshot</param>
    /// <param name="objectKey">Chave do objeto no storage</param>
    /// <param name="publicUrl">URL pública (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    Task<bool> MarkAsUploadedAsync(
        Guid screenshotId,
        string objectKey,
        string? publicUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementa contador de tentativas
    /// </summary>
    /// <param name="screenshotId">ID do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    Task<bool> IncrementRetryCountAsync(Guid screenshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um screenshot
    /// </summary>
    /// <param name="id">ID do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove screenshots antigos baseado em política de retenção
    /// </summary>
    /// <param name="cutoffDate">Data limite para remoção</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de screenshots removidos</returns>
    Task<int> DeleteOldScreenshotsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas de screenshots
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas dos screenshots</returns>
    Task<ScreenshotStatistics> GetStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém screenshots por hash (para detectar duplicatas)
    /// </summary>
    /// <param name="fileHash">Hash do arquivo</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots com o mesmo hash</returns>
    Task<IEnumerable<Screenshot>> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém tamanho total usado por screenshots
    /// </summary>
    /// <param name="agentId">ID do agente (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Tamanho total em bytes</returns>
    Task<long> GetTotalSizeAsync(Guid? agentId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém screenshots que contêm conteúdo sensível
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots com conteúdo sensível</returns>
    Task<IEnumerable<Screenshot>> GetWithSensitiveContentAsync(
        DateTime startDate,
        DateTime endDate,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Estatísticas de screenshots
/// </summary>
public class ScreenshotStatistics
{
    /// <summary>
    /// Total de screenshots
    /// </summary>
    public long TotalScreenshots { get; set; }

    /// <summary>
    /// Screenshots processados
    /// </summary>
    public long ProcessedScreenshots { get; set; }

    /// <summary>
    /// Screenshots pendentes
    /// </summary>
    public long PendingScreenshots { get; set; }

    /// <summary>
    /// Screenshots com erro
    /// </summary>
    public long ErrorScreenshots { get; set; }

    /// <summary>
    /// Screenshots expirados
    /// </summary>
    public long ExpiredScreenshots { get; set; }

    /// <summary>
    /// Tamanho total em bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Tamanho médio em bytes
    /// </summary>
    public long AverageSizeBytes { get; set; }

    /// <summary>
    /// Screenshots por dia
    /// </summary>
    public decimal AverageScreenshotsPerDay { get; set; }

    /// <summary>
    /// Distribuição por formato
    /// </summary>
    public Dictionary<string, int> FormatDistribution { get; set; } = new();

    /// <summary>
    /// Distribuição por qualidade
    /// </summary>
    public Dictionary<int, int> QualityDistribution { get; set; } = new();

    /// <summary>
    /// Screenshots com conteúdo sensível
    /// </summary>
    public long SensitiveContentCount { get; set; }

    /// <summary>
    /// Taxa de sucesso de upload
    /// </summary>
    public decimal UploadSuccessRate { get; set; }
}