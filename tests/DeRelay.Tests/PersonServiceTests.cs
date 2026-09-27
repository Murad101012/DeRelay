// Tests written by Muse Spark 1.3 AI
// Service-level Person flows (SQLite file DB per test):
// self-service CRUD via appUserId + mistake paths.
using DeRelay.Core.DTOs.Person;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using DeRelay.Data;
using DeRelay.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Tests;

public class PersonServiceTests
{
    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IPersonService Service { get; }
        private readonly string _path;
        public Scope()
        {
            _path = Path.Combine(Path.GetTempPath(), $"derelay_ps_{Guid.NewGuid():N}.db");
            var connection = new SqliteConnection($"DataSource={_path}");
            connection.Open();
            var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
            Context = new DeRelayDbContext(options);
            Context.Database.EnsureCreated();
            Service = new PersonService(Context, new AppUserService(Context));
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            File.Delete(_path);
        }
    }

    private static async Task<(int appUserId, int personId)> SeedUser(
        DeRelayDbContext ctx, string userName = "aysel97", string nickName = "aysel")
    {
        var personService = new PersonService(ctx, new AppUserService(ctx));
        var personId = await personService.CreatePersonAsync(
            new CreatePersonDto("Aysel", "Mammadova", nickName, Gender.Female, new DateTime(2000, 1, 1)));
        var appUser = new AppUser(userName, "HASH", personId);
        ctx.AppUsers.Add(appUser);
        await ctx.SaveChangesAsync();
        return (appUser.Id, personId);
    }

    [Fact]
    public async Task GetOwnProfile_ReturnsMappedDto()
    {
        await using var scope = new Scope();
        var (appUserId, personId) = await SeedUser(scope.Context);

        var dto = await scope.Service.GetPersonAsDtoByIdAsync(appUserId);

        Assert.Equal("Aysel", dto.FirstName);
        Assert.Equal("Mammadova", dto.LastName);
        Assert.Equal("aysel", dto.NickName);
        Assert.Equal(new DateTime(2000, 1, 1), dto.DateOfBirth);
    }

    [Fact]
    public async Task GetOwnProfile_GhostAppUser_Throws()
    {
        await using var scope = new Scope();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.GetPersonAsDtoByIdAsync(999));
    }

    [Fact]
    public async Task UpdateOwnProfile_ChangesFields()
    {
        await using var scope = new Scope();
        var (appUserId, _) = await SeedUser(scope.Context);

        await scope.Service.UpdatePersonByIdAsync(appUserId,
            new UpdatePersonDto("Sara", "Aliyeva", "sara", Gender.Female));

        var dto = await scope.Service.GetPersonAsDtoByIdAsync(appUserId);
        Assert.Equal("Sara", dto.FirstName);
        Assert.Equal("sara", dto.NickName);
    }

    [Fact]
    public async Task UpdateOwnProfile_GhostAppUser_Throws()
    {
        await using var scope = new Scope();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.UpdatePersonByIdAsync(999,
                new UpdatePersonDto("Sara", "Aliyeva", "sara", Gender.Female)));
    }

    [Fact]
    public async Task DeleteOwnProfile_RemovesPersonAndLoginCascades()
    {
        await using var scope = new Scope();
        var (appUserId, personId) = await SeedUser(scope.Context);

        await scope.Service.DeletePersonByIdAsync(appUserId);

        // Required FK defaults to cascade: profile delete wipes the login row too.
        Assert.Equal(0, await scope.Context.Persons.CountAsync());
        Assert.Equal(0, await scope.Context.AppUsers.CountAsync());
        Assert.False(await scope.Service.PersonExistsAsync(personId));
    }

    [Fact]
    public async Task DeleteOwnProfile_GhostAppUser_Throws()
    {
        await using var scope = new Scope();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.DeletePersonByIdAsync(999));
    }

    [Fact]
    public async Task PersonExists_TrueAndFalse()
    {
        await using var scope = new Scope();
        var (_, personId) = await SeedUser(scope.Context);

        Assert.True(await scope.Service.PersonExistsAsync(personId));
        Assert.False(await scope.Service.PersonExistsAsync(999));
    }
}
