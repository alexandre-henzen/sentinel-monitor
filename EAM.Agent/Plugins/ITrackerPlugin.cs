using EAM.Agent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EAM.Agent.Plugins
{
    public interface ITrackerPlugin
    {
        string Name { get; }
        ValueTask InitializeAsync(IConfiguration cfg, ILogger logger);
        ValueTask<IEnumerable<ActivityEvent>> PollAsync(CancellationToken ct);
    }
}