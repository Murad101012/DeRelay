// Tests written by Muse Spark 1.3 AI
// ClaimsPrincipal.GetAppUserId helper: valid extraction + all rejection paths.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeRelay.Api.Extensions;
using DeRelay.Core.Exceptions;

namespace DeRelay.Tests;

public class GetAppUserIdTests
{
    private static ClaimsPrincipal PrincipalWithSub(string? sub) =>
        new(new ClaimsIdentity(sub is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(JwtRegisteredClaimNames.Sub, sub) }));

    [Fact]
    public void ValidSub_ReturnsId()
    {
        Assert.Equal(9, PrincipalWithSub("9").GetAppUserId());
    }

    [Fact]
    public void MissingSub_Throws()
    {
        Assert.Throws<ValidationException>(() => PrincipalWithSub(null).GetAppUserId());
    }

    [Fact]
    public void NonNumericSub_Throws()
    {
        Assert.Throws<ValidationException>(() => PrincipalWithSub("abc").GetAppUserId());
    }

    [Fact]
    public void ZeroAndNegativeSub_Throw()
    {
        Assert.Throws<ValidationException>(() => PrincipalWithSub("0").GetAppUserId());
        Assert.Throws<ValidationException>(() => PrincipalWithSub("-5").GetAppUserId());
    }
}
