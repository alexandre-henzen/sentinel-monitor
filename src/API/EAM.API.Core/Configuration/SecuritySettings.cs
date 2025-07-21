namespace EAM.API.Core.Configuration;

/// <summary>
/// Configurações de segurança
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Nome da seção no appsettings.json
    /// </summary>
    public const string SectionName = "Security";

    /// <summary>
    /// Chave secreta para JWT
    /// </summary>
    public string JwtSecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Issuer do JWT
    /// </summary>
    public string JwtIssuer { get; set; } = string.Empty;

    /// <summary>
    /// Audience do JWT
    /// </summary>
    public string JwtAudience { get; set; } = string.Empty;

    /// <summary>
    /// Tempo de expiração do JWT em minutos
    /// </summary>
    public int JwtExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Tempo de expiração do refresh token em dias
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;

    /// <summary>
    /// Clock skew permitido em minutos
    /// </summary>
    public int JwtClockSkewMinutes { get; set; } = 5;

    /// <summary>
    /// Threshold para renovação de token em minutos
    /// </summary>
    public int TokenRenewalThresholdMinutes { get; set; } = 15;

    /// <summary>
    /// Máximo de tentativas de login
    /// </summary>
    public int MaxLoginAttempts { get; set; } = 5;

    /// <summary>
    /// Tempo de bloqueio após tentativas excessivas (em minutos)
    /// </summary>
    public int LockoutTimeMinutes { get; set; } = 30;

    /// <summary>
    /// Habilitar rate limiting
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Número máximo de requisições por minuto
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 100;

    /// <summary>
    /// Habilitar auditoria de segurança
    /// </summary>
    public bool EnableSecurityAudit { get; set; } = true;

    /// <summary>
    /// Chave padrão para agentes (development only)
    /// </summary>
    public string DefaultAgentKey { get; set; } = string.Empty;

    /// <summary>
    /// Tempo de vida da sessão em minutos
    /// </summary>
    public int SessionLifetimeMinutes { get; set; } = 480; // 8 horas

    /// <summary>
    /// Habilitar autenticação de dois fatores
    /// </summary>
    public bool EnableTwoFactor { get; set; } = false;

    /// <summary>
    /// Algoritmo de hash para senhas
    /// </summary>
    public string PasswordHashAlgorithm { get; set; } = "bcrypt";

    /// <summary>
    /// Força da criptografia (work factor para bcrypt)
    /// </summary>
    public int PasswordHashWorkFactor { get; set; } = 12;

    /// <summary>
    /// Requer HTTPS
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// Domínios permitidos para CORS
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Chaves de API válidas
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; } = new();

    /// <summary>
    /// Habilitar log de tentativas de acesso
    /// </summary>
    public bool EnableAccessLog { get; set; } = true;

    /// <summary>
    /// Tempo de retenção dos logs de acesso (em dias)
    /// </summary>
    public int AccessLogRetentionDays { get; set; } = 90;
}