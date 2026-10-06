using DeRelay.Core.Interfaces;
using DeRelay.Data;

namespace DeRelay.Api.BackgroundServices;

public class RefreshTokenCleanupService(IServiceScopeFactory scopes, ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var sweeper = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
            var ctx = scope.ServiceProvider.GetRequiredService<DeRelayDbContext>();
            var transaction = await ctx.Database.BeginTransactionAsync(stoppingToken);
            try
            {
                var deleted = await sweeper.DeleteOldRefreshTokensInSessionsAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Token cleanup swept {Deleted} dead refresh-token rows.", deleted);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(stoppingToken);
                logger.LogError(ex, "Token cleanup sweep failed; will retry next tick.");
            }
        }
    }
}