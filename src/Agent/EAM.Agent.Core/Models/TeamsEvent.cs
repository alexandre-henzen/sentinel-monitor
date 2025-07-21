using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.Agent.Core.Models;

/// <summary>
/// Evento específico de reunião do Microsoft Teams
/// </summary>
[Table("TeamsEvents")]
public class TeamsEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID do evento de atividade relacionado
    /// </summary>
    [Required]
    public Guid ActivityEventId { get; set; }

    /// <summary>
    /// ID da reunião do Teams (se disponível)
    /// </summary>
    [MaxLength(256)]
    public string? MeetingId { get; set; }

    /// <summary>
    /// Título da reunião
    /// </summary>
    [MaxLength(512)]
    public string? MeetingTitle { get; set; }

    /// <summary>
    /// Tipo de evento do Teams
    /// </summary>
    [Required]
    public TeamsEventType EventType { get; set; }

    /// <summary>
    /// Status da reunião
    /// </summary>
    public MeetingStatus Status { get; set; }

    /// <summary>
    /// Indica se o áudio está ativo
    /// </summary>
    public bool IsAudioActive { get; set; }

    /// <summary>
    /// Indica se o vídeo está ativo
    /// </summary>
    public bool IsVideoActive { get; set; }

    /// <summary>
    /// Indica se está compartilhando a tela
    /// </summary>
    public bool IsScreenSharing { get; set; }

    /// <summary>
    /// Indica se está gravando
    /// </summary>
    public bool IsRecording { get; set; }

    /// <summary>
    /// Número de participantes (estimado)
    /// </summary>
    public int ParticipantCount { get; set; }

    /// <summary>
    /// Duração da reunião em segundos
    /// </summary>
    public int DurationSeconds { get; set; }

    /// <summary>
    /// Hora de início da reunião
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Hora de fim da reunião
    /// </summary>
    public DateTime? EndTime { get; set; }

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
/// Tipos de evento do Teams
/// </summary>
public enum TeamsEventType
{
    MeetingStart = 1,
    MeetingEnd = 2,
    MeetingJoin = 3,
    MeetingLeave = 4,
    AudioToggle = 5,
    VideoToggle = 6,
    ScreenShareStart = 7,
    ScreenShareEnd = 8,
    RecordingStart = 9,
    RecordingEnd = 10,
    ParticipantJoin = 11,
    ParticipantLeave = 12
}

/// <summary>
/// Status da reunião
/// </summary>
public enum MeetingStatus
{
    Scheduled = 1,
    InProgress = 2,
    Ended = 3,
    Cancelled = 4,
    Waiting = 5
}