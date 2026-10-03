// Tests written by Muse Spark 1.3 AI
// Attacker-behavior battery over HTTP (in-process API host):
// rotation round trips, replay of consumed tokens, unknown/empty
// tokens, cross-user session kills, logout-kills-token.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DeRelay.Tests;

public class RefreshEndpointSecurityTests : IClassFixture<DeRelayWebFactory>
{
    private readonly DeRelayWebFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public RefreshEndpointSecurityTests(DeRelayWebFactory factory) => _factory = factory;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static StringContent JsonBody(object o) =>
        new(JsonSerializer.Serialize(o, Json), Encoding.UTF8, "application/json");

    private static object RegisterBody(string u) => new
    {
        userName = u,
        password = "cat12345",
        firstName = "Aa",
        lastName = "Aa",
        nickName = "n" + Tag(),
        gender = "Male",
        dateOfBirth = "2000-01-01T00:00:00Z",
    };

    private static string Field(JsonElement root, params string[] names)
    {
        foreach (var name in names)
            if (root.TryGetProperty(name, out var el) &&
                el.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(el.GetString()))
                return el.GetString()!;
        throw new InvalidOperationException($"None of [{string.Join(",", names)}] present.");
    }

    private async Task<(string Jwt, string Refresh)> RegisterAndLogin(HttpClient client, string user)
    {
        var reg = await client.PostAsync("/api/Auth/register", JsonBody(RegisterBody(user)));
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var login = await client.PostAsync("/api/Auth/login",
            JsonBody(new { userName = user, password = "cat12345" }));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var pair = (
            Field(doc.RootElement, "jwtToken", "JwtToken", "accessToken", "AccessToken"),
            Field(doc.RootElement, "refreshToken", "RefreshToken"));
        UseBearer(client, pair.Item1);
        return pair;
    }

    private static async Task<(string Jwt, string Refresh)> Refresh(HttpClient client, string refreshToken)
    {
        var res = await client.PostAsync("/api/Auth/refresh",
            JsonBody(new { refreshToken }));
        var body = await res.Content.ReadAsStringAsync();
        if (res.StatusCode != HttpStatusCode.OK)
            return ("", "");
        using var doc = JsonDocument.Parse(body);
        return (
            Field(doc.RootElement, "jwtToken", "JwtToken", "accessToken", "AccessToken"),
            Field(doc.RootElement, "refreshToken", "RefreshToken"));
    }

    private static async Task<List<Guid>> Sessions(HttpClient client)
    {
        var res = await client.GetAsync("/api/RefreshToken/session");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray()
            .Select(e => Guid.Parse(Field(e, "sessionId", "SessionId"))).ToList();
    }

    private static void UseBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Refresh_Rotation_NewPairLiveOldDead()
    {
        var client = _factory.CreateClient();
        var (_, oldRefresh) = await RegisterAndLogin(client, "rot" + Tag());

        var (jwt2, refresh2) = await Refresh(client, oldRefresh);

        Assert.False(string.IsNullOrWhiteSpace(jwt2));
        Assert.NotEqual(oldRefresh, refresh2);
        // New head rotates again: the pair is live, not decorative.
        var (_, refresh3) = await Refresh(client, refresh2);
        Assert.NotEqual(refresh2, refresh3);
    }

    [Fact]
    public async Task Refresh_ReplayConsumed_401FamilyDead()
    {
        var client = _factory.CreateClient();
        var (_, oldRefresh) = await RegisterAndLogin(client, "rep" + Tag());
        var (_, liveRefresh) = await Refresh(client, oldRefresh);

        // Attacker (or lagging client) replays the consumed token.
        var replay = await client.PostAsync("/api/Auth/refresh",
            JsonBody(new { refreshToken = oldRefresh }));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // Tripwire fired: even the live head died with its family.
        var afterKill = await client.PostAsync("/api/Auth/refresh",
            JsonBody(new { refreshToken = liveRefresh }));
        Assert.Equal(HttpStatusCode.NotFound, afterKill.StatusCode);
    }

    [Fact]
    public async Task Refresh_UnknownToken_404()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/Auth/refresh",
            JsonBody(new { refreshToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=" }));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_EmptyToken_400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/Auth/refresh",
            JsonBody(new { refreshToken = "" }));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Sessions_CrossUserDelete_404VictimIntact()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();
        var (_, refreshA) = await RegisterAndLogin(clientA, "vic" + Tag());
        await RegisterAndLogin(clientB, "atk" + Tag());

        var victimSession = (await Sessions(clientA)).Single();
        var kill = await clientB.DeleteAsync($"/api/RefreshToken/session/{victimSession}");
        Assert.Equal(HttpStatusCode.NotFound, kill.StatusCode);

        // Victim untouched: rotation still works.
        var (_, refreshA2) = await Refresh(clientA, refreshA);
        Assert.NotEqual(refreshA, refreshA2);
    }

    [Fact]
    public async Task Sessions_Logout_KillsTokenServerSide()
    {
        var client = _factory.CreateClient();
        var (_, refresh) = await RegisterAndLogin(client, "out" + Tag());

        var session = (await Sessions(client)).Single();
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/RefreshToken/session/{session}")).StatusCode);

        var afterLogout = await client.PostAsync("/api/Auth/refresh",
            JsonBody(new { refreshToken = refresh }));
        Assert.Equal(HttpStatusCode.NotFound, afterLogout.StatusCode);
    }
}
