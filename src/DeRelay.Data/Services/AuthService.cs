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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace DeRelay.Data.Services;

public class AuthService(DeRelayDbContext deRelayDbContext
    ,IPersonService iPersonService
    ,IPasswordHasher<AppUser> passwordHasher
    ,SigningCredentials signingCredentials
    ,IAppUserService iAppUserService
    ,IRefreshTokenService iRefreshTokenService
    ,IPendingRegistrationService iPendingRegistrationService
    ,IEmailService iEmailService
    ,ILogger<AuthService> iLogger): IAuthService
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
        string link;
        await using var transaction = await deRelayDbContext.Database.BeginTransactionAsync();
        if (pendingRegistration == null)
        {
            link = await iPendingRegistrationService.Create(dto.Email, passwordHash);
        }
        else if(pendingRegistration.ConfirmationExpiry < DateTime.UtcNow)
        {
            await iPendingRegistrationService.Delete(pendingRegistration);
            link = await iPendingRegistrationService.Create(dto.Email, passwordHash);
        }
        else
        {
            throw new AlreadyExistsException
                ("Email already waiting to be confirmed. Please check your inbox or spam box");
        }
        
        await deRelayDbContext.SaveChangesAsync();

        var message = $"Welcome to Dancing Line!\n\n" +
                      $"Confirm your account within 24 hours by opening this link:\n\n" +
                      $"https://derelay.sunnygameai.site/api/Auth/confirm?key={link}\n\n" +
                      $"If you didn't register, just ignore this mail.";

        try
        {
            await iEmailService.SendEmailAsync(dto.Email, "Confirm your Dancing Line account", message);
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            iLogger.LogError(e, "Error sending email");
            await transaction.RollbackAsync();
            throw;
        }
    }

    /*TODO: If user accidentally click twice at the very same time and
     both of them reach at the very same time to server, potentially second try cause 500 error in Database.
     It's because in first confirm time user clicked link first time, AppUser already created with that e-mail,
     and second attempt to write again to Database caught and throw error
     Since this error doesn't cause any problem as data integrity, it's postponed for now.*/
    public async Task AcceptConfirmationLink(string link)
    {
        var pendingRegistration = await iPendingRegistrationService.ValidateConfirmationLink(link);
        try
        {
            if (await iAppUserService.ReturnAppUserByEmail(pendingRegistration.Email) != null)
            {
                throw new AlreadyExistsException("Email already confirmed, please proceed to log in.");
            }
            await iAppUserService.CreateAppUserAsync(pendingRegistration.Email, pendingRegistration.HashedPassword);
            await deRelayDbContext.SaveChangesAsync();
        }
        catch (DbUpdateException e)
        {
            //NOTE: Learn about basic SqlState errors, exception to catch
            if (e.InnerException is PostgresException pg && pg.SqlState == "23505")
            {
                throw new AlreadyExistsException("Email already confirmed, please proceed to log in.");
            }
            iLogger.LogError(e, "Error when confirming the email");
            throw;
        }
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