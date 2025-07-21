using Microsoft.EntityFrameworkCore;
using EAM.API.Core.Entities;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Data.Context;

namespace EAM.Infrastructure.Data.Repositories;

/// <summary>
/// Implementação do repositório de screenshots
/// </summary>
public class ScreenshotRepository : IScreenshotRepository
{
    private readonly EamDbContext _context;

    /// <summary>
    /// Construtor do repositório
    /// </summary>
    /// <param name="context">Contexto do banco de dados</param>
    public ScreenshotRepository(EamDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtém um screenshot por ID
    /// </summary>
    /// <param name="id">ID do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Screenshot encontrado ou null</returns>
    public async Task<Screenshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Screenshots
            .AsNoTracking()
            .Include(s => s.Agent)
            .Include(s => s.ActivityEvent)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

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
    public async Task<IEnumerable<Screenshot>> GetByAgentAndPeriodAsync(
        Guid agentId,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _context.Screenshots
            .AsNoTracking()
            .Where(s => s.AgentId == agentId && s.Timestamp >= startDate && s.Timestamp <= endDate)
            .OrderByDescending(s => s.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

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
    public async Task<IEnumerable<Screenshot>> GetByUserAndPeriodAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _context.Screenshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Timestamp >= startDate && s.Timestamp <= endDate)
            .OrderByDescending(s => s.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém screenshots por status de processamento
    /// </summary>
    /// <param name="status">Status do processamento</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots</returns>
    public async Task<IEnumerable<Screenshot>> GetByProcessingStatusAsync(
        ScreenshotProcessingStatus status,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _context.Screenshots
            .AsNoTracking()
            .Where(s => s.ProcessingStatus == status)
            .OrderBy(s => s.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém screenshots não processados
    /// </summary>
    /// <param name="maxRetryCount">Número máximo de tentativas</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots pendentes</returns>
    public async Task<IEnumerable<Screenshot>> GetPendingProcessingAsync(
        int maxRetryCount = 3,
        CancellationToken cancellationToken = default)
    {
        return await _context.Screenshots
            .AsNoTracking()
            .Where(s => s.ProcessingStatus == ScreenshotProcessingStatus.Pending && s.RetryCount < maxRetryCount)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém screenshots expirados
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots expirados</returns>
    public async Task<IEnumerable<Screenshot>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        
        return await _context.Screenshots
            .AsNoTracking()
            .Where(s => s.ExpiresAt.HasValue && s.ExpiresAt.Value < now)
            .OrderBy(s => s.ExpiresAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Cria um novo screenshot
    /// </summary>
    /// <param name="screenshot">Dados do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Screenshot criado</returns>
    public async Task<Screenshot> CreateAsync(Screenshot screenshot, CancellationToken cancellationToken = default)
    {
        _context.Screenshots.Add(screenshot);
        await _context.SaveChangesAsync(cancellationToken);
        return screenshot;
    }

    /// <summary>
    /// Atualiza um screenshot existente
    /// </summary>
    /// <param name="screenshot">Dados do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Screenshot atualizado</returns>
    public async Task<Screenshot> UpdateAsync(Screenshot screenshot, CancellationToken cancellationToken = default)
    {
        _context.Screenshots.Update(screenshot);
        await _context.SaveChangesAsync(cancellationToken);
        return screenshot;
    }

    /// <summary>
    /// Atualiza o status de processamento
    /// </summary>
    /// <param name="screenshotId">ID do screenshot</param>
    /// <param name="status">Novo status</param>
    /// <param name="errorMessage">Mensagem de erro (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    public async Task<bool> UpdateProcessingStatusAsync(
        Guid screenshotId,
        ScreenshotProcessingStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var setters = new Dictionary<string, object>
        {
            [nameof(Screenshot.ProcessingStatus)] = status,
            [nameof(Screenshot.UpdatedAt)] = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(errorMessage))
        {
            setters[nameof(Screenshot.ErrorMessage)] = errorMessage;
        }

        var rowsAffected = await _context.Screenshots
            .Where(s => s.Id == screenshotId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.ProcessingStatus, status)
                .SetProperty(s => s.ErrorMessage, errorMessage)
                .SetProperty(s => s.UpdatedAt, DateTime.UtcNow), cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Marca screenshot como uploadado
    /// </summary>
    /// <param name="screenshotId">ID do screenshot</param>
    /// <param name="objectKey">Chave do objeto no storage</param>
    /// <param name="publicUrl">URL pública (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    public async Task<bool> MarkAsUploadedAsync(
        Guid screenshotId,
        string objectKey,
        string? publicUrl = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        
        var rowsAffected = await _context.Screenshots
            .Where(s => s.Id == screenshotId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.ObjectKey, objectKey)
                .SetProperty(s => s.PublicUrl, publicUrl)
                .SetProperty(s => s.IsUploaded, true)
                .SetProperty(s => s.UploadedAt, now)
                .SetProperty(s => s.ProcessingStatus, ScreenshotProcessingStatus.Processed)
                .SetProperty(s => s.UpdatedAt, now), cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Incrementa contador de tentativas
    /// </summary>
    /// <param name="screenshotId">ID do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    public async Task<bool> IncrementRetryCountAsync(Guid screenshotId, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Screenshots
            .Where(s => s.Id == screenshotId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.RetryCount, s => s.RetryCount + 1)
                .SetProperty(s => s.UpdatedAt, DateTime.UtcNow), cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Remove um screenshot
    /// </summary>
    /// <param name="id">ID do screenshot</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Screenshots
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Remove screenshots antigos baseado em política de retenção
    /// </summary>
    /// <param name="cutoffDate">Data limite para remoção</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de screenshots removidos</returns>
    public async Task<int> DeleteOldScreenshotsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await _context.Screenshots
            .Where(s => s.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém estatísticas de screenshots
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas dos screenshots</returns>
    public async Task<ScreenshotStatistics> GetStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Screenshots
            .Where(s => s.Timestamp >= startDate && s.Timestamp <= endDate);

        var totalScreenshots = await query.CountAsync(cancellationToken);
        var processedScreenshots = await query.CountAsync(s => s.ProcessingStatus == ScreenshotProcessingStatus.Processed, cancellationToken);
        var pendingScreenshots = await query.CountAsync(s => s.ProcessingStatus == ScreenshotProcessingStatus.Pending, cancellationToken);
        var errorScreenshots = await query.CountAsync(s => s.ProcessingStatus == ScreenshotProcessingStatus.Error, cancellationToken);
        var expiredScreenshots = await query.CountAsync(s => s.ProcessingStatus == ScreenshotProcessingStatus.Expired, cancellationToken);

        var totalSize = await query.SumAsync(s => s.FileSize, cancellationToken);
        var averageSize = totalScreenshots > 0 ? totalSize / totalScreenshots : 0;

        var totalDays = (endDate - startDate).Days + 1;
        var averageScreenshotsPerDay = totalDays > 0 ? (decimal)totalScreenshots / totalDays : 0;

        var formatDistribution = await query
            .GroupBy(s => s.Format)
            .Select(g => new { Format = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Format, x => x.Count, cancellationToken);

        var qualityDistribution = await query
            .GroupBy(s => s.Quality)
            .Select(g => new { Quality = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Quality, x => x.Count, cancellationToken);

        var sensitiveContentCount = await query.CountAsync(s => s.ContainsSensitiveContent, cancellationToken);

        var uploadedCount = await query.CountAsync(s => s.IsUploaded, cancellationToken);
        var uploadSuccessRate = totalScreenshots > 0 ? (decimal)uploadedCount / totalScreenshots * 100 : 0;

        return new ScreenshotStatistics
        {
            TotalScreenshots = totalScreenshots,
            ProcessedScreenshots = processedScreenshots,
            PendingScreenshots = pendingScreenshots,
            ErrorScreenshots = errorScreenshots,
            ExpiredScreenshots = expiredScreenshots,
            TotalSizeBytes = totalSize,
            AverageSizeBytes = averageSize,
            AverageScreenshotsPerDay = averageScreenshotsPerDay,
            FormatDistribution = formatDistribution,
            QualityDistribution = qualityDistribution,
            SensitiveContentCount = sensitiveContentCount,
            UploadSuccessRate = uploadSuccessRate
        };
    }

    /// <summary>
    /// Obtém screenshots por hash (para detectar duplicatas)
    /// </summary>
    /// <param name="fileHash">Hash do arquivo</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots com o mesmo hash</returns>
    public async Task<IEnumerable<Screenshot>> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        return await _context.Screenshots
            .AsNoTracking()
            .Where(s => s.FileHash == fileHash)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém tamanho total usado por screenshots
    /// </summary>
    /// <param name="agentId">ID do agente (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Tamanho total em bytes</returns>
    public async Task<long> GetTotalSizeAsync(Guid? agentId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Screenshots.AsQueryable();
        
        if (agentId.HasValue)
        {
            query = query.Where(s => s.AgentId == agentId.Value);
        }

        return await query.SumAsync(s => s.FileSize, cancellationToken);
    }

    /// <summary>
    /// Obtém screenshots que contêm conteúdo sensível
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de screenshots com conteúdo sensível</returns>
    public async Task<IEnumerable<Screenshot>> GetWithSensitiveContentAsync(
        DateTime startDate,
        DateTime endDate,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _context.Screenshots
            .AsNoTracking()
            .Where(s => s.ContainsSensitiveContent && s.Timestamp >= startDate && s.Timestamp <= endDate)
            .OrderByDescending(s => s.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
}