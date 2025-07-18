# Employee Activity Monitor (EAM) v5.0 - Infraestrutura

Este diretório contém toda a infraestrutura necessária para executar o Employee Activity Monitor v5.0, incluindo PostgreSQL 16 com tabelas particionadas, MinIO para armazenamento de screenshots, Redis para cache e sessões, e ferramentas de monitoramento.

## 🏗️ Arquitetura

```
EAM v5.0 Infrastructure
├── PostgreSQL 16 (Database)
│   ├── Tabelas particionadas por data
│   ├── Índices BRIN para time-series
│   ├── Views materializadas para agregações
│   └── Backup/restore automatizado
├── MinIO (Object Storage)
│   ├── Bucket: eam-screenshots
│   ├── Retenção: 90 dias
│   └── Versionamento habilitado
├── Redis 7 (Cache & Sessions)
│   ├── Cache de dados
│   ├── Sessões de usuário
│   ├── Revogação de tokens JWT
│   └── Rate limiting
└── Monitoramento
    ├── Jaeger (Tracing)
    ├── Prometheus (Metrics)
    └── Grafana (Visualization)
```

## 📁 Estrutura de Diretórios

```
infrastructure/
├── database/
│   ├── migrations/         # Scripts SQL versionados
│   │   └── 001_initial_schema.sql
│   ├── indexes/           # Índices otimizados
│   │   └── 002_optimized_indexes.sql
│   ├── views/             # Views materializadas
│   │   └── 003_materialized_views.sql
│   └── seeds/             # Dados iniciais
│       └── 004_seed_data.sql
├── storage/
│   ├── minio/             # Configuração MinIO
│   │   ├── minio-config.json
│   │   └── setup-minio.sh
│   └── redis/             # Configuração Redis
│       └── redis.conf
├── scripts/               # Scripts de manutenção
│   ├── backup-postgres.sh
│   └── restore-postgres.sh
├── docker-compose.yml     # Produção
├── docker-compose.dev.yml # Desenvolvimento
└── setup-database.sql    # Setup completo
```

## 🚀 Início Rápido

### 1. Ambiente de Desenvolvimento

```bash
# Clonar o repositório
git clone <repository-url>
cd sentinel-monitor/infrastructure

# Iniciar infraestrutura de desenvolvimento
docker-compose -f docker-compose.dev.yml up -d

# Verificar status dos serviços
docker-compose -f docker-compose.dev.yml ps
```

### 2. Ambiente de Produção

```bash
# Iniciar infraestrutura de produção
docker-compose up -d

# Verificar logs
docker-compose logs -f
```

### 3. Setup Manual do Banco de Dados

```bash
# Conectar ao PostgreSQL
psql -h localhost -p 5432 -U eam_user -d eam

# Executar setup completo
\i setup-database.sql
```

## 🛠️ Configuração Detalhada

### PostgreSQL 16

**Características:**
- Tabelas particionadas por mês (activity_logs)
- Índices BRIN para queries temporais
- Views materializadas para performance
- Backup automático com retenção de 30 dias

**Configuração da Aplicação:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=eam;Username=eam_user;Password=eam_pass"
  }
}
```

**Tabelas Principais:**
- `eam.users` - Usuários do sistema
- `eam.agents` - Agentes de monitoramento
- `eam.activity_logs` - Logs de atividade (particionada)
- `eam.daily_scores` - Scores diários agregados

### MinIO

**Características:**
- Bucket: `eam-screenshots`
- Retenção: 90 dias
- Versionamento habilitado
- Política de ciclo de vida automática

**Configuração da Aplicação:**
```json
{
  "Storage": {
    "MinIO": {
      "Endpoint": "localhost:9000",
      "AccessKey": "eam-api",
      "SecretKey": "eam-api-secret-key",
      "BucketName": "eam-screenshots",
      "UseSSL": false
    }
  }
}
```

### Redis 7

**Características:**
- Database 0: Cache geral
- Database 1: Revogação de tokens JWT
- Database 2: Sessões de usuário
- Database 3: Rate limiting

**Configuração da Aplicação:**
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

## 📊 Monitoramento

### Jaeger (Tracing)
- URL: http://localhost:16686
- Endpoint OTLP: http://localhost:4317

### Prometheus (Metrics)
- URL: http://localhost:9090
- Coleta métricas da aplicação

### Grafana (Visualization)
- URL: http://localhost:3000
- Usuário: admin / admin

## 🔧 Operações de Manutenção

### Backup do PostgreSQL

```bash
# Backup manual
docker exec eam-postgres /backups/backup-postgres.sh

# Backup com script
cd infrastructure/scripts
./backup-postgres.sh
```

### Restore do PostgreSQL

```bash
# Restore de backup
./restore-postgres.sh /backups/eam_backup_20240101_120000.sql.gz
```

### Manutenção de Partições

```bash
# Conectar ao PostgreSQL
psql -h localhost -p 5432 -U eam_user -d eam

# Executar manutenção
SELECT eam.maintain_partitions();
```

### Atualização de Views Materializadas

```bash
# Atualização completa
SELECT eam.refresh_all_materialized_views();

# Atualização incremental
SELECT eam.refresh_materialized_views_incremental();
```

## 🔒 Segurança

### Usuários do Banco de Dados

- **eam_user**: Usuário principal com privilégios completos
- **eam_readonly**: Usuário somente leitura para relatórios

### MinIO

- **minioadmin**: Usuário administrativo
- **eam-api**: Usuário da aplicação com acesso ao bucket

### Redis

- Configurado sem senha para desenvolvimento
- Para produção, descomente a linha `requirepass` em `redis.conf`

## 📈 Performance

### Configurações Otimizadas

- **PostgreSQL**: Configurado para ~10k EPS (events per second)
- **Índices BRIN**: Otimizados para time-series data
- **Particionamento**: Automático por mês
- **Materialized Views**: Atualizadas a cada 15 minutos

### Monitoramento de Performance

```sql
-- Estatísticas de tabelas
SELECT * FROM eam.get_database_stats();

-- Queries mais lentas
SELECT * FROM pg_stat_statements ORDER BY total_exec_time DESC LIMIT 10;

-- Uso de índices
SELECT * FROM pg_stat_user_indexes WHERE schemaname = 'eam';
```

## 🧪 Testes

### Verificação de Integridade

```bash
# Testar conexão PostgreSQL
docker exec eam-postgres pg_isready -U eam_user -d eam

# Testar MinIO
curl -f http://localhost:9000/minio/health/live

# Testar Redis
docker exec eam-redis redis-cli ping
```

### Testes de Performance

```sql
-- Teste de inserção em lote
INSERT INTO eam.activity_logs (agent_id, event_type, application_name, event_timestamp)
SELECT 
    (SELECT id FROM eam.agents LIMIT 1),
    'Test',
    'TestApp',
    NOW() + (i || ' seconds')::interval
FROM generate_series(1, 1000) i;

-- Teste de consulta temporal
SELECT COUNT(*) FROM eam.activity_logs 
WHERE event_timestamp >= NOW() - INTERVAL '1 hour';
```

## 🚨 Troubleshooting

### Problemas Comuns

1. **Container não inicia**
   ```bash
   # Verificar logs
   docker-compose logs service-name
   
   # Verificar recursos
   docker system df
   ```

2. **Erro de conexão PostgreSQL**
   ```bash
   # Verificar se o serviço está rodando
   docker-compose ps postgres
   
   # Verificar logs
   docker-compose logs postgres
   ```

3. **MinIO não responde**
   ```bash
   # Verificar health check
   curl -f http://localhost:9000/minio/health/live
   
   # Recriar container
   docker-compose restart minio
   ```

### Logs Importantes

```bash
# Logs de todos os serviços
docker-compose logs -f

# Logs específicos
docker-compose logs -f postgres
docker-compose logs -f redis
docker-compose logs -f minio
```

## 📝 Notas de Desenvolvimento

### Ambiente de Desenvolvimento

- PostgreSQL: porta 5433
- Redis: porta 6380
- MinIO: porta 9001
- Inclui PgAdmin e Redis Commander

### Ambiente de Produção

- PostgreSQL: porta 5432
- Redis: porta 6379
- MinIO: porta 9000
- Inclui Nginx reverse proxy

## 🔄 Atualizações

### Versionamento de Migrations

Para adicionar novas migrations:

1. Criar arquivo `005_nova_migration.sql`
2. Executar: `\i migrations/005_nova_migration.sql`
3. Atualizar `setup-database.sql`

### Atualizações de Configuração

Para modificar configurações:

1. Atualizar arquivos de configuração
2. Recriar containers: `docker-compose up -d --force-recreate`
3. Verificar funcionamento

## 📞 Suporte

Para dúvidas ou problemas:

1. Verificar logs dos containers
2. Consultar documentação do PostgreSQL 16
3. Verificar configurações de rede
4. Validar permissões de arquivo

---

**Employee Activity Monitor v5.0**  
*Infraestrutura de produção com PostgreSQL 16, MinIO e Redis*