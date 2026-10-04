using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DuplicateProperties.Tests;

/// <summary>
/// The post's advice: set AllowDuplicateProperties to false. These tests check
/// that the same endpoint then answers 400, and that an ordinary body still
/// goes through.
/// </summary>
public class RejectingDuplicatesTests
{
    [Fact]
    public async Task The_endpoints_own_400_names_the_repeated_property()
    {
        using var api = new PermissiveApi();
        var client = api.CreateClient();

        var response = await client.PostAsync("/preset/no-duplicates", Bodies.Json(Bodies.DuplicateAmount));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "Duplicate property 'amount' encountered during deserialization of type 'Payment'.",
            body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Aspnet_cores_own_binding_answers_400_with_no_try_catch_in_the_app()
    {
        using var api = new RejectingApi();
        var client = api.CreateClient();

        var response = await client.PostAsync("/pay", Bodies.Json(Bodies.DuplicateAmount));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_body_with_no_duplicate_still_works_on_the_rejecting_app()
    {
        using var api = new RejectingApi();
        var client = api.CreateClient();

        var response = await client.PostAsync("/pay", Bodies.Json(Bodies.SingleAmount));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10m, body.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Rejection_survives_whitespace_and_property_order()
    {
        using var api = new RejectingApi();
        var client = api.CreateClient();

        var padded = "{\n  \"amount\": 10,\n  \"amount\": 1000,\n  \"to\": \"alice\"\n}";
        var response = await client.PostAsync("/pay", Bodies.Json(padded));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_setting_is_off_in_both_option_sets_the_framework_hands_you()
    {
        using var api = new PermissiveApi();
        var client = api.CreateClient();

        var settings = await client.GetFromJsonAsync<JsonElement>("/settings");

        Assert.True(settings.GetProperty("default").GetProperty("allowDuplicateProperties").GetBoolean());
        Assert.True(settings.GetProperty("web").GetProperty("allowDuplicateProperties").GetBoolean());
        Assert.False(settings.GetProperty("no-duplicates").GetProperty("allowDuplicateProperties").GetBoolean());
        Assert.False(settings.GetProperty("strict").GetProperty("allowDuplicateProperties").GetBoolean());
    }
}
