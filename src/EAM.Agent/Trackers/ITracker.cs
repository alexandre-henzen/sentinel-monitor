using EAM.PluginSDK;

namespace EAM.Agent.Trackers;

/// <summary>
/// Interface base para todos os trackers do EAM Agent
/// </summary>
public interface ITracker
{
    /// <summary>
    /// Inicializa o tracker
    /// </summary>
    /// <returns>Task de inicialização</returns>
    Task InitializeAsync();
    
    /// <summary>
    /// Captura eventos de atividade
    /// </summary>
    /// <returns>Lista de eventos capturados</returns>
    Task<IEnumerable<ActivityEvent>> CaptureAsync();
    
    /// <summary>
    /// Para o tracker e libera recursos
    /// </summary>
    /// <returns>Task de parada</returns>
    Task StopAsync();
    
    /// <summary>
    /// Indica se o tracker está habilitado
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Nome do tracker
    /// </summary>
    string Name { get; }
}