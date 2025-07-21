using EAM.Agent.Core.Models;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Interface base para todos os trackers
/// </summary>
public interface ITracker
{
    /// <summary>
    /// Nome do tracker
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indica se o tracker está habilitado
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Interval de captura em segundos
    /// </summary>
    int IntervalSeconds { get; }

    /// <summary>
    /// Inicia o tracking
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Para o tracking
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Captura dados em uma execução única
    /// </summary>
    Task<IEnumerable<ActivityEvent>> CaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evento disparado quando um novo evento é capturado
    /// </summary>
    event EventHandler<ActivityEvent> EventCaptured;
}

/// <summary>
/// Configuração base para trackers
/// </summary>
public class TrackerConfiguration
{
    /// <summary>
    /// Indica se o tracker está habilitado
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Intervalo de captura em segundos
    /// </summary>
    public int IntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Configurações específicas do tracker
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();
}