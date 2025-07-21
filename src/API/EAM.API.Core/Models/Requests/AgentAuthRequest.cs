using System.ComponentModel.DataAnnotations;

namespace EAM.API.Core.Models.Requests;

/// <summary>
/// Requisição para autenticação do agente
/// </summary>
public class AgentAuthRequest
{
    /// <summary>
    /// ID único do agente
    /// </summary>
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// Nome do computador
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// Nome do usuário do sistema
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Domínio do usuário
    /// </summary>
    [MaxLength(256)]
    public string? DomainName { get; set; }

    /// <summary>
    /// Versão do agente
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Versão do sistema operacional
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Arquitetura do sistema (x86, x64, ARM64)
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string OsArchitecture { get; set; } = string.Empty;

    /// <summary>
    /// Endereço IP do agente
    /// </summary>
    [MaxLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }

    /// <summary>
    /// Endereço MAC da interface de rede principal
    /// </summary>
    [MaxLength(17)] // MAC address format
    public string? MacAddress { get; set; }

    /// <summary>
    /// Certificado do agente (Base64) para autenticação mútua
    /// </summary>
    public string? AgentCertificate { get; set; }

    /// <summary>
    /// Informações do sistema
    /// </summary>
    public SystemInfo? SystemInfo { get; set; }

    /// <summary>
    /// Timestamp da requisição
    /// </summary>
    [Required]
    public DateTime RequestTimestamp { get; set; }

    /// <summary>
    /// Nonce para prevenir replay attacks
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Nonce { get; set; } = string.Empty;

    /// <summary>
    /// Assinatura da requisição
    /// </summary>
    [MaxLength(512)]
    public string? RequestSignature { get; set; }
}

/// <summary>
/// Informações do sistema do agente
/// </summary>
public class SystemInfo
{
    /// <summary>
    /// Memória total em bytes
    /// </summary>
    public long TotalMemory { get; set; }

    /// <summary>
    /// Memória disponível em bytes
    /// </summary>
    public long AvailableMemory { get; set; }

    /// <summary>
    /// Nome do processador
    /// </summary>
    [MaxLength(256)]
    public string? ProcessorName { get; set; }

    /// <summary>
    /// Número de núcleos do processador
    /// </summary>
    public int ProcessorCores { get; set; }

    /// <summary>
    /// Interfaces de rede
    /// </summary>
    public string[]? NetworkInterfaces { get; set; }

    /// <summary>
    /// Fuso horário do sistema
    /// </summary>
    [MaxLength(64)]
    public string? TimeZone { get; set; }

    /// <summary>
    /// Tempo de atividade do sistema
    /// </summary>
    public TimeSpan? SystemUptime { get; set; }

    /// <summary>
    /// Espaço total em disco (bytes)
    /// </summary>
    public long? TotalDiskSpace { get; set; }

    /// <summary>
    /// Espaço disponível em disco (bytes)
    /// </summary>
    public long? AvailableDiskSpace { get; set; }

    /// <summary>
    /// Antivírus instalado
    /// </summary>
    [MaxLength(256)]
    public string? AntivirusName { get; set; }

    /// <summary>
    /// Versão do .NET Runtime
    /// </summary>
    [MaxLength(64)]
    public string? RuntimeVersion { get; set; }

    /// <summary>
    /// Metadados adicionais do sistema
    /// </summary>
    public Dictionary<string, object>? AdditionalInfo { get; set; }
}