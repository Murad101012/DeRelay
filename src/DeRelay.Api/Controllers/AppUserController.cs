using DeRelay.Api.Extensions;
using DeRelay.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeRelay.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AppUserController(IAppUserService iAppUserService): ControllerBase
{
    
    [HttpGet("profile-creation-check")]
    [EnableRateLimiting("after-login")]
    public async Task<IActionResult> HasProfile()
    {
        var result = await iAppUserService.CheckHasProfile(User.GetAppUserId());
        return Ok(result);
    }
}