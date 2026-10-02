// Tests written by Muse Spark 1.3 AI
// Stress: N concurrent registers with the SAME username (separate scopes,
// one shared database file). Exactly one must win; losers must leave nothing.
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Interfaces;
using DeRelay.Data;
using DeRelay.Data.Services;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DeRelay.Tests;

public class AuthStressTests
{
    private static RegisterDto ValidRegister(string userName = "racer") =>
        new(userName, "cat12345", "Race", "Runner", "racer" + Guid.NewGuid().ToString("N")[..8],
            Gender.Male, new DateTime(2000, 1, 1));

    private sealed class FailingSaveContext(DbContextOptions<DeRelayDbContext> options, int failOnCall) : DeRelayDbContext(options)
    {
        private int _calls;
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == failOnCall)
                throw new InvalidOperationException("Simulated save failure");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static string NewDbFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"derelay_stress_{Guid.NewGuid():N}.db");
        using var setup = new SqliteConnection($"DataSource={path}");
        setup.Open();
        var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(setup).Options;
        using var ctx = new DeRelayDbContext(options);
        ctx.Database.EnsureCreated();
        return path;
    }

    private static IAuthService ServiceFor(string path, int failSaveOnCall = 0)
    {
        var connection = new SqliteConnection($"DataSource={path}");
        connection.Open();
        var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
        DeRelayDbContext context = failSaveOnCall > 0
            ? new FailingSaveContext(options, failSaveOnCall)
            : new DeRelayDbContext(options);
        var testCreds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-secret-at-least-32-bytes!!")),
            SecurityAlgorithms.HmacSha256);
        return new AuthService(context, new PersonService(context, new AppUserService(context)), new PasswordHasher<AppUser>(), testCreds, new AppUserService(context), new RefreshTokenService(context));
    }

    private static (int persons, int users) Counts(string path)
    {
        using var connection = new SqliteConnection($"DataSource={path}");
        connection.Open();
        var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
        using var ctx = new DeRelayDbContext(options);
        return (ctx.Persons.Count(), ctx.AppUsers.Count());
    }

    [Fact]
    public async Task Register_ConcurrentSameUserName_ExactlyOneWinsNoOrphans()
    {
        var path = NewDbFile();
        try
        {
            // Nicknames differ per racer so ONLY the username collides.
            var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
            {
                try
                {
                    var dto = ValidRegister("racer") with { Password = "cat12345" };
                    await ServiceFor(path).RegisterAsync(dto);
                    return true;
                }
                catch
                {
                    // Loser: 409 taken, unique-violation 500, or lock contention.
                    // Any failure is acceptable; leftovers are not.
                    return false;
                }
            })).ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, results.Count(r => r)); // exactly one winner
            var (persons, users) = Counts(path);
            Assert.Equal(1, users);
            Assert.Equal(1, persons); // losers' Persons rolled back: no orphans
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Register_LoginSaveFails_PersonRolledBack()
    {
        var path = NewDbFile();
        try
        {
            // Call 1 = Person save (ok), call 2 = AppUser save (throws).
            var failing = ServiceFor(path, failSaveOnCall: 2);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                failing.RegisterAsync(ValidRegister("crash")));

            var (persons, users) = Counts(path);
            Assert.Equal(0, persons); // staged Person erased
            Assert.Equal(0, users);
        }
        finally { File.Delete(path); }
    }
}
