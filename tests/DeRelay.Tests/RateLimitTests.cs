// Tests written by Muse Spark 1.3 AI
// Rate-limit battery (own API instance: budgets start full):
// login budget trips at 8th attempt, refresh budget trips at 5th,
// rejections wear the ProblemDetails envelope.
using System.Net;
using System.Text;
using System.Text.Json;

namespace DeRelay.Tests;

public class RateLimitTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static StringContent JsonBody(object o) =>
        new(JsonSerializer.Serialize(o, Json), Encoding.UTF8, "application/json");

    private static object RegisterBody(string u) => new
    {
        email = u + "@mail.com",
        password = "cat12345",
        passwordVerify = "cat12345",
    };

    private async Task<string> LoginRefreshToken(DeRelayWebFactory factory, HttpClient client, string user)
    {
        var reg = await client.PostAsync("/api/Auth/register", JsonBody(RegisterBody(user)));
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var link = factory.IssuedLinks.Last();
        var confirm = await client.GetAsync($"/api/Auth/confirm?key={link}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var login = await client.PostAsync("/api/Auth/login",
            JsonBody(new { email = user + "@mail.com", password = "cat12345" }));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("refreshToken").GetString()!;
    }

    [Fact]
    public async Task Login_8thAttemptInAMinute_429ProblemDetails()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var user = "rl" + Tag();
        var reg = await client.PostAsync("/api/Auth/register", JsonBody(RegisterBody(user)));
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var link = factory.IssuedLinks.Last();
        var confirm = await client.GetAsync($"/api/Auth/confirm?key={link}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        HttpStatusCode eighth = 0;
        for (var i = 0; i < 8; i++)
        {
            var login = await client.PostAsync("/api/Auth/login",
                JsonBody(new { email = user + "@mail.com", password = "cat12345" }));
            if (i < 7) Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            else eighth = login.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, eighth);
    }

    [Fact]
    public async Task Rejection_WearsProblemDetailsEnvelope()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var user = "rle" + Tag();
        var reg = await client.PostAsync("/api/Auth/register", JsonBody(RegisterBody(user)));
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var link = factory.IssuedLinks.Last();
        var confirm = await client.GetAsync($"/api/Auth/confirm?key={link}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        HttpResponseMessage? rejected = null;
        for (var i = 0; i < 8 && rejected is null; i++)
        {
            var login = await client.PostAsync("/api/Auth/login",
                JsonBody(new { email = user + "@mail.com", password = "cat12345" }));
            if (login.StatusCode == HttpStatusCode.TooManyRequests)
                rejected = login;
        }

        Assert.NotNull(rejected);
        using var doc = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(429, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Too Many Requests", doc.RootElement.GetProperty("title").GetString());
        // NOTE: no Retry-After assert — the fixed-window limiter does not emit one;
        // clients back off on 429 + envelope instead.
    }

    [Fact]
    public async Task Refresh_5thCallInAMinute_429()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var token = await LoginRefreshToken(factory, client, "rr" + Tag());

        HttpStatusCode fifth = 0;
        for (var i = 0; i < 5; i++)
        {
            // Each rotation consumes its input: chain the live token forward.
            var res = await client.PostAsync("/api/Auth/refresh",
                JsonBody(new { refreshToken = token }));
            if (i < 4)
            {
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                token = doc.RootElement.GetProperty("refreshToken").GetString()!;
            }
            else fifth = res.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, fifth);
    }

    [Fact]
    public async Task Refresh_BudgetResets_AfterWindow()
    {
        using var factory = new DeRelayWebFactory();
        var client = factory.CreateClient();
        var token = await LoginRefreshToken(factory, client, "rw" + Tag());

        for (var i = 0; i < 4; i++)
        {
            var res = await client.PostAsync("/api/Auth/refresh",
                JsonBody(new { refreshToken = token }));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            token = doc.RootElement.GetProperty("refreshToken").GetString()!;
        }
        var rejected = await client.PostAsync("/api/Auth/refresh",
            JsonBody(new { refreshToken = token }));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        // Fixed window is 1 minute from the first call: outlive it for real.
        // Slow on purpose — a limiter that never resets is a lockout bug,
        // and only wall-clock proves recovery.
        await Task.Delay(TimeSpan.FromSeconds(61));

        var recovered = await client.PostAsync("/api/Auth/refresh",
            JsonBody(new { refreshToken = token }));
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }
}
