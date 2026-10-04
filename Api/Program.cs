using System.Text.Json;
using DuplicateProperties.Api;

var builder = WebApplication.CreateBuilder(args);

// The one line the post is about. Minimal APIs bind request bodies with these
// options, and their starting point is JsonSerializerDefaults.Web, which leaves
// AllowDuplicateProperties at true. Flip the setting and the SAME endpoint stops
// accepting a body that repeats a property.
var rejectDuplicates = builder.Configuration.GetValue("RejectDuplicates", false);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.AllowDuplicateProperties = !rejectDuplicates;
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// 1. Ordinary minimal API endpoint. No try/catch anywhere: when the options
//    reject the duplicate, the 400 below is ASP.NET Core's own, produced while
//    it binds the body.
// ---------------------------------------------------------------------------
app.MapPost("/pay", (Payment payment) => Results.Ok(new
{
    amount = payment.Amount,
    to = payment.To,
}));

// ---------------------------------------------------------------------------
// 2. Two readers, one body. The stand-in keeps the first "amount" and compares
//    it with a spend limit; System.Text.Json keeps the last one and that is
//    what the endpoint would act on.
// ---------------------------------------------------------------------------
const decimal GatewayLimit = 100m;

app.MapPost("/compare", async (HttpRequest request) =>
{
    var body = await ReadBodyAsync(request);
    var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    var seenByStandIn = FirstWinsAmountReader.ReadAmount(body);
    var payment = JsonSerializer.Deserialize<Payment>(body, web)!;

    return Results.Ok(new
    {
        standInSaw = seenByStandIn,
        standInLimit = GatewayLimit,
        standInAllowed = seenByStandIn <= GatewayLimit,
        apiWouldCharge = payment.Amount,
        agree = seenByStandIn == payment.Amount,
    });
});

// ---------------------------------------------------------------------------
// 3. The same body under four option sets, so the table in the README is the
//    program's own output. The 400 here is this endpoint's, not the framework's:
//    /pay above is where ASP.NET Core produces its own.
// ---------------------------------------------------------------------------
var presets = new Dictionary<string, JsonSerializerOptions>(StringComparer.OrdinalIgnoreCase)
{
    // What you get from `new JsonSerializerOptions()`.
    ["default"] = new(),
    // What minimal APIs and controllers start from.
    ["web"] = new(JsonSerializerDefaults.Web),
    // Web options with the one setting changed.
    ["no-duplicates"] = new(JsonSerializerDefaults.Web) { AllowDuplicateProperties = false },
    // The whole preset, which changes six settings, not one.
    ["strict"] = JsonSerializerOptions.Strict,
};

app.MapPost("/preset/{name}", async (string name, HttpRequest request) =>
{
    if (!presets.TryGetValue(name, out var options))
    {
        return Results.NotFound(new { known = presets.Keys });
    }

    var body = await ReadBodyAsync(request);
    try
    {
        var payment = JsonSerializer.Deserialize<Payment>(body, options)!;
        return Results.Ok(new { preset = name, amount = payment.Amount, to = payment.To });
    }
    catch (JsonException error)
    {
        return Results.Json(new { preset = name, error = error.Message }, statusCode: 400);
    }
});

// ---------------------------------------------------------------------------
// 4. What each option set actually sets, read off the objects themselves.
// ---------------------------------------------------------------------------
app.MapGet("/settings", () => Results.Ok(presets.ToDictionary(
    pair => pair.Key,
    pair => new
    {
        allowDuplicateProperties = pair.Value.AllowDuplicateProperties,
        propertyNameCaseInsensitive = pair.Value.PropertyNameCaseInsensitive,
        unmappedMemberHandling = pair.Value.UnmappedMemberHandling.ToString(),
        numberHandling = pair.Value.NumberHandling.ToString(),
        respectNullableAnnotations = pair.Value.RespectNullableAnnotations,
        respectRequiredConstructorParameters = pair.Value.RespectRequiredConstructorParameters,
    })));

app.Run();

static async Task<byte[]> ReadBodyAsync(HttpRequest request)
{
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer);
    return buffer.ToArray();
}

public partial class Program;
