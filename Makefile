# Cognitive Code Analysis — build/test targets for local use and containers.
#
# Local: requires .NET SDK 8.0+ on PATH for dotnet targets.
# Without dotnet on the host, use the container wrapper (creates artifacts/ on the bind mount):
#   make publish-single-docker
#   make publish-single-docker RID=linux-arm64
#
# Other container examples:
#   docker compose run --rm dev make ci
#   podman compose run --rm dev make ci
#
# publish-single: override RID, e.g. make publish-single RID=linux-x64
# Default RID is taken from `dotnet --info`, else linux-x64.

SLN := CognitiveCodeAnalysis.sln
CONSOLE := CognitiveCodeAnalysisConsoleApp/CognitiveCodeAnalysisConsoleApp.csproj
# Override if you use Podman: make publish-single-docker COMPOSE='podman compose -f compose.yaml'
COMPOSE ?= docker compose -f compose.yaml

_DOTNET_RID := $(shell dotnet --info 2>/dev/null | sed -n 's/^[[:space:]]*RID:[[:space:]]*//p' | head -1)
PUBLISH_RID := $(if $(strip $(RID)),$(strip $(RID)),$(if $(strip $(_DOTNET_RID)),$(_DOTNET_RID),linux-x64))

.DEFAULT_GOAL := help

.PHONY: help restore build-debug build build-release test test-debug test-release clean ci publish-single publish-single-docker

help:
	@echo "Cognitive Code Analysis — Makefile"
	@echo ""
	@echo "Usage:"
	@echo "  make <target>"
	@echo "  make publish-single RID=<runtime-identifier>"
	@echo ""
	@echo "Targets:"
	@echo "  restore          dotnet restore"
	@echo "  build-debug      dotnet build (Debug)"
	@echo "  build-release    dotnet build (Release)"
	@echo "  build            same as build-debug"
	@echo "  test             dotnet test (Release)"
	@echo "  test-debug       dotnet test (Debug)"
	@echo "  test-release     dotnet test (Release)"
	@echo "  clean            remove bin/obj folders under the solution"
	@echo "  ci               restore, build-release, test-release"
	@echo "  publish-single        self-contained single-file publish to artifacts/publish-<RID>"
	@echo "  publish-single-docker same, via compose dev image (no host dotnet needed)"
	@echo "  help                  show this message"

restore:
	dotnet restore $(SLN)

build-debug:
	dotnet build $(SLN) -c Debug

build: build-debug

build-release:
	dotnet build $(SLN) -c Release

test:
	dotnet test $(SLN) -c Release --verbosity normal

test-debug:
	dotnet test $(SLN) -c Debug --verbosity normal

test-release:
	dotnet test $(SLN) -c Release --verbosity normal

clean:
	@echo Removing bin/obj directories...
	@find . -type d -name bin -prune -exec rm -rf {} +
	@find . -type d -name obj -prune -exec rm -rf {} +

ci:
	dotnet restore $(SLN)
	dotnet build $(SLN) -c Release --no-restore
	dotnet test $(SLN) -c Release --verbosity normal --no-build

publish-single:
	@echo "Publishing self-contained single-file for RID=$(PUBLISH_RID) output=artifacts/publish-$(PUBLISH_RID)"
	@if [ -f "artifacts/publish-$(PUBLISH_RID)" ]; then \
	  printf '%s\n' \
	    "Refusing to publish: artifacts/publish-$(PUBLISH_RID) exists as a file, not a directory." \
	    "Remove it (often left behind by a shell treating '>' in an unquoted echo as redirection) and retry." >&2; \
	  exit 1; \
	fi
	@mkdir -p "artifacts/publish-$(PUBLISH_RID)"
	dotnet publish $(CONSOLE) -c Release -r $(PUBLISH_RID) --self-contained true -o "artifacts/publish-$(PUBLISH_RID)"
	@echo "Done. Output folder: artifacts/publish-$(PUBLISH_RID)"

# Runs publish-single inside the dev container; artifacts/ appears under the repo on the host.
publish-single-docker:
	$(COMPOSE) run --rm dev make publish-single $(if $(RID),RID=$(RID),)
