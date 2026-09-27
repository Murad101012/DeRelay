// Tests written by Muse Spark 1.3 AI
// Service-level FriendRequest flows (SQLite file DB per test):
// send / accept / decline success + mistake paths, list reads.
using System.Text;
using DeRelay.Core.DTOs.FriendRequest;
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

public class FriendRequestServiceTests
{
    private sealed class Scope : IAsyncDisposable
    {
        public DeRelayDbContext Context { get; }
        public IFriendRequestService Service { get; }
        private readonly string _path;
        public Scope()
        {
            _path = Path.Combine(Path.GetTempPath(), $"derelay_fr_{Guid.NewGuid():N}.db");
            var connection = new SqliteConnection($"DataSource={_path}");
            connection.Open();
            var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
            Context = new DeRelayDbContext(options);
            Context.Database.EnsureCreated();
            var appUserService = new AppUserService(Context);
            var personService = new PersonService(Context, appUserService);
            Service = new FriendRequestService(Context,
                new FriendshipService(Context, personService, appUserService), personService, appUserService);
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
        var userA = new AppUser("loginA", "HASH", personA);
        var userB = new AppUser("loginB", "HASH", personB);
        ctx.AppUsers.AddRange(userA, userB);
        await ctx.SaveChangesAsync();
        return (userA.Id, personA, userB.Id, personB);
    }

    [Fact]
    public async Task Send_Success_CreatesRow()
    {
        await using var scope = new Scope();
        var (appA, personA, _, personB) = await SeedTwoUsers(scope.Context);

        await scope.Service.SendFriendRequestAsync(appA, new SendFriendRequestDto(personB));

        var row = await scope.Context.FriendRequests.SingleAsync();
        Assert.Equal(personA, row.SenderId);
        Assert.Equal(personB, row.ReceiverId);
    }

    [Fact]
    public async Task Send_SelfRequest_ThrowsAndSavesNothing()
    {
        await using var scope = new Scope();
        var (appA, personA, _, _) = await SeedTwoUsers(scope.Context);

        await Assert.ThrowsAsync<ValidationException>(() =>
            scope.Service.SendFriendRequestAsync(appA, new SendFriendRequestDto(personA)));

        Assert.Equal(0, await scope.Context.FriendRequests.CountAsync());
    }

    [Fact]
    public async Task Send_Duplicate_ThrowsAndKeepsOneRow()
    {
        await using var scope = new Scope();
        var (appA, _, _, personB) = await SeedTwoUsers(scope.Context);

        await scope.Service.SendFriendRequestAsync(appA, new SendFriendRequestDto(personB));
        await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            scope.Service.SendFriendRequestAsync(appA, new SendFriendRequestDto(personB)));

        Assert.Equal(1, await scope.Context.FriendRequests.CountAsync());
    }

    [Fact]
    public async Task Send_UnknownReceiver_ThrowsAndSavesNothing()
    {
        await using var scope = new Scope();
        var (appA, _, _, _) = await SeedTwoUsers(scope.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.SendFriendRequestAsync(appA, new SendFriendRequestDto(999)));

        Assert.Equal(0, await scope.Context.FriendRequests.CountAsync());
    }

    [Fact]
    public async Task Accept_Success_CreatesFriendshipRemovesRequest()
    {
        await using var scope = new Scope();
        var (appA, personA, _, personB) = await SeedTwoUsers(scope.Context);
        await scope.Service.SendFriendRequestAsync(appA, new SendFriendRequestDto(personB));

        await scope.Service.AcceptFriendRequestAsync(appA, new AcceptFriendRequestDto(personB));

        Assert.Equal(0, await scope.Context.FriendRequests.CountAsync());
        Assert.Equal(1, await scope.Context.Friendships.CountAsync());
        var friendship = await scope.Context.Friendships.SingleAsync();
        Assert.Equal(Math.Min(personA, personB), friendship.User1Id);
        Assert.Equal(Math.Max(personA, personB), friendship.User2Id);
    }

    [Fact]
    public async Task Accept_Missing_Throws()
    {
        await using var scope = new Scope();
        var (appA, _, _, personB) = await SeedTwoUsers(scope.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.AcceptFriendRequestAsync(appA, new AcceptFriendRequestDto(personB)));
    }

    [Fact]
    public async Task Decline_Success_RemovesRequestNoFriendship()
    {
        await using var scope = new Scope();
        var (appA, _, _, personB) = await SeedTwoUsers(scope.Context);
        await scope.Service.SendFriendRequestAsync(appA, new SendFriendRequestDto(personB));

        await scope.Service.DeclineFriendRequestAsync(appA, new DeclineFriendRequestDto(personB));

        Assert.Equal(0, await scope.Context.FriendRequests.CountAsync());
        Assert.Equal(0, await scope.Context.Friendships.CountAsync());
    }

    [Fact]
    public async Task Decline_Missing_Throws()
    {
        await using var scope = new Scope();
        var (appA, _, _, personB) = await SeedTwoUsers(scope.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            scope.Service.DeclineFriendRequestAsync(appA, new DeclineFriendRequestDto(personB)));
    }

    [Fact]
    public async Task Lists_SentAndReceived_MatchDirections()
    {
        await using var scope = new Scope();
        var (appA, personA, appB, personB) = await SeedTwoUsers(scope.Context);
        await scope.Service.SendFriendRequestAsync(appA, new SendFriendRequestDto(personB));

        var sent = await scope.Service.GetAllFriendRequestOfUserSentByIdAsync(appA);
        var received = await scope.Service.GetAllFriendRequestOfUserReceivedByIdAsync(appB);
        var sentByB = await scope.Service.GetAllFriendRequestOfUserSentByIdAsync(appB);

        Assert.Contains(personB, sent.UsersId);
        Assert.Contains(personA, received.UsersId);
        Assert.Empty(sentByB.UsersId);
    }
}
