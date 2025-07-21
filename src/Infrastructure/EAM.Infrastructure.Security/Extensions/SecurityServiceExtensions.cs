using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace EAM.Infrastructure.Security.Extensions;

/// <summary>
/// Extensões para configuração de serviços de segurança
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// Configura os serviços de segurança
    /// </summary>
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar SecuritySettings
        services.Configure<SecuritySettings>(configuration.GetSection(SecuritySettings.SectionName));
        
        // Registrar serviços de segurança
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        
        // Configurar autenticação JWT
        var securitySettings = configuration.GetSection(SecuritySettings.SectionName).Get<SecuritySettings>() ?? new SecuritySettings();
        
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = securitySettings.JwtIssuer,
                    ValidAudience = securitySettings.JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securitySettings.JwtSecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(securitySettings.JwtClockSkewMinutes)
                };
                
                // Configurar eventos para logs
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning("Falha na autenticação JWT: {Error}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogDebug("Token JWT validado com sucesso");
                        return Task.CompletedTask;
                    }
                };
            });
        
        services.AddAuthorization();
        
        return services;
    }
    
    /// <summary>
    /// Configura CORS baseado nas configurações de segurança
    /// </summary>
    public static IServiceCollection AddSecurityCors(this IServiceCollection services, IConfiguration configuration)
    {
        var securitySettings = configuration.GetSection(SecuritySettings.SectionName).Get<SecuritySettings>() ?? new SecuritySettings();
        
        services.AddCors(options =>
        {
            options.AddPolicy("SecurityPolicy", policy =>
            {
                if (securitySettings.AllowedOrigins.Any())
                {
                    policy.WithOrigins(securitySettings.AllowedOrigins);
                }
                else
                {
                    policy.AllowAnyOrigin();
                }
                
                policy.AllowAnyHeader()
                      .AllowAnyMethod();
                
                // Só permitir credentials se origins específicas forem definidas
                if (securitySettings.AllowedOrigins.Any())
                {
                    policy.AllowCredentials();
                }
            });
        });
        
        return services;
    }
    
    /// <summary>
    /// Configura rate limiting baseado nas configurações de segurança
    /// </summary>
    public static IServiceCollection AddSecurityRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var securitySettings = configuration.GetSection(SecuritySettings.SectionName).Get<SecuritySettings>() ?? new SecuritySettings();
        
        if (securitySettings.EnableRateLimiting)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("SecurityPolicy", configure =>
                {
                    configure.PermitLimit = securitySettings.RateLimitPerMinute;
                    configure.Window = TimeSpan.FromMinutes(1);
                    configure.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    configure.QueueLimit = 10;
                });
            });
        }
        
        return services;
    }
    
    /// <summary>
    /// Adiciona BCrypt como dependência
    /// </summary>
    public static IServiceCollection AddBCryptPasswordHashing(this IServiceCollection services)
    {
        // BCrypt é uma biblioteca estática, não precisa de DI
        // Mas podemos adicionar uma abstração se necessário
        return services;
    }
}