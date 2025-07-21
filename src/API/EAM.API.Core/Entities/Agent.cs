using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.API.Core.Entities;

/// <summary>
/// Entidade representando um agente de monitoramento
/// </summary>
[Table("agents")]
public class Agent
{
    /// <summary>
    /// ID único do agente
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// ID do usuário associado
    /// </summary>
    [Column("user_id")]
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Nome do computador
    /// </summary>
    [Column("computer_name")]
    [Required]
    [MaxLength(256)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// Nome do usuário do sistema
    /// </summary>
    [Column("user_name")]
    [Required]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Domínio do usuário
    /// </summary>
    [Column("domain_name")]
    [MaxLength(256)]
    public string? DomainName { get; set; }

    /// <summary>
    /// Endereço IP do agente
    /// </summary>
    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Endereço MAC
    /// </summary>
    [Column("mac_address")]
    [MaxLength(17)]
    public string? MacAddress { get; set; }

    /// <summary>
    /// Versão do agente
    /// </summary>
    [Column("version")]
    [Required]
    [MaxLength(32)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Versão do sistema operacional
    /// </summary>
    [Column("os_version")]
    [Required]
    [MaxLength(256)]
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Arquitetura do sistema
    /// </summary>
    [Column("os_architecture")]
    [Required]
    [MaxLength(32)]
    public string OsArchitecture { get; set; } = string.Empty;

    /// <summary>
    /// Último heartbeat recebido
    /// </summary>
    [Column("last_heartbeat")]
    public DateTime LastHeartbeat { get; set; }

    /// <summary>
    /// Indica se o agente está ativo
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Status do agente
    /// </summary>
    [Column("status")]
    [Required]
    public AgentStatus Status { get; set; } = AgentStatus.Active;

    /// <summary>
    /// Informações do sistema em JSON
    /// </summary>
    [Column("system_info")]
    [Column(TypeName = "jsonb")]
    public string? SystemInfo { get; set; }

    /// <summary>
    /// Configurações do agente em JSON
    /// </summary>
    [Column("configuration")]
    [Column(TypeName = "jsonb")]
    public string? Configuration { get; set; }

    /// <summary>
    /// Certificado do agente
    /// </summary>
    [Column("certificate")]
    public string? Certificate { get; set; }

    /// <summary>
    /// Último token JWT emitido
    /// </summary>
    [Column("last_token_id")]
    public Guid? LastTokenId { get; set; }

    /// <summary>
    /// Data de criação
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data de atualização
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Relacionamento com eventos de atividade
    /// </summary>
    public virtual ICollection<ActivityEvent> ActivityEvents { get; set; } = new List<ActivityEvent>();

    /// <summary>
    /// Relacionamento com screenshots
    /// </summary>
    public virtual ICollection<Screenshot> Screenshots { get; set; } = new List<Screenshot>();

    /// <summary>
    /// Relacionamento com scores diários
    /// </summary>
    public virtual ICollection<DailyScore> DailyScores { get; set; } = new List<DailyScore>();
}

/// <summary>
/// Status do agente
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// Agente ativo e funcionando
    /// </summary>
    Active = 1,

    /// <summary>
    /// Agente inativo
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// Agente offline
    /// </summary>
    Offline = 3,

    /// <summary>
    /// Agente em manutenção
    /// </summary>
    Maintenance = 4,

    /// <summary>
    /// Agente desabilitado
    /// </summary>
    Disabled = 5,

    /// <summary>
    /// Agente com erro
    /// </summary>
    Error = 6
}