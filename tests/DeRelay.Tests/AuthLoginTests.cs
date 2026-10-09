// Tests written by Muse Spark 1.3 AI
// Auth Phase-4 behaviors under stress (SQLite file DB per test):
// login mint shape, failure-message parity, expiry window,
// concurrent logins, account-deletion cascade.
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DeRelay.Core.DTOs.AppUser;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace DeRelay.Tests;

public class AuthLoginTests
{
    // Deterministic confirmation link: the stub RNG always issues this token,
    // so tests can walk register -> confirm -> login like a user with mail.
    private const string FixedConfirmLink = "TEST-CONFIRM-LINK";

    private sealed class StubRng : ITokenGenerator
    {
        public string GenerateAsBase64() => FixedConfirmLink;
        public string GenerateAsBase64Url() => FixedConfirmLink;
    }

    private static IConfiguration TestConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Confirmation:Key"] = "dGVzdC1vbmx5LWNvbmZpcm1hdGlvbi1rZXk=",
        }).Build();

    private sealed class StubEmailService : IEmailService
    {
        public List<(string Email, string Subject, string Message)> Sent { get; } = new();
        public Task SendEmailAsync(string toEmail, string subject, string message)
        {
            Sent.Add((toEmail, subject, message));
            return Task.CompletedTask;
        }
    }

    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IAuthService Service { get; }
        private readonly string _path;
        private readonly bool _ownsFile;
        private readonly DbConnection _connection;
        public Scope(string? existingPath = null)
        {
            _path = existingPath ?? Path.Combine(Path.GetTempPath(), $"derelay_al_{Guid.NewGuid():N}.db");
            _ownsFile = existingPath is null;
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
                new PasswordHasher<AppUser>(), creds, appUserService, new RefreshTokenService(Context), pendingService, new StubEmailService(), NullLogger<AuthService>.Instance);
        }
        public string DbPath => _path;
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
            if (_ownsFile) File.Delete(_path);
        }
    }

    private static RegisterDto ValidRegister(string email = "aysel@mail.com") =>
        new(email, "cat12345");

    private static async Task<int> RegisterConfirmedAsync(Scope scope, string email = "aysel@mail.com")
    {
        await scope.Service.RegisterAsPending(ValidRegister(email));
        await scope.Service.AcceptConfirmationLink(FixedConfirmLink);
        return (await scope.Context.AppUsers.SingleAsync(u => u.Email == email)).Id;
    }

    private static async Task LinkPerson(DeRelayDbContext ctx, int appUserId, string nickName)
    {
        var person = new Person("Aa", "Aa", nickName, Gender.Male, new DateTime(2000, 1, 1));
        ctx.Persons.Add(person);
        await ctx.SaveChangesAsync();
        ctx.Entry(await ctx.AppUsers.FindAsync(appUserId)).Property(u => u.PersonId).CurrentValue = person.Id;
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_Success_MintsParsableTokenWithSub()
    {
        await using var scope = new Scope();
        var userId = await RegisterConfirmedAsync(scope);

        var pair = await scope.Service.LoginAsync(new LoginDto("aysel@mail.com", "cat12345"));
        var jwt = pair.JwtToken;

        Assert.False(string.IsNullOrWhiteSpace(jwt));
        Assert.False(string.IsNullOrWhiteSpace(pair.RefreshToken));
        Assert.Equal(3, jwt.Split('.').Length); // header.payload.signature
        var read = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.Equal(userId.ToString(), read.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("aysel@mail.com", read.Claims.First(c => c.Type == "name").Value);
    }

    [Fact]
    public async Task Login_ExpiryWindow_Is30Minutes()
    {
        await using var scope = new Scope();
        await RegisterConfirmedAsync(scope);

        var pair = await scope.Service.LoginAsync(new LoginDto("aysel@mail.com", "cat12345"));
        var jwt = pair.JwtToken;

        var read = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        // Mint sets exp only (no nbf/iat), so ValidFrom defaults to MinValue:
        // assert the expiry lands ~30min in the future instead.
        Assert.True(read.ValidTo > DateTime.UtcNow.AddMinutes(29));
        Assert.True(read.ValidTo <= DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public async Task Login_UnknownUser_And_WrongPassword_SameMessage()
    {
        await using var scope = new Scope();
        await RegisterConfirmedAsync(scope);

        // Unknown email vs known email + wrong password must be indistinguishable.
        var exUnknown = await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.LoginAsync(new LoginDto("ghost@mail.com", "cat12345")));
        var exWrongPass = await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.LoginAsync(new LoginDto("aysel@mail.com", "wrongpass")));

        Assert.Equal(exUnknown.Message, exWrongPass.Message);
    }

    [Fact]
    public async Task Login_ConcurrentSameUser_AllSucceed()
    {
        await using var scope = new Scope();
        var userId = await RegisterConfirmedAsync(scope);

        // One scope (one DbContext) per task: DbContext is not thread-safe,
        // mirroring production where each request gets its own scoped context.
        // Owner scope stays alive (no dispose) so the shared file persists.
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var s = new Scope(scope.DbPath);
            return await s.Service.LoginAsync(new LoginDto("aysel@mail.com", "cat12345"));
        })).ToArray();
        var pairs = await Task.WhenAll(tasks);

        Assert.All(pairs, p => Assert.False(string.IsNullOrWhiteSpace(p.JwtToken)));
        foreach (var p in pairs)
            Assert.Equal(userId.ToString(),
                new JwtSecurityTokenHandler().ReadJwtToken(p.JwtToken).Claims.First(c => c.Type == "sub").Value);
    }

    [Fact]
    public async Task DeleteAccount_Success_WipesPersonAndLogin()
    {
        await using var scope = new Scope();
        var userId = await RegisterConfirmedAsync(scope);
        await LinkPerson(scope.Context, userId, "aysel");

        await scope.Service.DeleteAccountAsync(userId);

        Assert.Equal(0, await scope.Context.Persons.CountAsync());
        Assert.Equal(0, await scope.Context.AppUsers.CountAsync());
    }

    [Fact]
    public async Task DeleteAccount_Ghost_ThrowsAndChangesNothing()
    {
        await using var scope = new Scope();
        await RegisterConfirmedAsync(scope);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.DeleteAccountAsync(999));

        Assert.Equal(0, await scope.Context.Persons.CountAsync());
        Assert.Equal(1, await scope.Context.AppUsers.CountAsync());
    }
}
