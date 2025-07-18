{{/*
Expand the name of the chart.
*/}}
{{- define "eam.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "eam.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "eam.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "eam.labels" -}}
helm.sh/chart: {{ include "eam.chart" . }}
{{ include "eam.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- with .Values.commonLabels }}
{{ toYaml . }}
{{- end }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "eam.selectorLabels" -}}
app.kubernetes.io/name: {{ include "eam.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "eam.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "eam.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
PostgreSQL host
*/}}
{{- define "eam.postgresql.host" -}}
{{- if .Values.postgresql.enabled }}
{{- printf "%s-postgresql" (include "eam.fullname" .) }}
{{- else }}
{{- .Values.externalPostgresql.host }}
{{- end }}
{{- end }}

{{/*
PostgreSQL port
*/}}
{{- define "eam.postgresql.port" -}}
{{- if .Values.postgresql.enabled }}
{{- .Values.postgresql.primary.service.ports.postgresql }}
{{- else }}
{{- .Values.externalPostgresql.port }}
{{- end }}
{{- end }}

{{/*
PostgreSQL database name
*/}}
{{- define "eam.postgresql.database" -}}
{{- if .Values.postgresql.enabled }}
{{- .Values.postgresql.auth.database }}
{{- else }}
{{- .Values.externalPostgresql.database }}
{{- end }}
{{- end }}

{{/*
PostgreSQL username
*/}}
{{- define "eam.postgresql.username" -}}
{{- if .Values.postgresql.enabled }}
{{- .Values.postgresql.auth.username }}
{{- else }}
{{- .Values.externalPostgresql.username }}
{{- end }}
{{- end }}

{{/*
Redis host
*/}}
{{- define "eam.redis.host" -}}
{{- if .Values.redis.enabled }}
{{- printf "%s-redis-master" (include "eam.fullname" .) }}
{{- else }}
{{- .Values.externalRedis.host }}
{{- end }}
{{- end }}

{{/*
Redis port
*/}}
{{- define "eam.redis.port" -}}
{{- if .Values.redis.enabled }}
{{- .Values.redis.master.service.ports.redis }}
{{- else }}
{{- .Values.externalRedis.port }}
{{- end }}
{{- end }}

{{/*
MinIO host
*/}}
{{- define "eam.minio.host" -}}
{{- if .Values.minio.enabled }}
{{- printf "%s-minio" (include "eam.fullname" .) }}
{{- else }}
{{- .Values.externalMinio.host }}
{{- end }}
{{- end }}

{{/*
MinIO port
*/}}
{{- define "eam.minio.port" -}}
{{- if .Values.minio.enabled }}
{{- .Values.minio.service.ports.api }}
{{- else }}
{{- .Values.externalMinio.port }}
{{- end }}
{{- end }}

{{/*
Create database connection string
*/}}
{{- define "eam.database.connectionString" -}}
{{- printf "Host=%s;Port=%s;Database=%s;Username=%s;Password=%s;SSL Mode=Require" (include "eam.postgresql.host" .) (include "eam.postgresql.port" .) (include "eam.postgresql.database" .) (include "eam.postgresql.username" .) .Values.postgresql.auth.password }}
{{- end }}

{{/*
Create Redis connection string
*/}}
{{- define "eam.redis.connectionString" -}}
{{- printf "%s:%s" (include "eam.redis.host" .) (include "eam.redis.port" .) }}
{{- end }}

{{/*
Create MinIO endpoint
*/}}
{{- define "eam.minio.endpoint" -}}
{{- printf "%s:%s" (include "eam.minio.host" .) (include "eam.minio.port" .) }}
{{- end }}