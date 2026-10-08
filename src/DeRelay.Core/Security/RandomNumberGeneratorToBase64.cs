using System.Security.Cryptography;
using DeRelay.Core.Interfaces;

namespace DeRelay.Core.Security;

public class RandomNumberGeneratorToBase64: IRandomNumberGeneratorToBase64
{
    public string GenerateRandomToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes);
    }
}