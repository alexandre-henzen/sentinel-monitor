using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace SamplePlugins
{
    /// <summary>
    /// Plugin de exemplo para rastreamento de performance
    /// Usado para testes de integração do sistema de plugins
    /// </summary>
    public class SamplePerformanceTracker : ITrackerPlugin
    {
        public string Name => "Sample Performance Tracker";
        public string Version => "1.0.0";
        public string Description => "Plugin de exemplo para rastreamento de performance para testes de integração";
        public Dictionary<string, object> Configuration { get; set; } = new();

        private bool _isInitialized = false;
        private readonly Random _random = new Random();

        public async Task<bool> InitializeAsync()
        {
            try
            {
                await Task.Delay(100);
                _isInitialized = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<List<ActivityEvent>> GetEventsAsync()
        {
            if (!_isInitialized)
            {
                return new List<ActivityEvent>();
            }

            var events = new List<ActivityEvent>();
            
            // Simular eventos de performance
            var performanceEvent = new ActivityEvent
            {
                Id = Guid.NewGuid(),
                ActivityType = ActivityType.CustomActivity,
                ProcessName = "system",
                WindowTitle = "Performance Monitor",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["pluginName"] = Name,
                    ["pluginVersion"] = Version,
                    ["eventType"] = "performance",
                    ["cpuUsage"] = _random.NextDouble() * 100,
                    ["memoryUsage"] = _random.Next(1000, 8000),
                    ["diskUsage"] = _random.NextDouble() * 100,
                    ["networkUsage"] = _random.Next(0, 1000),
                    ["activeProcesses"] = _random.Next(50, 200),
                    ["systemUptime"] = TimeSpan.FromHours(_random.Next(1, 168)).TotalSeconds,
                    ["testPlugin"] = true
                }
            };

            events.Add(performanceEvent);
            
            await Task.Delay(10);
            return events;
        }

        public async Task<bool> CleanupAsync()
        {
            try
            {
                _isInitialized = false;
                await Task.Delay(50);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}