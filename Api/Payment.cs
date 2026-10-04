// Deliberately in the global namespace. System.Text.Json puts the type's full
// name in the exception message it throws for a duplicate property, and the
// post quotes that message, so a short name keeps the quote readable.

/// <summary>
/// The body the endpoints bind. Deliberately boring: the demo is about which
/// value lands in <see cref="Amount"/> when the JSON carries two of them.
/// </summary>
public record Payment(decimal Amount, string To);
