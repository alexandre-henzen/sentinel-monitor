using EAM.PluginSDK;
using EAM.Agent.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace EAM.Agent.Trackers;

public class ScreenshotCapturer : ITracker
{
    private readonly ILogger<ScreenshotCapturer> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _screenshotDirectory;
    private DateTime _lastCaptureTime;

    public string Name => "ScreenshotCapturer";
    public bool IsEnabled => _configuration.GetValue<bool>("Trackers:ScreenshotCapturer:Enabled");

    public ScreenshotCapturer(ILogger<ScreenshotCapturer> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _screenshotDirectory = Path.Combine(baseDir, "EAM", "Screenshots");
        
        // Criar diretório se não existir
        if (!Directory.Exists(_screenshotDirectory))
        {
            Directory.CreateDirectory(_screenshotDirectory);
        }
        
        _lastCaptureTime = DateTime.UtcNow;
    }

    public Task InitializeAsync()
    {
        _logger.LogInformation("ScreenshotCapturer inicializado. Diretório: {Directory}", _screenshotDirectory);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync()
    {
        var events = new List<ActivityEvent>();

        try
        {
            var screenshotEvent = await CaptureScreenshotAsync();
            if (screenshotEvent != null)
            {
                events.Add(screenshotEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro capturando screenshot");
        }

        return events;
    }

    public Task StopAsync()
    {
        _logger.LogInformation("ScreenshotCapturer parado");
        return Task.CompletedTask;
    }

    private async Task<ActivityEvent?> CaptureScreenshotAsync()
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            var filename = $"screenshot_{timestamp:yyyyMMdd_HHmmss}.jpg";
            var filePath = Path.Combine(_screenshotDirectory, filename);

            var screenshotInfo = await CaptureScreenToFileAsync(filePath);
            if (screenshotInfo == null)
                return null;

            var screenshotEvent = new ActivityEvent("Screenshot")
            {
                ScreenshotPath = filePath,
                Timestamp = timestamp,
                ProductivityScore = null // Screenshots não têm score de produtividade
            };

            // Adicionar metadados
            screenshotEvent.AddMetadata("file_path", filePath);
            screenshotEvent.AddMetadata("file_name", filename);
            screenshotEvent.AddMetadata("file_size", screenshotInfo.FileSize);
            screenshotEvent.AddMetadata("width", screenshotInfo.Width);
            screenshotEvent.AddMetadata("height", screenshotInfo.Height);
            screenshotEvent.AddMetadata("quality", screenshotInfo.Quality);
            screenshotEvent.AddMetadata("compression_ratio", screenshotInfo.CompressionRatio);
            screenshotEvent.AddMetadata("capture_duration_ms", screenshotInfo.CaptureDuration.TotalMilliseconds);

            _lastCaptureTime = timestamp;
            _logger.LogDebug("Screenshot capturado: {FilePath} ({FileSize} bytes)", filePath, screenshotInfo.FileSize);

            return screenshotEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro capturando screenshot");
            return null;
        }
    }

    private async Task<ScreenshotInfo?> CaptureScreenToFileAsync(string filePath)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var quality = _configuration.GetValue<int>("Trackers:ScreenshotCapturer:Quality", 75);
            var maxWidth = _configuration.GetValue<int>("Trackers:ScreenshotCapturer:MaxWidth", 1920);
            var maxHeight = _configuration.GetValue<int>("Trackers:ScreenshotCapturer:MaxHeight", 1080);

            using var screenshot = CaptureScreen();
            if (screenshot == null)
                return null;

            var originalSize = screenshot.Size;
            
            // Redimensionar se necessário
            using var resizedScreenshot = ResizeImage(screenshot, maxWidth, maxHeight);
            
            // Salvar com compressão JPEG
            var jpegCodec = GetJpegCodec();
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            resizedScreenshot.Save(filePath, jpegCodec, encoderParams);

            var fileInfo = new FileInfo(filePath);
            var captureDuration = DateTime.UtcNow - startTime;

            return new ScreenshotInfo
            {
                Width = resizedScreenshot.Width,
                Height = resizedScreenshot.Height,
                Quality = quality,
                FileSize = fileInfo.Length,
                CaptureDuration = captureDuration,
                CompressionRatio = CalculateCompressionRatio(originalSize, resizedScreenshot.Size, fileInfo.Length)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro processando screenshot");
            return null;
        }
    }

    private Bitmap? CaptureScreen()
    {
        try
        {
            var bounds = GetVirtualScreenBounds();
            var screenshot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            
            using var graphics = Graphics.FromImage(screenshot);
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            
            return screenshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro capturando tela");
            return null;
        }
    }

    private Rectangle GetVirtualScreenBounds()
    {
        var left = 0;
        var top = 0;
        var right = 0;
        var bottom = 0;

        foreach (var screen in Screen.AllScreens)
        {
            left = Math.Min(left, screen.Bounds.X);
            top = Math.Min(top, screen.Bounds.Y);
            right = Math.Max(right, screen.Bounds.X + screen.Bounds.Width);
            bottom = Math.Max(bottom, screen.Bounds.Y + screen.Bounds.Height);
        }

        return new Rectangle(left, top, right - left, bottom - top);
    }

    private Bitmap ResizeImage(Bitmap original, int maxWidth, int maxHeight)
    {
        if (original.Width <= maxWidth && original.Height <= maxHeight)
        {
            return new Bitmap(original);
        }

        var ratioX = (double)maxWidth / original.Width;
        var ratioY = (double)maxHeight / original.Height;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = (int)(original.Width * ratio);
        var newHeight = (int)(original.Height * ratio);

        var resized = new Bitmap(newWidth, newHeight);
        using var graphics = Graphics.FromImage(resized);
        
        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        
        graphics.DrawImage(original, 0, 0, newWidth, newHeight);
        
        return resized;
    }

    private ImageCodecInfo GetJpegCodec()
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        return codecs.First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
    }

    private double CalculateCompressionRatio(Size originalSize, Size resizedSize, long fileSize)
    {
        // Estimar tamanho original descomprimido (4 bytes por pixel para RGBA)
        var originalBytes = originalSize.Width * originalSize.Height * 4;
        var resizedBytes = resizedSize.Width * resizedSize.Height * 4;
        
        return (double)fileSize / Math.Min(originalBytes, resizedBytes);
    }

    public async Task CleanupOldScreenshotsAsync()
    {
        try
        {
            var retentionDays = _configuration.GetValue<int>("Agent:OfflineRetentionDays", 7);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            var files = Directory.GetFiles(_screenshotDirectory, "screenshot_*.jpg");
            var deletedCount = 0;

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro deletando screenshot antigo: {File}", file);
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Removidos {Count} screenshots antigos", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro limpando screenshots antigos");
        }
    }

    private class ScreenshotInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Quality { get; set; }
        public long FileSize { get; set; }
        public TimeSpan CaptureDuration { get; set; }
        public double CompressionRatio { get; set; }
    }
}