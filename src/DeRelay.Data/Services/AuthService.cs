using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.DTOs.Person;
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.DTOs.TokenPair;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace DeRelay.Data.Services;

public class AuthService(DeRelayDbContext deRelayDbContext
    ,IPersonService iPersonService
    ,IPasswordHasher<AppUser> passwordHasher
    ,SigningCredentials signingCredentials
    ,IAppUserService iAppUserService
    ,IRefreshTokenService iRefreshTokenService
    ,IPendingRegistrationService iPendingRegistrationService): IAuthService
{
    /// <summary>
    /// Add new user into pending table until it confirmed the link, or it's expired
    /// </summary>
    public async Task RegisterAsPending(RegisterDto dto)
    {
        if (await iAppUserService.CheckIfEmailAvailableInAppUser(dto.Email))
            throw new AlreadyExistsException($"{dto.Email} is already in use");
        
        //TODO: Learn why this gets null! as parameter
        var passwordHash = passwordHasher.HashPassword(null!, dto.Password);
        
        var pendingRegistration = await iPendingRegistrationService.ReturnPendingRegistrationByEmail(dto.Email);
        if (pendingRegistration == null)
        {
            await iPendingRegistrationService.Create(dto.Email, passwordHash);
        }
        else if(pendingRegistration.ConfirmationExpiry < DateTime.UtcNow)
        {
            await iPendingRegistrationService.Delete(pendingRegistration);
            await iPendingRegistrationService.Create(dto.Email, passwordHash);
        }
        else
        {
            throw new AlreadyExistsException
                ("Email already waiting to be confirmed. Please check your inbox or spam box");
        }
        
        await deRelayDbContext.SaveChangesAsync();
    }

    /*TODO: If user accidentally click twice at the very same time and
     both of them reach at the very same time to server, potentially second try cause 500 error in Database.
     It's because in first confirm time user clicked link first time, AppUser already created with that e-mail,
     and second attempt to write again to Database caught and throw error
     Since this error doesn't cause any problem as data integrity, it's postponed for now.*/
    public async Task AcceptConfirmationLink(string link)
    {
        var pendingRegistration = await iPendingRegistrationService.ValidateConfirmationLink(link);
        // Twin request already won (or direct account exists): idempotent success,
        // never a duplicate user — the unique Email index stays the backstop.
        if (await iAppUserService.ReturnAppUserByEmail(pendingRegistration.Email) is not null)
        {
            await iPendingRegistrationService.Delete(pendingRegistration);
            await deRelayDbContext.SaveChangesAsync();
            return;
        }
        await iAppUserService.CreateAppUserAsync(pendingRegistration.Email, pendingRegistration.HashedPassword);
        await iPendingRegistrationService.Delete(pendingRegistration);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task<int> CompleteProfile(CreatePersonDto dto, int appUserId)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);
        if(appUser.CheckIfPersonIdExists())
            throw new AlreadyExistsException($"Profile already created for account that {appUser.Email}");
        var personId = await iPersonService.CreatePersonAsync(dto);
        appUser.SetPersonId(personId);
        await deRelayDbContext.SaveChangesAsync();
        return personId;
    }

    public async Task<JwtAndRefreshTokensDto> LoginAsync(LoginDto dto)
    {
        var appUser = await iAppUserService.ReturnAppUserByEmail(dto.Email);
        
        //Checking the user found  || Checking if the password is correct
        if (appUser == null || passwordHasher.VerifyHashedPassword(null!, appUser.PasswordHash, dto.Password) 
            == PasswordVerificationResult.Failed)
            throw new ValidationException("Wrong password or email, please try again");
        
        //Creating Refresh Token
        var returnNewRefreshTokenDto = await iRefreshTokenService.CreateRefreshTokenWithNewSession(appUser.Id);

        return new JwtAndRefreshTokensDto
            (JwtToken: GenerateJwtToken(appUser), RefreshToken: returnNewRefreshTokenDto.RefreshToken);
    }
    
    public async Task DeleteAccountAsync(int appUserId)
    {
        /*In here we only remove from Person and not also add AppUser, because
         since AppUser has FK to Person, only removing Person will be enough that
         related AppUser entity to Person also will be removed*/
        await iPersonService.DeletePersonByIdAsync(
            (await iAppUserService.ReturnAppUserByIdAsync(appUserId)).ValidatePersonIdAndReturn());
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task<JwtAndRefreshTokensDto> RefreshJwtAndRefreshTokensAsync
        (UserRefreshTokenDto dto)
    {
        var returnNewRefreshTokenDto = await iRefreshTokenService.RefreshTheRefreshTokenOfExistingSession(dto);
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
            new(JwtRegisteredClaimNames.Name, appUser.Email),
            new(JwtRegisteredClaimNames.Sub, appUser.Id.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: signingCredentials);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}