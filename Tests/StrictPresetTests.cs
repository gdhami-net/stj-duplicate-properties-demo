using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuplicateProperties.Api;
using Xunit;

namespace DuplicateProperties.Tests;

/// <summary>
/// The post's second judgment call: the Strict preset does reject duplicates,
/// but it also changes five other settings, and two of those break callers that
/// work today on a web API. These tests pin exactly what it sets, read off the
/// object rather than off the documentation.
/// </summary>
public class StrictPresetTests(PermissiveApi api) : IClassFixture<PermissiveApi>
{
    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public void Strict_differs_from_the_web_defaults_in_exactly_six_settings()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var strict = JsonSerializerOptions.Strict;

        Assert.True(web.AllowDuplicateProperties);
        Assert.False(strict.AllowDuplicateProperties);

        Assert.True(web.PropertyNameCaseInsensitive);
        Assert.False(strict.PropertyNameCaseInsensitive);

        Assert.Equal(JsonUnmappedMemberHandling.Skip, web.UnmappedMemberHandling);
        Assert.Equal(JsonUnmappedMemberHandling.Disallow, strict.UnmappedMemberHandling);

        Assert.Equal(JsonNumberHandling.AllowReadingFromString, web.NumberHandling);
        Assert.Equal(JsonNumberHandling.Strict, strict.NumberHandling);

        Assert.False(web.RespectNullableAnnotations);
        Assert.True(strict.RespectNullableAnnotations);

        Assert.False(web.RespectRequiredConstructorParameters);
        Assert.True(strict.RespectRequiredConstructorParameters);
    }

    [Fact]
    public void Strict_rejects_the_duplicate_when_the_names_match_the_members()
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Payment>(
            Bodies.DuplicateAmountPascalCase, JsonSerializerOptions.Strict));

        Assert.Equal(
            "Duplicate property 'Amount' encountered during deserialization of type 'Payment'.",
            error.Message);
    }

    [Fact]
    public async Task Strict_turns_a_working_camelCase_caller_into_a_400()
    {
        // No duplicate anywhere in this body. It goes through under the Web
        // options an ASP.NET Core app starts with.
        var onWeb = await _client.PostAsync("/preset/web", Bodies.Json(Bodies.SingleAmount));
        Assert.Equal(HttpStatusCode.OK, onWeb.StatusCode);

        // The same body under the Strict preset: case-insensitive matching is
        // off and unmapped members are an error, so "amount" matches nothing.
        var onStrict = await _client.PostAsync("/preset/strict", Bodies.Json(Bodies.SingleAmount));
        Assert.Equal(HttpStatusCode.BadRequest, onStrict.StatusCode);

        var body = await onStrict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "The JSON property 'amount' could not be mapped to any .NET member contained in type 'Payment'.",
            body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task One_setting_leaves_the_same_caller_working()
    {
        // The post's advice, on the same camelCase body that Strict refuses.
        var response = await _client.PostAsync("/preset/no-duplicates", Bodies.Json(Bodies.SingleAmount));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10m, body.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public void Strict_also_rejects_a_number_sent_as_a_string()
    {
        var quoted = """{"Amount": "10", "To": "alice"}""";

        // Fine on a web API: the Web defaults read a number out of a string.
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Equal(10m, JsonSerializer.Deserialize<Payment>("""{"amount": "10", "to": "alice"}""", web)!.Amount);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Payment>(quoted, JsonSerializerOptions.Strict));
    }

    [Fact]
    public void Strict_is_read_only_so_an_app_has_to_copy_it()
    {
        Assert.True(JsonSerializerOptions.Strict.IsReadOnly);
        Assert.Throws<InvalidOperationException>(
            () => JsonSerializerOptions.Strict.AllowDuplicateProperties = true);

        // The copy constructor gives you a writable one.
        var mine = new JsonSerializerOptions(JsonSerializerOptions.Strict) { PropertyNameCaseInsensitive = true };
        Assert.False(mine.IsReadOnly);
        Assert.False(mine.AllowDuplicateProperties);
    }
}
