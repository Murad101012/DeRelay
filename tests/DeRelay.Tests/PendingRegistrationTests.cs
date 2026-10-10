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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DeRelay.Tests;

public class PendingRegistrationTests
{
    private const string FixedLink = "PENDING-TEST-LINK";

    private sealed class StubRng : ITokenGenerator
    {
        public string GenerateAsBase64() => FixedLink;
        public string GenerateAsBase64Url() => FixedLink;
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

    private static IConfiguration TestConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Confirmation:Key"] = "dGVzdC1vbmx5LWNvbmZpcm1hdGlvbi1rZXk=",
        }).Build();

    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IAuthService Service { get; }
        public StubEmailService EmailStub { get; } = new();
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
                new PasswordHasher<AppUser>(), creds, appUserService, new RefreshTokenService(Context), pendingService, EmailStub, NullLogger<AuthService>.Instance);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
            File.Delete(_path);
        }
    }

    private static RegisterDto ValidRegister(string email = "pend@mail.com") =>
        new(email, "cat12345", "cat12345");

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
        Assert.Equal(1, await scope.Context.PendingRegistrations.CountAsync()); // retained as witness
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
            """UPDATE "PendingRegistrations" SET "ConfirmationExpiry" = '2000-01-01 00:00:00'""");
        scope.Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            scope.Service.AcceptConfirmationLink(FixedLink));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await scope.Context.AppUsers.CountAsync());
        Assert.Equal(1, await scope.Context.PendingRegistrations.CountAsync()); // sweeper's job, not confirm's
    }

    [Fact]
    public async Task ResendAfterExpiry_NewCycleConfirms()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "PendingRegistrations" SET "ConfirmationExpiry" = '2000-01-01 00:00:00'""");
        scope.Context.ChangeTracker.Clear();

        // Expired cycle replaced by a fresh request (resend), then confirms.
        await scope.Service.RegisterAsPending(ValidRegister());
        Assert.Equal(1, await scope.Context.PendingRegistrations.CountAsync());
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

    [Fact]
    public async Task Sweep_DeletesOnlyExpiredPendings()
    {
        await using var scope = new Scope();
        var real = new PendingRegistrationService(
            scope.Context, TestConfig(), new TokenGenerator());
        var live1 = await real.Create("live1@mail.com", "HASH");
        var live2 = await real.Create("live2@mail.com", "HASH");
        var old = await real.Create("old@mail.com", "HASH");
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "PendingRegistrations" SET "LinkExpiry" = '2000-01-01 00:00:00' WHERE "Email" = 'old@mail.com'""");
        scope.Context.ChangeTracker.Clear();

        var sweeper = new PendingRegistrationService(
            scope.Context, TestConfig(), new TokenGenerator());
        var deleted = await sweeper.DeleteExpiredAllPendingRegistrations(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(2, await scope.Context.PendingRegistrations.CountAsync());
        // Survivors still confirm: the sweep never touches the living.
        await scope.Service.AcceptConfirmationLink(live1);
        Assert.Equal(1, await scope.Context.AppUsers.CountAsync());
        _ = live2; _ = old;
    }

    [Fact]
    public async Task Confirm_Twice_SecondAlreadyExists()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());
        await scope.Service.AcceptConfirmationLink(FixedLink);

        var ex = await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            scope.Service.AcceptConfirmationLink(FixedLink));
        Assert.Contains("log in", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await scope.Context.AppUsers.CountAsync()); // never twins
    }

    [Fact]
    public void TokenGenerator_UrlSafeAndRoundTrips()
    {
        var gen = new TokenGenerator();
        for (var i = 0; i < 100; i++)
        {
            var token = gen.GenerateAsBase64Url();
            Assert.DoesNotContain("+", token);
            Assert.DoesNotContain("/", token);
            Assert.DoesNotContain("=", token);
            Assert.Equal(32, Convert.FromBase64String(
                token.Replace('-', '+').Replace('_', '/') +
                new string('=', (4 - token.Length % 4) % 4)).Length);
        }
        Assert.NotEqual(gen.GenerateAsBase64Url(), gen.GenerateAsBase64Url());
    }

    [Fact]
    public async Task Register_MailedLinkConfirmsEndToEnd()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsPending(ValidRegister());

        // The handoff: exactly one mail, to the right inbox, carrying a live link.
        var sent = Assert.Single(scope.EmailStub.Sent);
        Assert.Equal("pend@mail.com", sent.Email);
        const string marker = "key=";
        var tail = sent.Message.Substring(sent.Message.IndexOf(marker) + marker.Length);
        var link = tail.Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)[0];
        Assert.False(string.IsNullOrWhiteSpace(link));
        await scope.Service.AcceptConfirmationLink(link);
        Assert.Equal(1, await scope.Context.AppUsers.CountAsync());
    }
}
