using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using DeRelay.Core.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DeRelay.Data.Services;

public class PasswordResetService(
    DeRelayDbContext deRelayDbContext,
    IConfiguration iConfiguration,
    ITokenGenerator iTokenGenerator) : IPasswordResetService
{
    private string _secretKey = iConfiguration["PasswordReset:Key"] 
                                        ?? throw new InvalidOperationException("Secret Key couldn't found");

    public async Task<string> Create(string email)
    {
        await ValidateEmailCanBeAdd(email);
        var passwordReset = await deRelayDbContext.PasswordReset.
                                            FirstOrDefaultAsync(pr => pr.email == email);

        //Deleting old Password reset link if it's available
        if(passwordReset != null && passwordReset.resetExpire < DateTime.UtcNow)
        {
            await Delete(passwordReset);
        }
        var link = iTokenGenerator.GenerateAsBase64Url();
        var hashedLink = HashTheLink(link);
        var newPasswordReset = new PasswordReset(email, hashedLink);
        deRelayDbContext.PasswordReset.Add(newPasswordReset);
        await deRelayDbContext.SaveChangesAsync();
        return link;
    }

    public async Task Delete(PasswordReset passwordReset)
    {
        deRelayDbContext.PasswordReset.Remove(passwordReset);
        await deRelayDbContext.SaveChangesAsync();
    }
    
    public async Task<bool> CheckLink(string resetLink)
    {
        var hashedResetLink = HashTheLink(resetLink);
        var passwordReset =
            await deRelayDbContext.PasswordReset.FirstOrDefaultAsync(pr => pr.hashedLink == hashedResetLink);

        if (passwordReset == null || passwordReset.resetExpire <= DateTime.UtcNow)
            return false;
        return true;
    }

    public async Task<PasswordReset> ReturnByEmailAsync(string email)
    {
        return await deRelayDbContext.PasswordReset.FirstOrDefaultAsync(pr => pr.email == email)
        ?? throw new NotFoundException("Email couldn't found");
    }

    private async Task ValidateEmailCanBeAdd(string email)
    {
        var passwordReset = await deRelayDbContext.PasswordReset.FirstOrDefaultAsync(pr => pr.email == email);
        if(passwordReset != null && passwordReset.resetExpire > DateTime.UtcNow)
        //TODO: Change error description message
            throw new AlreadyExistsException("Email already exists and confirmation link hasn't expired yet." + 
                                             "Please check your inbox or spam box.");

    }

    private string HashTheLink(string resetLink)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(resetLink));
        return Base64Url.EncodeToString(hash);
    }

    public async Task<PasswordReset> ReturnByLinkAsync(string resetLink)
    {
        var hashedLink = HashTheLink(resetLink);
        var passwordReset = 
            await deRelayDbContext.PasswordReset.FirstOrDefaultAsync(pr => pr.hashedLink == hashedLink);
        
        if(passwordReset == null || passwordReset.resetExpire < DateTime.UtcNow)
            throw new NotFoundException("Link id invalid or expired, please request a new forget password");

        return passwordReset;    
    }

    public async Task<int> RemoveAllExpiredPasswordResetLinks(CancellationToken stoppingToken)
    {
        return await deRelayDbContext.PasswordReset.Where
            (pr => pr.linkExpire < DateTime.UtcNow).ExecuteDeleteAsync();
    }
}
