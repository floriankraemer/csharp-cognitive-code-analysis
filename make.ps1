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
$VsixProj = Join-Path $PSScriptRoot "CognitiveCodeAnalysisExtension\CognitiveCodeAnalysisExtension.Vsix\CognitiveCodeAnalysisExtension.Vsix.csproj"
$ExtensionProj = Join-Path $PSScriptRoot "CognitiveCodeAnalysisExtension\CognitiveCodeAnalysisExtension\CognitiveCodeAnalysisExtension.csproj"
$ExtensionCodeFixesProj = Join-Path $PSScriptRoot "CognitiveCodeAnalysisExtension\CognitiveCodeAnalysisExtension.CodeFixes\CognitiveCodeAnalysisExtension.CodeFixes.csproj"
$ExtensionPackageProj = Join-Path $PSScriptRoot "CognitiveCodeAnalysisExtension\CognitiveCodeAnalysisExtension.Package\CognitiveCodeAnalysisExtension.Package.csproj"

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)] [string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Get-MSBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path $path)) {
            return $path
        }
    }

    $fallbacks = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe")
    )

    foreach ($p in $fallbacks) {
        if (Test-Path $p) {
            return $p
        }
    }

    return $null
}

function Invoke-MSBuild {
    param(
        [Parameter(Mandatory = $true)] [string] $Project,
        [Parameter(Mandatory = $true)] [ValidateSet("Debug", "Release")] [string] $Configuration
    )

    $msbuild = Get-MSBuildPath
    if ([string]::IsNullOrWhiteSpace($msbuild)) {
        Write-Host "MSBuild.exe not found. Building the VSIX requires Visual Studio Build Tools / MSBuild." -ForegroundColor Red
        Write-Host "Install Visual Studio 2019/2022 (or Build Tools) and ensure 'Microsoft.Component.MSBuild' is available." -ForegroundColor Yellow
        exit 1
    }

    & $msbuild $Project /restore /t:Build /p:Configuration=$Configuration /p:DeployExtension=false /v:minimal
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
  build-extension  dotnet build extension projects (Release)
  build-vsix       build VSIX via MSBuild (Release)
  build-vsext      build extension + VSIX (Release)
  clean            remove bin/obj folders under the solution
  ci               restore, build-release, test-release, pack (dotnet tool nupkg)
  publish-single   self-contained single-file publish -> artifacts\publish-<RID>
  pack-tool        dotnet tool package -> artifacts\nupkg
  bootstrap-local-analyzer copy extension DLLs -> artifacts\local-analyzer (enables Roslyn analyzers on dotnet/VS builds)
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
    "build-extension" {
        Invoke-DotNet @("build", $ExtensionProj, "-c", "Release")
        Invoke-DotNet @("build", $ExtensionCodeFixesProj, "-c", "Release")
        Invoke-DotNet @("build", $ExtensionPackageProj, "-c", "Release")
    }
    "build-vsix" {
        Invoke-MSBuild -Project $VsixProj -Configuration "Release"
    }
    "build-vsext" {
        Invoke-DotNet @("build", $ExtensionProj, "-c", "Release")
        Invoke-DotNet @("build", $ExtensionCodeFixesProj, "-c", "Release")
        Invoke-DotNet @("build", $ExtensionPackageProj, "-c", "Release")
        Invoke-MSBuild -Project $VsixProj -Configuration "Release"
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
    "bootstrap-local-analyzer" {
        Invoke-DotNet @("build", $ExtensionProj, "-c", "Release")

        $extBin = Join-Path $PSScriptRoot (Join-Path "CognitiveCodeAnalysisExtension\CognitiveCodeAnalysisExtension" (Join-Path "bin\Release" "netstandard2.0"))
        $dst = Join-Path $PSScriptRoot (Join-Path "artifacts" "local-analyzer")
        if (-not (Test-Path $extBin)) {
            Write-Host "Expected extension output not found: $extBin" -ForegroundColor Red
            exit 1
        }
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Get-ChildItem -LiteralPath $extBin -Filter "*.dll" |
            Copy-Item -Destination $dst -Force
        Write-Host "Copied analyzer binaries -> $dst"
        Write-Host "Rebuild the solution so Roslyn picks up $(Join-Path $dst 'CognitiveCodeAnalysisExtension.dll')"
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
