-- Migration 002: Optimized Indexes
-- Employee Activity Monitor (EAM) v5.0
-- PostgreSQL 16 BRIN and B-tree indexes for high performance

-- BRIN indexes for timestamp columns (excellent for time-series data)
-- These indexes are very small and efficient for range queries on ordered data

-- BRIN index on activity_logs event_timestamp (partitioned table)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_event_timestamp_brin
ON eam.activity_logs USING BRIN (event_timestamp);

-- BRIN index on activity_logs created_at
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_created_at_brin
ON eam.activity_logs USING BRIN (created_at);

-- BRIN index on agents last_seen for monitoring queries
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_agents_last_seen_brin
ON eam.agents USING BRIN (last_seen);

-- BRIN index on daily_scores activity_date
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_daily_scores_activity_date_brin
ON eam.daily_scores USING BRIN (activity_date);

-- B-tree indexes for primary keys and foreign keys (high selectivity)

-- B-tree index on activity_logs agent_id (foreign key)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_agent_id_btree
ON eam.activity_logs (agent_id);

-- B-tree index on activity_logs event_type (frequently queried)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_event_type_btree
ON eam.activity_logs (event_type);

-- B-tree index on agents machine_id (unique constraint)
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS idx_agents_machine_id_unique
ON eam.agents (machine_id);

-- B-tree index on agents status (frequently filtered)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_agents_status_btree
ON eam.agents (status);

-- B-tree index on users username (unique constraint)
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS idx_users_username_unique
ON eam.users (username);

-- B-tree index on users email (unique constraint)
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS idx_users_email_unique
ON eam.users (email);

-- B-tree index on daily_scores agent_id (foreign key)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_daily_scores_agent_id_btree
ON eam.daily_scores (agent_id);

-- Composite indexes for common query patterns

-- Composite index for activity_logs queries by agent and time range
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_agent_time_composite
ON eam.activity_logs (agent_id, event_timestamp);

-- Composite index for productivity scoring queries
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_productivity_composite
ON eam.activity_logs (agent_id, event_timestamp, productivity_score)
WHERE productivity_score IS NOT NULL;

-- Composite index for application tracking
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_app_composite
ON eam.activity_logs (agent_id, application_name, event_timestamp)
WHERE application_name IS NOT NULL;

-- Composite index for daily_scores lookup
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS idx_daily_scores_agent_date_unique
ON eam.daily_scores (agent_id, activity_date);

-- GIN indexes for JSONB metadata searching
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_metadata_gin
ON eam.activity_logs USING GIN (metadata);

-- Partial indexes for performance optimization

-- Partial index for active agents only
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_agents_active_partial
ON eam.agents (last_seen, machine_name)
WHERE status = 'Active';

-- Partial index for recent activity (last 30 days)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_recent_partial
ON eam.activity_logs (agent_id, event_type, application_name)
WHERE event_timestamp >= NOW() - INTERVAL '30 days';

-- Partial index for high productivity events
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_activity_logs_high_productivity_partial
ON eam.activity_logs (agent_id, event_timestamp, application_name)
WHERE productivity_score >= 70;

-- Create statistics for better query planning
CREATE STATISTICS IF NOT EXISTS eam.activity_logs_multicolumn_stats
ON agent_id, event_type, application_name, event_timestamp
FROM eam.activity_logs;

CREATE STATISTICS IF NOT EXISTS eam.daily_scores_multicolumn_stats
ON agent_id, activity_date, avg_productivity
FROM eam.daily_scores;

-- Update table statistics
ANALYZE eam.users;
ANALYZE eam.agents;
ANALYZE eam.activity_logs;
ANALYZE eam.daily_scores;

-- Set index-specific storage parameters for performance
ALTER INDEX eam.idx_activity_logs_event_timestamp_brin SET (pages_per_range = 32);
ALTER INDEX eam.idx_activity_logs_created_at_brin SET (pages_per_range = 32);
ALTER INDEX eam.idx_agents_last_seen_brin SET (pages_per_range = 16);
ALTER INDEX eam.idx_daily_scores_activity_date_brin SET (pages_per_range = 16);

-- Set autovacuum parameters for high-write tables
ALTER TABLE eam.activity_logs SET (
    autovacuum_vacuum_scale_factor = 0.1,
    autovacuum_analyze_scale_factor = 0.05,
    autovacuum_vacuum_cost_delay = 10,
    autovacuum_vacuum_cost_limit = 1000
);

ALTER TABLE eam.daily_scores SET (
    autovacuum_vacuum_scale_factor = 0.2,
    autovacuum_analyze_scale_factor = 0.1
);

-- Create function to maintain partition-wise indexes
CREATE OR REPLACE FUNCTION eam.create_partition_indexes(partition_name text)
RETURNS void AS $$
BEGIN
    -- Create BRIN indexes on the new partition
    EXECUTE format('CREATE INDEX CONCURRENTLY IF NOT EXISTS %I_event_timestamp_brin 
                    ON eam.%I USING BRIN (event_timestamp)', 
                    partition_name, partition_name);
    
    EXECUTE format('CREATE INDEX CONCURRENTLY IF NOT EXISTS %I_agent_id_btree 
                    ON eam.%I (agent_id)', 
                    partition_name, partition_name);
    
    EXECUTE format('CREATE INDEX CONCURRENTLY IF NOT EXISTS %I_event_type_btree 
                    ON eam.%I (event_type)', 
                    partition_name, partition_name);
    
    -- Create composite indexes on the new partition
    EXECUTE format('CREATE INDEX CONCURRENTLY IF NOT EXISTS %I_agent_time_composite 
                    ON eam.%I (agent_id, event_timestamp)', 
                    partition_name, partition_name);
END;
$$ LANGUAGE plpgsql;

-- Update the partition creation function to include indexes
CREATE OR REPLACE FUNCTION eam.create_monthly_partition(table_name text, start_date date)
RETURNS void AS $$
DECLARE
    partition_name text;
    start_ts timestamp;
    end_ts timestamp;
BEGIN
    partition_name := table_name || '_' || to_char(start_date, 'YYYY_MM');
    start_ts := start_date;
    end_ts := start_date + interval '1 month';
    
    EXECUTE format('CREATE TABLE IF NOT EXISTS eam.%I PARTITION OF eam.%I 
                    FOR VALUES FROM (%L) TO (%L)', 
                    partition_name, table_name, start_ts, end_ts);
    
    -- Create indexes on the new partition
    PERFORM eam.create_partition_indexes(partition_name);
END;
$$ LANGUAGE plpgsql;

COMMIT;