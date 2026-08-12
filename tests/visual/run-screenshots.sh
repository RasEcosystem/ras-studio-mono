#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTPUT_DIRECTORY="${1:-$REPOSITORY_ROOT/artifacts/visual}"
RUN_DIRECTORY="$(mktemp -d -t ras-studio-visual-XXXXXX)"
APP_PID=""

cleanup() {
    if [[ -n "$APP_PID" ]]; then
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi

    rm -rf -- "$RUN_DIRECTORY"
}

trap cleanup EXIT INT TERM

for command_name in dotnet curl rg; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Required command not found: $command_name" >&2
        exit 1
    fi
done

BROWSER="${BROWSER:-}"

if [[ -z "$BROWSER" ]]; then
    for browser_candidate in chromium chromium-browser google-chrome; do
        if command -v "$browser_candidate" >/dev/null 2>&1; then
            BROWSER="$browser_candidate"
            break
        fi
    done
fi

if [[ -z "$BROWSER" ]] || ! command -v "$BROWSER" >/dev/null 2>&1; then
    echo "Required browser not found: set BROWSER to a Chromium-compatible executable." >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIRECTORY"

dotnet build "$REPOSITORY_ROOT/RasStudio.sln" --no-restore -m:1

themes=(Carbon Slate Light System)
port=5181

for theme in "${themes[@]}"; do
    slug="${theme,,}"
    settings_path="$RUN_DIRECTORY/$slug"
    log_path="$RUN_DIRECTORY/$slug.log"
    browser_theme_args=()

    if [[ "$theme" == "System" ]]; then
        slug="system-dark"
        browser_theme_args+=(--blink-settings=preferredColorScheme=dark)
    fi

    env \
        APP_PATH="$settings_path" \
        ASPNETCORE_ENVIRONMENT=Development \
        Desktop__DisableElectron=true \
        Desktop__DiagnosticPort="$port" \
        RasStudio__ThemeOverride="$theme" \
        dotnet run \
            --no-build \
            --no-launch-profile \
            --project "$REPOSITORY_ROOT/src/RasStudio.Web/RasStudio.Web.csproj" \
            >"$log_path" 2>&1 &

    APP_PID=$!

    ready=false
    for _ in {1..80}; do
        if curl -fsS "http://127.0.0.1:$port/" >/dev/null 2>&1; then
            ready=true
            break
        fi

        if ! kill -0 "$APP_PID" 2>/dev/null; then
            break
        fi

        sleep .25
    done

    if [[ "$ready" != true ]]; then
        echo "Application did not start for $theme." >&2
        tail -n 200 "$log_path" >&2
        exit 1
    fi

    headers_path="$RUN_DIRECTORY/$slug.headers"
    curl -fsS -D "$headers_path" -o /dev/null "http://127.0.0.1:$port/"

    required_header_patterns=(
        '^content-security-policy:'
        "frame-ancestors 'none'"
        '^permissions-policy:'
        '^referrer-policy: no-referrer'
        '^x-content-type-options: nosniff'
        '^x-frame-options: DENY'
    )

    for header_pattern in "${required_header_patterns[@]}"; do
        if ! rg --ignore-case --quiet "$header_pattern" "$headers_path"; then
            echo "Missing security response header for $theme: $header_pattern" >&2
            exit 1
        fi
    done

    for viewport in desktop mobile; do
        if [[ "$viewport" == "desktop" ]]; then
            window_size="1440,1000"
        else
            window_size="390,844"
        fi

        screenshot_path="$OUTPUT_DIRECTORY/home-$slug-$viewport.png"

        "$BROWSER" \
            --headless=new \
            --no-sandbox \
            --disable-gpu \
            --hide-scrollbars \
            --virtual-time-budget=2000 \
            --user-data-dir="$RUN_DIRECTORY/chromium-$slug-$viewport" \
            --window-size="$window_size" \
            "${browser_theme_args[@]}" \
            --screenshot="$screenshot_path" \
            "http://127.0.0.1:$port/"

        test -s "$screenshot_path"
        echo "Captured $screenshot_path"
    done

    if [[ "$theme" == "Carbon" ]]; then
        pages=(
            "ras-gates|/ras-gates"
            "clusters|/clusters"
            "settings|/settings"
            "not-found|/not-found"
            "error|/error"
        )

        for page in "${pages[@]}"; do
            page_name="${page%%|*}"
            page_path="${page#*|}"

            for viewport in desktop mobile; do
                if [[ "$viewport" == "desktop" ]]; then
                    window_size="1440,1000"
                else
                    window_size="390,844"
                fi

                screenshot_path="$OUTPUT_DIRECTORY/$page_name-$slug-$viewport.png"

                "$BROWSER" \
                    --headless=new \
                    --no-sandbox \
                    --disable-gpu \
                    --hide-scrollbars \
                    --virtual-time-budget=2000 \
                    --user-data-dir="$RUN_DIRECTORY/chromium-$slug-$page_name-$viewport" \
                    --window-size="$window_size" \
                    --screenshot="$screenshot_path" \
                    "http://127.0.0.1:$port$page_path"

                test -s "$screenshot_path"
                echo "Captured $screenshot_path"
            done
        done
    fi

    kill "$APP_PID" 2>/dev/null || true
    wait "$APP_PID" 2>/dev/null || true
    APP_PID=""
    port=$((port + 1))
done

if cmp -s \
    "$OUTPUT_DIRECTORY/home-light-mobile.png" \
    "$OUTPUT_DIRECTORY/home-system-dark-mobile.png"; then
    echo "System dark mode did not differ from Light." >&2
    exit 1
fi

echo "Visual smoke test completed: $OUTPUT_DIRECTORY"
