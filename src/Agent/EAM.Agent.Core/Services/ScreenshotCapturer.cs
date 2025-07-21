using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using EAM.Agent.Core.Models;
using EAM.Agent.Core.Windows;
using EAM.Agent.Core.Data;
using ActivityEvent = EAM.Agent.Core.Models.ActivityEvent;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Capturer para screenshots da tela
/// </summary>
public class ScreenshotCapturer : ITracker, IDisposable
{
    private readonly ILogger<ScreenshotCapturer> _logger;
    private readonly IActivityEventRepository _repository;
    private readonly ScreenshotConfiguration _configuration;
    private readonly System.Threading.Timer _timer;
    private readonly Guid _agentId;
    private readonly Guid _userId;
    private readonly string _screenshotDirectory;
    
    private bool _isRunning = false;
    private bool _disposed = false;
    private DateTime _lastCaptureTime = DateTime.MinValue;

    public ScreenshotCapturer(
        ILogger<ScreenshotCapturer> logger,
        IActivityEventRepository repository,
        IOptions<ScreenshotConfiguration> configuration)
    {
        _logger = logger;
        _repository = repository;
        _configuration = configuration.Value;
        _agentId = Guid.NewGuid();
        _userId = GetCurrentUserId();
        
        // Cria diretório para screenshots
        _screenshotDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EAM", "Screenshots");
        
        Directory.CreateDirectory(_screenshotDirectory);
        
        _timer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Name => "ScreenshotCapturer";
    public bool IsEnabled => _configuration.Enabled;
    public int IntervalSeconds => _configuration.IntervalSeconds;

    public event EventHandler<ActivityEvent> EventCaptured;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("ScreenshotCapturer está desabilitado");
            return;
        }

        _logger.LogInformation("Iniciando ScreenshotCapturer com intervalo de {IntervalSeconds}s", IntervalSeconds);
        
        _isRunning = true;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(IntervalSeconds));
        
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parando ScreenshotCapturer");
        
        _isRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var events = new List<ActivityEvent>();
        
        try
        {
            // Verifica se deve capturar screenshot
            if (!ShouldCaptureScreenshot())
            {
                return events;
            }

            var currentTime = DateTime.UtcNow;
            var foregroundWindow = WindowsApi.GetForegroundWindow();
            var foregroundApp = string.Empty;
            var foregroundTitle = string.Empty;

            if (foregroundWindow != IntPtr.Zero)
            {
                WindowsApi.GetWindowThreadProcessId(foregroundWindow, out uint processId);
                foregroundApp = WindowsApi.GetProcessName(processId);
                foregroundTitle = WindowsApi.GetWindowTitle(foregroundWindow);
            }

            // Verifica se a aplicação está na lista de exclusões
            if (IsApplicationExcluded(foregroundApp))
            {
                _logger.LogDebug("Screenshot cancelado - aplicação excluída: {App}", foregroundApp);
                return events;
            }

            // Captura screenshot
            var screenshotInfo = await CaptureScreenshotAsync(foregroundApp, foregroundTitle);
            
            if (screenshotInfo != null)
            {
                // Cria evento de atividade
                var activityEvent = new ActivityEvent
                {
                    Id = Guid.NewGuid(),
                    AgentId = _agentId,
                    UserId = _userId,
                    Timestamp = currentTime,
                    Type = ActivityType.Screenshot,
                    Application = foregroundApp,
                    WindowTitle = foregroundTitle,
                    Duration = TimeSpan.FromSeconds(IntervalSeconds),
                    IsActive = true,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime
                };

                // Cria evento específico de screenshot
                var screenshotEvent = new ScreenshotEvent
                {
                    Id = Guid.NewGuid(),
                    ActivityEventId = activityEvent.Id,
                    FileName = screenshotInfo.FileName,
                    FilePath = screenshotInfo.FilePath,
                    FileHash = screenshotInfo.FileHash,
                    Width = screenshotInfo.Width,
                    Height = screenshotInfo.Height,
                    FileSize = screenshotInfo.FileSize,
                    Quality = _configuration.Quality,
                    Format = EAM.Agent.Core.Models.ImageFormat.JPEG,
                    ForegroundApplication = foregroundApp,
                    ForegroundWindowTitle = foregroundTitle,
                    ContainsSensitiveContent = screenshotInfo.ContainsSensitiveContent,
                    IsProcessed = false,
                    IsUploaded = false,
                    Timestamp = currentTime
                };

                // Adiciona metadados
                var metadata = new Dictionary<string, object>
                {
                    { "FileName", screenshotInfo.FileName },
                    { "Width", screenshotInfo.Width },
                    { "Height", screenshotInfo.Height },
                    { "FileSize", screenshotInfo.FileSize },
                    { "Quality", _configuration.Quality },
                    { "ContainsSensitiveContent", screenshotInfo.ContainsSensitiveContent }
                };

                activityEvent.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);

                // Salva no banco de dados
                await _repository.AddActivityEventAsync(activityEvent, cancellationToken);
                await _repository.AddScreenshotEventAsync(screenshotEvent, cancellationToken);

                events.Add(activityEvent);
                _lastCaptureTime = currentTime;

                // Dispara evento
                EventCaptured?.Invoke(this, activityEvent);

                _logger.LogDebug("Screenshot capturado: {FileName} ({Width}x{Height})", 
                    screenshotInfo.FileName, screenshotInfo.Width, screenshotInfo.Height);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar screenshot");
        }

        return events;
    }

    private bool ShouldCaptureScreenshot()
    {
        // Verifica se está habilitado
        if (!IsEnabled)
            return false;

        // Verifica se é hora de capturar
        if (_lastCaptureTime != DateTime.MinValue)
        {
            var timeSinceLastCapture = DateTime.UtcNow - _lastCaptureTime;
            if (timeSinceLastCapture.TotalSeconds < IntervalSeconds)
                return false;
        }

        // Verifica se há regiões excluídas (implementação simplificada)
        if (_configuration.ExcludedRegions?.Any() == true)
        {
            _logger.LogDebug("Regiões excluídas detectadas - implementação futura");
        }

        return true;
    }

    private bool IsApplicationExcluded(string applicationName)
    {
        if (string.IsNullOrEmpty(applicationName))
            return false;

        var excludedApps = _configuration.ExcludedApplications ?? Array.Empty<string>();
        var sensitiveApps = _configuration.SensitiveApplications ?? Array.Empty<string>();

        return excludedApps.Any(app => 
            applicationName.Contains(app, StringComparison.OrdinalIgnoreCase)) ||
               sensitiveApps.Any(app => 
            applicationName.Contains(app, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ScreenshotInfo?> CaptureScreenshotAsync(string foregroundApp, string foregroundTitle)
    {
        try
        {
            var screenBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            
            // Aplica limitações de tamanho
            var captureWidth = Math.Min(screenBounds.Width, _configuration.MaxWidth);
            var captureHeight = Math.Min(screenBounds.Height, _configuration.MaxHeight);
            
            using var bitmap = new Bitmap(captureWidth, captureHeight);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Captura a tela
            graphics.CopyFromScreen(0, 0, 0, 0, new Size(captureWidth, captureHeight));
            
            // Se deve borrar conteúdo sensível
            if (_configuration.BlurSensitiveContent && ContainsSensitiveContent(foregroundApp))
            {
                BlurSensitiveAreas(graphics, bitmap);
            }
            
            // Salva como JPEG
            var fileName = GenerateFileName();
            var filePath = Path.Combine(_screenshotDirectory, fileName);
            
            var encoder = GetJpegEncoder();
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_configuration.Quality);
            
            bitmap.Save(filePath, encoder, encoderParams);
            
            var fileInfo = new FileInfo(filePath);
            var fileHash = await CalculateFileHashAsync(filePath);
            
            return new ScreenshotInfo
            {
                FileName = fileName,
                FilePath = filePath,
                FileHash = fileHash,
                Width = captureWidth,
                Height = captureHeight,
                FileSize = fileInfo.Length,
                ContainsSensitiveContent = ContainsSensitiveContent(foregroundApp)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar screenshot");
            return null;
        }
    }

    private string GenerateFileName()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var random = new Random().Next(1000, 9999);
        return $"screenshot_{timestamp}_{random}.jpg";
    }

    private ImageCodecInfo GetJpegEncoder()
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        return codecs.FirstOrDefault(codec => codec.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid)
               ?? codecs.First();
    }

    private bool ContainsSensitiveContent(string applicationName)
    {
        if (string.IsNullOrEmpty(applicationName))
            return false;

        var sensitiveApps = _configuration.SensitiveApplications ?? Array.Empty<string>();
        return sensitiveApps.Any(app => 
            applicationName.Contains(app, StringComparison.OrdinalIgnoreCase));
    }

    private void BlurSensitiveAreas(Graphics graphics, Bitmap bitmap)
    {
        // Implementação simplificada - aplica blur na imagem toda
        // Em produção, seria mais sofisticado com detecção de áreas específicas
        try
        {
            var blurBrush = new SolidBrush(Color.FromArgb(128, Color.Black));
            graphics.FillRectangle(blurBrush, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            blurBrush.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao aplicar blur em conteúdo sensível");
        }
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var md5 = MD5.Create();
            var hash = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao calcular hash do arquivo");
            return string.Empty;
        }
    }

    private async void OnTimerElapsed(object? state)
    {
        if (!_isRunning) return;

        try
        {
            await CaptureAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no timer do ScreenshotCapturer");
        }
    }

    private Guid GetCurrentUserId()
    {
        try
        {
            var userName = Environment.UserName;
            var domainName = Environment.UserDomainName;
            var userIdentifier = $"{domainName}\\{userName}";
            
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userIdentifier));
            return new Guid(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ID do usuário");
            return Guid.NewGuid();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _timer?.Dispose();
                StopAsync().Wait();
            }
            _disposed = true;
        }
    }

    private class ScreenshotInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public long FileSize { get; set; }
        public bool ContainsSensitiveContent { get; set; }
    }
}

/// <summary>
/// Configuração para captura de screenshots
/// </summary>
public class ScreenshotConfiguration
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int Quality { get; set; } = 75;
    public int MaxWidth { get; set; } = 1920;
    public int MaxHeight { get; set; } = 1080;
    public bool BlurSensitiveContent { get; set; } = true;
    public bool EnableObjectDetection { get; set; } = false;
    public string[] SensitiveApplications { get; set; } = Array.Empty<string>();
    public string[] ExcludedApplications { get; set; } = Array.Empty<string>();
    public Rectangle[] ExcludedRegions { get; set; } = Array.Empty<Rectangle>();
}