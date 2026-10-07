// Tests written by Muse Spark 1.3 AI
// Outside-attacker + real sign-in flows over HTTP (in-process API host):
// no/garbage/tampered/expired tokens vs locked endpoints, full
// register -> login -> bearer -> act round trips, short-lived tokens.
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using DeRelay.Data;

namespace DeRelay.Tests;

public class ControllerSecurityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static StringContent JsonBody(object o) =>
        new(JsonSerializer.Serialize(o, Json), Encoding.UTF8, "application/json");

    private static object RegisterBody(string u) => new
    {
        email = u + "@mail.com",
        password = "cat12345",
    };

    private async Task<string> RegisterAndLogin(HttpClient client, string user)
    {
        var reg = await client.PostAsync("/api/Auth/register", JsonBody(RegisterBody(user)));
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var login = await client.PostAsync("/api/Auth/login",
            JsonBody(new { email = user + "@mail.com", password = "cat12345" }));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        foreach (var name in new[] { "jwtToken", "JwtToken", "accessToken", "AccessToken", "token", "Token" })
            if (doc.RootElement.TryGetProperty(name, out var token) &&
                token.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(token.GetString()))
                return token.GetString()!;
        throw new InvalidOperationException("Login response did not contain a JWT access token.");
    }

    private static async Task CompleteProfile(HttpClient client, string tag)
    {
        var complete = await client.PostAsync("/api/Auth/complete-profile", JsonBody(new
        {
            firstName = "Aa",
            lastName = "Aa",
            nickName = "n" + tag,
            gender = "Male",
            dateOfBirth = "2000-01-01T00:00:00Z",
        }));
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
    }

    private static void UseBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private static string Mint(string sub, DateTime expires) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: new List<Claim> { new(JwtRegisteredClaimNames.Sub, sub) },
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DeRelayWebFactory.TestSecret)),
                SecurityAlgorithms.HmacSha256)));

    [Fact]
    public async Task SignInFlow_LockedEndpoint_200()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLogin(client, "sig" + Tag());
        UseBearer(client, token);
        await CompleteProfile(client, Tag());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Friendship")).StatusCode);
    }

    [Fact]
    public async Task NoToken_LockedEndpoint_401()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Friendship")).StatusCode);
    }

    [Fact]
    public async Task GarbageToken_LockedEndpoint_401()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        UseBearer(client, "garbage");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Friendship")).StatusCode);
    }

    [Fact]
    public async Task TamperedToken_LockedEndpoint_401()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLogin(client, "tmp" + Tag());
        var parts = token.Split('.');
        var sig = parts[2];
        var flipped = sig[..^1] + (sig[^1] == 'X' ? 'Y' : 'X');
        UseBearer(client, $"{parts[0]}.{parts[1]}.{flipped}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Friendship")).StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_LockedEndpoint_401()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLogin(client, "exp" + Tag());
        var sub = new JwtSecurityTokenHandler().ReadJwtToken(token)
            .Claims.First(c => c.Type == "sub").Value;
        UseBearer(client, Mint(sub, DateTime.UtcNow.AddMinutes(-5)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Friendship")).StatusCode);
    }

    [Fact]
    public async Task ShortLivedToken_10Seconds_200Now()
    {
        // The "very short time" case: 10s token must work inside its window.
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLogin(client, "s10" + Tag());
        var sub = new JwtSecurityTokenHandler().ReadJwtToken(token)
            .Claims.First(c => c.Type == "sub").Value;
        UseBearer(client, token);
        await CompleteProfile(client, Tag());
        UseBearer(client, Mint(sub, DateTime.UtcNow.AddSeconds(10)));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Friendship")).StatusCode);
    }

    [Fact]
    public async Task CrossUser_FriendRequestFlow_OverHttp()
    {
        using var factory = new DeRelayWebFactory();
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        var tokenA = await RegisterAndLogin(clientA, "frA" + Tag());
        var tokenB = await RegisterAndLogin(clientB, "frB" + Tag());
        UseBearer(clientA, tokenA);
        await CompleteProfile(clientA, Tag());
        UseBearer(clientB, tokenB);
        await CompleteProfile(clientB, Tag());
        UseBearer(clientA, tokenA);

        // Receiver person id read straight from the shared test DB (no id-oracle API needed).
        int receiverPersonId;
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<DeRelayDbContext>();
            receiverPersonId = await ctx.Persons
                .OrderByDescending(p => p.Id).Select(p => p.Id).FirstAsync();
        }
        var send = await clientA.PostAsync("/api/FriendRequest",
            JsonBody(new { receiverId = receiverPersonId }));
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUserName_409()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var user = "dup" + Tag();
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsync("/api/Auth/register", JsonBody(RegisterBody(user)))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync("/api/Auth/register", JsonBody(RegisterBody(user)))).StatusCode);
    }
}
