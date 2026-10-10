// Tests written by Muse Spark 1.3 AI
// Stress: N concurrent registers with the SAME email (separate scopes,
// one shared database file). Exactly one must win; losers must leave nothing.
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using DeRelay.Core.Security;
using DeRelay.Data;
using DeRelay.Data.Services;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace DeRelay.Tests;

public class AuthStressTests
{
    private static RegisterDto ValidRegister(string email = "racer@mail.com") =>
        new(email, "cat12345", "cat12345");

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

    private sealed class StubRng(string token) : ITokenGenerator
    {
        public string GenerateAsBase64() => token;
        public string GenerateAsBase64Url() => token;
    }

    private sealed class StubEmailService : IEmailService
    {
        public List<(string Email, string Subject, string Message)> Sent { get; } = new();
        public Task SendEmailAsync(string toEmail, string subject, string message)
        {
            Sent.Add((toEmail, subject, message));
            return Task.CompletedTask;
        }
    }

    private static IAuthService ServiceFor(string path, int failSaveOnCall = 0, string? fixedLink = null)
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
        ITokenGenerator rng = fixedLink is null
            ? new TokenGenerator()
            : new StubRng(fixedLink);
        var pendingService = new PendingRegistrationService(context, TestConfig(), rng);
        return new AuthService(context, new PersonService(context, new AppUserService(context)), new PasswordHasher<AppUser>(), testCreds, new AppUserService(context), new RefreshTokenService(context), pendingService, new StubEmailService(), NullLogger<AuthService>.Instance);
    }

    private static IConfiguration TestConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Confirmation:Key"] = "dGVzdC1vbmx5LWNvbmZpcm1hdGlvbi1rZXk=",
        }).Build();

    private static (int persons, int users, int pendings) Counts(string path)
    {
        using var connection = new SqliteConnection($"DataSource={path}");
        connection.Open();
        var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
        using var ctx = new DeRelayDbContext(options);
        return (ctx.Persons.Count(), ctx.AppUsers.Count(), ctx.PendingRegistrations.Count());
    }

    [Fact]
    public async Task Register_ConcurrentSameEmail_ExactlyOnePendingWins()
    {
        var path = NewDbFile();
        const string link = "RACE-LINK";
        try
        {
            // Same email for every racer: the unique Email index admits exactly
            // one pending row; losers die on the constraint, leaving nothing.
            var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
            {
                try
                {
                    await ServiceFor(path, fixedLink: link).RegisterAsPending(
                        ValidRegister("racer@mail.com") with { Password = "cat12345" });
                    return true;
                }
                catch
                {
                    return false;
                }
            })).ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, results.Count(r => r)); // exactly one winner
            var (persons, users, pendings) = Counts(path);
            Assert.Equal(1, pendings);
            Assert.Equal(0, users);
            Assert.Equal(0, persons);

            // First confirm wins a user; a second click meets the retained row
            // with the user already there: AlreadyExists, never twins.
            await ServiceFor(path, fixedLink: link).AcceptConfirmationLink(link);
            await Assert.ThrowsAsync<AlreadyExistsException>(() =>
                ServiceFor(path, fixedLink: link).AcceptConfirmationLink(link));
            (_, users, pendings) = Counts(path);
            Assert.Equal(1, users);
            Assert.Equal(1, pendings); // consumed row retained as witness by design
            var pair = await ServiceFor(path, fixedLink: link).LoginAsync(
                new LoginDto("racer@mail.com", "cat12345"));
            Assert.False(string.IsNullOrWhiteSpace(pair.JwtToken));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Register_LoginSaveFails_UserRolledBack()
    {
        var path = NewDbFile();
        try
        {
            // Call 1 = AppUser save (ok), call 2 = final save (throws).
            var failing = ServiceFor(path, failSaveOnCall: 2);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                failing.RegisterAsPending(ValidRegister("crash@mail.com")));

            var (persons, users, _) = Counts(path);
            Assert.Equal(0, persons);
            Assert.Equal(0, users); // staged pending erased by rollback
        }
        finally { File.Delete(path); }
    }
}
