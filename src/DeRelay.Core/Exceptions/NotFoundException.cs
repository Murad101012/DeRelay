namespace DeRelay.Core.Exceptions;

/// <summary>
/// Thrown when can't find asked value in a list, database etc.
/// <para>
/// Status Code: 404 (Not Found)
/// </para>
/// </summary>
/// <example>
/// Scenario: If a user named: "Ivan" couldn't find in Dictionary/Database
/// <code>
/// throw new NotFoundException("Person with ID 42 was not found.");
/// </code>
/// </example>
public class NotFoundException(string message) : DomainException(message);