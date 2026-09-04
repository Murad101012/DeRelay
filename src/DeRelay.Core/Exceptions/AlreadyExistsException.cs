namespace DeRelay.Core.Exceptions;

/// <summary>
/// Thrown when creating an object/value that already exist in Dictionary(Key)/Database(Primary Key) etc.
/// where only accepts unique object/value
/// <para>
/// Status Code: 409 (Conflict)
/// </para>
/// </summary>
/// <example>
/// Scenario: Trying to add a key named "Room" in a Dictionary that already exist with same named key
/// <code>
/// throw new AlreadyExistsException("Can't create nickname named "DarkGreen", it's already exist.");
/// </code>
/// </example>
public class AlreadyExistsException(string message) : DomainException(message);