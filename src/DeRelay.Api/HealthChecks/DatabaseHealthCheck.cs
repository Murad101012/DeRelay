using System.Diagnostics;
using DeRelay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeRelay.Api.HealthChecks;

public class DatabaseHealthCheck(DeRelayDbContext deRelayDbContext): IHealthCheck
{
    private const int SlowDatabaseResponseTimeLimitMilliseconds = 500; 
    public async Task<HealthCheckResult> CheckHealthAsync
        (HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            _ = await deRelayDbContext.Persons.AnyAsync(cancellationToken);
            stopwatch.Stop();

            return stopwatch.Elapsed <= TimeSpan.FromMilliseconds(SlowDatabaseResponseTimeLimitMilliseconds)
                ? HealthCheckResult.Healthy
                    ("Successfully connected to database")
                : HealthCheckResult.Degraded
                    ("Successfully connected to database, but response is slower than expected");
            
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy("Can't connect to database", e);
        }
        
    }
}