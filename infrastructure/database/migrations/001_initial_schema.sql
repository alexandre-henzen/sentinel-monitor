-- Migration 001: Initial Schema
-- Employee Activity Monitor (EAM) v5.0
-- PostgreSQL 16 with partitioned tables and optimized indexes

-- Create schema
CREATE SCHEMA IF NOT EXISTS eam;

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "btree_gin";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";

-- Create Users table
CREATE TABLE eam.users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username VARCHAR(255) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'User',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Create Agents table
CREATE TABLE eam.agents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    machine_id VARCHAR(255) NOT NULL UNIQUE,
    machine_name VARCHAR(255) NOT NULL,
    user_name VARCHAR(255) NOT NULL,
    os_version VARCHAR(255),
    agent_version VARCHAR(50),
    last_seen TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Create partitioned ActivityLogs table (partitioned by month)
CREATE TABLE eam.activity_logs (
    id UUID DEFAULT uuid_generate_v4(),
    agent_id UUID NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    application_name VARCHAR(255),
    window_title VARCHAR(500),
    url VARCHAR(2000),
    process_name VARCHAR(255),
    process_id INTEGER,
    duration_seconds INTEGER,
    productivity_score INTEGER,
    event_timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    screenshot_path VARCHAR(255),
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, event_timestamp),
    FOREIGN KEY (agent_id) REFERENCES eam.agents(id) ON DELETE CASCADE
) PARTITION BY RANGE (event_timestamp);

-- Create DailyScores table
CREATE TABLE eam.daily_scores (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    agent_id UUID NOT NULL,
    activity_date DATE NOT NULL,
    avg_productivity DECIMAL(5,2) NOT NULL DEFAULT 0,
    total_active_seconds INTEGER NOT NULL DEFAULT 0,
    total_events INTEGER NOT NULL DEFAULT 0,
    unique_applications INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    FOREIGN KEY (agent_id) REFERENCES eam.agents(id) ON DELETE CASCADE,
    UNIQUE(agent_id, activity_date)
);

-- Create function to auto-create partitions
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
END;
$$ LANGUAGE plpgsql;

-- Create initial partitions for activity_logs (current month and next 12 months)
DO $$
DECLARE
    current_month date;
    i integer;
BEGIN
    current_month := date_trunc('month', CURRENT_DATE);
    
    FOR i IN 0..12 LOOP
        PERFORM eam.create_monthly_partition('activity_logs', current_month + (i || ' months')::interval);
    END LOOP;
END $$;

-- Create function to automatically create partitions
CREATE OR REPLACE FUNCTION eam.create_partition_if_not_exists()
RETURNS trigger AS $$
DECLARE
    partition_date date;
BEGIN
    partition_date := date_trunc('month', NEW.event_timestamp);
    
    BEGIN
        PERFORM eam.create_monthly_partition('activity_logs', partition_date);
    EXCEPTION
        WHEN duplicate_table THEN
            -- Partition already exists, continue
            NULL;
    END;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to auto-create partitions
CREATE OR REPLACE TRIGGER activity_logs_partition_trigger
    BEFORE INSERT ON eam.activity_logs
    FOR EACH ROW
    EXECUTE FUNCTION eam.create_partition_if_not_exists();

-- Create function to update updated_at timestamp
CREATE OR REPLACE FUNCTION eam.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create triggers for updated_at
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON eam.users
    FOR EACH ROW
    EXECUTE FUNCTION eam.update_updated_at_column();

CREATE TRIGGER update_agents_updated_at
    BEFORE UPDATE ON eam.agents
    FOR EACH ROW
    EXECUTE FUNCTION eam.update_updated_at_column();

CREATE TRIGGER update_daily_scores_updated_at
    BEFORE UPDATE ON eam.daily_scores
    FOR EACH ROW
    EXECUTE FUNCTION eam.update_updated_at_column();

-- Set table ownership and permissions
ALTER TABLE eam.users OWNER TO eam_user;
ALTER TABLE eam.agents OWNER TO eam_user;
ALTER TABLE eam.activity_logs OWNER TO eam_user;
ALTER TABLE eam.daily_scores OWNER TO eam_user;

-- Grant permissions
GRANT USAGE ON SCHEMA eam TO eam_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA eam TO eam_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA eam TO eam_user;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA eam TO eam_user;

-- Create publication for logical replication (for backup/replication)
CREATE PUBLICATION eam_publication FOR ALL TABLES IN SCHEMA eam;

COMMIT;