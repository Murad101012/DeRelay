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
using DeRelay.Data;
using DeRelay.Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DeRelay.Tests;

public class AuthLoginTests
{
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
            Service = new AuthService(Context, personService,
                new PasswordHasher<AppUser>(), creds, appUserService);
        }
        public string DbPath => _path;
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
            if (_ownsFile) File.Delete(_path);
        }
    }

    private static RegisterDto ValidRegister(string userName = "aysel97") =>
        new(userName, "cat12345", "Aysel", "Mammadova", "aysel", Gender.Female, new DateTime(2000, 1, 1));

    [Fact]
    public async Task Login_Success_MintsParsableTokenWithSub()
    {
        await using var scope = new Scope();
        var userId = await scope.Service.RegisterAsync(ValidRegister());

        var jwt = await scope.Service.LoginAsync(new LoginDto("aysel97", "cat12345"));

        Assert.False(string.IsNullOrWhiteSpace(jwt));
        Assert.Equal(3, jwt.Split('.').Length); // header.payload.signature
        var read = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.Equal(userId.ToString(), read.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("aysel97", read.Claims.First(c => c.Type == "name").Value);
    }

    [Fact]
    public async Task Login_ExpiryWindow_Is30Minutes()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsync(ValidRegister());

        var jwt = await scope.Service.LoginAsync(new LoginDto("aysel97", "cat12345"));

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
        await scope.Service.RegisterAsync(ValidRegister());

        // Unknown username vs known username + wrong password must be indistinguishable.
        var exUnknown = await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.LoginAsync(new LoginDto("ghost", "cat12345")));
        var exWrongPass = await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.LoginAsync(new LoginDto("aysel97", "wrongpass")));

        Assert.Equal(exUnknown.Message, exWrongPass.Message);
    }

    [Fact]
    public async Task Login_ConcurrentSameUser_AllSucceed()
    {
        await using var scope = new Scope();
        var userId = await scope.Service.RegisterAsync(ValidRegister());

        // One scope (one DbContext) per task: DbContext is not thread-safe,
        // mirroring production where each request gets its own scoped context.
        // Owner scope stays alive (no dispose) so the shared file persists.
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var s = new Scope(scope.DbPath);
            return await s.Service.LoginAsync(new LoginDto("aysel97", "cat12345"));
        })).ToArray();
        var tokens = await Task.WhenAll(tasks);

        Assert.All(tokens, t => Assert.False(string.IsNullOrWhiteSpace(t)));
        foreach (var t in tokens)
            Assert.Equal(userId.ToString(),
                new JwtSecurityTokenHandler().ReadJwtToken(t).Claims.First(c => c.Type == "sub").Value);
    }

    [Fact]
    public async Task DeleteAccount_Success_WipesPersonAndLogin()
    {
        await using var scope = new Scope();
        var userId = await scope.Service.RegisterAsync(ValidRegister());

        await scope.Service.DeleteAccountAsync(userId);

        Assert.Equal(0, await scope.Context.Persons.CountAsync());
        Assert.Equal(0, await scope.Context.AppUsers.CountAsync());
    }

    [Fact]
    public async Task DeleteAccount_Ghost_ThrowsAndChangesNothing()
    {
        await using var scope = new Scope();
        await scope.Service.RegisterAsync(ValidRegister());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.DeleteAccountAsync(999));

        Assert.Equal(1, await scope.Context.Persons.CountAsync());
        Assert.Equal(1, await scope.Context.AppUsers.CountAsync());
    }
}
