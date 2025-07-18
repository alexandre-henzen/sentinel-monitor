# EAM Agent MSI Installer

Este diretório contém todos os arquivos necessários para construir o instalador MSI do EAM Agent usando o WiX Toolset.

## Estrutura do Projeto

```
tools/installer/
├── EAM.Installer.wixproj    # Projeto WiX principal
├── Product.wxs              # Definição do produto e componentes
├── Features.wxs             # Definição das features do instalador
├── README.md                # Esta documentação
├── Scripts/                 # Scripts de build e assinatura
│   ├── build-installer.ps1  # Script de build do MSI
│   ├── sign-installer.ps1   # Script de assinatura digital
│   └── build-and-sign.ps1   # Script master para build e assinatura
└── Resources/               # Recursos do instalador
    ├── eam-icon.ico         # Ícone do aplicativo
    ├── banner.bmp           # Banner do instalador
    ├── dialog.bmp           # Diálogo do instalador
    ├── LICENSE.txt          # Licença
    └── README.md            # Readme para o usuário
```

## Pré-requisitos

- **.NET SDK 8.0** ou superior
- **WiX Toolset v4.0** ou superior
- **Windows SDK** (para assinatura digital)
- **PowerShell 5.1** ou superior

### Instalação do WiX Toolset

```powershell
# Via dotnet tool
dotnet tool install --global wix

# Via Chocolatey
choco install wixtoolset

# Via winget
winget install Microsoft.WiX
```

## Build do Instalador

### Build Simples

```powershell
# Navegar para o diretório de scripts
cd tools/installer/Scripts

# Executar build básico
.\build-installer.ps1
```

### Build com Opções

```powershell
# Build completo com limpeza
.\build-installer.ps1 -Clean -Configuration Release -Platform x64

# Build com versão específica
.\build-installer.ps1 -Version "5.0.1" -Configuration Release

# Build sem executar testes
.\build-installer.ps1 -SkipTests

# Build com saída detalhada
.\build-installer.ps1 -Verbose
```

### Parâmetros do Build

- `-Configuration`: Debug ou Release (padrão: Release)
- `-Platform`: x64 ou x86 (padrão: x64)
- `-Version`: Versão específica (padrão: detectada do projeto)
- `-OutputPath`: Caminho de saída customizado
- `-Clean`: Limpa arquivos antes do build
- `-SkipTests`: Pula a execução de testes
- `-SkipPublish`: Pula a publicação do .NET (usa binários existentes)
- `-Verbose`: Saída detalhada

## Assinatura Digital

### Assinatura com Certificado

```powershell
# Assinar com arquivo de certificado
.\sign-installer.ps1 -MsiFile "EAM.Agent.v5.0.0.msi" -CertificateFile "cert.pfx" -CertificatePassword (ConvertTo-SecureString "password" -AsPlainText -Force)

# Assinar com certificado do store
.\sign-installer.ps1 -MsiFile "EAM.Agent.v5.0.0.msi" -Thumbprint "ABC123..."

# Assinar e verificar
.\sign-installer.ps1 -MsiFile "EAM.Agent.v5.0.0.msi" -CertificateFile "cert.pfx" -Verify
```

### Parâmetros da Assinatura

- `-MsiFile`: Arquivo MSI para assinar (obrigatório)
- `-CertificateFile`: Arquivo de certificado (.pfx/.p12)
- `-CertificatePassword`: Senha do certificado (SecureString)
- `-Thumbprint`: Thumbprint do certificado no store
- `-TimestampServer`: Servidor de timestamp (padrão: DigiCert)
- `-Description`: Descrição da assinatura
- `-Verify`: Verificar assinatura após assinar
- `-Force`: Forçar assinatura mesmo se já assinado

## Build e Assinatura Completo

```powershell
# Build e assinatura em um comando
.\build-and-sign.ps1 -Version "5.0.1" -CertificateFile "cert.pfx" -Clean

# Build sem assinatura
.\build-and-sign.ps1 -SkipSigning

# Build completo com todas as opções
.\build-and-sign.ps1 -Configuration Release -Version "5.0.1" -CertificateFile "cert.pfx" -Clean -Verbose
```

## Estrutura do Instalador

### Componentes Principais

1. **Aplicação Principal**: EAM.Agent.exe e dependências
2. **Serviço Windows**: Configuração automática do serviço
3. **Diretórios**: Estrutura de pastas no sistema
4. **Registro**: Entradas no registry do Windows
5. **Firewall**: Exceções de firewall automáticas
6. **Atalhos**: Start Menu e Desktop (opcional)

### Features Disponíveis

- **Core**: Aplicação principal (obrigatório)
- **Security**: Componentes de segurança
- **UI**: Interface do usuário
- **Monitoring**: Monitoramento e diagnósticos
- **Updates**: Sistema de atualizações automáticas
- **Plugins**: Sistema de plugins
- **Telemetry**: Telemetria e analytics
- **Developer**: Ferramentas de desenvolvimento
- **Languages**: Pacotes de idiomas
- **Documentation**: Documentação

### Diretórios de Instalação

- **Aplicação**: `C:\Program Files\EAM Agent\`
- **Dados**: `%LOCALAPPDATA%\EAM\`
- **Logs**: `%LOCALAPPDATA%\EAM\Logs\`
- **Backups**: `%LOCALAPPDATA%\EAM\Backups\`
- **Plugins**: `%PROGRAMDATA%\EAM\plugins\`
- **Configuração**: `%LOCALAPPDATA%\EAM\Config\`

## Logs e Relatórios

### Logs de Build

Os logs são salvos em `logs/` na raiz do projeto:

- `installer_build_*.log`: Logs detalhados do build
- `signing_*.log`: Logs da assinatura digital
- `master_build_*.log`: Log do processo completo
- `build_summary_*.json`: Resumo do build em JSON
- `signing_report_*.json`: Relatório de assinatura em JSON

### Verificação de Integridade

Cada MSI gerado inclui:

- **Checksums**: SHA256, SHA1, MD5
- **Arquivo de verificação**: `*.checksums.txt`
- **Validação estrutural**: Verificação da estrutura MSI
- **Verificação de assinatura**: Validação da assinatura digital

## Solução de Problemas

### Problemas Comuns

1. **WiX não encontrado**
   ```
   Solução: Instalar WiX Toolset ou especificar caminho manualmente
   ```

2. **Certificado inválido**
   ```
   Solução: Verificar validade e formato do certificado
   ```

3. **Falha na assinatura**
   ```
   Solução: Verificar permissões e configuração do signtool
   ```

4. **Binários não encontrados**
   ```
   Solução: Executar build completo do projeto antes do MSI
   ```

### Debug e Diagnóstico

```powershell
# Build com máximo detalhe
.\build-installer.ps1 -Verbose -Configuration Debug

# Verificar apenas assinatura
.\sign-installer.ps1 -MsiFile "arquivo.msi" -Verify

# Validar estrutura MSI
msiinfo.exe "arquivo.msi"
```

## Integração com CI/CD

### GitHub Actions

```yaml
- name: Build MSI Installer
  run: |
    cd tools/installer/Scripts
    .\build-and-sign.ps1 -Configuration Release -Version "${{ github.ref_name }}" -SkipSigning
```

### Azure DevOps

```yaml
- powershell: |
    cd tools/installer/Scripts
    .\build-and-sign.ps1 -Configuration Release -Version "$(Build.BuildNumber)" -CertificateFile "$(CertificateFile)" -CertificatePassword $(CertificatePassword)
  displayName: 'Build and Sign MSI'
```

## Customização

### Modificar Componentes

Editar `Product.wxs` para adicionar/remover componentes:

```xml
<Component Id="NovoComponente" Guid="GUID-AQUI">
    <File Source="caminho\para\arquivo.exe" />
</Component>
```

### Adicionar Features

Editar `Features.wxs` para adicionar novas features:

```xml
<Feature Id="NovaFeature" Title="Nova Feature">
    <ComponentRef Id="NovoComponente" />
</Feature>
```

### Personalizar Interface

Substituir arquivos em `Resources/`:

- `banner.bmp`: Banner do instalador (493x58)
- `dialog.bmp`: Diálogo principal (493x312)
- `eam-icon.ico`: Ícone do aplicativo (16x16, 32x32, 48x48)

## Suporte

Para suporte técnico:

- **Documentação**: https://docs.eam.local
- **Issues**: https://github.com/eam/agent/issues
- **Email**: support@eam.local