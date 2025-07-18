using Minio;
using Minio.DataModel.Args;

namespace EAM.API.Services;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IConfiguration configuration, ILogger<MinioStorageService> logger)
    {
        _logger = logger;
        
        var storageConfig = configuration.GetSection("Storage:MinIO");
        var endpoint = storageConfig["Endpoint"];
        var accessKey = storageConfig["AccessKey"];
        var secretKey = storageConfig["SecretKey"];
        var useSSL = storageConfig.GetValue<bool>("UseSSL");

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSSL)
            .Build();
    }

    public async Task<string> UploadFileAsync(string bucketName, string fileName, byte[] fileData, string contentType)
    {
        try
        {
            // Ensure bucket exists
            var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
            if (!bucketExists)
            {
                await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
            }

            // Upload file
            using var stream = new MemoryStream(fileData);
            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
                .WithStreamData(stream)
                .WithObjectSize(fileData.Length)
                .WithContentType(contentType));

            return $"/{bucketName}/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to MinIO");
            throw;
        }
    }

    public async Task<byte[]> DownloadFileAsync(string bucketName, string fileName)
    {
        try
        {
            using var stream = new MemoryStream();
            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
                .WithCallbackStream(s => s.CopyTo(stream)));

            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from MinIO");
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string bucketName, string fileName)
    {
        try
        {
            await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from MinIO");
            return false;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string bucketName, string fileName, TimeSpan expiry)
    {
        try
        {
            var presignedUrl = await _minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
                .WithExpiry((int)expiry.TotalSeconds));

            return presignedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating presigned URL");
            throw;
        }
    }
}

public class ScreenshotService : IScreenshotService
{
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScreenshotService> _logger;

    public ScreenshotService(IStorageService storageService, IConfiguration configuration, ILogger<ScreenshotService> logger)
    {
        _storageService = storageService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> UploadScreenshotAsync(byte[] imageData, string fileName, Guid agentId)
    {
        try
        {
            var bucketName = _configuration["Storage:MinIO:BucketName"] ?? "eam-screenshots";
            var objectName = $"screenshots/{agentId}/{fileName}";
            
            var path = await _storageService.UploadFileAsync(bucketName, objectName, imageData, "image/jpeg");
            
            _logger.LogInformation("Screenshot uploaded: {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading screenshot");
            throw;
        }
    }

    public async Task<string> GetScreenshotUrlAsync(string filePath)
    {
        try
        {
            var bucketName = _configuration["Storage:MinIO:BucketName"] ?? "eam-screenshots";
            var fileName = filePath.TrimStart('/').Replace($"{bucketName}/", "");
            
            var url = await _storageService.GetPresignedUrlAsync(bucketName, fileName, TimeSpan.FromHours(1));
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating screenshot URL");
            throw;
        }
    }

    public async Task<bool> DeleteScreenshotAsync(string filePath)
    {
        try
        {
            var bucketName = _configuration["Storage:MinIO:BucketName"] ?? "eam-screenshots";
            var fileName = filePath.TrimStart('/').Replace($"{bucketName}/", "");
            
            return await _storageService.DeleteFileAsync(bucketName, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting screenshot");
            return false;
        }
    }
}

public class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
    }

    public void RecordApiCall(string endpoint, string method, int statusCode, double duration)
    {
        _logger.LogInformation("API Call: {Method} {Endpoint} - {StatusCode} in {Duration}ms",
            method, endpoint, statusCode, duration);
    }

    public void RecordEventProcessed(string eventType, bool success)
    {
        _logger.LogInformation("Event Processed: {EventType} - {Success}", eventType, success ? "Success" : "Failed");
    }

    public void RecordError(string source, string errorType, string message)
    {
        _logger.LogError("Error recorded: {Source} - {ErrorType}: {Message}", source, errorType, message);
    }
}