using DeRelay.Api.Extensions;
using DeRelay.Core.DTOs.FriendRequest;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeRelay.Api.Controllers;

[EnableRateLimiting("after-login")]
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FriendRequestController(
    IFriendRequestService iFriendRequestService
    ,IValidator<SendFriendRequestDto> sendValidator
    ,IValidator<AcceptFriendRequestDto> acceptValidator
    ,IValidator<DeclineFriendRequestDto> deleteValidator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestDto dto)
    {
        //Validate
        var validate = await sendValidator.ValidateAsync(dto);
        if (!validate.IsValid) 
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await iFriendRequestService.SendFriendRequestAsync(User.GetAppUserId(), dto);
        return Created();
    }
    
    [HttpPost("accept")]
    public async Task<IActionResult> AcceptFriendRequest([FromBody] AcceptFriendRequestDto dto)
    {
        //Validate
        var validate = await acceptValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await iFriendRequestService.AcceptFriendRequestAsync(User.GetAppUserId(), dto);
        return Created();
    }

    [HttpDelete]
    public async Task<IActionResult> DeclineFriendRequest([FromBody] DeclineFriendRequestDto dto)
    {
        //Validate
        var validate = await deleteValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await iFriendRequestService.DeclineFriendRequestAsync(User.GetAppUserId(), dto);
        return NoContent();
    }
    
    [HttpGet("sent")]
    public async Task<ActionResult<ReturnPersonAllFriendRequestSentDto>> GetListOfSendFriendRequest()
    {
        return await iFriendRequestService.GetAllFriendRequestOfUserSentByIdAsync(User.GetAppUserId());
    }
    
    [HttpGet("receiver")]
    public async Task<ActionResult<ReturnPersonAllFriendRequestReceivedDto>> GetListOfReceivedFriendRequest()
    {
        return await iFriendRequestService.GetAllFriendRequestOfUserReceivedByIdAsync(User.GetAppUserId());
    }
}