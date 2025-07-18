using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EAM.PluginSDK;

/// <summary>
/// Interface principal para plugins de tracker do EAM
/// </summary>
public interface ITrackerPlugin
{
    /// <summary>
    /// Nome único do plugin
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Versão do plugin
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// Descrição do plugin
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Autor do plugin
    /// </summary>
    string Author { get; }
    
    /// <summary>
    /// Indica se o plugin está habilitado
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Inicializa o plugin com configuração e logger
    /// </summary>
    /// <param name="configuration">Configuração do plugin</param>
    /// <param name="logger">Logger para o plugin</param>
    /// <returns>Task de inicialização</returns>
    ValueTask InitializeAsync(IConfiguration configuration, ILogger logger);
    
    /// <summary>
    /// Realiza polling para capturar eventos de atividade
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de eventos capturados</returns>
    ValueTask<IEnumerable<ActivityEvent>> PollAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Verifica se o plugin está habilitado
    /// </summary>
    /// <returns>True se habilitado</returns>
    ValueTask<bool> IsEnabledAsync();
    
    /// <summary>
    /// Para o plugin e libera recursos
    /// </summary>
    /// <returns>Task de parada</returns>
    ValueTask StopAsync();
}