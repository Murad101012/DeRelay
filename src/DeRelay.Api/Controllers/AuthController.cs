using DeRelay.Api.Extensions;
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.DTOs.TokenPair;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ValidationException = DeRelay.Core.Exceptions.ValidationException;

namespace DeRelay.Api.Controllers;

/// <summary>
/// Lifecycle of an Account and jwt handling (by <see cref="Data.Services.AuthService"/>)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IAuthService iAuthService,
    IValidator<RegisterDto> registerValidator,
    IValidator<LoginDto> loginValidator,
    IValidator<UserRefreshTokenDto> userRefreshTokenValidator): ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public async Task<ActionResult<int>> Register([FromBody] RegisterDto dto)
    {
        var validate = await registerValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);

        return StatusCode(201, await iAuthService.RegisterAsync(dto));
    }

    [HttpPost("login")]
    [EnableRateLimiting("login-tight")]
    public async Task<ActionResult<JwtAndRefreshTokensDto>> Login([FromBody] LoginDto dto)
    {
        var validate = await loginValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        return StatusCode(200, await iAuthService.LoginAsync(dto));
    }

    [Authorize]
    [EnableRateLimiting("after-login")]
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        await iAuthService.DeleteAccountAsync(User.GetAppUserId());
        return NoContent();
    }
    
    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<JwtAndRefreshTokensDto>> Refresh([FromBody] UserRefreshTokenDto dto)
    {
        var validate = await userRefreshTokenValidator.ValidateAsync(dto);
        return !validate.IsValid ? 
            throw new ValidationException(validate.Errors.First().ErrorMessage) : 
            StatusCode(200, await iAuthService.RefreshJwtAndRefreshTokensAsync(dto));
    }
}