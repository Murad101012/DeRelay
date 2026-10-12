using DeRelay.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeRelay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PasswordResetController(IPasswordResetService iPasswordResetService): ControllerBase
{
    [HttpGet("verify-password-reset-link")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> VerifyPasswordResetLink([FromQuery] string key)
    {
        if (!await iPasswordResetService.CheckLink(key))
        {
            Response.StatusCode = 404;
            return Content("Password reset link is invalid or expired. " +
                           "Please request a new password reset", "text/plain");
        }
        Response.StatusCode = 200;
        //TODO: This written by Muse Spark 1.3. Write yourself after Stage 7
        return Content($$"""
            <html><body>
            <form method="post" action="/api/Auth/apply-new-password"
                  onsubmit="var p1 = document.getElementById('p1').value; var err = document.getElementById('err'); if (p1.length < 8 || p1.length > 100) { err.innerText = 'Password must be between 8 and 100 characters.'; return false; } if (p1 !== document.getElementById('p2').value) { err.innerText = 'Passwords do not match.'; return false; } return true">
            <p id="err" style="color:#ff8080"></p>
            <input type="hidden" name="token" value="{{key}}">
            <input type="password" id="p1" name="password" placeholder="New password" required>
            <input type="password" id="p2" name="confirmPassword" placeholder="Confirm password" required>
            <button type="submit">Change password</button>
            </form></body></html>
            """, "text/html");
    }
}