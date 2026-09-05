SHELL := /bin/sh

DOTNET ?= dotnet
CONFIGURATION ?= Debug

SOLUTION := RasStudio.sln
WEB_PROJECT := src/RasStudio.Web/RasStudio.Web.csproj

.DEFAULT_GOAL := help

.PHONY: help all submodules submodules-update restore build debug build-release run \
	package release package-linux package-windows package-audit packaged-linux-smoke format format-check \
	dotnet-tests desktop-smoke mcp-smoke electron-audit test clean

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
		'  make package-audit     Audit dependencies used by the package build and runtime' \
		'  make packaged-linux-smoke  Verify the packaged AppImage lifecycle' \
		'  make release           Run all checks, package for this host, and audit the package' \
		'  make format            Format the solution' \
		'  make format-check      Verify formatting exactly as CI should' \
		'  make dotnet-tests      Run unit and integration test projects' \
		'  make desktop-smoke     Verify Electron/Kestrel startup and shutdown lifecycle' \
		'  make mcp-smoke         Verify protected MCP discovery and tool invocation' \
		'  make test              Run build, dependency, .NET, desktop, and MCP checks' \
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

package:
	@case "$$(uname -s)" in \
		Linux) $(MAKE) package-linux ;; \
		MINGW*|MSYS*|CYGWIN*) $(MAKE) package-windows ;; \
		*) printf '%s\n' 'Packaging is configured for Windows and Linux hosts.' >&2; exit 1 ;; \
	esac

release:
	$(MAKE) test
	$(MAKE) package
	$(MAKE) package-audit
	@if [ "$$(uname -s)" = 'Linux' ]; then $(MAKE) packaged-linux-smoke; fi

package-linux:
	$(DOTNET) restore "$(WEB_PROJECT)" --runtime linux-x64
	$(DOTNET) publish "$(WEB_PROJECT)" --no-restore -p:PublishProfile=linux-x64

package-windows:
	$(DOTNET) restore "$(WEB_PROJECT)" --runtime win-x64
	$(DOTNET) publish "$(WEB_PROJECT)" --no-restore -p:PublishProfile=win-x64

package-audit:
	@case "$$(uname -s)" in \
		Linux) publish_dir='src/RasStudio.Web/bin/Release/net10.0/linux-x64/publish' ;; \
		MINGW*|MSYS*|CYGWIN*) publish_dir='src/RasStudio.Web/bin/Release/net10.0/win-x64/publish' ;; \
		*) printf '%s\n' 'Package auditing is configured for Windows and Linux hosts.' >&2; exit 1 ;; \
	esac; \
	npm --prefix "$$publish_dir/app" audit --omit=dev --audit-level=high; \
	npm --prefix "$$publish_dir" audit --omit=dev --audit-level=high

packaged-linux-smoke:
	tests/SmokeTests/Desktop/run-packaged-linux-smoke.sh

format: restore
	$(DOTNET) format "$(SOLUTION)" --no-restore

format-check: restore
	$(DOTNET) format "$(SOLUTION)" --no-restore --verify-no-changes

dotnet-tests: restore
	$(DOTNET) test "$(SOLUTION)" --configuration "$(CONFIGURATION)" --no-restore -m:1

desktop-smoke:
	CONFIGURATION="$(CONFIGURATION)" tests/SmokeTests/Desktop/run-smoke.sh

mcp-smoke:
	CONFIGURATION="$(CONFIGURATION)" tests/SmokeTests/RasStudio.McpSmoke/run-smoke.sh

electron-audit: build-release
	npm --prefix "src/RasStudio.Web/bin/Release/net10.0/.electron" audit --omit=dev --audit-level=high

test: CONFIGURATION := Release
test: build-release format-check electron-audit dotnet-tests desktop-smoke mcp-smoke

clean:
	$(DOTNET) clean "$(SOLUTION)"
	rm -rf -- artifacts
