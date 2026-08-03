SHELL := /bin/sh

DOTNET ?= dotnet
CONFIGURATION ?= Debug
RID ?= linux-x64

SOLUTION := RasStudio.sln
WEB_PROJECT := src/RasStudio.Web/RasStudio.Web.csproj
PUBLISH_DIR := artifacts/publish/$(RID)

.DEFAULT_GOAL := help

.PHONY: help all submodules submodules-update restore build debug build-release release publish clean

help:
	@printf '%s\n' \
		'RasStudio build commands:' \
		'' \
		'  make submodules        Initialize submodules at the recorded revisions' \
		'  make submodules-update Update submodules from their remote branches' \
		'  make restore           Restore all projects' \
		'  make build             Build the solution (CONFIGURATION=Debug by default)' \
		'  make debug             Build the solution in Debug mode' \
		'  make build-release     Build the solution in Release mode' \
		'  make release           Publish a self-contained single-file release' \
		'  make clean             Clean build and publish outputs' \
		'' \
		'Options:' \
		'  RID=linux-x64          Target runtime: linux-x64, linux-arm64, win-x64, ...' \
		'' \
		'Examples:' \
		'  make release' \
		'  make release RID=linux-arm64' \
		'  make release RID=win-x64'

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

release: publish

publish: build-release
	$(DOTNET) publish "$(WEB_PROJECT)" \
		--configuration Release \
		--runtime "$(RID)" \
		--self-contained true \
		--output "$(PUBLISH_DIR)" \
		-p:PublishSingleFile=true \
		-p:IncludeNativeLibrariesForSelfExtract=true \
		-p:IncludeAllContentForSelfExtract=true \
		-p:PublishTrimmed=false \
		-p:DebugType=None \
		-p:DebugSymbols=false
	@printf 'Release published to %s\n' "$(PUBLISH_DIR)"

clean:
	$(DOTNET) clean "$(SOLUTION)"
	rm -rf -- artifacts
