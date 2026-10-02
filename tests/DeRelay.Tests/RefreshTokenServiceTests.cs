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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
        var person = new Person("Rt", "User", "rtuser", Gender.Male, new DateTime(2000, 1, 1));
        ctx.Persons.Add(person);
        await ctx.SaveChangesAsync();
        var appUser = new AppUser(userName, "HASH", person.Id);
        ctx.AppUsers.Add(appUser);
        await ctx.SaveChangesAsync();
        return appUser.Id;
    }

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

        var second = await scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken));

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        var rows = await scope.Context.RefreshToken.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Single(rows.Where(r => r.IsRevoked));
        // New token is live: rotates again.
        var third = await scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto(second.RefreshToken));
        Assert.NotEqual(second.RefreshToken, third.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusedRevoked_KillsWholeFamily()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var first = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        var second = await scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken));

        // Attacker or lagging client replays the consumed token.
        await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto(first.RefreshToken)));

        Assert.Equal(0, await scope.Context.RefreshToken.CountAsync());
        // Even the live token died with its family.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto(second.RefreshToken)));
    }

    [Fact]
    public async Task Refresh_UnknownToken_ThrowsNotFound()
    {
        await using var scope = new Scope();
        await SeedAppUser(scope.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")));
    }

    [Fact]
    public async Task Families_AreIndependent_KillOneSparesOther()
    {
        await using var scope = new Scope();
        var appUserId = await SeedAppUser(scope.Context);
        var familyA = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        var familyB = await scope.Service.CreateRefreshTokenWithNewFamily(appUserId);
        var familyA2 = await scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto(familyA.RefreshToken));

        // Kill family A by replaying its consumed head.
        await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto(familyA.RefreshToken)));

        // Family B lives on, untouched.
        var familyB2 = await scope.Service.RefreshTokenOfExistingFamily(new UserRefreshTokenDto(familyB.RefreshToken));
        Assert.NotEqual(familyB.RefreshToken, familyB2.RefreshToken);
        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync(r => !r.IsRevoked));
        _ = familyA2;
    }
}
