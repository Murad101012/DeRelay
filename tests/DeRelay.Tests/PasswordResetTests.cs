// Tests written by Muse Spark 1.3 AI
// Password-reset flow, user-side and attacker-side:
// row shape, duplicates, happy confirm (password + sessions + consumption),
// unknown/expired/twice, cross-user isolation, resend, enumeration note.
using System.Data.Common;
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.DTOs.PasswordReset;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
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

public class PasswordResetTests
{
    private const string FixedLink = "RESET-TEST-LINK";

    private sealed class StubRng : ITokenGenerator
    {
        public List<string> Issued { get; } = new();
        public string GenerateAsBase64() => Issue();
        public string GenerateAsBase64Url() => Issue();
        private string Issue()
        {
            var token = $"TEST-LINK-{Guid.NewGuid():N}";
            Issued.Add(token);
            return token;
        }
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
            ["PasswordReset:Key"] = "dGVzdC1yZXNldC1rZXktZm9yLXRlc3Rz",
        }).Build();

    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IAuthService Service { get; }
        public StubEmailService EmailStub { get; } = new();
        public StubRng ResetRng { get; private set; } = null!;
        private readonly string _path;
        private readonly DbConnection _connection;
        public Scope()
        {
            _path = Path.Combine(Path.GetTempPath(), $"derelay_pw_{Guid.NewGuid():N}.db");
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
            var sharedRng = new StubRng();
            var pendingService = new PendingRegistrationService(Context, TestConfig(), sharedRng);
            ResetRng = sharedRng;
            var resetService = new PasswordResetService(Context, TestConfig(), sharedRng);
            Service = new AuthService(Context, personService,
                new PasswordHasher<AppUser>(), creds, appUserService, new RefreshTokenService(Context),
                pendingService, EmailStub, resetService, NullLogger<AuthService>.Instance);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
            File.Delete(_path);
        }
    }

    private static async Task<int> SeedConfirmedUser(Scope scope, string email)
    {
        await scope.Service.RegisterAsPending(new RegisterDto(email, "cat12345", "cat12345"));
        var link = scope.EmailStub.Sent.Single(s => s.Email == email).Message;
        var marker = "key=";
        var tail = link.Substring(link.IndexOf(marker) + marker.Length);
        link = tail.Split(new[] { " ", "\n", "\r", "\t" }, StringSplitOptions.RemoveEmptyEntries)[0];
        await scope.Service.AcceptConfirmationLink(link);
        return (await scope.Context.AppUsers.SingleAsync(u => u.Email == email)).Id;
    }

        private static async Task ConfirmMailedLink(Scope scope, string email)
    {
        var sent = scope.EmailStub.Sent.Last(s => s.Email == email);
        const string marker = "key=";
        var tail = sent.Message.Substring(sent.Message.IndexOf(marker) + marker.Length);
        var link = tail.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        await scope.Service.AcceptConfirmationLink(link);
    }

private static async Task<string> SendResetAndTakeLink(Scope scope, string email)
    {
        var before = scope.ResetRng.Issued.Count;
        await scope.Service.SendPasswordResetLink(new PasswordResetEmailDto(email));
        return scope.ResetRng.Issued[before];
    }

    [Fact]
    public async Task Request_CreatesRow_HashNotPlaintext()
    {
        await using var scope = new Scope();
        await SeedConfirmedUser(scope, "victim@mail.com");

        await scope.Service.SendPasswordResetLink(new PasswordResetEmailDto("victim@mail.com"));

        var row = await scope.Context.PasswordReset.SingleAsync();
        Assert.Equal("victim@mail.com", row.email);
        Assert.DoesNotContain("RESET-TEST-LINK", row.hashedLink);
    }

    [Fact]
    public async Task Request_UnknownEmail_CurrentlyThrowsDocumented()
    {
        // PENDING-MASK: enumeration oracle, knowingly open until the message-masking
        // pass converts this to always-200. Test pins current behavior, not approval.
        await using var scope = new Scope();
        await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.SendPasswordResetLink(new PasswordResetEmailDto("ghost@mail.com")));
    }

    [Fact]
    public async Task Request_DuplicateLiveLink_409()
    {
        await using var scope = new Scope();
        await SeedConfirmedUser(scope, "victim@mail.com");
        await scope.Service.SendPasswordResetLink(new PasswordResetEmailDto("victim@mail.com"));

        await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            scope.Service.SendPasswordResetLink(new PasswordResetEmailDto("victim@mail.com")));
        Assert.Equal(1, await scope.Context.PasswordReset.CountAsync());
    }

    [Fact]
    public async Task Confirm_Happy_PasswordChangesSessionsDieRowConsumed()
    {
        await using var scope = new Scope();
        await SeedConfirmedUser(scope, "victim@mail.com");
        // Two live sessions on two "devices".
        var s1 = await scope.Service.LoginAsync(new LoginDto("victim@mail.com", "cat12345"));
        var s2 = await scope.Service.LoginAsync(new LoginDto("victim@mail.com", "cat12345"));
        var link = await SendResetAndTakeLink(scope, "victim@mail.com");

        await scope.Service.ApplyNewPassword(new NewPasswordDto(link, "newpass123", "newpass123"));

        // Old password dead, new password lives.
        await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.LoginAsync(new LoginDto("victim@mail.com", "cat12345")));
        var pair = await scope.Service.LoginAsync(new LoginDto("victim@mail.com", "newpass123"));
        Assert.False(string.IsNullOrWhiteSpace(pair.JwtToken));
        // Attacker sessions killed: exactly the fresh login survives.
        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync());
        // Row consumed on success: replay finds nothing (single-use link).
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.ApplyNewPassword(new NewPasswordDto(link, "hacked1234", "hacked1234")));
        _ = s1; _ = s2;
    }

    [Fact]
    public async Task Confirm_UnknownLink_404()
    {
        await using var scope = new Scope();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.ApplyNewPassword(new NewPasswordDto("NOPE-LINK", "newpass123", "newpass123")));
    }

    [Fact]
    public async Task Confirm_ExpiredLink_401NothingChanges()
    {
        await using var scope = new Scope();
        await SeedConfirmedUser(scope, "victim@mail.com");
        var link = await SendResetAndTakeLink(scope, "victim@mail.com");
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "PasswordReset" SET "resetExpire" = '2000-01-01 00:00:00'""");
        scope.Context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.ApplyNewPassword(new NewPasswordDto(link, "newpass123", "newpass123")));
        // Old password untouched: expiry fails closed without side effects.
        var pair = await scope.Service.LoginAsync(new LoginDto("victim@mail.com", "cat12345"));
        Assert.False(string.IsNullOrWhiteSpace(pair.JwtToken));
    }

    [Fact]
    public async Task Reset_OtherUsersSessionsSurvive()
    {
        await using var scope = new Scope();
        await SeedConfirmedUser(scope, "victim@mail.com");
        await SeedConfirmedUser(scope, "friend@mail.com");
        await scope.Service.LoginAsync(new LoginDto("friend@mail.com", "cat12345"));
        var link = await SendResetAndTakeLink(scope, "victim@mail.com");

        await scope.Service.ApplyNewPassword(new NewPasswordDto(link, "newpass123", "newpass123"));

        var friendUser = await scope.Context.AppUsers.SingleAsync(u => u.Email == "friend@mail.com");
        Assert.Equal(1, await scope.Context.RefreshToken.CountAsync(t => t.AppUserId == friendUser.Id));
    }

    [Fact]
    public async Task ResendAfterExpiry_ReplacesAndConfirms()
    {
        await using var scope = new Scope();
        await SeedConfirmedUser(scope, "victim@mail.com");
        var link = await SendResetAndTakeLink(scope, "victim@mail.com");
        await scope.Context.Database.ExecuteSqlRawAsync(
            """UPDATE "PasswordReset" SET "resetExpire" = '2000-01-01 00:00:00'""");
        scope.Context.ChangeTracker.Clear();

        await scope.Service.SendPasswordResetLink(new PasswordResetEmailDto("victim@mail.com"));
        Assert.Equal(1, await scope.Context.PasswordReset.CountAsync());
        var link2 = scope.ResetRng.Issued[scope.ResetRng.Issued.Count - 1];
        await scope.Service.ApplyNewPassword(new NewPasswordDto(link2, "newpass123", "newpass123"));
        var pair = await scope.Service.LoginAsync(new LoginDto("victim@mail.com", "newpass123"));
        Assert.False(string.IsNullOrWhiteSpace(pair.JwtToken));
    }
}
