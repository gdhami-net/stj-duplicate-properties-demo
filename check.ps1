# Runs the suite that backs the post's claims. Exits non-zero if any fail.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'stj-duplicate-properties-demo - checking the post''s claims'
Write-Host '  WhichValueArrivesTests     which "amount" lands in the record under the plain defaults'
Write-Host '                             and the Web defaults, plus JsonDocument, JsonNode and'
Write-Host '                             Utf8JsonReader on the same body'
Write-Host '  TwoReadersDisagreeTests    a first-wins stand-in reader and System.Text.Json coming away'
Write-Host '                             from one request with two different amounts'
Write-Host '  RejectingDuplicatesTests   the 400 with AllowDuplicateProperties = false, including'
Write-Host '                             ASP.NET Core''s own 400 during body binding'
Write-Host '  WhatTheSettingCatchesTests what the setting catches and what it lets through:'
Write-Host '                             bound members at any depth yes, ignored properties no'
Write-Host '  StrictPresetTests          every setting the Strict preset differs on, and the'
Write-Host '                             camelCase caller it turns into a 400'
Write-Host ''

dotnet test --logger 'console;verbosity=normal'
exit $LASTEXITCODE
