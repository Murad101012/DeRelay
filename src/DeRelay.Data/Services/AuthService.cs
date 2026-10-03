using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.DTOs.TokenPair;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using DeRelay.Core.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DeRelay.Data.Services;

public class AuthService(DeRelayDbContext deRelayDbContext
    ,IPersonService iPersonService
    ,IPasswordHasher<AppUser> passwordHasher
    ,SigningCredentials signingCredentials
    ,IAppUserService iAppUserService
    ,IRefreshTokenService iRefreshTokenService): IAuthService
{
    /// <summary>
    /// Register new person and return newly created ID from Persons table
    /// </summary>
    public async Task<int> RegisterAsync(RegisterDto dto)
    {
        if (await iAppUserService.CheckIfUserNameAvailableInAppUser(dto.UserName))
            throw new AlreadyExistsException($"{dto.UserName} is already in use, please use another username");
        
        await using var transaction = await deRelayDbContext.Database.BeginTransactionAsync();
        try
        {
            var personId = await iPersonService.CreatePersonAsync(dto.ToCreatePersonDto());
            //TODO: Learn why this gets null! as parameter
            var passwordHash = passwordHasher.HashPassword(null!, dto.Password);

            int newAppUserId = await iAppUserService.CreateAppUserAsync(dto.UserName, passwordHash, personId);
            await deRelayDbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return newAppUserId;
        }
        catch
        {
            await transaction.RollbackAsync();
            deRelayDbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<JwtAndRefreshTokensDto> LoginAsync(LoginDto dto)
    {
        var appUser = await iAppUserService.ReturnAppUserByUsername(dto.UserName);
        
        //Checking the user found  || Checking if the password is correct
        if (appUser == null || passwordHasher.VerifyHashedPassword(null!, appUser.PasswordHash, dto.Password) 
            == PasswordVerificationResult.Failed)
            throw new ValidationException("Wrong password or username, please try again");
        
        //Creating Refresh Token
        var returnNewRefreshTokenDto = await iRefreshTokenService.CreateRefreshTokenWithNewFamily(appUser.Id);

        return new JwtAndRefreshTokensDto
            (JwtToken: GenerateJwtToken(appUser), RefreshToken: returnNewRefreshTokenDto.RefreshToken);
    }
    
    public async Task DeleteAccountAsync(int appUserId)
    {
        /*In here we only remove from Person and not also add AppUser, because
         since AppUser has FK to Person, only removing Person will be enough that
         related AppUser entity to Person also will be removed*/
        await iPersonService.DeletePersonByIdAsync(
            (await iAppUserService.ReturnAppUserByIdAsync(appUserId)).PersonId);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task<JwtAndRefreshTokensDto> RefreshJwtAndRefreshTokensAsync
        (UserRefreshTokenDto dto)
    {
        var returnNewRefreshTokenDto = await iRefreshTokenService.RefreshTheRefreshTokenOfExistingFamily(dto);
        var refreshToken = await iRefreshTokenService.
            GetRefreshTokenObjectFromUserRefreshTokenString(returnNewRefreshTokenDto.RefreshToken);
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(refreshToken.AppUserId);

        return new JwtAndRefreshTokensDto
            (JwtToken: GenerateJwtToken(appUser), RefreshToken: returnNewRefreshTokenDto.RefreshToken);
    }

    private string GenerateJwtToken(AppUser appUser)
    {
        var claims = new List<Claim>
        {
            //NOTE: JwtRegisteredClaimNames are just returning strings
            new(JwtRegisteredClaimNames.Name, appUser.UserName),
            new(JwtRegisteredClaimNames.Sub, appUser.Id.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: signingCredentials);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}