using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.Agent.Core.Models;

/// <summary>
/// Evento específico de processo (início/fim)
/// </summary>
[Table("ProcessEvents")]
public class ProcessEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID do evento de atividade relacionado
    /// </summary>
    [Required]
    public Guid ActivityEventId { get; set; }

    /// <summary>
    /// ID do processo
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Nome do processo
    /// </summary>
    [MaxLength(256)]
    [Required]
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Caminho completo do executável
    /// </summary>
    [MaxLength(512)]
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Argumentos da linha de comando
    /// </summary>
    [MaxLength(1024)]
    public string? CommandLine { get; set; }

    /// <summary>
    /// Diretório de trabalho
    /// </summary>
    [MaxLength(512)]
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Tipo de evento do processo
    /// </summary>
    [Required]
    public ProcessEventType EventType { get; set; }

    /// <summary>
    /// ID do processo pai
    /// </summary>
    public int? ParentProcessId { get; set; }

    /// <summary>
    /// Nome do processo pai
    /// </summary>
    [MaxLength(256)]
    public string? ParentProcessName { get; set; }

    /// <summary>
    /// Usuário que iniciou o processo
    /// </summary>
    [MaxLength(256)]
    public string? User { get; set; }

    /// <summary>
    /// Domínio do usuário
    /// </summary>
    [MaxLength(256)]
    public string? Domain { get; set; }

    /// <summary>
    /// Uso de memória em bytes
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Uso de CPU em percentual
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Número de handles abertos
    /// </summary>
    public int HandleCount { get; set; }

    /// <summary>
    /// Número de threads
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Hora de início do processo
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Hora de fim do processo (se aplicável)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Código de saída do processo
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Timestamp do evento
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Relacionamento com ActivityEvent
    /// </summary>
    [ForeignKey(nameof(ActivityEventId))]
    public ActivityEvent ActivityEvent { get; set; } = null!;
}

/// <summary>
/// Tipos de evento de processo
/// </summary>
public enum ProcessEventType
{
    Start = 1,
    End = 2,
    Crash = 3,
    HighMemory = 4,
    HighCpu = 5,
    Suspended = 6,
    Resumed = 7
}