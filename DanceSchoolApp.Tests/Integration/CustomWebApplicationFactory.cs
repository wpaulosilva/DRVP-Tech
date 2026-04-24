using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DanceSchoolApp.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    // Set env vars exactly once per process, avoiding repeated writes
    // from multiple factory instances (one per IClassFixture test class).
    private static readonly object _envLock = new();
    private static bool _envInitialized;

    public CustomWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        lock (_envLock)
        {
            if (!_envInitialized)
            {
                Environment.SetEnvironmentVariable("DanceSchoolApp_JWT_Secret",
                    "SuperSecretTestKey_AtLeast32Characters!!");
                Environment.SetEnvironmentVariable("DanceSchoolApp_DB",
                    "placeholder-suppresses-warning");
                _envInitialized = true;
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Remove EVERY descriptor whose ServiceType name contains
            // "DbContext" or "DbContextOptions" — this catches all the
            // EF Core internal registrations (options, options-configuration,
            // the context itself) regardless of EF Core version.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().FullName != null &&
                     d.ServiceType.GetGenericTypeDefinition().FullName!
                         .Contains("IDbContextOptionsConfiguration")))
                .ToList();

            foreach (var d in toRemove)
                services.Remove(d);

            // Remove the ClassLifecycleWorker background service.
            // It fires immediately at startup and every 5 minutes after,
            // mutating class statuses and creating notifications. This
            // causes race conditions during integration tests because the
            // worker modifies DB state concurrently with test assertions.
            var workerDescriptors = services
                .Where(d => d.ImplementationType == typeof(ClassLifecycleWorker))
                .ToList();
            foreach (var d in workerDescriptors)
                services.Remove(d);

            // Re-register AppDbContext against the shared in-memory SQLite
            // connection. The connection must stay open for the entire lifetime
            // of the factory or the in-memory DB is destroyed between scopes.
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Runs a seeder action inside a dedicated DI scope so tests can populate
    /// the in-memory database before sending HTTP requests.
    /// EnsureCreated() is idempotent — safe to call from multiple tests.
    /// </summary>
    public void SeedDatabase(Action<AppDbContext> seeder)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        seeder(db);
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}
