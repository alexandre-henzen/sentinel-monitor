using EAM.Agent.Data;
using EAM.Agent.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, "eam_local.db");
    options.UseSqlite($"Data Source={dbPath}");
});

builder.Services.AddHostedService<WindowTracker>();
builder.Services.AddHostedService<ProcessMonitor>();
builder.Services.AddHostedService<ApiSyncService>();
builder.Services.AddHostedService<PluginLoaderService>();

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri("https://api.eam.local");
});

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

host.Run();
