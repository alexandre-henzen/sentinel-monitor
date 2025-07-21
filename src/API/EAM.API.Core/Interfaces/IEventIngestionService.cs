using System.Threading.Channels;
using EAM.API.Core.Models.Requests;
using EAM.Infrastructure.Processing.Models;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para serviço de ingestão de eventos
/// </summary>
public interface IEventIngestionService : IDisposable
{
    /// <summary>
    /// Processa um lote de eventos de forma assíncrona
    /// </summary>
    /// <param name="request">Requisição com lote de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado do processamento</returns>
    Task<EventBatchProcessingResult> ProcessEventBatchAsync(
        EventBatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o leitor de eventos para processamento em background
    /// </summary>
    /// <returns>Leitor de eventos</returns>
    ChannelReader<EventBatch> GetEventReader();

    /// <summary>
    /// Obtém estatísticas de processamento
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de processamento</returns>
    Task<ProcessingStatistics> GetProcessingStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém informações sobre o canal de eventos
    /// </summary>
    /// <returns>Informações do canal</returns>
    ChannelInfo GetChannelInfo();

    /// <summary>
    /// Sinaliza que não haverá mais eventos
    /// </summary>
    /// <returns>True se sinalização foi bem-sucedida</returns>
    bool CompleteWriter();
}