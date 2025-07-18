using System.Text.Json.Serialization;

namespace EAM.PluginSDK;

/// <summary>
/// Representa um evento de atividade capturado por um plugin
/// </summary>
public class ActivityEvent
{
    /// <summary>
    /// Tipo do evento (WindowFocus, BrowserUrl, etc.)
    /// </summary>
    public string EventType { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome da aplicação relacionada ao evento
    /// </summary>
    public string? ApplicationName { get; set; }
    
    /// <summary>
    /// Título da janela
    /// </summary>
    public string? WindowTitle { get; set; }
    
    /// <summary>
    /// URL (para browsers)
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// Nome do processo
    /// </summary>
    public string? ProcessName { get; set; }
    
    /// <summary>
    /// ID do processo
    /// </summary>
    public int? ProcessId { get; set; }
    
    /// <summary>
    /// Duração da atividade em segundos
    /// </summary>
    public int? DurationSeconds { get; set; }
    
    /// <summary>
    /// Timestamp do evento
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Metadados adicionais do evento
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <summary>
    /// Score de produtividade (0-100)
    /// </summary>
    public int? ProductivityScore { get; set; }
    
    /// <summary>
    /// Caminho do screenshot associado (se houver)
    /// </summary>
    public string? ScreenshotPath { get; set; }
    
    /// <summary>
    /// Construtor padrão
    /// </summary>
    public ActivityEvent()
    {
        Metadata = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Construtor com tipo de evento
    /// </summary>
    /// <param name="eventType">Tipo do evento</param>
    public ActivityEvent(string eventType) : this()
    {
        EventType = eventType;
    }
    
    /// <summary>
    /// Adiciona metadados ao evento
    /// </summary>
    /// <param name="key">Chave do metadado</param>
    /// <param name="value">Valor do metadado</param>
    public void AddMetadata(string key, object value)
    {
        Metadata ??= new Dictionary<string, object>();
        Metadata[key] = value;
    }
    
    /// <summary>
    /// Obtém metadado tipado
    /// </summary>
    /// <typeparam name="T">Tipo do valor</typeparam>
    /// <param name="key">Chave do metadado</param>
    /// <returns>Valor tipado ou default</returns>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata?.TryGetValue(key, out var value) == true)
        {
            if (value is T typedValue)
                return typedValue;
            
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        
        return default;
    }
}