#!/usr/bin/env bash
set -euo pipefail

serve="false"
no_restore="false"

for arg in "$@"; do
  case "$arg" in
    --serve) serve="true" ;;
    --no-restore) no_restore="true" ;;
    *)
      echo "Unknown argument: $arg" >&2
      exit 1
      ;;
  esac
done

cd "$(dirname "$0")/.."

if [[ "$no_restore" != "true" ]]; then
  dotnet restore
  dotnet tool restore
  dotnet build --configuration Release
fi

if [[ "$serve" == "true" ]]; then
  dotnet tool run docfx docs/docfx.json --warningsAsErrors --serve
else
  dotnet tool run docfx docs/docfx.json --warningsAsErrors
fi
