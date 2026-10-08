namespace DeRelay.Core.Interfaces;

public interface IRandomNumberGeneratorToBase64
{
    /// <summary>
    /// Returns a token that BASE64 format
    /// </summary>
    /// <remarks>It creates a random number sequence using <see cref="System.Security.Cryptography.RandomNumberGenerator"/>
    /// and converts to BASE64 before returning</remarks>
    public string GenerateRandomToken();
}