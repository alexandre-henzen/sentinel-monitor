using EAM.PluginSDK;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace EAM.Agent.Data;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly string _databasePath;

    public DatabaseService(ILogger<DatabaseService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        _connectionString = _configuration.GetConnectionString("Default") ?? 
                           Environment.ExpandEnvironmentVariables("Data Source=%LOCALAPPDATA%\\EAM\\agent.db");
        
        _databasePath = ExtractDatabasePath(_connectionString);
        
        // Criar diretório se não existir
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Inicializando database SQLite em: {Path}", _databasePath);
            
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            await CreateTablesAsync(connection);
            await CreateIndexesAsync(connection);
            
            _logger.LogInformation("Database SQLite inicializado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inicializando database SQLite");
            throw;
        }
    }

    public async Task SaveEventsAsync(IEnumerable<ActivityEvent> events)
    {
        if (!events.Any()) return;

        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            await using var transaction = await connection.BeginTransactionAsync();
            
            const string sql = @"
                INSERT INTO activity_events (
                    event_type, application_name, window_title, url, process_name, 
                    process_id, duration_seconds, productivity_score, timestamp, 
                    screenshot_path, metadata, created_at, synced
                ) VALUES (
                    @EventType, @ApplicationName, @WindowTitle, @Url, @ProcessName,
                    @ProcessId, @DurationSeconds, @ProductivityScore, @Timestamp,
                    @ScreenshotPath, @Metadata, @CreatedAt, 0
                )";
            
            foreach (var eventItem in events)
            {
                await using var command = new SqliteCommand(sql, connection, transaction);
                
                command.Parameters.AddWithValue("@EventType", eventItem.EventType);
                command.Parameters.AddWithValue("@ApplicationName", eventItem.ApplicationName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@WindowTitle", eventItem.WindowTitle ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Url", eventItem.Url ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ProcessName", eventItem.ProcessName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ProcessId", eventItem.ProcessId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@DurationSeconds", eventItem.DurationSeconds ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ProductivityScore", eventItem.ProductivityScore ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Timestamp", eventItem.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                command.Parameters.AddWithValue("@ScreenshotPath", eventItem.ScreenshotPath ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Metadata", eventItem.Metadata != null ? JsonSerializer.Serialize(eventItem.Metadata) : (object)DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                
                await command.ExecuteNonQueryAsync();
            }
            
            await transaction.CommitAsync();
            _logger.LogDebug("Salvos {Count} eventos no database local", events.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro salvando eventos no database");
            throw;
        }
    }

    public async Task<List<ActivityEvent>> GetUnsyncedEventsAsync(int limit = 1000)
    {
        var events = new List<ActivityEvent>();
        
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            const string sql = @"
                SELECT id, event_type, application_name, window_title, url, process_name,
                       process_id, duration_seconds, productivity_score, timestamp,
                       screenshot_path, metadata, created_at
                FROM activity_events 
                WHERE synced = 0 
                ORDER BY created_at ASC 
                LIMIT @Limit";
            
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@Limit", limit);
            
            await using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var eventItem = new ActivityEvent
                {
                    EventType = reader.GetString("event_type"),
                    ApplicationName = reader.IsDBNull("application_name") ? null : reader.GetString("application_name"),
                    WindowTitle = reader.IsDBNull("window_title") ? null : reader.GetString("window_title"),
                    Url = reader.IsDBNull("url") ? null : reader.GetString("url"),
                    ProcessName = reader.IsDBNull("process_name") ? null : reader.GetString("process_name"),
                    ProcessId = reader.IsDBNull("process_id") ? null : reader.GetInt32("process_id"),
                    DurationSeconds = reader.IsDBNull("duration_seconds") ? null : reader.GetInt32("duration_seconds"),
                    ProductivityScore = reader.IsDBNull("productivity_score") ? null : reader.GetInt32("productivity_score"),
                    Timestamp = DateTime.Parse(reader.GetString("timestamp")),
                    ScreenshotPath = reader.IsDBNull("screenshot_path") ? null : reader.GetString("screenshot_path")
                };
                
                // Deserializar metadata
                if (!reader.IsDBNull("metadata"))
                {
                    var metadataJson = reader.GetString("metadata");
                    eventItem.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                }
                
                // Adicionar ID para marcação como sincronizado
                eventItem.AddMetadata("local_id", reader.GetInt64("id"));
                
                events.Add(eventItem);
            }
            
            _logger.LogDebug("Recuperados {Count} eventos não sincronizados", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro recuperando eventos não sincronizados");
            throw;
        }
        
        return events;
    }

    public async Task MarkEventsAsSyncedAsync(IEnumerable<long> eventIds)
    {
        if (!eventIds.Any()) return;
        
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var ids = string.Join(",", eventIds);
            var sql = $"UPDATE activity_events SET synced = 1, synced_at = @SyncedAt WHERE id IN ({ids})";
            
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@SyncedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            
            var updatedCount = await command.ExecuteNonQueryAsync();
            _logger.LogDebug("Marcados {Count} eventos como sincronizados", updatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro marcando eventos como sincronizados");
            throw;
        }
    }

    public async Task CleanupOldEventsAsync()
    {
        try
        {
            var retentionDays = _configuration.GetValue<int>("Agent:OfflineRetentionDays", 7);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            const string sql = "DELETE FROM activity_events WHERE synced = 1 AND created_at < @CutoffDate";
            
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@CutoffDate", cutoffDate.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            
            var deletedCount = await command.ExecuteNonQueryAsync();
            if (deletedCount > 0)
            {
                _logger.LogInformation("Removidos {Count} eventos antigos do cache local", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro limpando eventos antigos");
        }
    }

    private async Task CreateTablesAsync(SqliteConnection connection)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS activity_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                event_type TEXT NOT NULL,
                application_name TEXT,
                window_title TEXT,
                url TEXT,
                process_name TEXT,
                process_id INTEGER,
                duration_seconds INTEGER,
                productivity_score INTEGER,
                timestamp TEXT NOT NULL,
                screenshot_path TEXT,
                metadata TEXT,
                created_at TEXT NOT NULL,
                synced INTEGER DEFAULT 0,
                synced_at TEXT
            )";
        
        await using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateIndexesAsync(SqliteConnection connection)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_activity_events_synced ON activity_events(synced)",
            "CREATE INDEX IF NOT EXISTS idx_activity_events_created_at ON activity_events(created_at)",
            "CREATE INDEX IF NOT EXISTS idx_activity_events_timestamp ON activity_events(timestamp)",
            "CREATE INDEX IF NOT EXISTS idx_activity_events_event_type ON activity_events(event_type)"
        };
        
        foreach (var indexSql in indexes)
        {
            await using var command = new SqliteCommand(indexSql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string ExtractDatabasePath(string connectionString)
    {
        var parts = connectionString.Split(';');
        var dataSourcePart = parts.FirstOrDefault(p => p.Trim().StartsWith("Data Source", StringComparison.OrdinalIgnoreCase));
        
        if (dataSourcePart != null)
        {
            var path = dataSourcePart.Split('=')[1].Trim();
            return Environment.ExpandEnvironmentVariables(path);
        }
        
        return Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\EAM\agent.db");
    }
}