using System.Text.Json;
using Xunit;

namespace DuplicateProperties.Tests;

/// <summary>
/// The boundary of AllowDuplicateProperties = false, which the post states in
/// one sentence and this file pins down. Duplicates are detected by noticing
/// that a member is assigned twice, so the check follows the type: a repeated
/// property your type binds is an error at any depth, and a repeated property
/// your type ignores is not. JsonDocument has no type, so it catches every one.
///
/// These tests call JsonSerializer directly; the records below are fixtures for
/// the nested cases and are not part of the Api project's contract.
/// </summary>
public class WhatTheSettingCatchesTests
{
    private record Meta(string Ref);
    private record Order(decimal Amount, string To, Meta? Meta);

    private static readonly JsonSerializerOptions Reject =
        new(JsonSerializerDefaults.Web) { AllowDuplicateProperties = false };

    [Fact]
    public void A_repeated_top_level_property_is_an_error()
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Order>(
            """{"amount": 10, "to": "alice", "amount": 1000}""", Reject));

        Assert.Equal(
            "Duplicate property 'amount' encountered during deserialization of type 'DuplicateProperties.Tests.WhatTheSettingCatchesTests+Order'.",
            error.Message);
    }

    [Fact]
    public void Case_insensitive_matching_does_not_let_a_duplicate_through()
    {
        // The Web options match names case-insensitively, so "amount" and
        // "Amount" bind the same member, and that counts as a duplicate.
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Order>(
            """{"amount": 10, "to": "alice", "Amount": 1000}""", Reject));

        Assert.Contains("Duplicate property 'amount'", error.Message);
    }

    [Fact]
    public void A_repeated_property_inside_a_bound_nested_object_is_an_error()
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Order>(
            """{"amount": 10, "to": "alice", "meta": {"ref": "r1", "ref": "r2"}}""", Reject));

        Assert.Contains("Duplicate property 'ref'", error.Message);
        Assert.Contains("+Meta'.", error.Message);
    }

    [Fact]
    public void A_repeated_property_the_type_ignores_goes_straight_through()
    {
        // "note" matches no member and UnmappedMemberHandling is Skip, so nothing
        // is ever assigned twice and the check has nothing to notice.
        var order = JsonSerializer.Deserialize<Order>(
            """{"amount": 10, "to": "alice", "note": 1, "note": 2}""", Reject)!;

        Assert.Equal(10m, order.Amount);
    }

    [Fact]
    public void So_does_a_duplicate_inside_an_object_the_type_ignores()
    {
        var order = JsonSerializer.Deserialize<Order>(
            """{"amount": 10, "to": "alice", "extra": {"x": 1, "x": 2}}""", Reject)!;

        Assert.Equal(10m, order.Amount);
    }

    [Fact]
    public void JsonDocument_has_no_type_so_it_catches_all_three()
    {
        var options = new JsonDocumentOptions { AllowDuplicateProperties = false };

        foreach (var json in new[]
        {
            """{"amount": 10, "amount": 1000}""",
            """{"note": 1, "note": 2}""",
            """{"extra": {"x": 1, "x": 2}}""",
        })
        {
            Assert.Throws<JsonException>(() => JsonDocument.Parse(json, options).Dispose());
        }
    }

    [Fact]
    public void A_dictionary_target_reports_it_with_a_different_message()
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<string, decimal>>(
            """{"a": 1, "a": 2}""", Reject));

        Assert.Equal("Duplicate properties not allowed during deserialization.", error.Message);
    }
}
