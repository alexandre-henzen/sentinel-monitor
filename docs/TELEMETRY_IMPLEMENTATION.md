# EAM v5.0 - Implementação de Telemetria e Observabilidade

## Visão Geral

Este documento descreve a implementação completa da **Fase 4: Telemetria e Observabilidade** do Employee Activity Monitor (EAM) v5.0.

## Arquitetura de Observabilidade

### Stack Tecnológica

- **OpenTelemetry**: Instrumentação e coleta de dados de telemetria
- **Grafana Loki**: Agregação e consulta de logs
- **Grafana Tempo**: Armazenamento e consulta de traces distribuídos
- **Prometheus**: Coleta e armazenamento de métricas
- **Grafana**: Visualização e dashboards
- **AlertManager**: Gerenciamento de alertas

### Componentes Implementados

#### 1. OpenTelemetry Configuration
- **EAM.API**: Instrumentação ASP.NET Core, HTTP Client, traces distribuídos
- **EAM.Agent**: Instrumentação HTTP Client, métricas customizadas, traces

#### 2. Logs Estruturados (Serilog)
- Formato JSON estruturado
- Correlação de traces (TraceId/SpanId)
- Múltiplos destinos: Console, File, Event Log, Loki
- Enrichers: Machine Name, Process ID, Thread ID, Environment

#### 3. Métricas Customizadas

**EAM Agent Metrics:**
- `eam_agent_cpu_usage_percent`: Uso de CPU
- `eam_agent_memory_usage_bytes`: Uso de memória
- `eam_agent_events_captured_total`: Eventos capturados
- `eam_agent_events_lost_total`: Eventos perdidos
- `eam_agent_screenshots_captured_total`: Screenshots
- `eam_agent_sync_latency_seconds`: Latência de sincronização

**EAM API Metrics:**
- `http_requests_total`: Total de requests HTTP
- `http_request_duration_seconds`: Duração de requests
- `eam_api_events_processed_total`: Eventos processados
- `eam_api_db_connections`: Conexões de banco

#### 4. Dashboards Grafana

**EAM Agent Dashboard:**
- CPU e memória do agente
- Taxa de captura de eventos
- Latência de sincronização
- Performance geral

**EAM API Dashboard:**
- Taxa de requests HTTP
- Tempo de resposta (percentis)
- Taxa de erro
- Processing rate de eventos

#### 5. Alertas Automáticos

**Critérios de Alerta:**
- CPU do agente > 2%
- Eventos perdidos > 0.1%
- Latência da API > 200ms
- Taxa de erro HTTP > 1%
- Disponibilidade do sistema < 99.5%

## Configuração e Deploy

### 1. Iniciar Stack de Observabilidade

```bash
# Subir toda a stack
docker-compose -f docker-compose.observability.yml up -d

# Verificar status
docker-compose -f docker-compose.observability.yml ps
```

### 2. Configuração de Aplicações

**appsettings.json (EAM.API):**
```json
{
  "Telemetry": {
    "EnableTracing": true,
    "EnableMetrics": true,
    "EnablePrometheus": true,
    "ServiceName": "EAM.API",
    "ServiceVersion": "5.0.0",
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "OtlpEndpoint": "http://localhost:4317"
  }
}
```

**appsettings.json (EAM.Agent):**
```json
{
  "Telemetry": {
    "EnableTelemetry": true,
    "EnableTracing": true,
    "EnableMetrics": true,
    "ServiceName": "EAM.Agent",
    "JaegerEndpoint": "http://localhost:14268/api/traces",
    "PrometheusEndpoint": "http://localhost:9090/metrics"
  }
}
```

### 3. Acessar Interfaces

- **Grafana**: http://localhost:3000 (admin/admin123)
- **Prometheus**: http://localhost:9090
- **AlertManager**: http://localhost:9093
- **Loki**: http://localhost:3100
- **Tempo**: http://localhost:3200

## Monitoramento e Alertas

### Configuração de Notificações

#### Email
1. Configure SMTP no AlertManager
2. Adicione destinatários em `observability/alertmanager-config.yml`

#### Slack
1. Crie webhook no Slack
2. Configure URL em `observability/alertmanager-config.yml`

#### Teams
1. Configure webhook do Teams
2. Adicione configuração no AlertManager

### Regras de Alerta Customizadas

Edite `observability/alert_rules.yml` para adicionar novas regras:

```yaml
- alert: Custom_Alert_Name
  expr: your_metric_query > threshold
  for: 2m
  labels:
    severity: warning
  annotations:
    summary: "Alert summary"
    description: "Alert description"
```

## Troubleshooting

### Problemas Comuns

#### 1. Métricas não aparecem no Prometheus
- Verifique se o endpoint `/metrics` está exposto
- Confirme configuração no `prometheus-config.yml`
- Verifique logs do Prometheus: `docker logs prometheus`

#### 2. Logs não chegam ao Loki
- Verifique configuração do Serilog
- Confirme conectividade com Loki
- Verifique logs: `docker logs loki`

#### 3. Traces não aparecem no Tempo
- Confirme configuração OpenTelemetry
- Verifique se traces estão sendo gerados
- Verificar logs: `docker logs tempo`

#### 4. Alertas não são enviados
- Verifique configuração do AlertManager
- Confirme regras no Prometheus
- Teste conectividade SMTP/Webhook

### Logs de Debug

```bash
# Verificar logs dos serviços
docker logs grafana
docker logs prometheus
docker logs loki
docker logs tempo
docker logs alertmanager
docker logs otel-collector

# Verificar métricas específicas
curl http://localhost:9090/api/v1/query?query=eam_agent_cpu_usage_percent

# Verificar saúde dos serviços
curl http://localhost:9090/-/healthy
curl http://localhost:3100/ready
```

### Performance Tuning

#### Prometheus
- Ajustar `scrape_interval` baseado na necessidade
- Configurar `retention.time` para dados históricos
- Monitorar uso de disk space

#### Loki
- Configurar `retention_period` para logs
- Ajustar `max_chunk_age` para performance
- Monitorar uso de memória

#### Tempo
- Configurar `block_retention` para traces
- Ajustar `max_traces_per_user`
- Monitorar storage usage

## Métricas de SLA

### Targets de Performance
- **Disponibilidade**: > 99.5%
- **Latência P95 API**: < 200ms
- **Taxa de Erro**: < 1%
- **CPU Agent**: < 2%
- **Perda de Eventos**: < 0.1%

### Monitoramento Contínuo
- Dashboards em tempo real
- Alertas proativos
- Relatórios semanais automatizados
- Review mensal de métricas

## Próximos Passos

1. **Implementar Service Level Objectives (SLOs)**
2. **Criar dashboards executivos**
3. **Implementar distributed tracing completo**
4. **Adicionar métricas de negócio**
5. **Configurar backup de dados de telemetria**

## Suporte

Para suporte técnico ou dúvidas sobre telemetria:
- Email: devops@eam.com
- Slack: #eam-observability
- Documentação: [Wiki Interno]

---
**Versão**: 5.0.0  
**Data**: Janeiro 2025  
**Autor**: EAM Development Team