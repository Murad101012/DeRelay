// Tests written by opencode AI
// Phase 6 ops: /health/live (process only) and /health/ready (tagged checks).
using System.Net;
using System.Text.Json;
using DeRelay.Api.HealthChecks;
using DeRelay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeRelay.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Live_AlwaysReportsHealthy()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_ReportsDatabaseCheckHealthy()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("timestamp", out _));
        Assert.True(root.TryGetProperty("totalDurationMs", out _));

        var checks = root.GetProperty("checks").EnumerateArray().ToList();
        var database = Assert.Single(checks);
        Assert.Equal("database", database.GetProperty("name").GetString());
        Assert.Equal("Healthy", database.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DatabaseCheck_SchemaPresent_IsHealthy()
    {
        var path = Path.Combine(Path.GetTempPath(), $"derelay_health_{Guid.NewGuid():N}.db");
        try
        {
            await using var db = NewContext(path);
            await db.Database.EnsureCreatedAsync();
            var check = new DatabaseHealthCheck(db);

            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task DatabaseCheck_Unreachable_IsUnhealthyAndDoesNotThrow()
    {
        // Parent directory does not exist, so SQLite cannot open/create the file.
        var missingDir = Path.Combine(Path.GetTempPath(), $"derelay_missing_{Guid.NewGuid():N}");
        var path = Path.Combine(missingDir, "derelay.db");
        await using var db = NewContext(path);
        var check = new DatabaseHealthCheck(db);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    private static DeRelayDbContext NewContext(string path) =>
        new(new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite($"DataSource={path}").Options);
}
