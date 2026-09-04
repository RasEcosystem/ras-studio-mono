#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
PUBLISH_APP_DIRECTORY="$REPOSITORY_ROOT/src/RasStudio.Web/bin/Release/net10.0/linux-x64/publish/app"
RUN_DIRECTORY="$(mktemp -d -t ras-studio-packaged-XXXXXX)"
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

for command_name in node rg; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Required command not found: $command_name" >&2
        exit 1
    fi
done

if [[ "$(uname -s)" != "Linux" ]]; then
    echo "The packaged Linux smoke test must run on Linux." >&2
    exit 1
fi

package_version="$(
    node -p "require(process.argv[1]).version" "$PUBLISH_APP_DIRECTORY/package.json"
)"
artifact_path="$REPOSITORY_ROOT/artifacts/desktop/linux-x64/RasStudio-Mono-$package_version-linux-x86_64.AppImage"

if [[ ! -x "$artifact_path" ]]; then
    echo "Packaged AppImage not found: $artifact_path" >&2
    exit 1
fi

application_command=("$artifact_path" --disable-gpu)

if [[ ! -r /dev/fuse ]]; then
    (
        cd "$RUN_DIRECTORY"
        "$artifact_path" --appimage-extract >"$RUN_DIRECTORY/extract.log"
    )

    extracted_directory="$RUN_DIRECTORY/squashfs-root"
    application_command=(
        env APPDIR="$extracted_directory"
        "$extracted_directory/AppRun"
        --no-sandbox
        --disable-gpu
    )
fi

display_command=()
if [[ -z "${DISPLAY:-}" ]]; then
    if ! command -v xvfb-run >/dev/null 2>&1; then
        echo "DISPLAY is not set and xvfb-run is unavailable." >&2
        exit 1
    fi

    display_command=(xvfb-run -a)
fi

"${display_command[@]}" env \
    APP_PATH="$RUN_DIRECTORY/settings" \
    ASPNETCORE_ENVIRONMENT=Development \
    Desktop__SmokeTest=true \
    "${application_command[@]}" \
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
            echo "Packaged desktop process exited with code $exit_code." >&2
            cat "$LOG_PATH" >&2
            exit "$exit_code"
        fi

        if ! rg --quiet "Evaluated StartupMethod: PackagedElectronFirst" "$LOG_PATH" ||
            ! rg --quiet "Electron Socket: connected" "$LOG_PATH" ||
            ! rg --quiet \
                "RasStudio Mono started successfully and is listening on http://127.0.0.1:" \
                "$LOG_PATH"; then
            echo "Packaged Electron/.NET lifecycle did not complete." >&2
            cat "$LOG_PATH" >&2
            exit 1
        fi

        test -s "$RUN_DIRECTORY/settings/settings.db"

        log_files=("$RUN_DIRECTORY/settings/logs"/rasstudio-*.log)
        if [[ ! -s "${log_files[0]}" ]] ||
            ! rg --quiet "RasStudio Mono started successfully" "${log_files[@]}" ||
            ! rg --quiet "RasStudio Mono stopped successfully" "${log_files[@]}"; then
            echo "Packaged application lifecycle was not written to the rolling log." >&2
            cat "$LOG_PATH" >&2
            exit 1
        fi

        echo "Packaged Linux desktop lifecycle smoke test passed."
        exit 0
    fi

    sleep .25
done

echo "Packaged desktop process did not shut down after its window closed." >&2
cat "$LOG_PATH" >&2
exit 1
