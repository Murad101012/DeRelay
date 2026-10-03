// Tests written by Muse Spark 1.3 AI
// RefreshToken rotation flows (SQLite file DB per test):
// create, rotate, replay-kill, unknown, family independence.
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
    public async Task Create_NewFamily_StoresHashNotPlaintext()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);

        var result = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);

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
        var first = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);

        var second = await scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken));

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        var rows = await scope.Context.RefreshToken.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Single(rows.Where(r => r.IsRevoked));
        // New token is live: rotates again.
        var third = await scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(second.RefreshToken));
        Assert.NotEqual(second.RefreshToken, third.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusedRevoked_KillsWholeFamily()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        var second = await scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken));

        // Attacker or lagging client replays the consumed token.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken)));

        Assert.Equal(0, await scope.Context.RefreshToken.CountAsync());
        // Even the live token died with its family.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(second.RefreshToken)));
    }

    [Fact]
    public async Task Refresh_UnknownToken_ThrowsNotFound()
    {
        await using var scope = new Scope();
        await SeedAppUser(scope.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")));
    }

    [Fact]
    public async Task Families_AreIndependent_KillOneSparesOther()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var familyA = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        var familyB = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        var familyA2 = await scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(familyA.RefreshToken));

        // Kill family A by replaying its consumed head.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(familyA.RefreshToken)));

        // Family B lives on, untouched.
        var familyB2 = await scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(familyB.RefreshToken));
        Assert.NotEqual(familyB.RefreshToken, familyB2.RefreshToken);
        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync(r => !r.IsRevoked));
        _ = familyA2;
    }

    [Fact]
    public async Task Refresh_ExpiredFamily_ThrowsUnauthorized()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);

        // Attacker replays a stolen token after the 30-day family window closed.
        // FamilyExpiry has a private setter, so age it with SQL like time itself would.
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "RefreshToken" SET "FamilyExpiry" = '2000-01-01 00:00:00'""");
        // Raw SQL bypasses the change tracker: drop the stale in-memory row so the
        // rotation reads the aged row, exactly like a fresh request would.
        scope.Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken)));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Expired, not revoked: no kill, family row survives for forensics.
        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync());
    }

    [Fact]
    public async Task DeleteSession_KillsFamily_StolenTokenDies()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        var familyId = await scope.Context.RefreshToken
            .Where(r => r.AppUserId == appUserId).Select(r => r.FamilyId).SingleAsync();

        // Owner logs out this session (e.g. lost device): whole family removed.
        await scope.Service.DeleteSessionAsync(familyId, appUserId);

        Assert.Equal(0, await scope.Context.RefreshToken.CountAsync());
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken)));
    }

    [Fact]
    public async Task DeleteSession_CrossUser_ThrowsAndSparesVictim()
    {
        await using var scope = new Scope();
        var victimId = await SeedAppUser(scope.Context, "victim");
        var attackerId = await SeedAppUser(scope.Context, "attacker");
        var victimToken = await scope.Service.CreateRefreshTokenWithNewFamily(victimId);
        var victimFamily = await scope.Context.RefreshToken
            .Where(r => r.AppUserId == victimId).Select(r => r.FamilyId).SingleAsync();

        // Attacker names the victim's family id: must fail closed, nothing touched.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.DeleteSessionAsync(victimFamily, attackerId));

        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync(r => r.AppUserId == victimId));
        var rotated = await scope.Service.RefreshTheRefreshTokenOfExistingFamily(
            new UserRefreshTokenDto(victimToken.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(rotated.RefreshToken));
    }

    [Fact]
    public async Task Sessions_ListShowsOnlyLiveHead()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        await scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken));

        // Two rows on disk (one revoked corpse), but exactly one live session.
        Assert.Equal(2, await scope.Context.RefreshToken.CountAsync());
        var sessions = await scope.Service.ReturnAllSessionsAsync(appUserId);
        var liveFamily = await scope.Context.RefreshToken
            .Where(r => !r.IsRevoked).Select(r => r.FamilyId).SingleAsync();
        Assert.Single(sessions);
        Assert.Equal(liveFamily, sessions[0].SessionId);
    }

    [Fact]
    public async Task DeleteAccount_RemovesAllSessions_StolenTokensDie()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        // Two devices, two families.
        var phone = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        var laptop = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
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
            scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(phone.RefreshToken)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTheRefreshTokenOfExistingFamily(new UserRefreshTokenDto(laptop.RefreshToken)));
    }
}
