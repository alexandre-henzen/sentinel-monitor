namespace EAM.Infrastructure.Storage.Models;

/// <summary>
/// Resultado de uma operação de remoção em lote
/// </summary>
public class BulkDeleteResult
{
    /// <summary>
    /// Objetos removidos com sucesso
    /// </summary>
    public string[] DeletedObjects { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Objetos que falharam na remoção (chave: objeto, valor: erro)
    /// </summary>
    public Dictionary<string, string> FailedObjects { get; set; } = new();

    /// <summary>
    /// Número total de objetos processados
    /// </summary>
    public int TotalProcessed => DeletedObjects.Length + FailedObjects.Count;

    /// <summary>
    /// Número de objetos removidos com sucesso
    /// </summary>
    public int SuccessCount => DeletedObjects.Length;

    /// <summary>
    /// Número de objetos que falharam
    /// </summary>
    public int FailureCount => FailedObjects.Count;

    /// <summary>
    /// Indica se a operação foi totalmente bem-sucedida
    /// </summary>
    public bool IsFullySuccessful => FailedObjects.Count == 0;

    /// <summary>
    /// Taxa de sucesso da operação (0.0 a 1.0)
    /// </summary>
    public double SuccessRate => TotalProcessed == 0 ? 0.0 : (double)SuccessCount / TotalProcessed;
}