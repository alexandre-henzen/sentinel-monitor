# Test Screenshots

Este diretório contém screenshots de teste para validação do sistema EAM v5.0.

## Arquivos de Teste

### sample-screenshot-1920x1080.png
- **Resolução**: 1920x1080
- **Formato**: PNG
- **Tamanho**: ~2MB
- **Descrição**: Screenshot padrão para testes de upload e armazenamento

### sample-screenshot-1366x768.png
- **Resolução**: 1366x768
- **Formato**: PNG
- **Tamanho**: ~1.5MB
- **Descrição**: Screenshot para testes de diferentes resoluções

### sample-screenshot-small.png
- **Resolução**: 640x480
- **Formato**: PNG
- **Tamanho**: ~500KB
- **Descrição**: Screenshot pequeno para testes de performance

## Uso nos Testes

Estes arquivos são utilizados pelos testes de integração para:

1. **Testes de Upload**: Validar o upload de screenshots para o MinIO
2. **Testes de Formato**: Verificar suporte a diferentes formatos de imagem
3. **Testes de Tamanho**: Validar o processamento de diferentes tamanhos de arquivo
4. **Testes de Performance**: Medir tempo de upload e download

## Geração dos Arquivos

Os arquivos de teste podem ser gerados usando o script `generate-test-screenshots.ps1`:

```powershell
.\generate-test-screenshots.ps1
```

## Estrutura de Dados

Cada screenshot de teste contém:
- Cabeçalho PNG válido
- Dados de imagem comprimidos
- Metadados EXIF simulados
- Checksum para validação de integridade

## Validação

Para validar os arquivos de teste:

```powershell
# Verificar integridade dos arquivos
Get-ChildItem *.png | ForEach-Object { 
    Write-Host "$($_.Name): $($_.Length) bytes" 
}

# Validar formato PNG
Add-Type -AssemblyName System.Drawing
[System.Drawing.Image]::FromFile("sample-screenshot-1920x1080.png")
```

## Limpeza

Para limpar os arquivos de teste:

```powershell
Remove-Item *.png