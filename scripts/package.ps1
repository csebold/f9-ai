#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string[]]$RuntimeIdentifiers = @("win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64"),
    [string]$Configuration = $(if ($env:CONFIG) { $env:CONFIG } else { "Release" }),
    [string]$Version = $(if ($env:APP_VERSION) { $env:APP_VERSION } else { "" })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repoRoot "ChatClient/ChatClient.csproj"
$versionFile = Join-Path $repoRoot "VERSION"

if (-not $Version -or $Version.Trim().Length -eq 0) {
    if (Test-Path $versionFile) {
        $Version = (Get-Content $versionFile -Raw).Trim()
    }
}

if (-not $Version -or $Version.Trim().Length -eq 0) {
    try {
        $gitArgs = @("describe", "--tags", "--dirty", "--always")
        $Version = (git -C $repoRoot @gitArgs 2>$null).Trim()
    } catch {
        $Version = $null
    }
}

if (-not $Version -or $Version.Trim().Length -eq 0) {
    $Version = Get-Date -Format "yyyyMMddHHmm"
}

$publishRoot = Join-Path $repoRoot "artifacts/publish"
$packageRoot = Join-Path $repoRoot "artifacts/packages/$Version"

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

foreach ($rid in $RuntimeIdentifiers) {
    Write-Host ":: Packaging $rid"
    $publishDir = Join-Path $publishRoot $rid
    $packageName = "ChatClient-$Version-$rid.zip"
    $packagePath = Join-Path $packageRoot $packageName

    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

    dotnet publish $project `
        -c $Configuration `
        -r $rid `
        --self-contained `
        true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -o $publishDir | Out-Null

    if (Test-Path $packagePath) {
        Remove-Item -Force $packagePath
    }

    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $packagePath
    Write-Host "   -> $packagePath"
}

Write-Host ":: Completed packaging to $packageRoot"
