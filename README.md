# stj-duplicate-properties-demo

Companion repo for **"Stop System.Text.Json from accepting the same JSON property
twice"** ([gdhami.net](https://gdhami.net) — link added when the post is live).

Send one JSON body with `"amount"` in it twice and System.Text.Json keeps the
last copy, answers 200, and says nothing. .NET 10 added
`JsonSerializerOptions.AllowDuplicateProperties`, which defaults to `true`, and a
`JsonSerializerOptions.Strict` preset that turns the rejection on along with five
other changes. This repo runs the same body through all of it.

Measured on **SDK 10.0.201, ASP.NET Core runtime 10.0.5**, Windows 11. The `Api`
project is booted in-process by `WebApplicationFactory`; nothing listens on a
real port.

## Run it

```
./check.sh          # or .\check.ps1 on Windows
```

29 tests, all passing:

```
Test Run Successful.
Total tests: 29
     Passed: 29
 Total time: 0.7168 Seconds
```

## The body

```json
{"amount": 10, "to": "alice", "amount": 1000}
```

No serializer will produce that for you, which is the point: it comes from a
caller who wrote it on purpose. `Tests/ApiFactory.cs` holds it as a raw string
constant so every test uses the same bytes.

## What the same body does under each option set

From `GET /settings` and `POST /preset/{name}`, both real endpoint output:

| Option set | `AllowDuplicateProperties` | camelCase body | PascalCase body |
|---|---|---|---|
| `new JsonSerializerOptions()` | `true` | 200, `amount: 0` (names never matched) | 200, `amount: 1000` |
| `JsonSerializerDefaults.Web` | `true` | 200, `amount: 1000` | 200, `amount: 1000` |
| Web + `AllowDuplicateProperties = false` | `false` | 400 | 400 |
| `JsonSerializerOptions.Strict` | `false` | 400, but about mapping, not duplicates | 400, duplicate |

The two 400 messages, quoted from the responses:

```
Duplicate property 'amount' encountered during deserialization of type 'Payment'.
The JSON property 'amount' could not be mapped to any .NET member contained in type 'Payment'.
```

The second one is the Strict preset's answer to a camelCase body, with no
duplicate needed: it turns case-sensitive matching back on and refuses unmapped
properties, so `amount` matches nothing at all.

## Two readers, two amounts

`POST /compare` reads one body twice and reports both results:

```json
{"standInSaw":10,"standInLimit":100,"standInAllowed":true,"apiWouldCharge":1000,"agree":false}
```

`Api/FirstWinsAmountReader.cs` is a **stand-in**, not a real product. It keeps the
first `amount` it meets, which is one of the two reasonable choices a
hand-written reader makes, and the one that disagrees with System.Text.Json.
Nothing here claims that any particular gateway, firewall or proxy behaves this
way — check yours before you assume either way.

## What the setting catches, and what it lets through

`Tests/WhatTheSettingCatchesTests.cs`. Duplicates are detected by noticing that a
member is assigned twice, so the check follows your type:

| Body, against a type with `Amount`, `To` and a nested `Meta` | Result |
|---|---|
| `{"amount": 10, "to": "a", "amount": 1000}` | 400, duplicate `amount` |
| `{"amount": 10, "to": "a", "Amount": 1000}` | 400 — case-insensitive matching binds the same member |
| `{"amount": 10, "to": "a", "meta": {"ref": "r1", "ref": "r2"}}` | 400, duplicate `ref` on `Meta` |
| `{"amount": 10, "to": "a", "note": 1, "note": 2}` | **200** — `note` matches no member, so nothing is assigned twice |
| `{"amount": 10, "to": "a", "extra": {"x": 1, "x": 2}}` | **200** — same reason, the object is skipped whole |

So a repeated property your type binds is caught at any depth, and a repeated
property your type ignores is not. If something in front of your API reads a
field your type doesn't have, this setting won't line the two up.

`JsonDocument` has no type to bind, so `JsonDocumentOptions.AllowDuplicateProperties
= false` catches the ignored ones too:

```
Duplicate property 'amount' encountered during deserialization.
```

A dictionary target reports it differently again:

```
Duplicate properties not allowed during deserialization.
```

## The other readers in System.Text.Json

- `JsonDocument.Parse` keeps the last copy by default, and takes
  `AllowDuplicateProperties` on `JsonDocumentOptions`.
- `JsonNode.Parse` is the odd one out. The parse succeeds, and the first property
  access throws `ArgumentException: An item with the same key has already been
  added. Key: amount` while the object builds its dictionary. `JsonNodeOptions`
  has no `AllowDuplicateProperties` in .NET 10, so there is nothing to configure.
- `Utf8JsonReader` reports all three property names and checks nothing. Any
  duplicate policy above it is the caller's.

## Wiring it into a minimal API

In your own app, one call:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.AllowDuplicateProperties = false;
});
```

`Api/Program.cs` has the same call, but drives the value from a `RejectDuplicates`
setting instead of writing `false` in the source, so the test suite can boot the
same app both ways and compare them. That is the only difference.

`POST /pay` then answers 400 on the duplicate body with no `try`/`catch` anywhere
in the app — the rejection happens while ASP.NET Core binds the body.

Two things to know before you ship that. The 400 has an **empty body**, and the
reason is logged at debug level only:

```
dbug: Microsoft.AspNetCore.Http.RequestDelegateFactory[2]
      Failed to read parameter "Payment payment" from the request body as JSON.
      System.Text.Json.JsonException: Duplicate property 'amount' encountered during deserialization of type 'Payment'.
```

So at the default log level nothing explains the 400, on either side. Add
`AddProblemDetails()` or your own handling if callers need to be told what was
wrong. Second, `JsonSerializerOptions.Strict` is read-only
(`IsReadOnly` is `true`), so an app that wants it has to copy it:

```csharp
var mine = new JsonSerializerOptions(JsonSerializerOptions.Strict)
{
    PropertyNameCaseInsensitive = true,   // put back what a web API needs
};
```

## Everything the Strict preset changes

`Tests/StrictPresetTests.cs` reads these off the objects themselves. Measured
against the Web defaults, which is what an ASP.NET Core app starts from:

| Setting | Web | Strict |
|---|---|---|
| `AllowDuplicateProperties` | `true` | `false` |
| `PropertyNameCaseInsensitive` | `true` | `false` |
| `UnmappedMemberHandling` | `Skip` | `Disallow` |
| `NumberHandling` | `AllowReadingFromString` | `Strict` |
| `RespectNullableAnnotations` | `false` | `true` |
| `RespectRequiredConstructorParameters` | `false` | `true` |

The .NET 10 release notes list four of these, because they describe Strict
against `JsonSerializerOptions.Default`, where `NumberHandling` is already
`Strict` and name matching is already case-sensitive. Against a web API the gap
is six, and two of them break callers that work today: camelCase names stop
matching, and a number sent as `"10"` stops being read.

## Project layout

```
Api/Program.cs                  endpoints: /pay, /compare, /preset/{name}, /settings
Api/Payment.cs                  the record the body binds to
Api/FirstWinsAmountReader.cs    the first-wins stand-in (Utf8JsonReader)
Tests/ApiFactory.cs             two app configurations, and the bodies
Tests/WhichValueArrivesTests.cs which value lands where, across the readers
Tests/TwoReadersDisagreeTests.cs one request, two amounts
Tests/RejectingDuplicatesTests.cs the 400, including ASP.NET Core's own
Tests/WhatTheSettingCatchesTests.cs the boundary of the check
Tests/StrictPresetTests.cs      what the preset really sets
```

## Sources

- [JsonSerializerOptions.AllowDuplicateProperties](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions.allowduplicateproperties)
  — "The default is `true`"; `JsonException` when set to `false`.
- [What's new in .NET 10 libraries](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)
  — "Option to disallow duplicate JSON properties" and "Strict JSON serialization options".
- [JsonSerializerOptions.Strict](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions.strict)

MIT licensed.
