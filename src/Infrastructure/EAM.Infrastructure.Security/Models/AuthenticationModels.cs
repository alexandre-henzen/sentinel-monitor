namespace EAM.Infrastructure.Security.Models;

/// <summary>
/// Resultado da autenticação
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Indica se a autenticação foi bem-sucedida
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Token JWT gerado
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Token de refresh
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Data de expiração do token
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Informações do usuário/agente autenticado
    /// </summary>
    public AuthenticatedUser? User { get; set; }

    /// <summary>
    /// Mensagem de erro (se houver)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Código de erro
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Tentativas restantes em caso de falha
    /// </summary>
    public int RemainingAttempts { get; set; }

    /// <summary>
    /// Tempo até poder tentar novamente (em caso de bloqueio)
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }
}

/// <summary>
/// Informações do usuário autenticado
/// </summary>
public class AuthenticatedUser
{
    /// <summary>
    /// ID do usuário/agente
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nome do usuário
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Email do usuário
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Roles/perfis do usuário
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Permissões específicas
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Tipo de usuário (Agent, Admin, Viewer)
    /// </summary>
    public UserType UserType { get; set; }

    /// <summary>
    /// Data da última autenticação
    /// </summary>
    public DateTime LastAuthenticationAt { get; set; }

    /// <summary>
    /// IP do último acesso
    /// </summary>
    public string? LastIpAddress { get; set; }

    /// <summary>
    /// Dados adicionais do usuário
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Tipo de usuário
/// </summary>
public enum UserType
{
    /// <summary>
    /// Agente (cliente que envia dados)
    /// </summary>
    Agent,

    /// <summary>
    /// Administrador do sistema
    /// </summary>
    Admin,

    /// <summary>
    /// Visualizador (apenas leitura)
    /// </summary>
    Viewer,

    /// <summary>
    /// Gerente (pode ver dados de sua equipe)
    /// </summary>
    Manager,

    /// <summary>
    /// Usuário do sistema
    /// </summary>
    User
}

/// <summary>
/// Requisição de autenticação
/// </summary>
public class AuthenticationRequest
{
    /// <summary>
    /// Identificador do usuário (email, username, agent ID)
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Senha ou chave de acesso
    /// </summary>
    public string Credential { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de autenticação
    /// </summary>
    public AuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// IP do cliente
    /// </summary>
    public string? ClientIpAddress { get; set; }

    /// <summary>
    /// User-Agent do cliente
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Dados adicionais da requisição
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Tipo de autenticação
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// Autenticação por usuário e senha
    /// </summary>
    UserPassword,

    /// <summary>
    /// Autenticação por chave de agente
    /// </summary>
    AgentKey,

    /// <summary>
    /// Autenticação por token de refresh
    /// </summary>
    RefreshToken,

    /// <summary>
    /// Autenticação por API key
    /// </summary>
    ApiKey
}

/// <summary>
/// Configurações de token JWT
/// </summary>
public class TokenConfiguration
{
    /// <summary>
    /// Chave secreta para assinatura
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Issuer do token
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Audience do token
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Tempo de expiração do token (em minutos)
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Tempo de expiração do refresh token (em dias)
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;

    /// <summary>
    /// Algoritmo de assinatura
    /// </summary>
    public string Algorithm { get; set; } = "HS256";

    /// <summary>
    /// Clock skew permitido (em minutos)
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 5;
}

/// <summary>
/// Claims personalizados para o JWT
/// </summary>
public static class CustomClaims
{
    /// <summary>
    /// ID do usuário
    /// </summary>
    public const string UserId = "user_id";

    /// <summary>
    /// Tipo de usuário
    /// </summary>
    public const string UserType = "user_type";

    /// <summary>
    /// Permissões do usuário
    /// </summary>
    public const string Permissions = "permissions";

    /// <summary>
    /// ID da sessão
    /// </summary>
    public const string SessionId = "session_id";

    /// <summary>
    /// IP do cliente
    /// </summary>
    public const string ClientIp = "client_ip";

    /// <summary>
    /// Timestamp da autenticação
    /// </summary>
    public const string AuthTime = "auth_time";

    /// <summary>
    /// Tipo de autenticação usado
    /// </summary>
    public const string AuthType = "auth_type";
}

/// <summary>
/// Resultado da validação de token
/// </summary>
public class TokenValidationResult
{
    /// <summary>
    /// Indica se o token é válido
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Usuário extraído do token
    /// </summary>
    public AuthenticatedUser? User { get; set; }

    /// <summary>
    /// Claims do token
    /// </summary>
    public Dictionary<string, object> Claims { get; set; } = new();

    /// <summary>
    /// Mensagem de erro (se houver)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Indica se o token expirou
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// Tempo restante até expiração
    /// </summary>
    public TimeSpan? TimeUntilExpiry { get; set; }

    /// <summary>
    /// Indica se o token precisa ser renovado
    /// </summary>
    public bool RequiresRenewal { get; set; }
}

/// <summary>
/// Informações de uma sessão ativa
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// ID da sessão
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// ID do usuário
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Data de criação da sessão
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última atividade
    /// </summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>
    /// Data de expiração
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// IP do cliente
    /// </summary>
    public string? ClientIpAddress { get; set; }

    /// <summary>
    /// User-Agent do cliente
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Indica se a sessão está ativa
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Dados adicionais da sessão
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Estatísticas de autenticação
/// </summary>
public class AuthenticationStatistics
{
    /// <summary>
    /// Total de tentativas de autenticação
    /// </summary>
    public long TotalAttempts { get; set; }

    /// <summary>
    /// Tentativas bem-sucedidas
    /// </summary>
    public long SuccessfulAttempts { get; set; }

    /// <summary>
    /// Tentativas falhadas
    /// </summary>
    public long FailedAttempts { get; set; }

    /// <summary>
    /// Taxa de sucesso
    /// </summary>
    public double SuccessRate => TotalAttempts > 0 ? (double)SuccessfulAttempts / TotalAttempts : 0;

    /// <summary>
    /// Sessões ativas
    /// </summary>
    public int ActiveSessions { get; set; }

    /// <summary>
    /// Usuários únicos autenticados
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// Última atualização das estatísticas
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Tentativas por tipo de autenticação
    /// </summary>
    public Dictionary<AuthenticationType, long> AttemptsByType { get; set; } = new();

    /// <summary>
    /// Falhas por motivo
    /// </summary>
    public Dictionary<string, long> FailuresByReason { get; set; } = new();
}