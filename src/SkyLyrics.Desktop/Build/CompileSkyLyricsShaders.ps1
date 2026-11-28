[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$windowsKitsRoots = @()
if ($env:WindowsSdkDir) {
    $windowsKitsRoots += $env:WindowsSdkDir
}
$programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
if ($programFilesX86) {
    $windowsKitsRoots += (Join-Path $programFilesX86 "Windows Kits\10")
}

$compilerCandidates = @()
foreach ($root in ($windowsKitsRoots | Select-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        continue
    }

    $binRoot = Join-Path $root "bin"
    if (-not (Test-Path -LiteralPath $binRoot -PathType Container)) {
        continue
    }

    $compilerCandidates += Get-ChildItem -LiteralPath $binRoot -Filter "fxc.exe" -File -Recurse -ErrorAction SilentlyContinue
}

$compiler = $compilerCandidates |
    Sort-Object -Property FullName -Descending |
    Select-Object -First 1

if ($null -eq $compiler) {
    throw "Windows SDK fxc.exe was not found. Install the Windows 10/11 SDK with the x64 tools component."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$sourceDirectory = Join-Path $PSScriptRoot "..\Assets\Effects"

$shaders = @(
    @{
        Source = Join-Path $sourceDirectory "grain.hlsl"
        Output = Join-Path $OutputDirectory "Grain.ps"
    },
    @{
        Source = Join-Path $sourceDirectory "chromatic-aberration.hlsl"
        Output = Join-Path $OutputDirectory "ChromaticAberration.ps"
    }
)

foreach ($shader in $shaders) {
    if (-not (Test-Path -LiteralPath $shader.Source -PathType Leaf)) {
        throw "Shader source file was not found: $($shader.Source)"
    }

    & $compiler.FullName /nologo /T ps_3_0 /E main /Fo $shader.Output $shader.Source
    if ($LASTEXITCODE -ne 0) {
        throw "fxc failed to compile $($shader.Source) with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $shader.Output -PathType Leaf) -or
        (Get-Item -LiteralPath $shader.Output).Length -lt 64) {
        throw "fxc did not produce a valid pixel shader: $($shader.Output)"
    }

    Write-Host "Compiled $($shader.Source) -> $($shader.Output)"
}
