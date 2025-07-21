using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with structured logging and trace correlation
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] [{SpanId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/eam-api-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{TraceId}] [{SpanId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .Enrich.WithProperty("Service", "EAM.API")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Enrich.WithProperty("Version", "5.0.0")
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure OpenAPI/Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EAM API",
        Version = "v5.0",
        Description = "Employee Activity Monitor API",
        Contact = new OpenApiContact
        {
            Name = "EAM Development Team",
            Email = "dev@eam.com"
        }
    });

    // JWT Bearer Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });

    // Include XML comments if available
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Security");
var secretKey = jwtSettings["JwtSecretKey"] ?? "your-super-secret-key-that-should-be-at-least-32-characters-long";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["JwtIssuer"] ?? "EAM.API",
            ValidAudience = jwtSettings["JwtAudience"] ?? "EAM.API",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Falha na autenticação JWT: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogDebug("Token JWT validado com sucesso");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? 
                           new[] { "http://localhost:4200", "https://localhost:4200" };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("ApiPolicy", configure =>
    {
        configure.PermitLimit = 100;
        configure.Window = TimeSpan.FromMinutes(1);
        configure.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        configure.QueueLimit = 10;
    });
});

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("application", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running"), tags: new[] { "application", "self" });

// Configure OpenTelemetry
var telemetrySettings = builder.Configuration.GetSection("Telemetry");
var serviceName = telemetrySettings["ServiceName"] ?? "EAM.API";
var serviceVersion = telemetrySettings["ServiceVersion"] ?? "5.0.0";
var enableTracing = telemetrySettings.GetValue<bool>("EnableTracing", true);
var enableMetrics = telemetrySettings.GetValue<bool>("EnableMetrics", true);

if (enableTracing || enableMetrics)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: serviceName,
            serviceVersion: serviceVersion,
            serviceInstanceId: Environment.MachineName)
            .AddAttributes(new Dictionary<string, object>
            {
                {"service.environment", builder.Environment.EnvironmentName},
                {"service.instance.id", Environment.MachineName},
                {"host.name", Environment.MachineName},
                {"process.pid", Environment.ProcessId}
            }))
        .WithTracing(tracing =>
        {
            if (enableTracing)
            {
                tracing.AddSource(serviceName);
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        activity.SetTag("http.request.user_agent", request.Headers.UserAgent.ToString());
                        activity.SetTag("http.request.client_ip", GetClientIpAddress(request));
                    };
                    options.EnrichWithHttpResponse = (activity, response) =>
                    {
                        activity.SetTag("http.response.content_length", response.ContentLength);
                    };
                });
                tracing.AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                });
                
                // Configurar Jaeger se endpoint estiver configurado
                var jaegerEndpoint = telemetrySettings["JaegerEndpoint"];
                if (!string.IsNullOrEmpty(jaegerEndpoint))
                {
                    tracing.AddJaegerExporter(options =>
                    {
                        options.Endpoint = new Uri(jaegerEndpoint);
                    });
                }
            }
        })
        .WithMetrics(metrics =>
        {
            if (enableMetrics)
            {
                metrics.AddMeter(serviceName);
                metrics.AddMeter("EAM.API.Metrics");
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddRuntimeInstrumentation();
                
                // Configurar Prometheus se habilitado
                var enablePrometheus = telemetrySettings.GetValue<bool>("EnablePrometheus", false);
                if (enablePrometheus)
                {
                    metrics.AddPrometheusExporter();
                }
            }
        });
}

// Configure HTTP Client
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EAM API v5.0");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// Apply Rate Limiting
app.UseRateLimiter();

// Apply CORS
app.UseCors("AllowedOrigins");

// Apply Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map Health Checks
app.MapHealthChecks("/health");

// Map OpenTelemetry metrics endpoint
var enablePrometheus = telemetrySettings.GetValue<bool>("EnablePrometheus", false);
if (enablePrometheus)
{
    app.MapPrometheusScrapingEndpoint("/metrics");
}

app.Run();

// Helper methods
static string? GetClientIpAddress(HttpRequest request)
{
    return request.Headers.ContainsKey("X-Forwarded-For")
        ? request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        : request.HttpContext.Connection.RemoteIpAddress?.ToString();
}