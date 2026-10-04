using System.Text.Json;

namespace DuplicateProperties.Api;

/// <summary>
/// A STAND-IN for whatever reads the body before your API does: an API gateway
/// rule, a web application firewall, a signature or spend check, a logging
/// proxy. It is NOT a real product, and it does not claim any real product
/// behaves this way. All it does is keep the FIRST "amount" it meets, which is
/// one of the two reasonable choices a hand-written reader makes, and the one
/// that disagrees with System.Text.Json.
///
/// Written with Utf8JsonReader so the first-wins behaviour is visible in the
/// code instead of hidden behind a library's own duplicate policy.
/// </summary>
public static class FirstWinsAmountReader
{
    /// <summary>
    /// The first top-level "amount" in <paramref name="json"/>, or null if the
    /// body has none. Case-sensitive, top level only: enough for the demo.
    /// </summary>
    public static decimal? ReadAmount(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        var depth = 0;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    depth--;
                    break;
                case JsonTokenType.PropertyName when depth == 1 && reader.ValueTextEquals("amount"):
                    reader.Read();
                    // The first one wins: we return instead of carrying on.
                    return reader.TokenType == JsonTokenType.Number ? reader.GetDecimal() : null;
            }
        }

        return null;
    }
}
