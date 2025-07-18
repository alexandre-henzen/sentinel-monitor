#!/bin/bash

# MinIO Setup Script for EAM v5.0
# Configures bucket, policies, and lifecycle rules

set -e

# Configuration
MINIO_ENDPOINT="localhost:9000"
MINIO_ACCESS_KEY="minioadmin"
MINIO_SECRET_KEY="minioadmin"
BUCKET_NAME="eam-screenshots"
MINIO_ALIAS="eam-minio"

echo "🚀 Configurando MinIO para EAM v5.0..."

# Wait for MinIO to be ready
echo "⏳ Aguardando MinIO estar disponível..."
until curl -f http://${MINIO_ENDPOINT}/minio/health/live > /dev/null 2>&1; do
    echo "   MinIO não está pronto, aguardando..."
    sleep 2
done

echo "✅ MinIO está pronto!"

# Configure MinIO client
echo "🔧 Configurando cliente MinIO..."
mc alias set ${MINIO_ALIAS} http://${MINIO_ENDPOINT} ${MINIO_ACCESS_KEY} ${MINIO_SECRET_KEY}

# Create bucket if it doesn't exist
echo "📦 Criando bucket ${BUCKET_NAME}..."
mc mb ${MINIO_ALIAS}/${BUCKET_NAME} --ignore-existing

# Enable versioning
echo "🔄 Habilitando versionamento..."
mc version enable ${MINIO_ALIAS}/${BUCKET_NAME}

# Set lifecycle policy (90 days retention)
echo "📋 Configurando política de retenção (90 dias)..."
cat > /tmp/lifecycle-policy.json << EOF
{
    "Rules": [
        {
            "ID": "eam-screenshots-retention",
            "Status": "Enabled",
            "Filter": {
                "Prefix": ""
            },
            "Expiration": {
                "Days": 90
            },
            "NoncurrentVersionExpiration": {
                "NoncurrentDays": 30
            },
            "AbortIncompleteMultipartUpload": {
                "DaysAfterInitiation": 7
            }
        }
    ]
}
EOF

mc ilm set ${MINIO_ALIAS}/${BUCKET_NAME} < /tmp/lifecycle-policy.json

# Create access policy for EAM API
echo "🔐 Configurando política de acesso..."
cat > /tmp/eam-policy.json << EOF
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": {
                "AWS": "*"
            },
            "Action": [
                "s3:GetObject",
                "s3:PutObject",
                "s3:DeleteObject",
                "s3:ListBucket"
            ],
            "Resource": [
                "arn:aws:s3:::${BUCKET_NAME}",
                "arn:aws:s3:::${BUCKET_NAME}/*"
            ]
        }
    ]
}
EOF

mc anonymous set-json /tmp/eam-policy.json ${MINIO_ALIAS}/${BUCKET_NAME}

# Create service account for EAM API
echo "👤 Criando conta de serviço..."
mc admin user add ${MINIO_ALIAS} eam-api eam-api-secret-key

# Create policy for EAM API user
cat > /tmp/eam-api-policy.json << EOF
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "s3:GetObject",
                "s3:PutObject",
                "s3:DeleteObject",
                "s3:ListBucket",
                "s3:GetBucketLocation",
                "s3:ListBucketMultipartUploads",
                "s3:AbortMultipartUpload",
                "s3:ListMultipartUploadParts"
            ],
            "Resource": [
                "arn:aws:s3:::${BUCKET_NAME}",
                "arn:aws:s3:::${BUCKET_NAME}/*"
            ]
        }
    ]
}
EOF

mc admin policy create ${MINIO_ALIAS} eam-api-policy /tmp/eam-api-policy.json
mc admin policy attach ${MINIO_ALIAS} eam-api-policy --user eam-api

# Configure bucket notification (optional)
echo "🔔 Configurando notificações do bucket..."
cat > /tmp/bucket-notification.json << EOF
{
    "CloudWatchConfigurations": null,
    "LambdaConfigurations": null,
    "QueueConfigurations": null,
    "TopicConfigurations": null
}
EOF

mc event add ${MINIO_ALIAS}/${BUCKET_NAME} arn:minio:sqs::1:webhook --event put,delete

# Set bucket tags
echo "🏷️ Configurando tags do bucket..."
mc tag set ${MINIO_ALIAS}/${BUCKET_NAME} "Project=EAM" "Environment=Production" "Purpose=Screenshots" "Retention=90days"

# Test upload
echo "🧪 Testando upload..."
echo "Test screenshot from EAM v5.0 - $(date)" > /tmp/test-screenshot.txt
mc cp /tmp/test-screenshot.txt ${MINIO_ALIAS}/${BUCKET_NAME}/test/test-screenshot.txt

# Verify upload
echo "✅ Verificando upload..."
mc ls ${MINIO_ALIAS}/${BUCKET_NAME}/test/

# Clean up test file
mc rm ${MINIO_ALIAS}/${BUCKET_NAME}/test/test-screenshot.txt

# Clean up temporary files
rm -f /tmp/lifecycle-policy.json /tmp/eam-policy.json /tmp/eam-api-policy.json /tmp/bucket-notification.json /tmp/test-screenshot.txt

echo "✅ MinIO configurado com sucesso!"
echo ""
echo "📊 Resumo da configuração:"
echo "   - Bucket: ${BUCKET_NAME}"
echo "   - Versionamento: Habilitado"
echo "   - Retenção: 90 dias"
echo "   - Versões antigas: 30 dias"
echo "   - Endpoint: http://${MINIO_ENDPOINT}"
echo "   - Usuário API: eam-api"
echo ""
echo "🔧 Configuração no appsettings.json:"
echo "   \"MinIO\": {"
echo "     \"Endpoint\": \"${MINIO_ENDPOINT}\","
echo "     \"AccessKey\": \"eam-api\","
echo "     \"SecretKey\": \"eam-api-secret-key\","
echo "     \"BucketName\": \"${BUCKET_NAME}\","
echo "     \"UseSSL\": false"
echo "   }"