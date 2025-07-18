using System;

namespace EAM.Shared.Models;

public enum UpdateState
{
    None,
    CheckingForUpdate,
    UpdateAvailable,
    Downloading,
    Downloaded,
    BackupInProgress,
    BackupCompleted,
    Installing,
    Installed,
    RestartRequired,
    RollingBack,
    RolledBack,
    Failed,
    Cancelled
}

public class UpdateStatus
{
    public UpdateState State { get; set; } = UpdateState.None;
    
    public string? CurrentVersion { get; set; }
    
    public string? AvailableVersion { get; set; }
    
    public DateTime LastChecked { get; set; }
    
    public DateTime? LastUpdateAttempt { get; set; }
    
    public DateTime? LastSuccessfulUpdate { get; set; }
    
    public int ProgressPercentage { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public string? StatusMessage { get; set; }
    
    public bool IsUpdateRequired { get; set; }
    
    public bool IsPreRelease { get; set; }
    
    public long? TotalBytes { get; set; }
    
    public long? DownloadedBytes { get; set; }
    
    public int RetryCount { get; set; }
    
    public int MaxRetries { get; set; } = 3;
    
    public UpdateInfo? UpdateInfo { get; set; }
    
    public Dictionary<string, object> Metadata { get; set; } = new();

    public bool IsInProgress => State switch
    {
        UpdateState.CheckingForUpdate => true,
        UpdateState.Downloading => true,
        UpdateState.BackupInProgress => true,
        UpdateState.Installing => true,
        UpdateState.RollingBack => true,
        _ => false
    };

    public bool IsCompleted => State switch
    {
        UpdateState.Installed => true,
        UpdateState.RestartRequired => true,
        UpdateState.RolledBack => true,
        UpdateState.Failed => true,
        UpdateState.Cancelled => true,
        _ => false
    };

    public bool IsError => State switch
    {
        UpdateState.Failed => true,
        UpdateState.RolledBack => true,
        _ => false
    };

    public void SetProgress(int percentage, string? message = null)
    {
        ProgressPercentage = Math.Max(0, Math.Min(100, percentage));
        if (!string.IsNullOrWhiteSpace(message))
            StatusMessage = message;
    }

    public void SetError(string errorMessage, Exception? exception = null)
    {
        State = UpdateState.Failed;
        ErrorMessage = errorMessage;
        StatusMessage = "Erro durante a atualização";
        
        if (exception != null)
        {
            Metadata["ExceptionType"] = exception.GetType().Name;
            Metadata["ExceptionMessage"] = exception.Message;
            Metadata["StackTrace"] = exception.StackTrace ?? "";
        }
    }

    public void Reset()
    {
        State = UpdateState.None;
        ProgressPercentage = 0;
        ErrorMessage = null;
        StatusMessage = null;
        TotalBytes = null;
        DownloadedBytes = null;
        RetryCount = 0;
        UpdateInfo = null;
        Metadata.Clear();
    }
}