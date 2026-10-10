using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using DeRelay.Core.Interfaces;


namespace DeRelay.Core.Security;

public class TokenGenerator: ITokenGenerator
{
    public string GenerateAsBase64()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes);
    }

    public string GenerateAsBase64Url()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url.EncodeToString(randomBytes);
    }
}