using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.Agent.Core.Models;

/// <summary>
/// Evento específico de janela/foco
/// </summary>
[Table("WindowEvents")]
public class WindowEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID do evento de atividade relacionado
    /// </summary>
    [Required]
    public Guid ActivityEventId { get; set; }

    /// <summary>
    /// Handle da janela (HWND)
    /// </summary>
    [NotMapped]
    public IntPtr WindowHandle { get; set; }

    /// <summary>
    /// Nome da classe da janela
    /// </summary>
    [MaxLength(256)]
    public string? WindowClassName { get; set; }

    /// <summary>
    /// ID do processo proprietário
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Nome do processo
    /// </summary>
    [MaxLength(256)]
    public string? ProcessName { get; set; }

    /// <summary>
    /// Caminho do executável
    /// </summary>
    [MaxLength(512)]
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Estado da janela
    /// </summary>
    public WindowState State { get; set; }

    /// <summary>
    /// Posição X da janela
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Posição Y da janela
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Largura da janela
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Altura da janela
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Indica se a janela está visível
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Indica se a janela está em foco
    /// </summary>
    public bool IsForeground { get; set; }

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
/// Estados possíveis da janela
/// </summary>
public enum WindowState
{
    Normal = 1,
    Minimized = 2,
    Maximized = 3,
    Hidden = 4,
    Fullscreen = 5
}