using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace SamplePlugins
{
    /// <summary>
    /// Plugin de exemplo para rastreamento de janelas
    /// Usado para testes de integração do sistema de plugins
    /// </summary>
    public class SampleWindowTracker : ITrackerPlugin
    {
        public string Name => "Sample Window Tracker";
        public string Version => "1.0.0";
        public string Description => "Plugin de exemplo para rastreamento de janelas para testes de integração";
        public Dictionary<string, object> Configuration { get; set; } = new();

        private bool _isInitialized = false;
        private DateTime _lastCapture = DateTime.MinValue;
        private Random _random = new Random();

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Simular inicialização
                await Task.Delay(100);
                _isInitialized = true;
                _lastCapture = DateTime.UtcNow;
                
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
            
            // Simular eventos de mudança de janela
            var windowTitles = new[]
            {
                "Sample Application - Document1",
                "Sample Application - Document2",
                "Web Browser - Sample Page",
                "Text Editor - sample.txt",
                "File Explorer - Documents"
            };

            var processes = new[]
            {
                "SampleApp",
                "chrome",
                "notepad",
                "explorer"
            };

            // Gerar 1-3 eventos por chamada
            var eventCount = _random.Next(1, 4);
            
            for (int i = 0; i < eventCount; i++)
            {
                var windowTitle = windowTitles[_random.Next(windowTitles.Length)];
                var processName = processes[_random.Next(processes.Length)];
                
                var activityEvent = new ActivityEvent
                {
                    Id = Guid.NewGuid(),
                    ActivityType = ActivityType.WindowChange,
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    Timestamp = DateTime.UtcNow.AddSeconds(-_random.Next(0, 60)),
                    Metadata = new Dictionary<string, object>
                    {
                        ["pluginName"] = Name,
                        ["pluginVersion"] = Version,
                        ["windowId"] = _random.Next(1000, 9999),
                        ["processId"] = _random.Next(1000, 9999),
                        ["isActive"] = true,
                        ["captureMethod"] = "WindowsAPI",
                        ["testPlugin"] = true
                    }
                };

                events.Add(activityEvent);
            }

            _lastCapture = DateTime.UtcNow;
            await Task.Delay(10); // Simular pequena latência
            
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