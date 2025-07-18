-- Setup Database Script for EAM v5.0
-- Employee Activity Monitor - Complete Database Setup
-- PostgreSQL 16 with partitioned tables and optimized performance

-- ==============================================================================
-- INITIAL SETUP
-- ==============================================================================

-- Create database user if not exists
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_user WHERE usename = 'eam_user') THEN
        CREATE USER eam_user WITH PASSWORD 'eam_pass';
    END IF;
END $$;

-- Create database if not exists
SELECT 'CREATE DATABASE eam OWNER eam_user'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'eam');

-- Connect to eam database
\c eam eam_user

-- ==============================================================================
-- EXTENSIONS AND SCHEMA
-- ==============================================================================

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "btree_gin";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- Create schema
CREATE SCHEMA IF NOT EXISTS eam;
ALTER SCHEMA eam OWNER TO eam_user;

-- Set search path
SET search_path TO eam, public;

-- ==============================================================================
-- EXECUTE MIGRATION SCRIPTS
-- ==============================================================================

-- Execute migration scripts in order
\echo 'Executing migration 001: Initial Schema'
\i migrations/001_initial_schema.sql

\echo 'Executing migration 002: Optimized Indexes'
\i indexes/002_optimized_indexes.sql

\echo 'Executing migration 003: Materialized Views'
\i views/003_materialized_views.sql

\echo 'Executing migration 004: Seed Data'
\i seeds/004_seed_data.sql

-- ==============================================================================
-- PERFORMANCE OPTIMIZATION
-- ==============================================================================

-- Set PostgreSQL configuration for optimal performance
ALTER SYSTEM SET shared_preload_libraries = 'pg_stat_statements';
ALTER SYSTEM SET max_connections = 200;
ALTER SYSTEM SET shared_buffers = '256MB';
ALTER SYSTEM SET effective_cache_size = '1GB';
ALTER SYSTEM SET maintenance_work_mem = '64MB';
ALTER SYSTEM SET checkpoint_completion_target = 0.9;
ALTER SYSTEM SET wal_buffers = '16MB';
ALTER SYSTEM SET default_statistics_target = 100;
ALTER SYSTEM SET random_page_cost = 1.1;
ALTER SYSTEM SET effective_io_concurrency = 200;
ALTER SYSTEM SET work_mem = '4MB';
ALTER SYSTEM SET min_wal_size = '1GB';
ALTER SYSTEM SET max_wal_size = '4GB';
ALTER SYSTEM SET max_worker_processes = 8;
ALTER SYSTEM SET max_parallel_workers_per_gather = 2;
ALTER SYSTEM SET max_parallel_workers = 8;
ALTER SYSTEM SET max_parallel_maintenance_workers = 2;

-- ==============================================================================
-- MONITORING AND MAINTENANCE
-- ==============================================================================

-- Create monitoring functions
CREATE OR REPLACE FUNCTION eam.get_database_stats()
RETURNS TABLE (
    table_name text,
    row_count bigint,
    total_size text,
    index_size text,
    toast_size text
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        schemaname||'.'||tablename as table_name,
        n_tup_ins - n_tup_del as row_count,
        pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as total_size,
        pg_size_pretty(pg_indexes_size(schemaname||'.'||tablename)) as index_size,
        pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename) - pg_relation_size(schemaname||'.'||tablename)) as toast_size
    FROM pg_stat_user_tables 
    WHERE schemaname = 'eam'
    ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
END;
$$ LANGUAGE plpgsql;

-- Create partition maintenance function
CREATE OR REPLACE FUNCTION eam.maintain_partitions()
RETURNS void AS $$
DECLARE
    current_month date;
    future_month date;
    partition_name text;
BEGIN
    -- Create partitions for next 6 months
    FOR i IN 1..6 LOOP
        future_month := date_trunc('month', CURRENT_DATE) + (i || ' months')::interval;
        partition_name := 'activity_logs_' || to_char(future_month, 'YYYY_MM');
        
        BEGIN
            PERFORM eam.create_monthly_partition('activity_logs', future_month);
            RAISE NOTICE 'Created partition: %', partition_name;
        EXCEPTION
            WHEN duplicate_table THEN
                RAISE NOTICE 'Partition already exists: %', partition_name;
        END;
    END LOOP;
    
    -- Drop partitions older than 1 year
    FOR partition_name IN 
        SELECT schemaname||'.'||tablename 
        FROM pg_tables 
        WHERE schemaname = 'eam' 
        AND tablename LIKE 'activity_logs_%' 
        AND to_date(substring(tablename from 15), 'YYYY_MM') < date_trunc('month', CURRENT_DATE - INTERVAL '1 year')
    LOOP
        EXECUTE 'DROP TABLE IF EXISTS ' || partition_name || ' CASCADE';
        RAISE NOTICE 'Dropped old partition: %', partition_name;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- Create cleanup function for old data
CREATE OR REPLACE FUNCTION eam.cleanup_old_data()
RETURNS void AS $$
BEGIN
    -- Clean up old activity logs (keep only 1 year)
    DELETE FROM eam.activity_logs 
    WHERE event_timestamp < NOW() - INTERVAL '1 year';
    
    -- Clean up old daily scores (keep only 2 years)
    DELETE FROM eam.daily_scores 
    WHERE activity_date < CURRENT_DATE - INTERVAL '2 years';
    
    -- Update statistics
    ANALYZE eam.activity_logs;
    ANALYZE eam.daily_scores;
    
    -- Refresh materialized views
    PERFORM eam.refresh_all_materialized_views();
END;
$$ LANGUAGE plpgsql;

-- ==============================================================================
-- SECURITY CONFIGURATION
-- ==============================================================================

-- Revoke unnecessary permissions
REVOKE ALL ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON DATABASE eam FROM PUBLIC;

-- Grant specific permissions to eam_user
GRANT USAGE ON SCHEMA eam TO eam_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA eam TO eam_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA eam TO eam_user;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA eam TO eam_user;

-- Create read-only user for reporting
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_user WHERE usename = 'eam_readonly') THEN
        CREATE USER eam_readonly WITH PASSWORD 'eam_readonly_pass';
    END IF;
END $$;

GRANT USAGE ON SCHEMA eam TO eam_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA eam TO eam_readonly;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA eam TO eam_readonly;

-- ==============================================================================
-- SCHEDULED JOBS (commented out - requires pg_cron extension)
-- ==============================================================================

-- Uncomment these lines if pg_cron extension is available
-- SELECT cron.schedule('eam-partition-maintenance', '0 2 1 * *', 'SELECT eam.maintain_partitions();');
-- SELECT cron.schedule('eam-cleanup-old-data', '0 3 1 * *', 'SELECT eam.cleanup_old_data();');
-- SELECT cron.schedule('eam-refresh-materialized-views', '*/15 * * * *', 'SELECT eam.refresh_materialized_views_incremental();');
-- SELECT cron.schedule('eam-update-statistics', '0 1 * * *', 'ANALYZE;');

-- ==============================================================================
-- FINAL VALIDATION
-- ==============================================================================

-- Test database functionality
DO $$
DECLARE
    table_count integer;
    index_count integer;
    function_count integer;
    view_count integer;
BEGIN
    -- Count tables
    SELECT COUNT(*) INTO table_count 
    FROM information_schema.tables 
    WHERE table_schema = 'eam' AND table_type = 'BASE TABLE';
    
    -- Count indexes
    SELECT COUNT(*) INTO index_count 
    FROM pg_indexes 
    WHERE schemaname = 'eam';
    
    -- Count functions
    SELECT COUNT(*) INTO function_count 
    FROM information_schema.routines 
    WHERE routine_schema = 'eam';
    
    -- Count materialized views
    SELECT COUNT(*) INTO view_count 
    FROM pg_matviews 
    WHERE schemaname = 'eam';
    
    RAISE NOTICE '=== EAM Database Setup Complete ===';
    RAISE NOTICE 'Tables: %', table_count;
    RAISE NOTICE 'Indexes: %', index_count;
    RAISE NOTICE 'Functions: %', function_count;
    RAISE NOTICE 'Materialized Views: %', view_count;
    
    -- Verify essential tables exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'eam' AND table_name = 'users') THEN
        RAISE EXCEPTION 'Essential table "users" not found!';
    END IF;
    
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'eam' AND table_name = 'agents') THEN
        RAISE EXCEPTION 'Essential table "agents" not found!';
    END IF;
    
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'eam' AND table_name = 'activity_logs') THEN
        RAISE EXCEPTION 'Essential table "activity_logs" not found!';
    END IF;
    
    RAISE NOTICE 'Database validation successful!';
END $$;

-- Display database statistics
SELECT * FROM eam.get_database_stats();

-- ==============================================================================
-- COMPLETION MESSAGE
-- ==============================================================================

\echo ''
\echo '██████╗ ███╗   ███╗     ██╗   ██╗███████╗   ██████╗ '
\echo '██╔══██╗████╗ ████║     ██║   ██║██╔════╝   ██╔═══██╗'
\echo '██████╔╝██╔████╔██║     ██║   ██║███████╗   ██║   ██║'
\echo '██╔══██╗██║╚██╔╝██║     ╚██╗ ██╔╝╚════██║   ██║   ██║'
\echo '██████╔╝██║ ╚═╝ ██║      ╚████╔╝ ███████║██╗╚██████╔╝'
\echo '╚═════╝ ╚═╝     ╚═╝       ╚═══╝  ╚══════╝╚═╝ ╚═════╝ '
\echo ''
\echo 'Employee Activity Monitor v5.0 Database Setup Complete!'
\echo ''
\echo 'Database: eam'
\echo 'Schema: eam'
\echo 'User: eam_user'
\echo 'Read-only user: eam_readonly'
\echo ''
\echo 'Features configured:'
\echo '- PostgreSQL 16 with partitioned tables'
\echo '- BRIN and B-tree indexes for optimal performance'
\echo '- Materialized views for fast aggregations'
\echo '- Automatic partition management'
\echo '- Comprehensive backup and restore scripts'
\echo '- Security configurations'
\echo '- Monitoring and maintenance functions'
\echo ''
\echo 'Next steps:'
\echo '1. Configure application connection strings'
\echo '2. Start MinIO and Redis services'
\echo '3. Run initial data seed if needed'
\echo '4. Configure scheduled maintenance jobs'
\echo ''
\echo 'Happy monitoring! 🚀'
\echo ''