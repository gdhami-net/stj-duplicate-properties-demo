using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DuplicateProperties.Api;
using Xunit;

namespace DuplicateProperties.Tests;

/// <summary>
/// The post's point: a reader that keeps the FIRST copy and System.Text.Json,
/// which keeps the last, come away from one request with two different amounts.
/// FirstWinsAmountReader is a stand-in for whatever sits in front of the API; it
/// is not a claim about any real gateway or firewall product.
/// </summary>
public class TwoReadersDisagreeTests(PermissiveApi api) : IClassFixture<PermissiveApi>
{
    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task One_body_two_amounts()
    {
        var response = await _client.PostAsync("/compare", Bodies.Json(Bodies.DuplicateAmount));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(10m, body.GetProperty("standInSaw").GetDecimal());
        Assert.Equal(1000m, body.GetProperty("apiWouldCharge").GetDecimal());
        Assert.False(body.GetProperty("agree").GetBoolean());
    }

    [Fact]
    public async Task The_spend_limit_passes_on_a_value_the_api_never_uses()
    {
        var response = await _client.PostAsync("/compare", Bodies.Json(Bodies.DuplicateAmount));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // 10 is under the stand-in's limit of 100, so it lets the request through.
        Assert.True(body.GetProperty("standInAllowed").GetBoolean());
        Assert.Equal(100m, body.GetProperty("standInLimit").GetDecimal());
        // And the amount the endpoint would act on is ten times the limit.
        Assert.Equal(1000m, body.GetProperty("apiWouldCharge").GetDecimal());
    }

    [Fact]
    public async Task Without_a_duplicate_the_two_readers_agree()
    {
        var response = await _client.PostAsync("/compare", Bodies.Json(Bodies.SingleAmount));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("agree").GetBoolean());
        Assert.Equal(10m, body.GetProperty("apiWouldCharge").GetDecimal());
    }

    [Fact]
    public void The_stand_in_keeps_the_first_copy_and_the_serializer_keeps_the_last()
    {
        var body = Encoding.UTF8.GetBytes(Bodies.DuplicateAmount);
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        Assert.Equal(10m, FirstWinsAmountReader.ReadAmount(body));
        Assert.Equal(1000m, JsonSerializer.Deserialize<Payment>(body, web)!.Amount);
    }
}
