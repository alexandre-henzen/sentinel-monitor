using EAM.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Diagnostics;

namespace EAM.Agent.Services;

public class VersionManager
{
    private readonly ILogger<VersionManager> _logger;
    private VersionInfo? _cachedVersion;
    private readonly object _versionLock = new();

    public VersionManager(ILogger<VersionManager> logger)
    {
        _logger = logger;
    }

    public VersionInfo GetCurrentVersion()
    {
        lock (_versionLock)
        {
            if (_cachedVersion == null)
            {
                _cachedVersion = DetectCurrentVersion();
            }
            return _cachedVersion;
        }
    }

    public void InvalidateCache()
    {
        lock (_versionLock)
        {
            _cachedVersion = null;
        }
    }

    private VersionInfo DetectCurrentVersion()
    {
        try
        {
            // Tentar obter versão do assembly atual
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            
            if (assemblyVersion != null)
            {
                _logger.LogDebug("Versão detectada do assembly: {Version}", assemblyVersion);
                return new VersionInfo(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build);
            }

            // Tentar obter versão do arquivo executável
            var processPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(processPath))
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(processPath);
                if (!string.IsNullOrEmpty(fileVersion.FileVersion))
                {
                    if (VersionInfo.TryParse(fileVersion.FileVersion, out var version))
                    {
                        _logger.LogDebug("Versão detectada do arquivo: {Version}", version);
                        return version;
                    }
                }
            }

            // Versão padrão se não conseguir detectar
            _logger.LogWarning("Não foi possível detectar a versão atual, usando versão padrão");
            return new VersionInfo(5, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao detectar versão atual");
            return new VersionInfo(5, 0, 0);
        }
    }

    public bool IsUpdateRequired(VersionInfo currentVersion, VersionInfo availableVersion, VersionInfo? minimumVersion = null)
    {
        if (minimumVersion != null && currentVersion < minimumVersion)
        {
            _logger.LogInformation("Atualização obrigatória: versão atual {Current} está abaixo da versão mínima {Minimum}", 
                currentVersion, minimumVersion);
            return true;
        }

        return false;
    }

    public bool IsUpdateAvailable(VersionInfo currentVersion, VersionInfo availableVersion, bool includePreRelease = false)
    {
        if (!includePreRelease && availableVersion.PreRelease.Length > 0)
        {
            _logger.LogDebug("Ignorando pre-release: {Version}", availableVersion);
            return false;
        }

        var isNewer = availableVersion > currentVersion;
        if (isNewer)
        {
            _logger.LogInformation("Atualização disponível: {Current} -> {Available}", currentVersion, availableVersion);
        }
        else
        {
            _logger.LogDebug("Nenhuma atualização disponível: {Current} >= {Available}", currentVersion, availableVersion);
        }

        return isNewer;
    }

    public UpdatePriority GetUpdatePriority(VersionInfo currentVersion, VersionInfo availableVersion, VersionInfo? minimumVersion = null)
    {
        if (minimumVersion != null && currentVersion < minimumVersion)
        {
            return UpdatePriority.Critical;
        }

        if (availableVersion.Major > currentVersion.Major)
        {
            return UpdatePriority.High;
        }

        if (availableVersion.Minor > currentVersion.Minor)
        {
            return UpdatePriority.Medium;
        }

        if (availableVersion.Patch > currentVersion.Patch)
        {
            return UpdatePriority.Low;
        }

        return UpdatePriority.None;
    }

    public string GetVersionString()
    {
        return GetCurrentVersion().ToString();
    }

    public Dictionary<string, string> GetVersionMetadata()
    {
        var version = GetCurrentVersion();
        var metadata = new Dictionary<string, string>
        {
            ["version"] = version.ToString(),
            ["major"] = version.Major.ToString(),
            ["minor"] = version.Minor.ToString(),
            ["patch"] = version.Patch.ToString(),
            ["prerelease"] = version.PreRelease,
            ["build"] = version.Build
        };

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            if (assemblyVersion != null)
            {
                metadata["assembly_version"] = assemblyVersion.ToString();
            }

            var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);
            if (!string.IsNullOrEmpty(fileVersion.FileVersion))
            {
                metadata["file_version"] = fileVersion.FileVersion;
            }

            if (!string.IsNullOrEmpty(fileVersion.ProductVersion))
            {
                metadata["product_version"] = fileVersion.ProductVersion;
            }

            metadata["build_date"] = File.GetCreationTime(assembly.Location).ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao obter metadados da versão");
        }

        return metadata;
    }
}

public enum UpdatePriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}