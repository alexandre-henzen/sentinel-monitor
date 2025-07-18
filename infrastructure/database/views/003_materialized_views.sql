-- Migration 003: Materialized Views
-- Employee Activity Monitor (EAM) v5.0
-- PostgreSQL 16 materialized views for fast aggregations

-- Materialized view for daily productivity aggregations
CREATE MATERIALIZED VIEW eam.mv_daily_productivity_summary AS
SELECT 
    a.id as agent_id,
    a.machine_name,
    a.user_name,
    DATE(al.event_timestamp) as activity_date,
    COUNT(*) as total_events,
    COUNT(DISTINCT al.application_name) as unique_applications,
    ROUND(AVG(al.productivity_score), 2) as avg_productivity_score,
    SUM(al.duration_seconds) as total_active_seconds,
    COUNT(*) FILTER (WHERE al.productivity_score >= 70) as high_productivity_events,
    COUNT(*) FILTER (WHERE al.productivity_score < 30) as low_productivity_events,
    STRING_AGG(DISTINCT al.application_name, ', ' ORDER BY al.application_name) as applications_used,
    MIN(al.event_timestamp) as first_activity,
    MAX(al.event_timestamp) as last_activity
FROM eam.agents a
INNER JOIN eam.activity_logs al ON a.id = al.agent_id
WHERE al.event_timestamp >= CURRENT_DATE - INTERVAL '90 days'
GROUP BY a.id, a.machine_name, a.user_name, DATE(al.event_timestamp);

-- Create unique index on materialized view
CREATE UNIQUE INDEX idx_mv_daily_productivity_summary_unique
ON eam.mv_daily_productivity_summary (agent_id, activity_date);

-- Create additional indexes for performance
CREATE INDEX idx_mv_daily_productivity_summary_date
ON eam.mv_daily_productivity_summary (activity_date);

CREATE INDEX idx_mv_daily_productivity_summary_productivity
ON eam.mv_daily_productivity_summary (avg_productivity_score);

-- Materialized view for application usage statistics
CREATE MATERIALIZED VIEW eam.mv_application_usage_stats AS
SELECT 
    al.application_name,
    COUNT(*) as total_events,
    COUNT(DISTINCT al.agent_id) as unique_agents,
    ROUND(AVG(al.productivity_score), 2) as avg_productivity_score,
    SUM(al.duration_seconds) as total_duration_seconds,
    ROUND(SUM(al.duration_seconds) / 3600.0, 2) as total_hours,
    COUNT(*) FILTER (WHERE al.event_timestamp >= CURRENT_DATE) as events_today,
    COUNT(*) FILTER (WHERE al.event_timestamp >= CURRENT_DATE - INTERVAL '7 days') as events_last_7_days,
    COUNT(*) FILTER (WHERE al.event_timestamp >= CURRENT_DATE - INTERVAL '30 days') as events_last_30_days,
    MIN(al.event_timestamp) as first_used,
    MAX(al.event_timestamp) as last_used
FROM eam.activity_logs al
WHERE al.application_name IS NOT NULL
  AND al.event_timestamp >= CURRENT_DATE - INTERVAL '90 days'
GROUP BY al.application_name;

-- Create indexes on application usage stats
CREATE INDEX idx_mv_application_usage_stats_app_name
ON eam.mv_application_usage_stats (application_name);

CREATE INDEX idx_mv_application_usage_stats_total_events
ON eam.mv_application_usage_stats (total_events DESC);

CREATE INDEX idx_mv_application_usage_stats_productivity
ON eam.mv_application_usage_stats (avg_productivity_score DESC);

-- Materialized view for agent performance metrics
CREATE MATERIALIZED VIEW eam.mv_agent_performance_metrics AS
SELECT 
    a.id as agent_id,
    a.machine_name,
    a.user_name,
    a.status,
    a.last_seen,
    COUNT(al.id) as total_events,
    COUNT(DISTINCT DATE(al.event_timestamp)) as active_days,
    ROUND(AVG(al.productivity_score), 2) as avg_productivity_score,
    SUM(al.duration_seconds) as total_active_seconds,
    ROUND(SUM(al.duration_seconds) / 3600.0, 2) as total_hours,
    COUNT(DISTINCT al.application_name) as unique_applications,
    COUNT(*) FILTER (WHERE al.event_timestamp >= CURRENT_DATE) as events_today,
    COUNT(*) FILTER (WHERE al.event_timestamp >= CURRENT_DATE - INTERVAL '7 days') as events_last_7_days,
    COUNT(*) FILTER (WHERE al.event_timestamp >= CURRENT_DATE - INTERVAL '30 days') as events_last_30_days,
    ROUND(
        COUNT(*) FILTER (WHERE al.event_timestamp >= CURRENT_DATE - INTERVAL '7 days') / 7.0, 2
    ) as avg_events_per_day_last_7_days,
    CASE 
        WHEN COUNT(DISTINCT DATE(al.event_timestamp)) > 0 THEN
            ROUND(COUNT(al.id)::numeric / COUNT(DISTINCT DATE(al.event_timestamp)), 2)
        ELSE 0
    END as avg_events_per_active_day,
    MIN(al.event_timestamp) as first_activity,
    MAX(al.event_timestamp) as last_activity
FROM eam.agents a
LEFT JOIN eam.activity_logs al ON a.id = al.agent_id
    AND al.event_timestamp >= CURRENT_DATE - INTERVAL '90 days'
GROUP BY a.id, a.machine_name, a.user_name, a.status, a.last_seen;

-- Create indexes on agent performance metrics
CREATE UNIQUE INDEX idx_mv_agent_performance_metrics_agent_id
ON eam.mv_agent_performance_metrics (agent_id);

CREATE INDEX idx_mv_agent_performance_metrics_productivity
ON eam.mv_agent_performance_metrics (avg_productivity_score DESC);

CREATE INDEX idx_mv_agent_performance_metrics_status
ON eam.mv_agent_performance_metrics (status);

-- Materialized view for hourly activity patterns
CREATE MATERIALIZED VIEW eam.mv_hourly_activity_patterns AS
SELECT 
    EXTRACT(HOUR FROM al.event_timestamp) as hour_of_day,
    EXTRACT(DOW FROM al.event_timestamp) as day_of_week,
    COUNT(*) as total_events,
    COUNT(DISTINCT al.agent_id) as unique_agents,
    ROUND(AVG(al.productivity_score), 2) as avg_productivity_score,
    SUM(al.duration_seconds) as total_duration_seconds,
    COUNT(*) FILTER (WHERE al.productivity_score >= 70) as high_productivity_events,
    COUNT(*) FILTER (WHERE al.productivity_score < 30) as low_productivity_events,
    COUNT(DISTINCT al.application_name) as unique_applications
FROM eam.activity_logs al
WHERE al.event_timestamp >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY EXTRACT(HOUR FROM al.event_timestamp), EXTRACT(DOW FROM al.event_timestamp);

-- Create indexes on hourly activity patterns
CREATE INDEX idx_mv_hourly_activity_patterns_hour_dow
ON eam.mv_hourly_activity_patterns (hour_of_day, day_of_week);

CREATE INDEX idx_mv_hourly_activity_patterns_events
ON eam.mv_hourly_activity_patterns (total_events DESC);

-- Materialized view for productivity trends
CREATE MATERIALIZED VIEW eam.mv_productivity_trends AS
SELECT 
    DATE(al.event_timestamp) as activity_date,
    COUNT(*) as total_events,
    COUNT(DISTINCT al.agent_id) as unique_agents,
    ROUND(AVG(al.productivity_score), 2) as avg_productivity_score,
    ROUND(STDDEV(al.productivity_score), 2) as productivity_stddev,
    COUNT(*) FILTER (WHERE al.productivity_score >= 70) as high_productivity_events,
    COUNT(*) FILTER (WHERE al.productivity_score BETWEEN 30 AND 69) as medium_productivity_events,
    COUNT(*) FILTER (WHERE al.productivity_score < 30) as low_productivity_events,
    SUM(al.duration_seconds) as total_active_seconds,
    COUNT(DISTINCT al.application_name) as unique_applications,
    -- Calculate 7-day moving average
    ROUND(AVG(AVG(al.productivity_score)) OVER (
        ORDER BY DATE(al.event_timestamp) 
        ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
    ), 2) as productivity_7day_avg
FROM eam.activity_logs al
WHERE al.event_timestamp >= CURRENT_DATE - INTERVAL '90 days'
  AND al.productivity_score IS NOT NULL
GROUP BY DATE(al.event_timestamp);

-- Create indexes on productivity trends
CREATE UNIQUE INDEX idx_mv_productivity_trends_date
ON eam.mv_productivity_trends (activity_date);

CREATE INDEX idx_mv_productivity_trends_productivity
ON eam.mv_productivity_trends (avg_productivity_score DESC);

-- Function to refresh all materialized views
CREATE OR REPLACE FUNCTION eam.refresh_all_materialized_views()
RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY eam.mv_daily_productivity_summary;
    REFRESH MATERIALIZED VIEW eam.mv_application_usage_stats;
    REFRESH MATERIALIZED VIEW eam.mv_agent_performance_metrics;
    REFRESH MATERIALIZED VIEW eam.mv_hourly_activity_patterns;
    REFRESH MATERIALIZED VIEW eam.mv_productivity_trends;
    
    -- Update statistics
    ANALYZE eam.mv_daily_productivity_summary;
    ANALYZE eam.mv_application_usage_stats;
    ANALYZE eam.mv_agent_performance_metrics;
    ANALYZE eam.mv_hourly_activity_patterns;
    ANALYZE eam.mv_productivity_trends;
END;
$$ LANGUAGE plpgsql;

-- Function to refresh materialized views incrementally
CREATE OR REPLACE FUNCTION eam.refresh_materialized_views_incremental()
RETURNS void AS $$
BEGIN
    -- Refresh only views that might have changed based on recent data
    IF EXISTS (
        SELECT 1 FROM eam.activity_logs 
        WHERE created_at >= NOW() - INTERVAL '1 hour'
    ) THEN
        REFRESH MATERIALIZED VIEW CONCURRENTLY eam.mv_daily_productivity_summary;
        REFRESH MATERIALIZED VIEW CONCURRENTLY eam.mv_application_usage_stats;
        REFRESH MATERIALIZED VIEW CONCURRENTLY eam.mv_agent_performance_metrics;
        REFRESH MATERIALIZED VIEW CONCURRENTLY eam.mv_productivity_trends;
    END IF;
    
    -- Always refresh hourly patterns (smaller dataset)
    REFRESH MATERIALIZED VIEW eam.mv_hourly_activity_patterns;
END;
$$ LANGUAGE plpgsql;

-- Create a scheduled job to refresh materialized views (requires pg_cron extension)
-- This will be commented out since pg_cron may not be available in all environments
-- SELECT cron.schedule('refresh-materialized-views', '*/15 * * * *', 'SELECT eam.refresh_materialized_views_incremental();');

-- Initial refresh of all materialized views
SELECT eam.refresh_all_materialized_views();

-- Grant permissions
GRANT SELECT ON eam.mv_daily_productivity_summary TO eam_user;
GRANT SELECT ON eam.mv_application_usage_stats TO eam_user;
GRANT SELECT ON eam.mv_agent_performance_metrics TO eam_user;
GRANT SELECT ON eam.mv_hourly_activity_patterns TO eam_user;
GRANT SELECT ON eam.mv_productivity_trends TO eam_user;

COMMIT;