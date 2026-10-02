using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Api.Infrastructure;

internal static class DatabaseStartupExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (options.InitializeOnStartup)
        {
            await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync(app.Lifetime.ApplicationStopping);
        }

        if (options.SeedDemoData)
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(app.Lifetime.ApplicationStopping);
        }
    }
}
