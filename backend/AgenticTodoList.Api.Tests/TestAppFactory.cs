using BriefappTodoList.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BriefappTodoList.Api.Tests;

public class TestAppFactory : WebApplicationFactory<Program>
{
    private static readonly InMemoryDatabaseRoot SharedDatabaseRoot = new();
    private string? _webhookSecret;
    private bool _openClawEnabled = false;
    private string? _openClawSecret;

    public void WithWebhookSecret(string secret) => _webhookSecret = secret;

    /// <summary>Enable OpenClaw feature flag for tests that need it.</summary>
    public void WithOpenClawEnabled() => _openClawEnabled = true;

    /// <summary>Set the HMAC secret used in the OpenClaw webhook receiver.</summary>
    public void WithOpenClawSecret(string secret) => _openClawSecret = secret;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("briefapp-tests", SharedDatabaseRoot));
        });

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var extra = new Dictionary<string, string?>
            {
                ["Auth:ApiKeys:0"]   = "test-api-key-1234",
                ["OpenClaw:Enabled"] = _openClawEnabled ? "true" : "false",
                ["OpenClaw:BaseUrl"] = "http://localhost:9700",
                ["OpenClaw:ApiKey"]  = "test-key",
            };
            if (_webhookSecret is not null)
                extra["DevLake:WebhookSecret"] = _webhookSecret;
            if (_openClawSecret is not null)
                extra["OpenClaw:WebhookSecret"] = _openClawSecret;

            cfg.AddInMemoryCollection(extra);
        });
    }
}
