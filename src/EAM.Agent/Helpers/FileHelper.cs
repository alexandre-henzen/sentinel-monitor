using Microsoft.Extensions.Logging;
using System.Security.AccessControl;
using System.Security.Principal;

namespace EAM.Agent.Helpers;

public class FileHelper
{
    private readonly ILogger<FileHelper> _logger;

    public FileHelper(ILogger<FileHelper> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Copiando arquivo: {Source} -> {Destination}", sourcePath, destinationPath);

            if (!File.Exists(sourcePath))
            {
                _logger.LogError("Arquivo de origem não encontrado: {SourcePath}", sourcePath);
                return false;
            }

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            if (File.Exists(destinationPath) && !overwrite)
            {
                _logger.LogWarning("Arquivo de destino já existe e overwrite está desabilitado: {DestinationPath}", destinationPath);
                return false;
            }

            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
            
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            
            _logger.LogDebug("Arquivo copiado com sucesso: {DestinationPath}", destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao copiar arquivo: {Source} -> {Destination}", sourcePath, destinationPath);
            return false;
        }
    }

    public async Task<bool> MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false)
    {
        try
        {
            _logger.LogDebug("Movendo arquivo: {Source} -> {Destination}", sourcePath, destinationPath);

            if (!File.Exists(sourcePath))
            {
                _logger.LogError("Arquivo de origem não encontrado: {SourcePath}", sourcePath);
                return false;
            }

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            if (File.Exists(destinationPath))
            {
                if (!overwrite)
                {
                    _logger.LogWarning("Arquivo de destino já existe e overwrite está desabilitado: {DestinationPath}", destinationPath);
                    return false;
                }
                File.Delete(destinationPath);
            }

            File.Move(sourcePath, destinationPath);
            
            _logger.LogDebug("Arquivo movido com sucesso: {DestinationPath}", destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao mover arquivo: {Source} -> {Destination}", sourcePath, destinationPath);
            return false;
        }
    }

    public bool DeleteFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Arquivo não existe, não há necessidade de deletar: {FilePath}", filePath);
                return true;
            }

            File.Delete(filePath);
            _logger.LogDebug("Arquivo deletado com sucesso: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar arquivo: {FilePath}", filePath);
            return false;
        }
    }

    public bool DeleteDirectory(string directoryPath, bool recursive = false)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogDebug("Diretório não existe, não há necessidade de deletar: {DirectoryPath}", directoryPath);
                return true;
            }

            Directory.Delete(directoryPath, recursive);
            _logger.LogDebug("Diretório deletado com sucesso: {DirectoryPath}", directoryPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar diretório: {DirectoryPath}", directoryPath);
            return false;
        }
    }

    public long GetFileSize(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return 0;
            }

            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter tamanho do arquivo: {FilePath}", filePath);
            return 0;
        }
    }

    public DateTime GetFileLastModified(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return DateTime.MinValue;
            }

            return File.GetLastWriteTimeUtc(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter data de modificação do arquivo: {FilePath}", filePath);
            return DateTime.MinValue;
        }
    }

    public bool IsFileInUse(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar se arquivo está em uso: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> WaitForFileToBeAvailableAsync(string filePath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var endTime = DateTime.UtcNow + timeout;
        
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            if (!IsFileInUse(filePath))
            {
                return true;
            }

            await Task.Delay(1000, cancellationToken);
        }

        return false;
    }

    public bool SetFilePermissions(string filePath, FileSystemRights rights, AccessControlType accessType)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileSecurity = fileInfo.GetAccessControl();
            
            var currentUser = WindowsIdentity.GetCurrent();
            var rule = new FileSystemAccessRule(currentUser.User, rights, accessType);
            
            fileSecurity.SetAccessRule(rule);
            fileInfo.SetAccessControl(fileSecurity);
            
            _logger.LogDebug("Permissões do arquivo alteradas: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar permissões do arquivo: {FilePath}", filePath);
            return false;
        }
    }

    public bool EnsureDirectoryExists(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                _logger.LogDebug("Diretório criado: {DirectoryPath}", directoryPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar diretório: {DirectoryPath}", directoryPath);
            return false;
        }
    }

    public async Task<string> ReadFileTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError("Arquivo não encontrado: {FilePath}", filePath);
                return string.Empty;
            }

            using var reader = new StreamReader(filePath);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler arquivo: {FilePath}", filePath);
            return string.Empty;
        }
    }

    public async Task<bool> WriteFileTextAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using var writer = new StreamWriter(filePath, false);
            await writer.WriteAsync(content);
            
            _logger.LogDebug("Arquivo escrito com sucesso: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao escrever arquivo: {FilePath}", filePath);
            return false;
        }
    }

    public List<string> GetFilesInDirectory(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogDebug("Diretório não encontrado: {DirectoryPath}", directoryPath);
                return new List<string>();
            }

            return Directory.GetFiles(directoryPath, searchPattern, searchOption).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar arquivos no diretório: {DirectoryPath}", directoryPath);
            return new List<string>();
        }
    }

    public long GetDirectorySize(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return 0;
            }

            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            return files.Sum(file => new FileInfo(file).Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular tamanho do diretório: {DirectoryPath}", directoryPath);
            return 0;
        }
    }

    public bool IsPathValid(string path)
    {
        try
        {
            Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Caminho inválido: {Path}", path);
            return false;
        }
    }

    public string GetSafeFileName(string fileName)
    {
        try
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeFileName = fileName;
            
            foreach (var invalidChar in invalidChars)
            {
                safeFileName = safeFileName.Replace(invalidChar, '_');
            }
            
            return safeFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar nome de arquivo seguro: {FileName}", fileName);
            return "safe_filename";
        }
    }

    public string GetTempFileName(string extension = ".tmp")
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var fileName = $"{Guid.NewGuid()}{extension}";
            return Path.Combine(tempPath, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar nome de arquivo temporário");
            return Path.Combine(Path.GetTempPath(), $"temp_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}");
        }
    }

    public bool HasWritePermission(string directoryPath)
    {
        try
        {
            var testFile = Path.Combine(directoryPath, $"test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Sem permissão de escrita no diretório: {DirectoryPath}", directoryPath);
            return false;
        }
    }
}