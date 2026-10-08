// Tests written by Muse Spark 1.3 AI
// RefreshToken rotation flows (SQLite file DB per test):
// create, rotate, replay-kill, unknown, session independence.
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using DeRelay.Core.Security;
using DeRelay.Data;
using DeRelay.Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DeRelay.Tests;

public class RefreshTokenServiceTests
{
    // NOTE: silenced until the pending-flow test commit at the tip of this branch.
    // The bodies below target the post-confirm shapes and do not compile against
    // this step's source yet; they are restored verbatim there. Do not extend here.
#if false
    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IRefreshTokenService Service { get; }
        private readonly string _path;
        public Scope()
        {
            _path = Path.Combine(Path.GetTempPath(), $"derelay_rt_{Guid.NewGuid():N}.db");
            var connection = new SqliteConnection($"DataSource={_path}");
            connection.Open();
            var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
            Context = new DeRelayDbContext(options);
            Context.Database.EnsureCreated();
            Service = new RefreshTokenService(Context);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            File.Delete(_path);
        }
    }

    private static async Task<int> SeedAppUser(DeRelayDbContext ctx, string email = "rtuser@mail.com")
    {
        var person = new Person("Rt", "User", email.Split('@')[0], Gender.Male, new DateTime(2000, 1, 1));
        ctx.Persons.Add(person);
        await ctx.SaveChangesAsync();
        var appUser = new AppUser(email, "HASH");
        ctx.AppUsers.Add(appUser);
        await ctx.SaveChangesAsync();
        ctx.Entry(appUser).Property(u => u.PersonId).CurrentValue = person.Id;
        await ctx.SaveChangesAsync();
        return appUser.Id;
    }

    private static SigningCredentials TestCreds() => new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-secret-at-least-32-bytes!!")),
        SecurityAlgorithms.HmacSha256);

    private static IConfiguration TestConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Confirmation:Key"] = "dGVzdC1vbmx5LWNvbmZpcm1hdGlvbi1rZXk=",
        }).Build();

    [Fact]
    public async Task Create_NewSession_StoresHashNotPlaintext()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);

        var result = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);

        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        var row = await scope.Context.RefreshToken.SingleAsync();
        Assert.NotEqual(result.RefreshToken, row.HashedToken); // never plain at rest
        Assert.False(row.IsRevoked);
        Assert.Equal(appUserId, row.AppUserId);
    }

    [Fact]
    public async Task Refresh_RotatesNewPairRevokesOld()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);

        var second = await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(first.RefreshToken));

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        var rows = await scope.Context.RefreshToken.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Single(rows.Where(r => r.IsRevoked));
        // New token is live: rotates again.
        var third = await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(second.RefreshToken));
        Assert.NotEqual(second.RefreshToken, third.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusedRevoked_KillsWholeSession()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        var second = await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(first.RefreshToken));

        // Attacker or lagging client replays the consumed token.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(first.RefreshToken)));

        Assert.Equal(0, await scope.Context.RefreshToken.CountAsync());
        // Even the live token died with its session.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(second.RefreshToken)));
    }

    [Fact]
    public async Task Refresh_UnknownToken_ThrowsNotFound()
    {
        await using var scope = new Scope();
        await SeedAppUser(scope.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")));
    }

    [Fact]
    public async Task Sessions_AreIndependent_KillOneSparesOther()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var sessionA = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        var sessionB = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        var sessionA2 = await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(sessionA.RefreshToken));

        // Kill session A by replaying its consumed head.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(sessionA.RefreshToken)));

        // Session B lives on, untouched.
        var sessionB2 = await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(sessionB.RefreshToken));
        Assert.NotEqual(sessionB.RefreshToken, sessionB2.RefreshToken);
        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync(r => !r.IsRevoked));
        _ = sessionA2;
    }

    [Fact]
    public async Task Refresh_ExpiredSession_ThrowsUnauthorized()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);

        // Attacker replays a stolen token after the 30-day session window closed.
        // SessionExpiry has a private setter, so age it with SQL like time itself would.
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "RefreshToken" SET "SessionExpiry" = '2000-01-01 00:00:00'""");
        // Raw SQL bypasses the change tracker: drop the stale in-memory row so the
        // rotation reads the aged row, exactly like a fresh request would.
        scope.Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(first.RefreshToken)));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Expired, not revoked: no kill, session row survives for forensics.
        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync());
    }

    [Fact]
    public async Task DeleteSession_KillsSession_StolenTokenDies()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        var sessionId = await scope.Context.RefreshToken
            .Where(r => r.AppUserId == appUserId).Select(r => r.SessionId).SingleAsync();

        // Owner logs out this session (e.g. lost device): whole session removed.
        await scope.Service.DeleteSessionAsync(sessionId, appUserId);

        Assert.Equal(0, await scope.Context.RefreshToken.CountAsync());
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(first.RefreshToken)));
    }

    [Fact]
    public async Task DeleteSession_CrossUser_ThrowsAndSparesVictim()
    {
        await using var scope = new Scope();
        var victimId = await SeedAppUser(scope.Context, "victim");
        var attackerId = await SeedAppUser(scope.Context, "attacker");
        var victimToken = await scope.Service.CreateRefreshTokenWithNewSession(victimId);
        var victimSession = await scope.Context.RefreshToken
            .Where(r => r.AppUserId == victimId).Select(r => r.SessionId).SingleAsync();

        // Attacker names the victim's session id: must fail closed, nothing touched.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.DeleteSessionAsync(victimSession, attackerId));

        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync(r => r.AppUserId == victimId));
        var rotated = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
            new UserRefreshTokenDto(victimToken.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(rotated.RefreshToken));
    }

    [Fact]
    public async Task Sessions_ListShowsOnlyLiveHead()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(first.RefreshToken));

        // Two rows on disk (one revoked corpse), but exactly one live session.
        Assert.Equal(2, await scope.Context.RefreshToken.CountAsync());
        var sessions = await scope.Service.ReturnAllSessionsAsync(appUserId);
        var liveSession = await scope.Context.RefreshToken
            .Where(r => !r.IsRevoked).Select(r => r.SessionId).SingleAsync();
        Assert.Single(sessions);
        Assert.Equal(liveSession, sessions[0].SessionId);
    }

    [Fact]
    public async Task DeleteAccount_RemovesAllSessions_StolenTokensDie()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        // Two devices, two sessions.
        var phone = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        var laptop = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        Assert.Equal(2, await scope.Context.RefreshToken.CountAsync());

        var appUserService = new AppUserService(scope.Context);
        var pendingService = new PendingRegistrationService(scope.Context, TestConfig(), new RandomNumberGeneratorToBase64());
        var authService = new AuthService(scope.Context,
            new PersonService(scope.Context, appUserService),
            new PasswordHasher<AppUser>(), TestCreds(), appUserService,
            scope.Service, pendingService);
        await authService.DeleteAccountAsync(appUserId);

        // Person → AppUser → RefreshToken cascade: no orphan credential material.
        Assert.Equal(0, await scope.Context.Persons.CountAsync());
        Assert.Equal(0, await scope.Context.AppUsers.CountAsync());
        Assert.Equal(0, await scope.Context.RefreshToken.CountAsync());
        // Both stolen tokens die as unknown, not as usable sessions.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(phone.RefreshToken)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(laptop.RefreshToken)));
    }

    [Fact]
    public async Task Sweep_RepeatedCycles_LongChainsTrimmedShortUntouched()
    {
        await using var scope = new Scope();
        var quietId = await SeedAppUser(scope.Context, "quiet");
        var busyId = await SeedAppUser(scope.Context, "busy");

        // Quiet session: 3 revoked + live, never exceeds the cap.
        var quiet = await scope.Service.CreateRefreshTokenWithNewSession(quietId);
        for (var i = 0; i < 3; i++)
            quiet = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
                new UserRefreshTokenDto(quiet.RefreshToken));

        // Busy session: chain past 100 from the start (105 revoked + live).
        var busy = await scope.Service.CreateRefreshTokenWithNewSession(busyId);
        for (var i = 0; i < 105; i++)
            busy = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
                new UserRefreshTokenDto(busy.RefreshToken));

        async Task<int> Revoked(int appUserId) => await scope.Context.RefreshToken
            .CountAsync(t => t.AppUserId == appUserId && t.IsRevoked);
        async Task<int> Live(int appUserId) => await scope.Context.RefreshToken
            .CountAsync(t => t.AppUserId == appUserId && !t.IsRevoked);

        // Six ticks: sweep, grow both sessions, sweep again.
        for (var cycle = 0; cycle < 6; cycle++)
        {
            var deleted = await scope.Service.DeleteOldRefreshTokensInSessionsAsync(CancellationToken.None);
            Assert.Equal(1, await Live(quietId));
            Assert.Equal(1, await Live(busyId));
            var busyRevoked = await Revoked(busyId);
            Assert.True(busyRevoked <= 20);
            if (cycle == 0)
            {
                // First tick: 105 -> 20, exactly 85 gone; quiet untouched (3 <= 20).
                Assert.Equal(85, deleted);
                Assert.Equal(3, await Revoked(quietId));
            }

            // Grow: busy +5 rotations, quiet +1 (quiet ends at 9, still under cap).
            for (var i = 0; i < 5; i++)
                busy = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
                    new UserRefreshTokenDto(busy.RefreshToken));
            quiet = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
                new UserRefreshTokenDto(quiet.RefreshToken));
        }

        // After 6 cycles + final sweep: quiet 9 revoked (never trimmed),
        // busy pinned at 20, 2 live heads.
        await scope.Service.DeleteOldRefreshTokensInSessionsAsync(CancellationToken.None);
        Assert.Equal(9, await Revoked(quietId));
        Assert.Equal(20, await Revoked(busyId));
        Assert.Equal(2, await scope.Context.RefreshToken.CountAsync(t => !t.IsRevoked));
        // Trim kept the newest links: busy's lowest surviving chain is Max - 19.
        var numbers = await scope.Context.RefreshToken
            .Where(t => t.AppUserId == busyId && t.IsRevoked)
            .Select(t => t.ChainNumber).ToListAsync();
        Assert.Equal(20, numbers.Count);
        Assert.Equal(numbers.Max(), numbers.Min() + 19);
    }

    [Fact]
    public async Task DeleteSession_EmptyGuid_ThrowsNotFoundChangesNothing()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.DeleteSessionAsync(Guid.Empty, appUserId));

        // Nothing touched: the session still rotates.
        var second = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
            new UserRefreshTokenDto(first.RefreshToken));
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    }

    [Fact]
    public async Task Sessions_MultipleSessionsWithCorpses_ListsOnePerLive()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var s1 = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        var s2 = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        // Two rotations each: corpses pile up, one live head per session.
        s1 = await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(s1.RefreshToken));
        s1 = await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(s1.RefreshToken));
        s2 = await scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(s2.RefreshToken));
        Assert.Equal(5, await scope.Context.RefreshToken.CountAsync());

        var sessions = await scope.Service.ReturnAllSessionsAsync(appUserId);
        Assert.Equal(2, sessions.Count);
        var liveIds = await scope.Context.RefreshToken
            .Where(t => !t.IsRevoked).Select(t => t.SessionId).ToListAsync();
        Assert.Equal(liveIds.OrderBy(x => x).ToList(), sessions.Select(s => s.SessionId).OrderBy(x => x).ToList());
        _ = s1; _ = s2;
    }

    [Fact]
    public async Task Refresh_DailyActiveSession_StillExpiresAt30Days()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        // Active user: rotates fine today...
        var token = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        token = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
            new UserRefreshTokenDto(token.RefreshToken));

        // ...but rotation never extends the window: aged live head dies anyway.
        // Pins ABSOLUTE (not sliding) expiry — conscious product decision.
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "RefreshToken" SET "SessionExpiry" = '2000-01-01 00:00:00' WHERE "IsRevoked" = 0""");
        scope.Context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(token.RefreshToken)));
    }

    [Fact]
    public async Task Refresh_PrunedHistory_ReplaysAsNotFoundWithoutKill()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var ancient = await scope.Service.CreateRefreshTokenWithNewSession(appUserId);
        var live = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
            new UserRefreshTokenDto(ancient.RefreshToken));

        // Simulate the sweeper pruning the ancient revoked link.
        await scope.Context.Database.ExecuteSqlRawAsync(
            """DELETE FROM "RefreshToken" WHERE "IsRevoked" = 1""");
        scope.Context.ChangeTracker.Clear();
        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync());

        // Attacker replays the pruned token: unknown, not a kill — live head survives.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingSession(new UserRefreshTokenDto(ancient.RefreshToken)));
        var live2 = await scope.Service.RefreshTheRefreshTokenOfExistingSession(
            new UserRefreshTokenDto(live.RefreshToken));
        Assert.NotEqual(live.RefreshToken, live2.RefreshToken);
    }
#endif
}
