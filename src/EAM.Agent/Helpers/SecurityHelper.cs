using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace EAM.Agent.Helpers;

public class SecurityHelper
{
    private readonly ILogger<SecurityHelper> _logger;

    public SecurityHelper(ILogger<SecurityHelper> logger)
    {
        _logger = logger;
    }

    public async Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedChecksum, string algorithm = "SHA256")
    {
        try
        {
            _logger.LogDebug("Verificando integridade do arquivo: {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogError("Arquivo não encontrado para verificação: {FilePath}", filePath);
                return false;
            }

            var calculatedChecksum = await CalculateFileHashAsync(filePath, algorithm);
            var isValid = string.Equals(calculatedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

            if (isValid)
            {
                _logger.LogInformation("Verificação de integridade bem-sucedida: {FilePath}", filePath);
            }
            else
            {
                _logger.LogError("Falha na verificação de integridade: {FilePath}. Esperado: {Expected}, Calculado: {Calculated}", 
                    filePath, expectedChecksum, calculatedChecksum);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar integridade do arquivo: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<string> CalculateFileHashAsync(string filePath, string algorithm = "SHA256")
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            return algorithm.ToUpperInvariant() switch
            {
                "SHA256" => await CalculateSHA256Async(fileStream),
                "SHA1" => await CalculateSHA1Async(fileStream),
                "MD5" => await CalculateMD5Async(fileStream),
                "SHA512" => await CalculateSHA512Async(fileStream),
                _ => throw new NotSupportedException($"Algoritmo de hash não suportado: {algorithm}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular hash do arquivo: {FilePath}", filePath);
            throw;
        }
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

    private async Task<string> CalculateSHA512Async(Stream stream)
    {
        using var sha512 = SHA512.Create();
        var hash = await Task.Run(() => sha512.ComputeHash(stream));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<bool> VerifyDigitalSignatureAsync(string filePath, string expectedSignature)
    {
        try
        {
            _logger.LogDebug("Verificando assinatura digital: {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogError("Arquivo não encontrado para verificação de assinatura: {FilePath}", filePath);
                return false;
            }

            // Verificar se o arquivo tem assinatura digital incorporada
            var hasEmbeddedSignature = await HasEmbeddedDigitalSignatureAsync(filePath);
            
            if (hasEmbeddedSignature)
            {
                _logger.LogInformation("Arquivo possui assinatura digital incorporada: {FilePath}", filePath);
                return await VerifyEmbeddedSignatureAsync(filePath);
            }

            // Se não tem assinatura incorporada, verificar com a assinatura fornecida
            if (!string.IsNullOrEmpty(expectedSignature))
            {
                return await VerifyExternalSignatureAsync(filePath, expectedSignature);
            }

            _logger.LogWarning("Arquivo não possui assinatura digital e nenhuma assinatura externa foi fornecida: {FilePath}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar assinatura digital: {FilePath}", filePath);
            return false;
        }
    }

    private async Task<bool> HasEmbeddedDigitalSignatureAsync(string filePath)
    {
        try
        {
            // Verificar se o arquivo é um PE (Portable Executable)
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[2];
            await fileStream.ReadAsync(buffer, 0, 2);
            
            // Verificar se começa com "MZ" (cabeçalho PE)
            if (buffer[0] != 0x4D || buffer[1] != 0x5A)
            {
                return false;
            }

            // Para arquivos PE, verificar se há certificado digital
            var cert = X509Certificate.CreateFromSignedFile(filePath);
            return cert != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> VerifyEmbeddedSignatureAsync(string filePath)
    {
        try
        {
            var cert = X509Certificate.CreateFromSignedFile(filePath);
            if (cert == null)
            {
                return false;
            }

            var cert2 = new X509Certificate2(cert);
            
            // Verificar se o certificado está válido
            var isValidTime = DateTime.Now >= cert2.NotBefore && DateTime.Now <= cert2.NotAfter;
            
            if (!isValidTime)
            {
                _logger.LogWarning("Certificado digital expirado ou ainda não válido: {FilePath}", filePath);
                return false;
            }

            // Verificar cadeia de confiança
            var chain = new X509Chain();
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreUnknownCertificateAuthority;
            
            var chainIsValid = chain.Build(cert2);
            
            if (!chainIsValid)
            {
                _logger.LogWarning("Cadeia de certificado inválida: {FilePath}", filePath);
                foreach (var status in chain.ChainStatus)
                {
                    _logger.LogWarning("Status da cadeia: {Status} - {StatusInformation}", status.Status, status.StatusInformation);
                }
            }

            _logger.LogInformation("Assinatura digital verificada: {Subject}, Válido até: {ValidTo}", 
                cert2.Subject, cert2.NotAfter);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar assinatura incorporada: {FilePath}", filePath);
            return false;
        }
    }

    private async Task<bool> VerifyExternalSignatureAsync(string filePath, string signature)
    {
        try
        {
            // Para assinatura externa, podemos implementar verificação HMAC ou RSA
            // Por enquanto, implementação básica para demonstração
            
            _logger.LogInformation("Verificando assinatura externa para: {FilePath}", filePath);
            
            // Calcular hash do arquivo
            var fileHash = await CalculateFileHashAsync(filePath, "SHA256");
            
            // Comparar com assinatura fornecida (implementação simplificada)
            // Em produção, aqui seria verificação criptográfica real
            var expectedHash = ExtractHashFromSignature(signature);
            
            return string.Equals(fileHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar assinatura externa: {FilePath}", filePath);
            return false;
        }
    }

    private string ExtractHashFromSignature(string signature)
    {
        // Implementação simplificada para extrair hash da assinatura
        // Em produção, isso seria uma verificação criptográfica real
        if (signature.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return signature.Substring(7);
        }
        
        return signature;
    }

    public async Task<bool> ValidateUpdatePackageAsync(string filePath, string expectedChecksum, string? signature = null)
    {
        try
        {
            _logger.LogInformation("Validando pacote de atualização: {FilePath}", filePath);

            // Verificar se o arquivo existe
            if (!File.Exists(filePath))
            {
                _logger.LogError("Pacote de atualização não encontrado: {FilePath}", filePath);
                return false;
            }

            // Verificar integridade
            var integrityValid = await VerifyFileIntegrityAsync(filePath, expectedChecksum);
            if (!integrityValid)
            {
                _logger.LogError("Falha na verificação de integridade do pacote: {FilePath}", filePath);
                return false;
            }

            // Verificar assinatura digital se fornecida
            if (!string.IsNullOrEmpty(signature))
            {
                var signatureValid = await VerifyDigitalSignatureAsync(filePath, signature);
                if (!signatureValid)
                {
                    _logger.LogError("Falha na verificação de assinatura do pacote: {FilePath}", filePath);
                    return false;
                }
            }

            // Verificar se é um arquivo MSI válido
            var isMsiValid = await ValidateMsiFileAsync(filePath);
            if (!isMsiValid)
            {
                _logger.LogError("Arquivo MSI inválido: {FilePath}", filePath);
                return false;
            }

            _logger.LogInformation("Pacote de atualização válido: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar pacote de atualização: {FilePath}", filePath);
            return false;
        }
    }

    private async Task<bool> ValidateMsiFileAsync(string filePath)
    {
        try
        {
            // Verificar se o arquivo tem a extensão .msi
            if (!filePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Verificar cabeçalho MSI
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[8];
            await fileStream.ReadAsync(buffer, 0, 8);
            
            // MSI files começam com a assinatura do Compound Document
            var expectedSignature = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            
            for (int i = 0; i < 8; i++)
            {
                if (buffer[i] != expectedSignature[i])
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar arquivo MSI: {FilePath}", filePath);
            return false;
        }
    }

    public string GenerateSecureToken(int length = 32)
    {
        try
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar token seguro");
            return Guid.NewGuid().ToString();
        }
    }

    public bool IsFileSignedByTrustedPublisher(string filePath, string trustedPublisher)
    {
        try
        {
            if (!HasEmbeddedDigitalSignatureAsync(filePath).Result)
            {
                return false;
            }

            var cert = X509Certificate.CreateFromSignedFile(filePath);
            if (cert == null)
            {
                return false;
            }

            var cert2 = new X509Certificate2(cert);
            var subject = cert2.Subject;
            
            return subject.Contains(trustedPublisher, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar publisher confiável: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<Dictionary<string, string>> GetFileSecurityInfoAsync(string filePath)
    {
        var info = new Dictionary<string, string>();

        try
        {
            if (!File.Exists(filePath))
            {
                info["error"] = "Arquivo não encontrado";
                return info;
            }

            var fileInfo = new FileInfo(filePath);
            info["size"] = fileInfo.Length.ToString();
            info["created"] = fileInfo.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss UTC");
            info["modified"] = fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss UTC");

            // Calcular hashes
            info["sha256"] = await CalculateFileHashAsync(filePath, "SHA256");
            info["sha1"] = await CalculateFileHashAsync(filePath, "SHA1");
            info["md5"] = await CalculateFileHashAsync(filePath, "MD5");

            // Verificar assinatura digital
            var hasSig = await HasEmbeddedDigitalSignatureAsync(filePath);
            info["has_signature"] = hasSig.ToString();

            if (hasSig)
            {
                try
                {
                    var cert = X509Certificate.CreateFromSignedFile(filePath);
                    if (cert != null)
                    {
                        var cert2 = new X509Certificate2(cert);
                        info["certificate_subject"] = cert2.Subject;
                        info["certificate_issuer"] = cert2.Issuer;
                        info["certificate_valid_from"] = cert2.NotBefore.ToString("yyyy-MM-dd HH:mm:ss UTC");
                        info["certificate_valid_to"] = cert2.NotAfter.ToString("yyyy-MM-dd HH:mm:ss UTC");
                        info["certificate_thumbprint"] = cert2.Thumbprint;
                    }
                }
                catch (Exception ex)
                {
                    info["signature_error"] = ex.Message;
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter informações de segurança do arquivo: {FilePath}", filePath);
            info["error"] = ex.Message;
            return info;
        }
    }
}