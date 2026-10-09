// Tests written by Muse Spark 1.3 AI
// In-process API host for controller-level (HTTP) tests:
// real routing + middleware + JwtBearer, SQLite file DB, test JWT secret.
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DeRelay.Core.Interfaces;
using DeRelay.Data;

namespace DeRelay.Tests;

public class DeRelayWebFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "test-only-secret-at-least-32-bytes!!";
    public string DbPath { get; } =
        Path.Combine(Path.GetTempPath(), $"derelay_web_{Guid.NewGuid():N}.db");

    // Issued confirmation links for this app instance: the stub RNG records
    // every token it mints, so HTTP tests confirm the exact link just mailed.
    private readonly StubRng _rng = new();
    public IReadOnlyList<string> IssuedLinks => _rng.Issued;

    private sealed class StubRng : ITokenGenerator
    {
        public List<string> Issued { get; } = new();
        public string GenerateAsBase64() => Issue();
        public string GenerateAsBase64Url() => Issue();
        private string Issue()
        {
            var token = $"TEST-LINK-{Guid.NewGuid():N}";
            Issued.Add(token);
            return token;
        }
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string message) =>
            Task.CompletedTask;
    }

    // Env vars beat user-secrets/appointments in every ordering, so the test
    // secret deterministically wins over the developer's real user-secrets value
    // no matter when ConfigureWebHost callbacks run relative to CreateBuilder.
    static DeRelayWebFactory() =>
        Environment.SetEnvironmentVariable("Jwt__Key", TestSecret);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestSecret,
                ["Confirmation:Key"] = "dGVzdC1vbmx5LWNvbmZpcm1hdGlvbi1rZXk=",
                ["ConnectionStrings:DefaultConnection"] = "Host=none;Database=none",
            }));
        builder.ConfigureServices(services =>
        {
            // AddDbContext registers the optionsAction as IDbContextOptionsConfiguration<T>,
            // and ALL such configurators run into one options object — that merger is what
            // mixed Npgsql+Sqlite. Remove it alongside the options/context descriptors.
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<DeRelayDbContext>));
            services.RemoveAll(typeof(DbContextOptions<DeRelayDbContext>));
            services.RemoveAll(typeof(DeRelayDbContext));
            services.RemoveAll(typeof(IDbContextOptionsExtension));
            services.AddDbContext<DeRelayDbContext>(o => o.UseSqlite($"DataSource={DbPath}"));
            services.RemoveAll(typeof(ITokenGenerator));
            services.AddScoped<ITokenGenerator>(_ => _rng);
            services.RemoveAll(typeof(IEmailService));
            services.AddScoped<IEmailService>(_ => new NoOpEmailService());
            var ensureOptions = new DbContextOptionsBuilder<DeRelayDbContext>()
                .UseSqlite($"DataSource={DbPath}").Options;
            using (var ensureCtx = new DeRelayDbContext(ensureOptions))
                ensureCtx.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) try { File.Delete(DbPath); } catch { /* best effort */ }
    }
}
