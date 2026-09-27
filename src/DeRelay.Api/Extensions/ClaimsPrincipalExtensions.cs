using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeRelay.Core.Constants;
using DeRelay.Core.Exceptions;

namespace DeRelay.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetAppUserId(this ClaimsPrincipal user)
    {
        if (!int.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            || id < PersonConstraints.PersonIdMin)
            throw new ValidationException("Token carries no usable identity.");
        return id;

    }

}