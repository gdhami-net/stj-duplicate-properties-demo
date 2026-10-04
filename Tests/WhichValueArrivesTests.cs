using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DuplicateProperties.Tests;

/// <summary>
/// The post's first claim: send "amount" twice and System.Text.Json keeps the
/// last copy, under the defaults and under the Web defaults that ASP.NET Core
/// uses. No warning, no error, status 200.
/// </summary>
public class WhichValueArrivesTests(PermissiveApi api) : IClassFixture<PermissiveApi>
{
    private readonly HttpClient _client = api.CreateClient();

    // The plain default options match property names case-sensitively, so they
    // get the PascalCase body; the Web options an ASP.NET Core app starts with
    // get the camelCase one a browser or another service would send. Both keep
    // the last copy, and both answer 200.
    [Theory]
    [InlineData("default", Bodies.DuplicateAmountPascalCase)]
    [InlineData("web", Bodies.DuplicateAmount)]
    public async Task Last_copy_wins(string preset, string body)
    {
        var response = await _client.PostAsync($"/preset/{preset}", Bodies.Json(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1000m, payload.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Bound_endpoint_accepts_the_duplicate_and_charges_the_last_value()
    {
        var response = await _client.PostAsync("/pay", Bodies.Json(Bodies.DuplicateAmount));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1000m, body.GetProperty("amount").GetDecimal());
        Assert.Equal("alice", body.GetProperty("to").GetString());
    }

    [Fact]
    public void JsonDocument_keeps_the_last_copy_too()
    {
        using var document = JsonDocument.Parse(Bodies.DuplicateAmount);

        Assert.Equal(1000m, document.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public void JsonNode_is_the_odd_one_out_and_throws_on_first_access()
    {
        // JsonNodeOptions has no AllowDuplicateProperties in .NET 10, and the
        // parse itself succeeds. The throw comes when the object builds its
        // dictionary, and it is an ArgumentException, not a JsonException.
        Assert.Null(typeof(System.Text.Json.Nodes.JsonNodeOptions).GetProperty("AllowDuplicateProperties"));

        var node = System.Text.Json.Nodes.JsonNode.Parse(Bodies.DuplicateAmount)!;

        var error = Assert.Throws<ArgumentException>(() => node["amount"]);
        Assert.Contains("An item with the same key has already been added", error.Message);
    }

    [Fact]
    public void Utf8JsonReader_reports_every_property_name_and_checks_nothing()
    {
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(Bodies.DuplicateAmount));
        var names = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName) names++;
        }

        Assert.Equal(3, names);
    }

    [Fact]
    public void JsonDocument_can_reject_duplicates_too()
    {
        var options = new JsonDocumentOptions { AllowDuplicateProperties = false };

        var error = Assert.Throws<JsonException>(() => JsonDocument.Parse(Bodies.DuplicateAmount, options));
        Assert.Equal(
            "Duplicate property 'amount' encountered during deserialization.",
            error.Message);
    }
}
