// Tests written by Muse Spark 1.3 AI
// Service-level Friendship flows (SQLite file DB per test):
// add / remove success + mistake paths, list reads.
using System.Text;
using DeRelay.Core.DTOs.Friendship;
using DeRelay.Core.DTOs.Person;
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

public class FriendshipServiceTests
{
    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IFriendshipService Service { get; }
        private readonly string _path;
        public Scope()
        {
            _path = Path.Combine(Path.GetTempPath(), $"derelay_fs_{Guid.NewGuid():N}.db");
            var connection = new SqliteConnection($"DataSource={_path}");
            connection.Open();
            var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
            Context = new DeRelayDbContext(options);
            Context.Database.EnsureCreated();
            var appUserService = new AppUserService(Context);
            var personService = new PersonService(Context, appUserService);
            Service = new FriendshipService(Context, personService, appUserService);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            File.Delete(_path);
        }
    }

    private static async Task<(int appUserA, int personA, int appUserB, int personB)> SeedTwoUsers(DeRelayDbContext ctx)
    {
        var personService = new PersonService(ctx, new AppUserService(ctx));
        var personA = await personService.CreatePersonAsync(
            new CreatePersonDto("Aa", "Aa", "useraa", Gender.Male, new DateTime(2000, 1, 1)));
        var personB = await personService.CreatePersonAsync(
            new CreatePersonDto("Bb", "Bb", "userbb", Gender.Female, new DateTime(2001, 2, 2)));
        var userA = new AppUser("loginA@mail.com", "HASH");
        var userB = new AppUser("loginB@mail.com", "HASH");
        ctx.AppUsers.AddRange(userA, userB);
        await ctx.SaveChangesAsync();
        ctx.Entry(userA).Property(u => u.PersonId).CurrentValue = personA;
        ctx.Entry(userB).Property(u => u.PersonId).CurrentValue = personB;
        await ctx.SaveChangesAsync();
        return (userA.Id, personA, userB.Id, personB);
    }

    [Fact]
    public async Task AddFriend_Success_StoresOrderedRow()
    {
        await using var scope = new Scope();
        var (appA, personA, _, personB) = await SeedTwoUsers(scope.Context);

        await scope.Service.AddFriendAsync(appA, personB);

        var row = await scope.Context.Friendships.SingleAsync();
        Assert.Equal(Math.Min(personA, personB), row.User1Id);
        Assert.Equal(Math.Max(personA, personB), row.User2Id);
    }

    [Fact]
    public async Task AddFriend_Self_ThrowsAndSavesNothing()
    {
        await using var scope = new Scope();
        var (appA, personA, _, _) = await SeedTwoUsers(scope.Context);

        await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.AddFriendAsync(appA, personA));

        Assert.Equal(0, await scope.Context.Friendships.CountAsync());
    }

    [Fact]
    public async Task AddFriend_Duplicate_ThrowsAndKeepsOneRow()
    {
        await using var scope = new Scope();
        var (appA, _, _, personB) = await SeedTwoUsers(scope.Context);

        await scope.Service.AddFriendAsync(appA, personB);
        await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            scope.Service.AddFriendAsync(appA, personB));

        Assert.Equal(1, await scope.Context.Friendships.CountAsync());
    }

    [Fact]
    public async Task AddFriend_UnknownPerson_ThrowsAndSavesNothing()
    {
        await using var scope = new Scope();
        var (appA, _, _, _) = await SeedTwoUsers(scope.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.AddFriendAsync(appA, 999));

        Assert.Equal(0, await scope.Context.Friendships.CountAsync());
    }

    [Fact]
    public async Task RemoveFriend_Success_RemovesRowKeepsPersons()
    {
        await using var scope = new Scope();
        var (appA, _, _, personB) = await SeedTwoUsers(scope.Context);
        await scope.Service.AddFriendAsync(appA, personB);

        await scope.Service.RemoveFriendAsync(appA, new RemoveFriendDto(personB));

        Assert.Equal(0, await scope.Context.Friendships.CountAsync());
        Assert.Equal(2, await scope.Context.Persons.CountAsync());
    }

    [Fact]
    public async Task RemoveFriend_Self_ThrowsAndKeepsRow()
    {
        await using var scope = new Scope();
        var (appA, personA, _, personB) = await SeedTwoUsers(scope.Context);
        await scope.Service.AddFriendAsync(appA, personB);

        await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.RemoveFriendAsync(appA, new RemoveFriendDto(personA)));

        Assert.Equal(1, await scope.Context.Friendships.CountAsync());
    }

    [Fact]
    public async Task RemoveFriend_NonFriend_Throws()
    {
        await using var scope = new Scope();
        var (appA, _, _, personB) = await SeedTwoUsers(scope.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.RemoveFriendAsync(appA, new RemoveFriendDto(personB)));
    }

    [Fact]
    public async Task GetAllFriends_ReturnsBothDirections()
    {
        await using var scope = new Scope();
        var (appA, _, appB, personB) = await SeedTwoUsers(scope.Context);
        var personC = await new PersonService(scope.Context, new AppUserService(scope.Context)).CreatePersonAsync(
            new CreatePersonDto("Cc", "Cc", "usercc", Gender.Male, new DateTime(2002, 3, 3)));
        await scope.Service.AddFriendAsync(appA, personB);
        await scope.Service.AddFriendAsync(appA, personC);

        var friendsOfA = await scope.Service.GetAllFriendsOfUserByIdAsync(appA);
        var friendsOfB = await scope.Service.GetAllFriendsOfUserByIdAsync(appB);

        Assert.Equal(2, friendsOfA.UserIds.Count);
        Assert.Contains(personB, friendsOfA.UserIds);
        Assert.Contains(personC, friendsOfA.UserIds);
        Assert.Single(friendsOfB.UserIds);
    }
}
