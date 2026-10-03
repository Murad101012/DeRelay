// Tests written by Muse Spark 1.3 AI
// RefreshToken rotation flows (SQLite file DB per test):
// create, rotate, replay-kill, unknown, session independence.
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using DeRelay.Data;
using DeRelay.Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DeRelay.Tests;

public class RefreshTokenServiceTests
{
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

    private static async Task<int> SeedAppUser(DeRelayDbContext ctx, string userName = "rtuser")
    {
        var person = new Person("Rt", "User", userName, Gender.Male, new DateTime(2000, 1, 1));
        ctx.Persons.Add(person);
        await ctx.SaveChangesAsync();
        var appUser = new AppUser(userName, "HASH", person.Id);
        ctx.AppUsers.Add(appUser);
        await ctx.SaveChangesAsync();
        return appUser.Id;
    }

    private static SigningCredentials TestCreds() => new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-secret-at-least-32-bytes!!")),
        SecurityAlgorithms.HmacSha256);

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
        var authService = new AuthService(scope.Context,
            new PersonService(scope.Context, appUserService),
            new PasswordHasher<AppUser>(), TestCreds(), appUserService,
            scope.Service);
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
}
