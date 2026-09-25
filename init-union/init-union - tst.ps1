[CmdletBinding()]
param(
    [string]$Project,
    [string]$PackageId = "UnionTypes",
    [string]$Version,
    [string]$Source,
    [switch]$SkipExtension
)

$ErrorActionPreference = "Stop"

function Invoke-NativeCommand {
    param([string]$FilePath, [string[]]$Arguments)

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed ($LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK is required."
}

if ([string]::IsNullOrWhiteSpace($Project)) {
    $projects = @(Get-ChildItem -LiteralPath $PWD -Filter "*.csproj" -File)
    if ($projects.Count -eq 0) {
        throw "No .csproj was found in '$PWD'. Pass -Project <path-to-project.csproj>."
    }
    if ($projects.Count -gt 1) {
        throw "Multiple projects were found in '$PWD'. Pass -Project <path-to-project.csproj>."
    }

    $Project = $projects[0].FullName
}

$projectPath = (Resolve-Path -LiteralPath $Project).Path
if ([IO.Path]::GetExtension($projectPath) -ne ".csproj") {
    throw "'$projectPath' is not a C# project file."
}

$packageArguments = @("add", $projectPath, "package", $PackageId)
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $packageArguments += @("--version", $Version)
}
if (-not [string]::IsNullOrWhiteSpace($Source)) {
    $packageArguments += @("--source", $Source)
}

Invoke-NativeCommand "dotnet" $packageArguments

if ($SkipExtension) {
    return
}

if (-not (Get-Command code -ErrorAction SilentlyContinue)) {
    throw "The VS Code 'code' command was not found. Install it from VS Code's Command Palette, or rerun with -SkipExtension."
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required to build the VS Code extension. Rerun with -SkipExtension to only add the NuGet package."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$extensionDirectory = Join-Path $repositoryRoot "vscode-extension"
$extensionPackage = Join-Path $extensionDirectory "package.json"
if (-not (Test-Path -LiteralPath $extensionPackage -PathType Leaf)) {
    throw "The bundled VS Code extension was not found at '$extensionDirectory'."
}

Push-Location $extensionDirectory
try {
    Invoke-NativeCommand "npm" @("ci")
    Invoke-NativeCommand "npm" @("run", "compile")

    $vsixPath = Join-Path $extensionDirectory "union-types.vsix"
    Invoke-NativeCommand "npx" @("-y", "@vscode/vsce", "package", "--out", $vsixPath)
    Invoke-NativeCommand "code" @("--install-extension", $vsixPath, "--force")
}
finally {
    Pop-Location
}

Write-Host "Installed $PackageId in '$projectPath' and installed the Union Types VS Code extension."
