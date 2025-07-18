# EAM Frontend Dockerfile - Angular SPA
# Multi-stage build for optimized Angular container

# Build stage
FROM node:18-alpine AS build
WORKDIR /app

# Install build dependencies
RUN apk add --no-cache git python3 make g++

# Copy package files
COPY src/package*.json ./
RUN npm ci --only=production --no-audit --no-fund

# Copy source code
COPY src/ ./

# Build the application
RUN npm run build:prod

# Runtime stage
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Install runtime dependencies
RUN apk add --no-cache curl tzdata

# Remove default nginx static assets
RUN rm -rf ./*

# Copy built application
COPY --from=build /app/dist/eam-frontend .

# Copy nginx configuration
COPY deploy/nginx/nginx.conf /etc/nginx/nginx.conf
COPY deploy/nginx/default.conf /etc/nginx/conf.d/default.conf

# Copy additional configuration files
COPY deploy/nginx/mime.types /etc/nginx/mime.types
COPY deploy/nginx/security-headers.conf /etc/nginx/security-headers.conf

# Create nginx cache directories
RUN mkdir -p /var/cache/nginx/client_temp \
    /var/cache/nginx/proxy_temp \
    /var/cache/nginx/fastcgi_temp \
    /var/cache/nginx/uwsgi_temp \
    /var/cache/nginx/scgi_temp

# Set proper permissions
RUN chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /etc/nginx/conf.d

# Add health check endpoint
RUN echo '<!DOCTYPE html><html><head><title>Health Check</title></head><body><h1>OK</h1></body></html>' > /usr/share/nginx/html/health

# Environment variables
ENV NODE_ENV=production
ENV NGINX_WORKER_PROCESSES=auto
ENV NGINX_WORKER_CONNECTIONS=1024

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

# Expose port
EXPOSE 80

# Switch to non-root user
USER nginx

# Start nginx
CMD ["nginx", "-g", "daemon off;"]