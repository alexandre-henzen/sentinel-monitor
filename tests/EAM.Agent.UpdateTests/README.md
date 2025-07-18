# EAM Agent Update System Tests

Este diretório contém testes abrangentes para o sistema de auto-update do agente Windows EAM.

## Estrutura dos Testes

### Testes Unitários
- **`UpdateServiceTests.cs`** - Testes unitários para o serviço principal de atualização
- **`VersionManagerTests.cs`** - Testes para gerenciamento de versões semânticas
- **`UpdateConfigTests.cs`** - Testes para configurações de atualização
- **`VersionInfoTests.cs`** - Testes para comparação e parsing de versões

### Testes de Integração
- **`UpdateServiceIntegrationTests.cs`** - Testes de integração com mocks HTTP
- **`MSIInstallerTests.cs`** - Testes para validação do sistema MSI
- **`BuildScriptsTests.cs`** - Testes para validar scripts de build PowerShell

### Testes End-to-End
- **`EndToEndUpdateTests.cs`** - Testes completos do fluxo de atualização

## Executando os Testes

### Pré-requisitos
1. .NET 8 SDK instalado
2. Visual Studio 2022 ou VS Code
3. Pacotes NuGet restaurados

### Comandos de Execução

#### Executar todos os testes
```bash
dotnet test tests/EAM.Agent.UpdateTests/
```

#### Executar categoria específica
```bash
# Testes unitários
dotnet test tests/EAM.Agent.UpdateTests/ --filter "Category=Unit"

# Testes de integração
dotnet test tests/EAM.Agent.UpdateTests/ --filter "Category=Integration"

# Testes end-to-end
dotnet test tests/EAM.Agent.UpdateTests/ --filter "Category=EndToEnd"
```

#### Executar classe específica
```bash
dotnet test tests/EAM.Agent.UpdateTests/ --filter "ClassName=UpdateServiceTests"
```

#### Executar com cobertura de código
```bash
dotnet test tests/EAM.Agent.UpdateTests/ --collect:"XPlat Code Coverage"
```

#### Executar com relatório detalhado
```bash
dotnet test tests/EAM.Agent.UpdateTests/ --logger:trx --results-directory ./TestResults
```

## Cenários de Teste Cobertos

### 1. Verificação de Atualizações
- ✅ Detecção de atualizações disponíveis
- ✅ Comparação de versões semânticas
- ✅ Validação de requisitos mínimos
- ✅ Tratamento de cenários sem atualização

### 2. Download de Atualizações
- ✅ Download seguro com verificação de integridade
- ✅ Validação de checksum SHA256
- ✅ Retry automático em caso de falha
- ✅ Timeout e cancelamento de downloads

### 3. Validação de Segurança
- ✅ Verificação de assinatura digital
- ✅ Validação de certificados confiáveis
- ✅ Verificação de integridade do pacote
- ✅ Validação de publisher confiável

### 4. Processo de Instalação
- ✅ Instalação silenciosa MSI
- ✅ Backup automático antes da instalação
- ✅ Rollback em caso de falha
- ✅ Validação de parâmetros de instalação

### 5. Janela de Manutenção
- ✅ Respeito à janela de manutenção configurada
- ✅ Bypass para atualizações críticas
- ✅ Agendamento de atualizações
- ✅ Validação de horários de manutenção

### 6. Tratamento de Erros
- ✅ Recuperação de falhas de rede
- ✅ Retry com backoff exponencial
- ✅ Logging detalhado de erros
- ✅ Notificação de status para usuário

### 7. Configuração e Personalização
- ✅ Validação de configurações
- ✅ Configurações específicas por ambiente
- ✅ Habilitação/desabilitação de funcionalidades
- ✅ Configuração de URLs e timeouts

### 8. Scripts de Build
- ✅ Validação de scripts PowerShell
- ✅ Verificação de estrutura de comandos
- ✅ Validação de parâmetros obrigatórios
- ✅ Tratamento de erros em scripts

## Mocks e Fakes

### HttpClientFactory Mock
Os testes usam `Mock<HttpMessageHandler>` para simular respostas da API:
- Simulação de diferentes cenários de resposta
- Controle de timeouts e erros de rede
- Validação de requests enviados

### Sistema de Arquivos Mock
Para testes que não devem afetar o sistema real:
- Diretórios temporários para downloads
- Arquivos de teste para validação
- Limpeza automática após testes

### Configuração Mock
Configurações específicas para cada teste:
- Diferentes cenários de configuração
- Simulação de ambientes variados
- Validação de comportamentos específicos

## Relatórios e Cobertura

### Cobertura de Código
Os testes cobrem:
- **Serviços principais**: UpdateService, VersionManager, DownloadManager, BackupService
- **Helpers**: FileHelper, ProcessHelper, SecurityHelper
- **Configuração**: UpdateConfig, MaintenanceWindow
- **Modelos**: UpdateInfo, VersionInfo, UpdateStatus

### Métricas de Qualidade
- **Cobertura de código**: > 90%
- **Testes unitários**: > 95% dos métodos públicos
- **Testes de integração**: Principais fluxos de trabalho
- **Testes end-to-end**: Cenários completos de uso

## Debugging e Troubleshooting

### Logs de Teste
Os testes incluem logging detalhado através do `ITestOutputHelper`:
```csharp
_output.WriteLine($"Progress: {status.State} - {status.Message}");
```

### Arquivos Temporários
Os testes criam arquivos temporários em:
- `%TEMP%\EAM.UpdateTests\{GUID}\`
- Limpeza automática via `IDisposable`

### Variáveis de Ambiente
Para testes locais, configure:
```bash
# Opcional: URL da API de teste
export EAM_API_BASE_URL=https://api.test.com

# Opcional: Certificado para testes de assinatura
export EAM_TEST_CERT_PATH=C:\path\to\test-cert.pfx
```

## Integração com CI/CD

### GitHub Actions
```yaml
- name: Run Update Tests
  run: dotnet test tests/EAM.Agent.UpdateTests/ --configuration Release --logger trx --results-directory ./TestResults
```

### Azure DevOps
```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Update Tests'
  inputs:
    command: 'test'
    projects: 'tests/EAM.Agent.UpdateTests/*.csproj'
    arguments: '--configuration Release --logger trx --results-directory $(Agent.TempDirectory)'
```

## Contribuindo

### Adicionando Novos Testes
1. Siga o padrão de nomenclatura: `[ClassUnderTest]Tests.cs`
2. Use Arrange-Act-Assert pattern
3. Inclua documentação clara dos cenários
4. Adicione cleanup apropriado via `IDisposable`

### Convenções
- Use `FluentAssertions` para asserções mais legíveis
- Inclua logging via `ITestOutputHelper`
- Crie mocks apropriados para dependências externas
- Mantenha testes independentes e determinísticos

### Exemplo de Teste
```csharp
[Fact]
public async Task UpdateService_WhenUpdateAvailable_ShouldReturnUpdateInfo()
{
    // Arrange
    var expectedUpdate = new UpdateInfo { Version = new VersionInfo(2, 0, 0) };
    SetupHttpMock(expectedUpdate);

    // Act
    var result = await _updateService.CheckForUpdateAsync();

    // Assert
    result.Should().NotBeNull();
    result.Version.Should().Be(expectedUpdate.Version);
    _output.WriteLine($"✓ Update detected: {result.Version}");
}
```

## Troubleshooting Comum

### Erro: "Could not find project root"
- Certifique-se de que o arquivo `EAM.sln` está no diretório raiz
- Execute os testes a partir do diretório do projeto

### Erro: "Access denied" em arquivos temporários
- Verifique permissões no diretório %TEMP%
- Execute como administrador se necessário

### Erro: "Mock setup failed"
- Verifique se os mocks estão configurados corretamente
- Confirme que as URLs e parâmetros estão corretos

### Timeouts em testes de integração
- Aumente o timeout nos testes se necessário
- Verifique conectividade de rede para testes que usam HTTP