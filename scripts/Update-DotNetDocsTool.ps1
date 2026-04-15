[CmdletBinding()]
param(
    [ValidateSet("global", "local")]
    [string]$Scope = "global",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SmokeTest
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $repoRoot "src\dotnet-docs\DotNetDocs.Tool.csproj"
$artifactsDir = Join-Path $repoRoot "artifacts\tool"

if (-not (Test-Path $projectPath)) {
    throw "Could not find tool project at $projectPath"
}

[xml]$projectXml = Get-Content $projectPath
$propertyGroup = $projectXml.Project.PropertyGroup | Select-Object -First 1

$packageId = if ($propertyGroup.PackageId) { $propertyGroup.PackageId } else { "dotnet-docs" }
$baseVersion = if ($propertyGroup.Version) { $propertyGroup.Version } else { "0.1.0" }

function Get-NextDevVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionText
    )

    $stablePart = ($VersionText -split "-", 2)[0]
    $version = [Version]$stablePart
    $nextPatch = $version.Build + 1
    if ($nextPatch -lt 0) {
        $nextPatch = 1
    }

    return "{0}.{1}.{2}-dev.{3}" -f $version.Major, $version.Minor, $nextPatch, (Get-Date -Format 'yyyyMMddHHmmss')
}

$packageVersion = Get-NextDevVersion -VersionText $baseVersion

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
Get-ChildItem -Path $artifactsDir -Filter "$packageId*.nupkg" -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "Packing $packageId $packageVersion..." -ForegroundColor Cyan
dotnet pack $projectPath `
    -c $Configuration `
    -o $artifactsDir `
    -p:Version=$packageVersion

if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed."
}

if ($Scope -eq "local") {
    $manifestPath = Join-Path $repoRoot ".config\dotnet-tools.json"
    if (-not (Test-Path $manifestPath)) {
        Write-Host "Creating local tool manifest..." -ForegroundColor Yellow
        Push-Location $repoRoot
        try {
            dotnet new tool-manifest
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet new tool-manifest failed."
            }
        }
        finally {
            Pop-Location
        }
    }
}

function Invoke-ToolCommand {
    param(
        [string[]]$Arguments
    )

    Write-Host ("dotnet " + ($Arguments -join " ")) -ForegroundColor DarkGray
    & dotnet @Arguments | Out-Host
    $exitCode = $LASTEXITCODE
    return $exitCode
}

if ($Scope -eq "global") {
    Write-Host "Refreshing global tool..." -ForegroundColor Cyan
    Invoke-ToolCommand -Arguments @("tool", "uninstall", "--global", $packageId) | Out-Null
    $result = Invoke-ToolCommand -Arguments @(
        "tool", "install", "--global", $packageId,
        "--add-source", $artifactsDir,
        "--version", $packageVersion
    )
    if ($result -ne 0) {
        throw "Failed to install the global tool."
    }
}
else {
    Write-Host "Updating local tool..." -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        $result = Invoke-ToolCommand -Arguments @(
            "tool", "update", "--local", $packageId,
            "--add-source", $artifactsDir,
            "--version", $packageVersion
        )

        if ($result -ne 0) {
            Write-Host "Local update did not succeed. Trying fresh install..." -ForegroundColor Yellow
            Invoke-ToolCommand -Arguments @("tool", "uninstall", "--local", $packageId) | Out-Null
            $result = Invoke-ToolCommand -Arguments @(
                "tool", "install", "--local", $packageId,
                "--add-source", $artifactsDir,
                "--version", $packageVersion
            )
            if ($result -ne 0) {
                throw "Failed to install the local tool."
            }
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "Tool updated successfully." -ForegroundColor Green

if ($Scope -eq "global") {
    Write-Host "Run it with: dotnet-docs --help"
    Write-Host "Example:     dotnet-docs System.IO.Directory.EnumerateFiles"
}
else {
    Write-Host "Run it with: dotnet tool run dotnet-docs -- --help"
    Write-Host "Example:     dotnet tool run dotnet-docs -- System.IO.Directory.EnumerateFiles"
}

if ($SmokeTest) {
    Write-Host ""
    Write-Host "Running smoke test..." -ForegroundColor Cyan
    if ($Scope -eq "global") {
        & dotnet-docs --help
    }
    else {
        Push-Location $repoRoot
        try {
            & dotnet tool run dotnet-docs -- --help
        }
        finally {
            Pop-Location
        }
    }
}
