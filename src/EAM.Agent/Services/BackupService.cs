using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Diagnostics;

namespace EAM.Agent.Services;

public class BackupResult
{
    public bool Success { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public long BackupSize { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public int FilesBackedUp { get; set; }
}

public class RestoreResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public int FilesRestored { get; set; }
}

public class BackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupDirectory;
    private readonly string _applicationDirectory;
    private readonly TimeSpan _backupRetention;

    public BackupService(ILogger<BackupService> logger)
    {
        _logger = logger;
        _backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EAM", "Backups");
        _applicationDirectory = AppContext.BaseDirectory;
        _backupRetention = TimeSpan.FromDays(30); // Manter backups por 30 dias
        
        // Garantir que o diretório de backup existe
        Directory.CreateDirectory(_backupDirectory);
    }

    public async Task<BackupResult> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"EAM_Agent_Backup_{timestamp}.zip";
        var backupPath = Path.Combine(_backupDirectory, backupFileName);

        try
        {
            _logger.LogInformation("Iniciando backup do agente para: {BackupPath}", backupPath);

            // Limpar backups antigos primeiro
            await CleanupOldBackupsAsync();

            var filesToBackup = GetFilesToBackup();
            _logger.LogDebug("Encontrados {Count} arquivos para backup", filesToBackup.Count);

            if (filesToBackup.Count == 0)
            {
                return new BackupResult
                {
                    Success = false,
                    ErrorMessage = "Nenhum arquivo encontrado para backup"
                };
            }

            // Criar arquivo ZIP com os arquivos do agente
            using var zipArchive = new ZipArchive(new FileStream(backupPath, FileMode.Create), ZipArchiveMode.Create);
            
            var filesBackedUp = 0;
            
            foreach (var file in filesToBackup)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Backup cancelado pelo usuário");
                    break;
                }

                try
                {
                    var relativePath = Path.GetRelativePath(_applicationDirectory, file);
                    var entry = zipArchive.CreateEntry(relativePath);
                    
                    using var entryStream = entry.Open();
                    using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    
                    await fileStream.CopyToAsync(entryStream, cancellationToken);
                    filesBackedUp++;
                    
                    _logger.LogDebug("Arquivo adicionado ao backup: {RelativePath}", relativePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao adicionar arquivo ao backup: {FilePath}", file);
                    // Continuar com outros arquivos
                }
            }

            stopwatch.Stop();
            
            var backupInfo = new FileInfo(backupPath);
            var backupSize = backupInfo.Length;
            
            _logger.LogInformation("Backup concluído: {FilesBackedUp} arquivos, {BackupSize:N0} bytes, {Duration:mm\\:ss}", 
                filesBackedUp, backupSize, stopwatch.Elapsed);

            return new BackupResult
            {
                Success = true,
                BackupPath = backupPath,
                BackupSize = backupSize,
                FilesBackedUp = filesBackedUp,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o backup");
            
            // Remover arquivo de backup parcial
            if (File.Exists(backupPath))
            {
                try
                {
                    File.Delete(backupPath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Erro ao remover backup parcial: {BackupPath}", backupPath);
                }
            }
            
            return new BackupResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<RestoreResult> RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            if (!File.Exists(backupPath))
            {
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = $"Arquivo de backup não encontrado: {backupPath}"
                };
            }

            _logger.LogInformation("Iniciando restauração do backup: {BackupPath}", backupPath);

            // Criar diretório temporário para extração
            var tempRestoreDir = Path.Combine(Path.GetTempPath(), $"EAM_Restore_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempRestoreDir);

            var filesRestored = 0;

            try
            {
                // Extrair arquivos do backup
                using var zipArchive = new ZipArchive(new FileStream(backupPath, FileMode.Open, FileAccess.Read), ZipArchiveMode.Read);
                
                foreach (var entry in zipArchive.Entries)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Restauração cancelada pelo usuário");
                        break;
                    }

                    var destinationPath = Path.Combine(tempRestoreDir, entry.FullName);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    
                    if (!string.IsNullOrEmpty(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    using var entryStream = entry.Open();
                    using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                    
                    await entryStream.CopyToAsync(fileStream, cancellationToken);
                    filesRestored++;
                    
                    _logger.LogDebug("Arquivo extraído: {EntryName}", entry.FullName);
                }

                // Parar o serviço atual se estiver rodando
                await StopCurrentServiceAsync();

                // Copiar arquivos extraídos para o diretório da aplicação
                await CopyDirectoryAsync(tempRestoreDir, _applicationDirectory, cancellationToken);

                _logger.LogInformation("Restauração concluída: {FilesRestored} arquivos restaurados", filesRestored);

                return new RestoreResult
                {
                    Success = true,
                    FilesRestored = filesRestored,
                    Duration = stopwatch.Elapsed
                };
            }
            finally
            {
                // Limpar diretório temporário
                if (Directory.Exists(tempRestoreDir))
                {
                    try
                    {
                        Directory.Delete(tempRestoreDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro ao limpar diretório temporário: {TempDir}", tempRestoreDir);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a restauração");
            
            return new RestoreResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private List<string> GetFilesToBackup()
    {
        var filesToBackup = new List<string>();
        
        try
        {
            var extensions = new[] { ".exe", ".dll", ".config", ".json", ".xml" };
            var excludePatterns = new[] { "*.tmp", "*.log", "*.cache", "*.pdb" };
            
            foreach (var extension in extensions)
            {
                var files = Directory.GetFiles(_applicationDirectory, $"*{extension}", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var shouldExclude = excludePatterns.Any(pattern => 
                        fileName.Equals(pattern.Replace("*", ""), StringComparison.OrdinalIgnoreCase) ||
                        (pattern.StartsWith("*.") && fileName.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase)));
                    
                    if (!shouldExclude)
                    {
                        filesToBackup.Add(file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enumerar arquivos para backup");
        }
        
        return filesToBackup;
    }

    private async Task StopCurrentServiceAsync()
    {
        try
        {
            _logger.LogInformation("Tentando parar o serviço EAM Agent...");
            
            // Tentar parar via Windows Service
            var processInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = "stop \"EAM Agent\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Serviço EAM Agent parado com sucesso");
                }
                else
                {
                    _logger.LogWarning("Falha ao parar o serviço EAM Agent (código: {ExitCode})", process.ExitCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao tentar parar o serviço");
        }
    }

    private async Task CopyDirectoryAsync(string sourceDir, string targetDir, CancellationToken cancellationToken)
    {
        var sourceDirInfo = new DirectoryInfo(sourceDir);
        var targetDirInfo = new DirectoryInfo(targetDir);

        if (!targetDirInfo.Exists)
        {
            targetDirInfo.Create();
        }

        // Copiar arquivos
        foreach (var file in sourceDirInfo.GetFiles())
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var targetPath = Path.Combine(targetDir, file.Name);
            
            try
            {
                using var sourceStream = file.OpenRead();
                using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(targetStream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao copiar arquivo: {SourcePath} -> {TargetPath}", file.FullName, targetPath);
            }
        }

        // Copiar subdiretórios recursivamente
        foreach (var directory in sourceDirInfo.GetDirectories())
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var targetPath = Path.Combine(targetDir, directory.Name);
            await CopyDirectoryAsync(directory.FullName, targetPath, cancellationToken);
        }
    }

    private async Task CleanupOldBackupsAsync()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - _backupRetention;
            var backupFiles = Directory.GetFiles(_backupDirectory, "EAM_Agent_Backup_*.zip");
            
            foreach (var backupFile in backupFiles)
            {
                var fileInfo = new FileInfo(backupFile);
                if (fileInfo.CreationTimeUtc < cutoffDate)
                {
                    _logger.LogDebug("Removendo backup antigo: {BackupFile}", backupFile);
                    File.Delete(backupFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao limpar backups antigos");
        }
    }

    public async Task<List<string>> GetAvailableBackupsAsync()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "EAM_Agent_Backup_*.zip")
                .OrderByDescending(f => new FileInfo(f).CreationTime)
                .ToList();
            
            return backupFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar backups disponíveis");
            return new List<string>();
        }
    }

    public long GetBackupDirectorySize()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "*.zip");
            return backupFiles.Sum(f => new FileInfo(f).Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao calcular tamanho do diretório de backup");
            return 0;
        }
    }
}