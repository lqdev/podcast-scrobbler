using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PodcastScrobbler.Config;
using PodcastScrobbler.Data;

namespace PodcastScrobbler.Tests;

public class ScrobblerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace config with in-memory DuckDB
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ScrobblerConfig));
            if (descriptor != null)
                services.Remove(descriptor);

            var config = new ScrobblerConfig
            {
                DatabasePath = ":memory:",
                ScrobblerToken = "test-token",
                RequireAuthForReads = false,
                Port = "5000"
            };
            services.AddSingleton(config);

            // Replace DuckDbContext with in-memory version
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DuckDbContext));
            if (dbDescriptor != null)
                services.Remove(dbDescriptor);

            services.AddSingleton<DuckDbContext>();

            // Replace PlayingNowStore
            var pnsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PlayingNowStore));
            if (pnsDescriptor != null)
                services.Remove(pnsDescriptor);

            services.AddSingleton<PlayingNowStore>();
        });
    }
}
