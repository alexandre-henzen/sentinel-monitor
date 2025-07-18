using System;
using System.Text.RegularExpressions;

namespace EAM.Shared.Models;

public class VersionInfo : IComparable<VersionInfo>
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }
    public string PreRelease { get; set; } = string.Empty;
    public string Build { get; set; } = string.Empty;

    public VersionInfo()
    {
    }

    public VersionInfo(int major, int minor, int patch, string preRelease = "", string build = "")
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        Build = build;
    }

    public static VersionInfo Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be null or empty", nameof(version));

        // Regex para versão semântica: major.minor.patch[-prerelease][+build]
        var regex = new Regex(@"^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z\-\.]+))?(?:\+([0-9A-Za-z\-\.]+))?$");
        var match = regex.Match(version);

        if (!match.Success)
            throw new ArgumentException($"Invalid version format: {version}", nameof(version));

        return new VersionInfo
        {
            Major = int.Parse(match.Groups[1].Value),
            Minor = int.Parse(match.Groups[2].Value),
            Patch = int.Parse(match.Groups[3].Value),
            PreRelease = match.Groups[4].Value,
            Build = match.Groups[5].Value
        };
    }

    public static bool TryParse(string version, out VersionInfo? versionInfo)
    {
        try
        {
            versionInfo = Parse(version);
            return true;
        }
        catch
        {
            versionInfo = null;
            return false;
        }
    }

    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        
        if (!string.IsNullOrEmpty(PreRelease))
            version += $"-{PreRelease}";
            
        if (!string.IsNullOrEmpty(Build))
            version += $"+{Build}";
            
        return version;
    }

    public int CompareTo(VersionInfo? other)
    {
        if (other == null) return 1;

        // Comparar major.minor.patch
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        // Pre-release tem precedência menor que release
        if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
            return 1;
        if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
            return -1;

        // Comparar pre-release lexicograficamente
        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);
    }

    public static bool operator >(VersionInfo left, VersionInfo right)
        => left.CompareTo(right) > 0;

    public static bool operator <(VersionInfo left, VersionInfo right)
        => left.CompareTo(right) < 0;

    public static bool operator >=(VersionInfo left, VersionInfo right)
        => left.CompareTo(right) >= 0;

    public static bool operator <=(VersionInfo left, VersionInfo right)
        => left.CompareTo(right) <= 0;

    public static bool operator ==(VersionInfo left, VersionInfo right)
        => left.CompareTo(right) == 0;

    public static bool operator !=(VersionInfo left, VersionInfo right)
        => left.CompareTo(right) != 0;

    public override bool Equals(object? obj)
        => obj is VersionInfo other && CompareTo(other) == 0;

    public override int GetHashCode()
        => HashCode.Combine(Major, Minor, Patch, PreRelease, Build);
}