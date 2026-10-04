using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DuplicateProperties.Tests;

/// <summary>
/// Boots the Api project in-process. Nothing listens on a real port, so the
/// suite is deterministic and needs no free TCP port.
/// </summary>
public class ApiFactory(bool rejectDuplicates) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("RejectDuplicates", rejectDuplicates ? "true" : "false");

        // Keeps the run summary readable. A rejected body is an expected result
        // here, and ASP.NET Core logs the JsonException it caught while binding,
        // stack trace included, which would bury the test counts.
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore", "None");
    }
}

/// <summary>The app as it ships by default: duplicates are accepted.</summary>
public sealed class PermissiveApi() : ApiFactory(rejectDuplicates: false);

/// <summary>The same app with AllowDuplicateProperties = false.</summary>
public sealed class RejectingApi() : ApiFactory(rejectDuplicates: true);

public static class Bodies
{
    /// <summary>
    /// The body the whole demo turns on: "amount" twice, small then large.
    /// Written as a raw string because no serializer will produce it for you.
    /// </summary>
    public const string DuplicateAmount =
        """{"amount": 10, "to": "alice", "amount": 1000}""";

    /// <summary>The same body with the second copy removed.</summary>
    public const string SingleAmount =
        """{"amount": 10, "to": "alice"}""";

    /// <summary>PascalCase names, which is what the Strict preset expects.</summary>
    public const string DuplicateAmountPascalCase =
        """{"Amount": 10, "To": "alice", "Amount": 1000}""";

    public const string SingleAmountPascalCase =
        """{"Amount": 10, "To": "alice"}""";

    public static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
