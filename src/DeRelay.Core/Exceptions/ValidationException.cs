namespace DeRelay.Core.Exceptions;

/// <summary>
/// Thrown when entity properties or inputs fail domain validation rules.
/// <para>
/// Status Code: 400 (Bad Request)
/// </para>
/// </summary>
/// <example>
/// Scenario: Attempting to set an invalid or empty first name.
/// <code>
/// throw new ValidationException("First name cannot be empty or longer than 50 characters.");
/// </code>
/// </example>
public class ValidationException(string message) : DomainException(message);