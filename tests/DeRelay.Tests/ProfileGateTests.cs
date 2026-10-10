// Tests written by Muse Spark 1.3 AI
// Profile gate: every friend/person surface must refuse callers
// without a Person, before touching data.
using DeRelay.Core.DTOs.FriendRequest;
using DeRelay.Core.DTOs.Friendship;
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

public class ProfileGateTests
{
    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IFriendRequestService Requests { get; }
        public IFriendshipService Friends { get; }
        public IPersonService Persons { get; }
        private readonly string _path;
        public Scope()
        {
            _path = Path.Combine(Path.GetTempPath(), $"derelay_pg_{Guid.NewGuid():N}.db");
            var connection = new SqliteConnection($"DataSource={_path}");
            connection.Open();
            var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
            Context = new DeRelayDbContext(options);
            Context.Database.EnsureCreated();
            var appUsers = new AppUserService(Context);
            var persons = new PersonService(Context, appUsers);
            Requests = new FriendRequestService(Context,
                new FriendshipService(Context, persons, appUsers), persons, appUsers);
            Friends = new FriendshipService(Context, persons, appUsers);
            Persons = persons;
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            File.Delete(_path);
        }
    }

    private static async Task<(int appUserId, int personId)> SeedProfiled(DeRelayDbContext ctx, string email, string nick)
    {
        var person = new Person("Aa", "Aa", nick, Gender.Male, new DateTime(2000, 1, 1));
        ctx.Persons.Add(person);
        await ctx.SaveChangesAsync();
        var user = new AppUser(email, "HASH");
        ctx.AppUsers.Add(user);
        await ctx.SaveChangesAsync();
        ctx.Entry(user).Property(u => u.PersonId).CurrentValue = person.Id;
        await ctx.SaveChangesAsync();
        return (user.Id, person.Id);
    }

    private static async Task<int> SeedBare(DeRelayDbContext ctx, string email)
    {
        var user = new AppUser(email, "HASH");
        ctx.AppUsers.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<int> SeedRequest(Scope scope, int senderAppId, int receiverPersonId)
    {
        await scope.Requests.SendFriendRequestAsync(senderAppId, new SendFriendRequestDto(receiverPersonId));
        return receiverPersonId;
    }

    [Fact]
    public async Task Send_NoProfile_ThrowsAndSavesNothing()
    {
        await using var scope = new Scope();
        var ghost = await SeedBare(scope.Context, "ghost@mail.com");
        var (_, friendPerson) = await SeedProfiled(scope.Context, "friend@mail.com", "friendnick");

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Requests.SendFriendRequestAsync(ghost, new SendFriendRequestDto(friendPerson)));
        Assert.Contains("create one", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await scope.Context.FriendRequests.CountAsync());
    }

    [Fact]
    public async Task Accept_NoProfile_ThrowsAndKeepsRequest()
    {
        await using var scope = new Scope();
        var (sender, _) = await SeedProfiled(scope.Context, "sender@mail.com", "sendernick");
        var (_, receiverPerson) = await SeedProfiled(scope.Context, "receiver@mail.com", "receivernick");
        var ghost = await SeedBare(scope.Context, "ghost@mail.com");
        await SeedRequest(scope, sender, receiverPerson);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Requests.AcceptFriendRequestAsync(ghost, new AcceptFriendRequestDto(receiverPerson)));
        Assert.Contains("create one", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await scope.Context.FriendRequests.CountAsync());
    }

    [Fact]
    public async Task Decline_NoProfile_ThrowsAndKeepsRequest()
    {
        await using var scope = new Scope();
        var (sender, _) = await SeedProfiled(scope.Context, "sender@mail.com", "sendernick");
        var (_, receiverPerson) = await SeedProfiled(scope.Context, "receiver@mail.com", "receivernick");
        var ghost = await SeedBare(scope.Context, "ghost@mail.com");
        await SeedRequest(scope, sender, receiverPerson);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Requests.DeclineFriendRequestAsync(ghost, new DeclineFriendRequestDto(receiverPerson)));
        Assert.Contains("create one", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await scope.Context.FriendRequests.CountAsync());
    }

    [Fact]
    public async Task Lists_NoProfile_Throw()
    {
        await using var scope = new Scope();
        var ghost = await SeedBare(scope.Context, "ghost@mail.com");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Requests.GetAllFriendRequestOfUserReceivedByIdAsync(ghost));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Requests.GetAllFriendRequestOfUserSentByIdAsync(ghost));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Friends.GetAllFriendsOfUserByIdAsync(ghost));
    }

    [Fact]
    public async Task RemoveFriend_NoProfile_ThrowsAndKeepsRow()
    {
        await using var scope = new Scope();
        var (userA, _) = await SeedProfiled(scope.Context, "a@mail.com", "anick");
        var (_, personB) = await SeedProfiled(scope.Context, "b@mail.com", "bnick");
        var ghost = await SeedBare(scope.Context, "ghost@mail.com");
        await scope.Friends.AddFriendAsync(userA, personB);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Friends.RemoveFriendAsync(ghost, new RemoveFriendDto(personB)));
        Assert.Contains("create one", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await scope.Context.Friendships.CountAsync());
    }

    [Fact]
    public async Task ProfileReadUpdate_NoProfile_Throw()
    {
        await using var scope = new Scope();
        var ghost = await SeedBare(scope.Context, "ghost@mail.com");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Persons.GetPersonAsDtoByIdAsync(ghost));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Persons.UpdatePersonByIdAsync(ghost,
                new UpdatePersonDto("Aa", "Aa", "ghostnick", Gender.Male)));
    }
}
