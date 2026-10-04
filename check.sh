#!/usr/bin/env bash
# Runs the suite that backs the post's claims. Exits non-zero if any fail.
set -euo pipefail
cd "$(dirname "$0")"

echo "stj-duplicate-properties-demo — checking the post's claims"
echo "  WhichValueArrivesTests     which \"amount\" lands in the record under the plain defaults"
echo "                             and the Web defaults, plus JsonDocument, JsonNode and"
echo "                             Utf8JsonReader on the same body"
echo "  TwoReadersDisagreeTests    a first-wins stand-in reader and System.Text.Json coming away"
echo "                             from one request with two different amounts"
echo "  RejectingDuplicatesTests   the 400 with AllowDuplicateProperties = false, including"
echo "                             ASP.NET Core's own 400 during body binding"
echo "  WhatTheSettingCatchesTests what the setting catches and what it lets through:"
echo "                             bound members at any depth yes, ignored properties no"
echo "  StrictPresetTests          every setting the Strict preset differs on, and the"
echo "                             camelCase caller it turns into a 400"
echo

dotnet test --logger "console;verbosity=normal"
