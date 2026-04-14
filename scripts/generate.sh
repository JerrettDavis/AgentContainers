#!/usr/bin/env bash
# Shell script to run the AgentContainers generator
set -euo pipefail

COMMAND="${1:-generate}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
GENERATOR_PROJECT="$REPO_ROOT/src/AgentContainers.Generator/AgentContainers.Generator.csproj"

if [ ! -f "$GENERATOR_PROJECT" ]; then
    echo "Error: Generator project not found at: $GENERATOR_PROJECT" >&2
    exit 1
fi

echo "Running AgentContainers Generator ($COMMAND)..."
echo ""

cd "$REPO_ROOT"
dotnet run --project "$GENERATOR_PROJECT" -- "$COMMAND"
