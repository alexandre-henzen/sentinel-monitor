using System;
using System.ComponentModel.DataAnnotations;

namespace EAM.Shared.Models;

public class UpdateInfo
{
    [Required]
    public string Version { get; set; } = string.Empty;

    [Required]
    public string DownloadUrl { get; set; } = string.Empty;

    [Required]
    public string Checksum { get; set; } = string.Empty;

    [Required]
    public string ChecksumAlgorithm { get; set; } = "SHA256";

    [Required]
    public long FileSize { get; set; }

    [Required]
    public DateTime ReleaseDate { get; set; }

    public string ReleaseNotes { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public bool IsPreRelease { get; set; }

    public string MinimumVersion { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public string SignatureAlgorithm { get; set; } = "SHA256withRSA";

    public Dictionary<string, string> Metadata { get; set; } = new();
}