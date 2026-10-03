namespace DeRelay.Core.Exceptions;

/// <summary>
/// Thrown when the presented credentials are expired, revoked or otherwise no longer acceptable,
/// and the client must authenticate again.
/// <para>
/// Status Code: 401 (Unauthorized)
/// </para>
/// </summary>
/// <example>
/// Scenario: Trying to rotate with a refresh token whose family already expired.
/// <code>
/// throw new UnauthorizedException("Session expired, please login again.");
/// </code>
/// </example>
public class UnauthorizedException(string message) : DomainException(message);
