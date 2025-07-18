using EAM.Shared.Models;
using EAM.Agent.Helpers;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Net.Http;
using System.Diagnostics;

namespace EAM.Agent.Services;

public class DownloadResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public string Checksum { get; set; } = string.Empty;
}

public class DownloadManager
{
    private readonly ILogger<DownloadManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SecurityHelper _securityHelper;
    private readonly FileHelper _fileHelper;
    private readonly string _downloadDirectory;
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _timeout;

    public DownloadManager(
        ILogger<DownloadManager> logger,
        IHttpClientFactory httpClientFactory,
        SecurityHelper securityHelper,
        FileHelper fileHelper)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _securityHelper = securityHelper;
        _fileHelper = fileHelper;
        
        _downloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EAM", "Downloads");
        _maxRetryAttempts = 3;
        _timeout = TimeSpan.FromMinutes(30);
        
        // Garantir que o diretório de downloads existe
        Directory.CreateDirectory(_downloadDirectory);
    }

    public async Task<DownloadResult> DownloadAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var fileName = $"EAM.Agent.v{updateInfo.Version}.msi";
        var filePath = Path.Combine(_downloadDirectory, fileName);

        try
        {
            _logger.LogInformation("Iniciando download: {Url} -> {FilePath}", updateInfo.DownloadUrl, filePath);

            // Remover arquivo existente se houver
            if (File.Exists(filePath))
            {
                _logger.LogDebug("Removendo arquivo existente: {FilePath}", filePath);
                File.Delete(filePath);
            }

            var result = await DownloadWithRetryAsync(updateInfo.DownloadUrl, filePath, updateInfo.FileSize, cancellationToken);
            
            if (!result.Success)
            {
                return result;
            }

            // Verificar integridade do arquivo
            _logger.LogInformation("Verificando integridade do arquivo...");
            var calculatedChecksum = await CalculateChecksumAsync(filePath, updateInfo.ChecksumAlgorithm);
            
            if (!string.Equals(calculatedChecksum, updateInfo.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Falha na verificação de integridade. Esperado: {Expected}, Calculado: {Calculated}", 
                    updateInfo.Checksum, calculatedChecksum);
                
                // Remover arquivo corrompido
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "Falha na verificação de integridade do arquivo"
                };
            }

            _logger.LogInformation("Download concluído com sucesso: {FilePath} ({Size:N0} bytes)", filePath, result.FileSize);
            
            result.Checksum = calculatedChecksum;
            result.Duration = stopwatch.Elapsed;
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o download");
            
            // Limpar arquivo parcial em caso de erro
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Erro ao remover arquivo parcial: {FilePath}", filePath);
                }
            }
            
            return new DownloadResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private async Task<DownloadResult> DownloadWithRetryAsync(string url, string filePath, long expectedSize, CancellationToken cancellationToken)
    {
        var lastException = new Exception("Tentativa de download não foi executada");
        
        for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                _logger.LogDebug("Tentativa de download {Attempt}/{MaxAttempts}: {Url}", attempt, _maxRetryAttempts, url);
                
                var result = await PerformDownloadAsync(url, filePath, expectedSize, cancellationToken);
                
                if (result.Success)
                {
                    return result;
                }
                
                lastException = new Exception(result.ErrorMessage ?? "Erro desconhecido no download");
                
                if (attempt < _maxRetryAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                    _logger.LogWarning("Tentativa {Attempt} falhou, tentando novamente em {Delay}s: {Error}", 
                        attempt, delay.TotalSeconds, result.ErrorMessage);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro na tentativa {Attempt}/{MaxAttempts}", attempt, _maxRetryAttempts);
                lastException = ex;
                
                if (attempt < _maxRetryAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
        
        return new DownloadResult
        {
            Success = false,
            ErrorMessage = $"Falha após {_maxRetryAttempts} tentativas: {lastException.Message}"
        };
    }

    private async Task<DownloadResult> PerformDownloadAsync(string url, string filePath, long expectedSize, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = _timeout;
        
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            return new DownloadResult
            {
                Success = false,
                ErrorMessage = $"Erro HTTP: {response.StatusCode} - {response.ReasonPhrase}"
            };
        }
        
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value != expectedSize)
        {
            _logger.LogWarning("Tamanho do conteúdo ({ContentLength}) difere do esperado ({ExpectedSize})", 
                contentLength.Value, expectedSize);
        }
        
        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        
        var buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;
        
        var lastProgressReport = DateTime.UtcNow;
        
        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;
            
            // Reportar progresso a cada 5 segundos
            var now = DateTime.UtcNow;
            if (now - lastProgressReport >= TimeSpan.FromSeconds(5))
            {
                var progressPercentage = contentLength.HasValue ? (int)((totalBytesRead * 100) / contentLength.Value) : 0;
                _logger.LogDebug("Progresso do download: {Progress}% ({Bytes:N0} bytes)", progressPercentage, totalBytesRead);
                lastProgressReport = now;
            }
        }
        
        await fileStream.FlushAsync(cancellationToken);
        
        var finalSize = new FileInfo(filePath).Length;
        
        return new DownloadResult
        {
            Success = true,
            FilePath = filePath,
            FileSize = finalSize
        };
    }

    private async Task<string> CalculateChecksumAsync(string filePath, string algorithm)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        
        return algorithm.ToUpperInvariant() switch
        {
            "SHA256" => await CalculateSHA256Async(fileStream),
            "SHA1" => await CalculateSHA1Async(fileStream),
            "MD5" => await CalculateMD5Async(fileStream),
            _ => throw new NotSupportedException($"Algoritmo de hash não suportado: {algorithm}")
        };
    }

    private async Task<string> CalculateSHA256Async(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hash = await Task.Run(() => sha256.ComputeHash(stream));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<string> CalculateSHA1Async(Stream stream)
    {
        using var sha1 = SHA1.Create();
        var hash = await Task.Run(() => sha1.ComputeHash(stream));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<string> CalculateMD5Async(Stream stream)
    {
        using var md5 = MD5.Create();
        var hash = await Task.Run(() => md5.ComputeHash(stream));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void CleanupOldDownloads(TimeSpan maxAge)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            var files = Directory.GetFiles(_downloadDirectory, "*.msi");
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTimeUtc < cutoffTime)
                {
                    _logger.LogDebug("Removendo download antigo: {FilePath}", file);
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao limpar downloads antigos");
        }
    }

    public long GetAvailableDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_downloadDirectory) ?? "C:");
            return drive.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao obter espaço livre em disco");
            return 0;
        }
    }

    public bool HasSufficientDiskSpace(long requiredBytes)
    {
        var availableSpace = GetAvailableDiskSpace();
        var requiredSpaceWithBuffer = requiredBytes + (100 * 1024 * 1024); // 100MB buffer
        
        return availableSpace >= requiredSpaceWithBuffer;
    }
}