using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace SamplePlugins
{
    /// <summary>
    /// Plugin de exemplo para rastreamento de navegadores
    /// Usado para testes de integração do sistema de plugins
    /// </summary>
    public class SampleBrowserTracker : ITrackerPlugin
    {
        public string Name => "Sample Browser Tracker";
        public string Version => "1.0.0";
        public string Description => "Plugin de exemplo para rastreamento de navegadores para testes de integração";
        public Dictionary<string, object> Configuration { get; set; } = new();

        private bool _isInitialized = false;
        private readonly Random _random = new Random();

        public async Task<bool> InitializeAsync()
        {
            try
            {
                await Task.Delay(150);
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
            
            // Simular eventos de navegação
            var urls = new[]
            {
                "https://www.google.com",
                "https://www.github.com",
                "https://stackoverflow.com",
                "https://docs.microsoft.com",
                "https://www.w3schools.com"
            };

            var browsers = new[]
            {
                "chrome",
                "firefox",
                "edge",
                "safari"
            };

            var titles = new[]
            {
                "Google",
                "GitHub",
                "Stack Overflow",
                "Microsoft Docs",
                "W3Schools"
            };

            // Gerar 1-2 eventos por chamada
            var eventCount = _random.Next(1, 3);
            
            for (int i = 0; i < eventCount; i++)
            {
                var index = _random.Next(urls.Length);
                var url = urls[index];
                var title = titles[index];
                var browser = browsers[_random.Next(browsers.Length)];
                
                var activityEvent = new ActivityEvent
                {
                    Id = Guid.NewGuid(),
                    ActivityType = ActivityType.BrowserNavigation,
                    ProcessName = browser,
                    WindowTitle = $"{title} - {browser}",
                    Timestamp = DateTime.UtcNow.AddSeconds(-_random.Next(0, 300)),
                    Metadata = new Dictionary<string, object>
                    {
                        ["pluginName"] = Name,
                        ["pluginVersion"] = Version,
                        ["url"] = url,
                        ["domain"] = new Uri(url).Host,
                        ["browserType"] = browser,
                        ["tabId"] = _random.Next(1, 20),
                        ["isIncognito"] = _random.Next(0, 10) < 2,
                        ["testPlugin"] = true,
                        ["navigationSource"] = "user"
                    }
                };

                events.Add(activityEvent);
            }

            await Task.Delay(20);
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