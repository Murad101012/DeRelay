using DeRelay.Api.Extensions;
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeRelay.Api.Controllers;

[EnableRateLimiting("after-login")]
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RefreshTokenController(IRefreshTokenService iRefreshTokenService): ControllerBase
{
    [HttpGet("session")]
    public async Task<ActionResult<List<ReturnSessionDto>>> GetSessions()
    {
        return await iRefreshTokenService.ReturnAllSessionsAsync(User.GetAppUserId());
    }
    
    [HttpDelete("session/{sessionId:guid}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId)
    {
        await iRefreshTokenService.DeleteSessionAsync(sessionId, User.GetAppUserId());
        return NoContent();
    }
}