#Requires -Version 5.1
<#
.SYNOPSIS
  Build and test helpers (make-style targets).

.DESCRIPTION
  Run from the repository root, for example:
    .\make.ps1 build-debug
    .\make.ps1 build-release
    .\make.ps1 test
    .\make.ps1 clean
    .\make.ps1 publish-single
    .\make.ps1 publish-single linux-x64
    .\make.ps1 coverage

.PARAMETER Target
  The target to run. Use 'help' or omit to list targets.

.PARAMETER RuntimeIdentifier
  Optional RID for publish-single (e.g. win-x64, linux-x64, osx-arm64). Defaults to this machine's RID.
#>
param(
    [Parameter(Position = 0)]
    [string] $Target = "help",

    [Parameter(Position = 1)]
    [string] $RuntimeIdentifier = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Sln = Join-Path $PSScriptRoot "CognitiveCodeAnalysis.sln"
$ConsoleProj = Join-Path $PSScriptRoot "CognitiveCodeAnalysisConsoleApp\CognitiveCodeAnalysisConsoleApp.csproj"

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)] [string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Get-DefaultRuntimeIdentifier {
    # RuntimeIdentifier is not available in Windows PowerShell 5.1/.NET Framework.
    # Try reflection first, then dotnet --info, then a safe Windows fallback.
    $runtimeInfoType = [System.Runtime.InteropServices.RuntimeInformation]
    $runtimeIdProp = $runtimeInfoType.GetProperty("RuntimeIdentifier", [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)
    if ($null -ne $runtimeIdProp) {
        $runtimeId = [string] $runtimeIdProp.GetValue($null, $null)
        if (-not [string]::IsNullOrWhiteSpace($runtimeId)) {
            return $runtimeId
        }
    }

    $ridLine = (& dotnet --info 2>$null) | Select-String -Pattern "^\s*RID:\s*(.+)$" | Select-Object -First 1
    if ($null -ne $ridLine) {
        $runtimeId = $ridLine.Matches[0].Groups[1].Value.Trim()
        if (-not [string]::IsNullOrWhiteSpace($runtimeId)) {
            return $runtimeId
        }
    }

    if ([Environment]::Is64BitOperatingSystem) {
        return "win-x64"
    }

    return "win-x86"
}

function Show-Help {
    @"
Cognitive Code Analysis (C#) — make.ps1

Usage:
  .\make.ps1 <target> [runtime-identifier]

Targets:
  restore          dotnet restore
  build-debug      dotnet build (Debug)
  build-release    dotnet build (Release)
  build            same as build-debug
  test             dotnet test (Release)
  test-debug       dotnet test (Debug)
  test-release     dotnet test (Release)
  clean            remove bin/obj folders under the solution
  ci               restore, build-release, test-release, pack (dotnet tool nupkg)
  publish-single   self-contained single-file publish -> artifacts\publish-<RID>
  pack-tool        dotnet tool package -> artifacts\nupkg
  coverage         run tests with Coverlet + HTML report -> artifacts/coverage
  help             show this message
"@
}

switch ($Target.ToLowerInvariant()) {
    "help" { Show-Help }
    "restore" {
        Invoke-DotNet @("restore", $Sln)
    }
    "build-debug" {
        Invoke-DotNet @("build", $Sln, "-c", "Debug")
    }
    "build" {
        Invoke-DotNet @("build", $Sln, "-c", "Debug")
    }
    "build-release" {
        Invoke-DotNet @("build", $Sln, "-c", "Release")
    }
    "test" {
        Invoke-DotNet @("test", $Sln, "-c", "Release", "--verbosity", "normal")
    }
    "test-debug" {
        Invoke-DotNet @("test", $Sln, "-c", "Debug", "--verbosity", "normal")
    }
    "test-release" {
        Invoke-DotNet @("test", $Sln, "-c", "Release", "--verbosity", "normal")
    }
    "clean" {
        Get-ChildItem -Path $PSScriptRoot -Recurse -Directory -Filter "bin" -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Get-ChildItem -Path $PSScriptRoot -Recurse -Directory -Filter "obj" -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed bin/obj directories."
    }
    "ci" {
        Invoke-DotNet @("restore", $Sln)
        Invoke-DotNet @("build", $Sln, "-c", "Release", "--no-restore")
        Invoke-DotNet @("test", $Sln, "-c", "Release", "--verbosity", "normal", "--no-build")
        Invoke-DotNet @("pack", $ConsoleProj, "-c", "Release", "--no-restore")
    }
    "pack-tool" {
        $outDir = Join-Path $PSScriptRoot (Join-Path "artifacts" "nupkg")
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        Write-Host "Packing .NET tool -> $outDir"
        Invoke-DotNet @("pack", $ConsoleProj, "-c", "Release", "-o", $outDir)
        Write-Host "Done."
    }
    "publish-single" {
        $rid = $RuntimeIdentifier
        if ([string]::IsNullOrWhiteSpace($rid)) {
            $rid = Get-DefaultRuntimeIdentifier
        }
        $outDir = Join-Path $PSScriptRoot (Join-Path "artifacts" "publish-$rid")
        Write-Host "Publishing self-contained single-file for RID: $rid -> $outDir"
        Invoke-DotNet @(
            "publish", $ConsoleProj,
            "-c", "Release",
            "-r", $rid,
            "--self-contained", "true",
            "-o", $outDir
        )
        Write-Host "Done. Output folder: $outDir"
    }
    "coverage" {
        $coverageDir = Join-Path $PSScriptRoot (Join-Path "artifacts" "coverage")
        New-Item -ItemType Directory -Force -Path $coverageDir | Out-Null
        $coverageBase = Join-Path $coverageDir "coverage"
        $coberturaPath = "$coverageBase.cobertura.xml"
        $htmlDir = Join-Path $coverageDir "html"

        Write-Host "Restoring dotnet tools (ReportGenerator)..."
        Push-Location $PSScriptRoot
        try {
            Invoke-DotNet @("tool", "restore")

            Write-Host "Running tests with code coverage (Coverlet)..."
            Invoke-DotNet @(
                "test", $Sln,
                "-c", "Release",
                "--verbosity", "minimal",
                "/p:CollectCoverage=true",
                "/p:CoverletOutput=$coverageBase",
                "/p:CoverletOutputFormat=cobertura"
            )

            if (-not (Test-Path $coberturaPath)) {
                Write-Host "Expected Cobertura file not found: $coberturaPath" -ForegroundColor Red
                exit 1
            }

            if (Test-Path $htmlDir) {
                Remove-Item -Recurse -Force $htmlDir
            }

            # Use repo-relative paths for ReportGenerator so Windows drive letters are not parsed as -reports flags.
            $coberturaRel = "artifacts/coverage/coverage.cobertura.xml"
            $htmlRel = "artifacts/coverage/html"

            Write-Host "Generating HTML report (ReportGenerator)..."
            Invoke-DotNet @(
                "tool", "run", "reportgenerator",
                "--",
                "-reports:$coberturaRel",
                "-targetdir:$htmlRel",
                "-reporttypes:Html"
            )
        }
        finally {
            Pop-Location
        }

        $indexHtml = Join-Path $htmlDir "index.html"
        Write-Host ""
        Write-Host "Coverage complete." -ForegroundColor Green
        Write-Host "  Cobertura: $coberturaPath"
        Write-Host "  HTML:      $indexHtml"
    }
    default {
        Write-Host "Unknown target: $Target" -ForegroundColor Red
        Write-Host ""
        Show-Help
        exit 1
    }
}
