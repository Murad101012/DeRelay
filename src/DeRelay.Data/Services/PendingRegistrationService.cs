using System.Security.Cryptography;
using System.Text;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DeRelay.Data.Services;

public class PendingRegistrationService(
    DeRelayDbContext deRelayDbContext,
    IConfiguration iConfiguration,
    ITokenGenerator iTokenGenerator): IPendingRegistrationService
{
    private readonly string _secretKey = iConfiguration["Confirmation:Key"] ?? 
                               throw new InvalidOperationException("Confirmation:Key is missing");
    
    public async Task<string> Create(string email, string hashedPassword)
    {
        var newPendingRegistration = new PendingRegistration(email, hashedPassword);
        var confirmationLink = iTokenGenerator.GenerateAsBase64Url();
        newPendingRegistration.Hash = HashConfirmationLink(confirmationLink);
        await deRelayDbContext.PendingRegistrations.AddAsync(newPendingRegistration);
        await deRelayDbContext.SaveChangesAsync();
        return confirmationLink;
    }

    public async Task Delete(PendingRegistration pendingRegistration)
    {
        deRelayDbContext.Remove(pendingRegistration);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task<PendingRegistration?> ReturnPendingRegistrationByEmail(string email)
    {
        return await deRelayDbContext.PendingRegistrations.
            FirstOrDefaultAsync(p => p.Email == email);
    }

    public async Task<PendingRegistration> ValidateConfirmationLink(string confirmationLink)
    {
        var confirmationLinkHash = HashConfirmationLink(confirmationLink);
        var pendingRegistration = await deRelayDbContext.PendingRegistrations.FirstOrDefaultAsync
            (p => p.Hash == confirmationLinkHash);
        if (pendingRegistration == null)
        {
            throw new NotFoundException("Registration not found. " +
                                        "It could be expired or never registered with the corresponding email." +
                                        " Please register again.");
        }
        return pendingRegistration.ConfirmationExpiry < DateTime.UtcNow ? 
            throw new UnauthorizedException("Link expired. Please register again.") : pendingRegistration;
    }

    public async Task<int> DeleteExpiredAllPendingRegistrations(CancellationToken stoppingToken)
    {
        return await deRelayDbContext.PendingRegistrations.
            Where(pg => pg.LinkExpiry < DateTime.UtcNow).
            ExecuteDeleteAsync(stoppingToken);
    }
    

    private string HashConfirmationLink(string confirmationLink)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(confirmationLink));
        return Convert.ToBase64String(hash);
    }
}