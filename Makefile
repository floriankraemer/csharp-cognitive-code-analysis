# Cognitive Code Analysis (C#) — same targets as make.ps1 (restore, build, test, ci, coverage, …).
#
# All targets execute inside the dev container (bind-mounts this repo at /src).
# The host only needs a container runtime (Docker or Podman) and `make`.
# No local .NET SDK is required.
#
# Runtime auto-detected (override with CONTAINER_RUNNER):
#   docker compose   (default if `docker` is on PATH)
#   podman compose   (fallback if `podman` is on PATH)
#
#   make ci
#   make test
#   make coverage
#   make publish-single RID=linux-x64
#
# Escape hatch: set IN_CONTAINER=1 to run recipes directly on the host
# (e.g. when already inside the container, or to use a host .NET SDK):
#   IN_CONTAINER=1 make ci

SLN := CognitiveCodeAnalysis.sln
CONSOLE := CognitiveCodeAnalysisConsoleApp/CognitiveCodeAnalysisConsoleApp.csproj
BENCH := CognitiveCodeAnalysis.Benchmarks/CognitiveCodeAnalysis.Benchmarks.csproj
COV_DIR := $(CURDIR)/artifacts/coverage
BENCH_DIR := $(CURDIR)/artifacts/benchmark

_DOTNET_RID := $(shell dotnet --info 2>/dev/null | sed -n 's/^[[:space:]]*RID:[[:space:]]*//p' | head -1)
PUBLISH_RID := $(if $(strip $(RID)),$(strip $(RID)),$(if $(strip $(_DOTNET_RID)),$(_DOTNET_RID),linux-x64))

# Auto-pick a container runner unless the caller overrode it.
ifeq ($(origin CONTAINER_RUNNER), undefined)
  _HAS_DOCKER := $(shell command -v docker 2>/dev/null)
  _HAS_PODMAN := $(shell command -v podman 2>/dev/null)
  ifneq ($(strip $(_HAS_DOCKER)),)
    CONTAINER_RUNNER := docker compose
  else ifneq ($(strip $(_HAS_PODMAN)),)
    CONTAINER_RUNNER := podman compose
  else
    CONTAINER_RUNNER := docker compose
  endif
endif

# When IN_CONTAINER=1 (or we're already inside the dev container), run recipes
# directly. Otherwise re-exec the same target inside the dev container.
ifeq ($(strip $(IN_CONTAINER)),1)
  _NATIVE := 1
else ifneq ($(wildcard /.dockerenv),)
  _NATIVE := 1
else ifeq ($(strip $(container)),podman)
  _NATIVE := 1
else
  _NATIVE :=
endif

# Forwards args (RID, etc.) so `make publish-single RID=...` still works.
_CONTAINER_RUN = $(CONTAINER_RUNNER) run --rm dev make $@ RID="$(RID)" IN_CONTAINER=1

.DEFAULT_GOAL := help

.PHONY: help restore build-debug build build-release test test-debug test-release clean ci publish-single pack-tool coverage benchmark

help:
	@echo "Cognitive Code Analysis (C#) — Makefile (same targets as make.ps1)"
	@echo ""
	@echo "All targets run inside the dev container (bind-mounted at /src)."
	@echo "Container runner: $(CONTAINER_RUNNER)  (override with CONTAINER_RUNNER=...)"
	@echo ""
	@echo "Usage:"
	@echo "  make <target>"
	@echo "  make publish-single RID=<runtime-identifier>"
	@echo "  IN_CONTAINER=1 make <target>    # run on host instead of container"
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
	@echo "  ci               restore, build-release, test-release, pack (dotnet tool nupkg)"
	@echo "  publish-single   self-contained single-file publish -> artifacts/publish-<RID>"
	@echo "  pack-tool        dotnet tool package -> artifacts/nupkg"
	@echo "  coverage         run tests with Coverlet + HTML report -> artifacts/coverage"
	@echo "  benchmark        run BenchmarkDotNet analysis benchmarks -> artifacts/benchmark"
	@echo "  help             show this message"

ifeq ($(_NATIVE),1)

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
	dotnet pack $(CONSOLE) -c Release --no-restore

publish-single:
	@echo "Publishing self-contained single-file for RID: $(PUBLISH_RID) -> artifacts/publish-$(PUBLISH_RID)"
	dotnet publish $(CONSOLE) -c Release -r $(PUBLISH_RID) --self-contained true -o artifacts/publish-$(PUBLISH_RID)
	@echo "Done. Output folder: artifacts/publish-$(PUBLISH_RID)"

pack-tool:
	@mkdir -p artifacts/nupkg
	@echo "Packing .NET tool -> artifacts/nupkg"
	dotnet pack $(CONSOLE) -c Release -o artifacts/nupkg
	@echo "Done."

coverage:
	mkdir -p $(COV_DIR)
	@echo "Restoring dotnet tools (ReportGenerator)..."
	dotnet tool restore
	@echo "Running tests with code coverage (Coverlet)..."
	dotnet test $(SLN) -c Release --verbosity minimal \
		/p:CollectCoverage=true \
		/p:CoverletOutput=$(COV_DIR)/coverage \
		/p:CoverletOutputFormat=cobertura
	test -f $(COV_DIR)/coverage.cobertura.xml
	rm -rf $(COV_DIR)/html
	@echo "Generating HTML report (ReportGenerator)..."
	dotnet tool run reportgenerator -- \
		-reports:artifacts/coverage/coverage.cobertura.xml \
		-targetdir:artifacts/coverage/html \
		-reporttypes:Html
	@echo ""
	@echo "Coverage complete."
	@echo "  Cobertura: artifacts/coverage/coverage.cobertura.xml"
	@echo "  HTML:      artifacts/coverage/html/index.html"

benchmark:
	mkdir -p $(BENCH_DIR)
	dotnet run -c Release --project $(BENCH) -- --filter '*'
	@echo ""
	@echo "Benchmark complete."
	@echo "  Results: $(BENCH_DIR)/"

else

restore build-debug build build-release test test-debug test-release clean ci publish-single pack-tool coverage benchmark:
	$(_CONTAINER_RUN)

endif
