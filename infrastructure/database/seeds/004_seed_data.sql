-- Migration 004: Seed Data
-- Employee Activity Monitor (EAM) v5.0
-- Initial data for development and testing

-- Insert default admin user
INSERT INTO eam.users (id, username, email, password_hash, role, is_active, created_at, updated_at) 
VALUES (
    uuid_generate_v4(),
    'admin',
    'admin@eam.local',
    '$2a$11$qKvZVIzLZQqKLF8MIGAx3u.fZPfqcgQnKQY/qCzVpQCJlKNtZxaLi', -- BCrypt hash for 'admin123'
    'Admin',
    true,
    NOW(),
    NOW()
) ON CONFLICT (username) DO NOTHING;

-- Insert default regular user
INSERT INTO eam.users (id, username, email, password_hash, role, is_active, created_at, updated_at) 
VALUES (
    uuid_generate_v4(),
    'user',
    'user@eam.local',
    '$2a$11$qKvZVIzLZQqKLF8MIGAx3u.fZPfqcgQnKQY/qCzVpQCJlKNtZxaLi', -- BCrypt hash for 'user123'
    'User',
    true,
    NOW(),
    NOW()
) ON CONFLICT (username) DO NOTHING;

-- Insert demo manager user
INSERT INTO eam.users (id, username, email, password_hash, role, is_active, created_at, updated_at) 
VALUES (
    uuid_generate_v4(),
    'manager',
    'manager@eam.local',
    '$2a$11$qKvZVIzLZQqKLF8MIGAx3u.fZPfqcgQnKQY/qCzVpQCJlKNtZxaLi', -- BCrypt hash for 'manager123'
    'Manager',
    true,
    NOW(),
    NOW()
) ON CONFLICT (username) DO NOTHING;

-- Insert demo agents for development
INSERT INTO eam.agents (id, machine_id, machine_name, user_name, os_version, agent_version, last_seen, status, created_at, updated_at) 
VALUES 
(
    uuid_generate_v4(),
    'WKS-001-DEV',
    'DESKTOP-DEV001',
    'john.doe',
    'Windows 11 Pro 22H2',
    '5.0.0',
    NOW() - INTERVAL '5 minutes',
    'Active',
    NOW() - INTERVAL '7 days',
    NOW()
),
(
    uuid_generate_v4(),
    'WKS-002-DEV',
    'DESKTOP-DEV002',
    'jane.smith',
    'Windows 11 Pro 22H2',
    '5.0.0',
    NOW() - INTERVAL '2 hours',
    'Active',
    NOW() - INTERVAL '5 days',
    NOW()
),
(
    uuid_generate_v4(),
    'WKS-003-DEV',
    'DESKTOP-DEV003',
    'bob.wilson',
    'Windows 10 Pro 21H2',
    '4.9.2',
    NOW() - INTERVAL '1 day',
    'Offline',
    NOW() - INTERVAL '10 days',
    NOW()
),
(
    uuid_generate_v4(),
    'LAP-001-DEV',
    'LAPTOP-DEV001',
    'alice.johnson',
    'Windows 11 Pro 22H2',
    '5.0.0',
    NOW() - INTERVAL '10 minutes',
    'Active',
    NOW() - INTERVAL '3 days',
    NOW()
),
(
    uuid_generate_v4(),
    'LAP-002-DEV',
    'LAPTOP-DEV002',
    'charlie.brown',
    'Windows 11 Pro 22H2',
    '5.0.0',
    NOW() - INTERVAL '4 hours',
    'Idle',
    NOW() - INTERVAL '1 day',
    NOW()
)
ON CONFLICT (machine_id) DO NOTHING;

-- Insert sample activity logs for the last 30 days
DO $$
DECLARE
    agent_record RECORD;
    current_date_iter DATE;
    hour_iter INTEGER;
    event_count INTEGER;
    productivity_score INTEGER;
    applications TEXT[] := ARRAY[
        'Microsoft Word', 'Microsoft Excel', 'Microsoft PowerPoint', 'Microsoft Outlook',
        'Google Chrome', 'Mozilla Firefox', 'Visual Studio Code', 'Visual Studio',
        'Slack', 'Microsoft Teams', 'Zoom', 'Discord',
        'Notepad++', 'Adobe Photoshop', 'Adobe Illustrator', 'Figma',
        'IntelliJ IDEA', 'PyCharm', 'Sublime Text', 'Atom',
        'Calculator', 'Windows Explorer', 'Task Manager', 'Control Panel'
    ];
    event_types TEXT[] := ARRAY[
        'WindowActivated', 'ApplicationLaunched', 'ApplicationClosed', 'KeyboardActivity',
        'MouseActivity', 'ScreenshotTaken', 'FileAccessed', 'UrlVisited'
    ];
    urls TEXT[] := ARRAY[
        'https://www.google.com', 'https://www.microsoft.com', 'https://github.com',
        'https://stackoverflow.com', 'https://docs.microsoft.com', 'https://www.youtube.com',
        'https://www.linkedin.com', 'https://portal.azure.com', 'https://office.com',
        'https://teams.microsoft.com', 'https://outlook.office.com', 'https://www.w3schools.com'
    ];
    window_titles TEXT[] := ARRAY[
        'Document1 - Microsoft Word', 'Workbook1 - Microsoft Excel', 'Presentation1 - Microsoft PowerPoint',
        'Inbox - Microsoft Outlook', 'New Tab - Google Chrome', 'Visual Studio Code - main.cs',
        'Slack - EAM Team', 'Microsoft Teams - General', 'Zoom Meeting', 'Task Manager',
        'Windows Explorer - Downloads', 'Calculator', 'Control Panel - System',
        'GitHub - EAM Repository', 'Stack Overflow - C# Questions'
    ];
BEGIN
    -- Generate activity logs for each agent for the last 30 days
    FOR agent_record IN SELECT id, machine_name, user_name FROM eam.agents LOOP
        current_date_iter := CURRENT_DATE - INTERVAL '30 days';
        
        WHILE current_date_iter <= CURRENT_DATE LOOP
            -- Skip weekends for some agents (simulate work patterns)
            IF EXTRACT(DOW FROM current_date_iter) NOT IN (0, 6) OR 
               agent_record.machine_name LIKE '%LAP%' THEN
                
                -- Generate 8-12 hours of activity per day
                FOR hour_iter IN 8..19 LOOP
                    -- More activity during work hours
                    IF hour_iter BETWEEN 9 AND 17 THEN
                        event_count := 3 + (RANDOM() * 8)::INTEGER;
                    ELSE
                        event_count := 0 + (RANDOM() * 3)::INTEGER;
                    END IF;
                    
                    FOR i IN 1..event_count LOOP
                        -- Calculate productivity score based on time and application
                        CASE 
                            WHEN hour_iter BETWEEN 9 AND 12 OR hour_iter BETWEEN 14 AND 17 THEN
                                productivity_score := 60 + (RANDOM() * 30)::INTEGER;
                            WHEN hour_iter BETWEEN 13 AND 14 THEN
                                productivity_score := 30 + (RANDOM() * 40)::INTEGER;
                            ELSE
                                productivity_score := 20 + (RANDOM() * 50)::INTEGER;
                        END CASE;
                        
                        INSERT INTO eam.activity_logs (
                            id, agent_id, event_type, application_name, window_title, url,
                            process_name, process_id, duration_seconds, productivity_score,
                            event_timestamp, metadata, created_at
                        ) VALUES (
                            uuid_generate_v4(),
                            agent_record.id,
                            event_types[1 + (RANDOM() * array_length(event_types, 1))::INTEGER],
                            applications[1 + (RANDOM() * array_length(applications, 1))::INTEGER],
                            window_titles[1 + (RANDOM() * array_length(window_titles, 1))::INTEGER],
                            CASE 
                                WHEN RANDOM() < 0.3 THEN urls[1 + (RANDOM() * array_length(urls, 1))::INTEGER]
                                ELSE NULL
                            END,
                            CASE 
                                WHEN RANDOM() < 0.7 THEN 'chrome.exe'
                                WHEN RANDOM() < 0.8 THEN 'winword.exe'
                                WHEN RANDOM() < 0.9 THEN 'excel.exe'
                                ELSE 'devenv.exe'
                            END,
                            1000 + (RANDOM() * 9000)::INTEGER,
                            30 + (RANDOM() * 600)::INTEGER, -- 30 seconds to 10 minutes
                            productivity_score,
                            current_date_iter + (hour_iter || ' hours')::INTERVAL + (RANDOM() * 3600 || ' seconds')::INTERVAL,
                            jsonb_build_object(
                                'version', '5.0.0',
                                'session_id', uuid_generate_v4(),
                                'screen_resolution', '1920x1080',
                                'cpu_usage', (RANDOM() * 100)::INTEGER,
                                'memory_usage', (RANDOM() * 100)::INTEGER
                            ),
                            current_date_iter + (hour_iter || ' hours')::INTERVAL + (RANDOM() * 3600 || ' seconds')::INTERVAL
                        );
                    END LOOP;
                END LOOP;
            END IF;
            
            current_date_iter := current_date_iter + INTERVAL '1 day';
        END LOOP;
    END LOOP;
END $$;

-- Calculate and insert daily scores based on the generated activity logs
INSERT INTO eam.daily_scores (id, agent_id, activity_date, avg_productivity, total_active_seconds, total_events, unique_applications, created_at, updated_at)
SELECT 
    uuid_generate_v4(),
    agent_id,
    DATE(event_timestamp),
    ROUND(AVG(productivity_score), 2),
    SUM(duration_seconds),
    COUNT(*),
    COUNT(DISTINCT application_name),
    NOW(),
    NOW()
FROM eam.activity_logs
WHERE event_timestamp >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY agent_id, DATE(event_timestamp)
ON CONFLICT (agent_id, activity_date) DO UPDATE SET
    avg_productivity = EXCLUDED.avg_productivity,
    total_active_seconds = EXCLUDED.total_active_seconds,
    total_events = EXCLUDED.total_events,
    unique_applications = EXCLUDED.unique_applications,
    updated_at = NOW();

-- Update agents last_seen based on their latest activity
UPDATE eam.agents SET last_seen = subquery.last_activity
FROM (
    SELECT agent_id, MAX(event_timestamp) as last_activity
    FROM eam.activity_logs
    GROUP BY agent_id
) AS subquery
WHERE eam.agents.id = subquery.agent_id;

-- Update agents status based on last_seen
UPDATE eam.agents SET status = 
    CASE 
        WHEN last_seen >= NOW() - INTERVAL '5 minutes' THEN 'Active'
        WHEN last_seen >= NOW() - INTERVAL '30 minutes' THEN 'Idle'
        ELSE 'Offline'
    END;

-- Create sample configuration data in metadata
INSERT INTO eam.activity_logs (
    id, agent_id, event_type, application_name, window_title,
    event_timestamp, metadata, created_at
)
SELECT 
    uuid_generate_v4(),
    id,
    'SystemInfo',
    'EAM Agent',
    'System Information',
    NOW(),
    jsonb_build_object(
        'os_version', os_version,
        'agent_version', agent_version,
        'timezone', 'UTC-03:00',
        'language', 'pt-BR',
        'architecture', 'x64',
        'processors', 8,
        'memory_gb', 16,
        'disk_gb', 512,
        'network_interfaces', jsonb_build_array(
            jsonb_build_object('name', 'Ethernet', 'ip', '192.168.1.100'),
            jsonb_build_object('name', 'WiFi', 'ip', '192.168.1.101')
        )
    ),
    NOW()
FROM eam.agents;

-- Refresh materialized views with new data
SELECT eam.refresh_all_materialized_views();

-- Update table statistics
ANALYZE eam.users;
ANALYZE eam.agents;
ANALYZE eam.activity_logs;
ANALYZE eam.daily_scores;

-- Log the completion
INSERT INTO eam.activity_logs (
    id, agent_id, event_type, application_name, window_title,
    event_timestamp, metadata, created_at
)
SELECT 
    uuid_generate_v4(),
    id,
    'SystemEvent',
    'EAM Database',
    'Seed Data Completed',
    NOW(),
    jsonb_build_object(
        'event', 'seed_data_completed',
        'total_agents', (SELECT COUNT(*) FROM eam.agents),
        'total_users', (SELECT COUNT(*) FROM eam.users),
        'total_activity_logs', (SELECT COUNT(*) FROM eam.activity_logs),
        'total_daily_scores', (SELECT COUNT(*) FROM eam.daily_scores),
        'timestamp', NOW()
    ),
    NOW()
FROM eam.agents
LIMIT 1;

COMMIT;