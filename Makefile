SHELL := /bin/sh

DOTNET ?= dotnet
CONFIGURATION ?= Debug

SOLUTION := RasStudio.sln
WEB_PROJECT := src/RasStudio.Web/RasStudio.Web.csproj

.DEFAULT_GOAL := help

.PHONY: help all submodules submodules-update restore build debug build-release run \
	package release package-linux package-windows format format-check dotnet-tests visual desktop-smoke \
	mcp-smoke test clean

help:
	@printf '%s\n' \
		'RasStudio desktop commands:' \
		'' \
		'  make submodules        Initialize submodules at the recorded revisions' \
		'  make submodules-update Update submodules from their remote branches' \
		'  make restore           Restore .NET and ElectronNET.Core dependencies' \
		'  make build             Build the solution (CONFIGURATION=Debug by default)' \
		'  make run               Start the unpackaged Electron desktop application' \
		'  make package           Build a package for the current Windows/Linux host' \
		'  make package-linux     Build the Linux x64 AppImage (run on Linux)' \
		'  make package-windows   Build Windows x64 installer and portable app (run on Windows)' \
		'  make format            Format the solution' \
		'  make format-check      Verify formatting exactly as CI should' \
		'  make dotnet-tests      Run unit and integration test projects' \
		'  make visual            Capture Home page screenshots for every theme' \
		'  make desktop-smoke     Verify Electron/Kestrel startup and shutdown lifecycle' \
		'  make mcp-smoke         Verify protected MCP discovery and tool invocation' \
		'  make test              Run build, .NET, visual, desktop, and MCP checks' \
		'  make clean             Clean build and package outputs'

all: build

submodules:
	git submodule sync --recursive
	git submodule update --init --recursive

submodules-update:
	git submodule sync --recursive
	git submodule update --init --recursive --remote

restore: submodules
	$(DOTNET) restore "$(SOLUTION)"

build: restore
	$(DOTNET) build "$(SOLUTION)" --configuration "$(CONFIGURATION)" --no-restore

debug:
	$(MAKE) build CONFIGURATION=Debug

build-release:
	$(MAKE) build CONFIGURATION=Release

run: build
	$(DOTNET) run --no-build --no-launch-profile --project "$(WEB_PROJECT)" -- -unpackeddotnet

package release:
	@case "$$(uname -s)" in \
		Linux) $(MAKE) package-linux ;; \
		MINGW*|MSYS*|CYGWIN*) $(MAKE) package-windows ;; \
		*) printf '%s\n' 'Packaging is configured for Windows and Linux hosts.' >&2; exit 1 ;; \
	esac

package-linux:
	$(DOTNET) restore "$(WEB_PROJECT)" --runtime linux-x64
	$(DOTNET) publish "$(WEB_PROJECT)" --no-restore -p:PublishProfile=linux-x64

package-windows:
	$(DOTNET) restore "$(WEB_PROJECT)" --runtime win-x64
	$(DOTNET) publish "$(WEB_PROJECT)" --no-restore -p:PublishProfile=win-x64

format: restore
	$(DOTNET) format "$(SOLUTION)" --no-restore

format-check: restore
	$(DOTNET) format "$(SOLUTION)" --no-restore --verify-no-changes

dotnet-tests: restore
	$(DOTNET) test "$(SOLUTION)" --configuration "$(CONFIGURATION)" --no-restore -m:1

visual:
	tests/SmokeTests/Visual/run-screenshots.sh

desktop-smoke:
	tests/SmokeTests/Desktop/run-smoke.sh

mcp-smoke:
	tests/SmokeTests/RasStudio.McpSmoke/run-smoke.sh

test: build-release format-check dotnet-tests visual desktop-smoke mcp-smoke

clean:
	$(DOTNET) clean "$(SOLUTION)"
	rm -rf -- artifacts
