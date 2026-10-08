// Tests written by Muse Spark 1.3 AI
// Pending-registration flow (SQLite file DB per scope):
// row shape, duplicates, confirm happy/unknown/expired/twice,
// resend-after-expiry, unconfirmed-login parity.
using System.Data.Common;
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.Entities;
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

public class PendingRegistrationTests
{
    private const string FixedLink = "PENDING-TEST-LINK";

    private sealed class StubRng : IRandomNumberGeneratorToBase64
    {
        public string GenerateRandomToken() => FixedLink;
    }

    private static IConfiguration TestConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Confirmation:Key"] = "dGVzdC1vbmx5LWNvbmZpcm1hdGlvbi1rZXk=",
        }).Build();

    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IAuthService Service { get; }
        private readonly string _path;
        private readonly DbConnection _connection;
        public Scope()
        {
            _path = Path.Combine(Path.GetTempPath(), $"derelay_pr_{Guid.NewGuid():N}.db");
            _connection = new SqliteConnection($"DataSource={_path}");
            _connection.Open();
            var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(_connection).Options;
            Context = new DeRelayDbContext(options);
            Context.Database.EnsureCreated();
            var appUserService = new AppUserService(Context);
            var personService = new PersonService(Context, appUserService);
            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-secret-at-least-32-bytes!!")),
                SecurityAlgorithms.HmacSha256);
            var pendingService = new PendingRegistrationService(Context, TestConfig(), new StubRng());
            Service = new AuthService(Context, personService,
                new PasswordHasher<AppUser>(), creds, appUserService, new RefreshTokenService(Context), pendingService);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
            File.Delete(_path);
        }
    }

    private static RegisterDto ValidRegister(string email = "pend@mail.com") =>
        new(email, "cat12345");

    [Fact]
    public async Task Register_CreatesPendingRow_HashNotPlaintext()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());

        var row = await scope.Context.PendingRegistrations.SingleAsync();
        Assert.Equal("pend@mail.com", row.Email);
        Assert.False(string.IsNullOrWhiteSpace(row.HashedPassword));
        Assert.NotEqual(FixedLink, row.Hash); // link never at rest
        Assert.Equal(0, await scope.Context.AppUsers.CountAsync()); // no user yet
    }

    [Fact]
    public async Task Register_DuplicateLivePending_409InboxMessage()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());

        var ex = await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            scope.Service.RegisterAsPending(ValidRegister()));
        Assert.Contains("inbox", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await scope.Context.PendingRegistrations.CountAsync());
    }

    [Fact]
    public async Task Register_AfterConfirm_TakenEmail_409()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());
        await scope.Service.AcceptConfirmationLink(FixedLink);

        await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            scope.Service.RegisterAsPending(ValidRegister()));
    }

    [Fact]
    public async Task Confirm_Happy_CreatesUserDeletesPendingAndLogsIn()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());
        await scope.Service.AcceptConfirmationLink(FixedLink);

        var user = await scope.Context.AppUsers.SingleAsync();
        Assert.Equal("pend@mail.com", user.Email);
        Assert.Equal(0, await scope.Context.PendingRegistrations.CountAsync());
        var pair = await scope.Service.LoginAsync(new LoginDto("pend@mail.com", "cat12345"));
        Assert.False(string.IsNullOrWhiteSpace(pair.JwtToken));
    }

    [Fact]
    public async Task Confirm_UnknownLink_404()
    {
        await using var scope = new Scope();
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.AcceptConfirmationLink("NOPE-NOPE-NOPE-NOPE-NOPE-NOPE-NOPE-NOPE-AAAA"));
        Assert.Contains("register again", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirm_ExpiredLink_401RowSurvives()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "PendingRegistrations" SET "LinkExpiry" = '2000-01-01 00:00:00'""");
        scope.Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.AcceptConfirmationLink(FixedLink));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await scope.Context.AppUsers.CountAsync());
        Assert.Equal(1, await scope.Context.PendingRegistrations.CountAsync()); // sweeper's job, not confirm's
    }

    [Fact]
    public async Task Confirm_Twice_Second404LoginStillWorks()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());
        await scope.Service.AcceptConfirmationLink(FixedLink);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.AcceptConfirmationLink(FixedLink));
        var pair = await scope.Service.LoginAsync(new LoginDto("pend@mail.com", "cat12345"));
        Assert.False(string.IsNullOrWhiteSpace(pair.JwtToken));
    }

    [Fact]
    public async Task ResendAfterExpiry_NewCycleConfirms()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "PendingRegistrations" SET "LinkExpiry" = '2000-01-01 00:00:00'""");
        scope.Context.ChangeTracker.Clear();

        // Expired cycle replaced by a fresh request (resend), then confirms.
        var pendingSvc = new PendingRegistrationService(scope.Context, TestConfig(), new StubRng());
        await pendingSvc.Delete(await scope.Context.PendingRegistrations.SingleAsync());
        await scope.Context.SaveChangesAsync();
        await scope.Service.RegisterAsPending(ValidRegister());
        await scope.Service.AcceptConfirmationLink(FixedLink);

        Assert.Equal(1, await scope.Context.AppUsers.CountAsync());
    }

    [Fact]
    public async Task Login_Unconfirmed_MatchesWrongPasswordMessage()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());

        // Pending is not an account: same neutral message as bad credentials (no oracle).
        var exPending = await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.LoginAsync(new LoginDto("pend@mail.com", "cat12345")));
        await scope.Service.AcceptConfirmationLink(FixedLink);
        var exWrongPass = await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.LoginAsync(new LoginDto("pend@mail.com", "wrongpass")));
        Assert.Equal(exPending.Message, exWrongPass.Message);
    }
}
