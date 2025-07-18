namespace EAM.API.Services;

public interface IAuthService
{
    Task<string> GenerateTokenAsync(string machineId, string scope);
    Task<bool> ValidateTokenAsync(string token);
}

public interface IScreenshotService
{
    Task<string> UploadScreenshotAsync(byte[] imageData, string fileName, Guid agentId);
    Task<string> GetScreenshotUrlAsync(string filePath);
    Task<bool> DeleteScreenshotAsync(string filePath);
}

public interface IStorageService
{
    Task<string> UploadFileAsync(string bucketName, string fileName, byte[] fileData, string contentType);
    Task<byte[]> DownloadFileAsync(string bucketName, string fileName);
    Task<bool> DeleteFileAsync(string bucketName, string fileName);
    Task<string> GetPresignedUrlAsync(string bucketName, string fileName, TimeSpan expiry);
}

public interface ITelemetryService
{
    void RecordApiCall(string endpoint, string method, int statusCode, double duration);
    void RecordEventProcessed(string eventType, bool success);
    void RecordError(string source, string errorType, string message);
}