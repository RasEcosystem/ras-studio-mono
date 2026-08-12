#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUN_DIRECTORY="$(mktemp -d -t ras-studio-desktop-XXXXXX)"
LOG_PATH="$RUN_DIRECTORY/desktop.log"
APP_PID=""

cleanup() {
    if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi

    rm -rf -- "$RUN_DIRECTORY"
}

trap cleanup EXIT INT TERM

for command_name in dotnet node rg; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Required command not found: $command_name" >&2
        exit 1
    fi
done

dotnet build "$REPOSITORY_ROOT/RasStudio.sln" --no-restore -m:1

electron_manifest="$REPOSITORY_ROOT/src/RasStudio.Web/bin/Debug/net10.0/.electron/package.json"
electron_security_hook="$REPOSITORY_ROOT/src/RasStudio.Web/bin/Debug/net10.0/.electron/custom_main.js"

if [[ ! -s "$electron_security_hook" ]]; then
    echo "Electron security hook was not copied to the application output." >&2
    exit 1
fi

if ! rg --quiet 'if \(!app\.requestSingleInstanceLock\(\)\)' "$electron_security_hook" ||
    ! rg --quiet 'process\.exit\(0\)' "$electron_security_hook"; then
    echo "Electron startup hook does not stop duplicate instances before backend startup." >&2
    exit 1
fi

electron_configuration="$(
    node -e '
        const manifest = require(process.argv[1]);
        process.stdout.write(`${manifest.singleInstance}\n${manifest.devDependencies.electron}\n`);
    ' "$electron_manifest"
)"

if [[ "$electron_configuration" != $'true\n43.4.0' ]]; then
    echo "Unexpected Electron configuration in $electron_manifest." >&2
    printf '%s\n' "$electron_configuration" >&2
    exit 1
fi

package_id="$(
    dotnet msbuild \
        "$REPOSITORY_ROOT/src/RasStudio.Web/RasStudio.Web.csproj" \
        -getProperty:PackageId \
        -p:Configuration=Release \
        -p:RuntimeIdentifier=linux-x64
)"

if [[ "$package_id" != "RasStudio.Web" ]]; then
    echo "Unexpected PackageId '$package_id'; published scoped CSS would not load." >&2
    exit 1
fi

env \
    APP_PATH="$RUN_DIRECTORY/settings" \
    ASPNETCORE_ENVIRONMENT=Development \
    Desktop__SmokeTest=true \
    Desktop__ElectronArguments="--headless --no-sandbox --disable-gpu" \
    dotnet run \
        --no-build \
        --no-launch-profile \
        --project "$REPOSITORY_ROOT/src/RasStudio.Web/RasStudio.Web.csproj" \
        -- \
        -unpackeddotnet \
        >"$LOG_PATH" 2>&1 &

APP_PID=$!

for _ in {1..240}; do
    if ! kill -0 "$APP_PID" 2>/dev/null; then
        set +e
        wait "$APP_PID"
        exit_code=$?
        set -e
        APP_PID=""

        if [[ "$exit_code" -ne 0 ]]; then
            echo "Desktop process exited with code $exit_code." >&2
            cat "$LOG_PATH" >&2
            exit "$exit_code"
        fi

        if ! grep -q "Now listening on: http://127.0.0.1:" "$LOG_PATH"; then
            echo "Kestrel did not bind to the loopback interface." >&2
            cat "$LOG_PATH" >&2
            exit 1
        fi

        if ! grep -q "Electron Socket: connected" "$LOG_PATH"; then
            echo "Electron did not connect to the .NET backend." >&2
            cat "$LOG_PATH" >&2
            exit 1
        fi

        test -s "$RUN_DIRECTORY/settings/settings.db"
        echo "Desktop lifecycle smoke test passed."
        exit 0
    fi

    sleep .25
done

echo "Desktop process did not shut down after its window closed." >&2
cat "$LOG_PATH" >&2
exit 1
