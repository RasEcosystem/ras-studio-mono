#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BUILD_CONFIGURATION="${CONFIGURATION:-Debug}"
RUN_DIRECTORY="$(mktemp -d -t ras-studio-mcp-XXXXXX)"
LOG_PATH="$RUN_DIRECTORY/mcp-host.log"
ACCESS_TOKEN="rasstudio-mcp-smoke-token"
APP_PID=""

cleanup() {
    if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi

    rm -rf -- "$RUN_DIRECTORY"
}

trap cleanup EXIT INT TERM

for command_name in curl dotnet rg; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Required command not found: $command_name" >&2
        exit 1
    fi
done

dotnet build \
    "$REPOSITORY_ROOT/RasStudio.sln" \
    --configuration "$BUILD_CONFIGURATION" \
    --no-restore \
    -m:1

env \
    APP_PATH="$RUN_DIRECTORY/settings" \
    ASPNETCORE_ENVIRONMENT=Development \
    Desktop__DisableElectron=true \
    Desktop__DiagnosticPort=0 \
    Mcp__AccessToken="$ACCESS_TOKEN" \
    dotnet run \
        --configuration "$BUILD_CONFIGURATION" \
        --no-build \
        --no-launch-profile \
        --project "$REPOSITORY_ROOT/src/RasStudio.Web/RasStudio.Web.csproj" \
        >"$LOG_PATH" 2>&1 &

APP_PID=$!
endpoint=""

for _ in {1..240}; do
    if ! kill -0 "$APP_PID" 2>/dev/null; then
        set +e
        wait "$APP_PID"
        exit_code=$?
        set -e
        APP_PID=""
        echo "MCP smoke host exited with code $exit_code." >&2
        cat "$LOG_PATH" >&2
        exit "$exit_code"
    fi

    endpoint="$(rg --only-matching 'http://127\.0\.0\.1:[0-9]+' "$LOG_PATH" | head -n 1 || true)"

    if [[ -n "$endpoint" ]] && curl --silent --fail --output /dev/null "$endpoint"; then
        break
    fi

    endpoint=""
    sleep .25
done

if [[ -z "$endpoint" ]]; then
    echo "MCP smoke host did not become ready." >&2
    cat "$LOG_PATH" >&2
    exit 1
fi

unauthorized_status="$(
    curl \
        --silent \
        --output "$RUN_DIRECTORY/unauthorized-response" \
        --write-out '%{http_code}' \
        "$endpoint/mcp"
)"

if [[ "$unauthorized_status" != "401" ]]; then
    echo "Expected an unauthenticated MCP request to return 401, got $unauthorized_status." >&2
    cat "$RUN_DIRECTORY/unauthorized-response" >&2
    exit 1
fi

dotnet run \
    --configuration "$BUILD_CONFIGURATION" \
    --no-build \
    --no-restore \
    --project "$REPOSITORY_ROOT/tests/SmokeTests/RasStudio.McpSmoke/RasStudio.McpSmoke.csproj" \
    -- \
    "$endpoint/mcp" \
    "$ACCESS_TOKEN"
