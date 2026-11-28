[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$googleFontsCommit = "5026800714d07231937d92eb4cceabf06e05d74e"
$fontRoot = Join-Path $PSScriptRoot "..\Assets\Fonts"
$fonts = @(
    @{
        Uri = "https://raw.githubusercontent.com/google/fonts/$googleFontsCommit/ofl/poppins/Poppins-Regular.ttf"
        Output = Join-Path $fontRoot "Poppins\Poppins-Regular.ttf"
        ExpectedBlobSha = "0bda228ade88b0bb5aac7da2c881d0c3f64d0817"
        ExpectedLength = 160316
    },
    @{
        Uri = "https://raw.githubusercontent.com/google/fonts/$googleFontsCommit/ofl/bebasneue/BebasNeue-Regular.ttf"
        Output = Join-Path $fontRoot "BebasNeue\BebasNeue-Regular.ttf"
        ExpectedBlobSha = "c328c6e08b20a20a1de47d823e007ee73812a438"
        ExpectedLength = 61400
    }
)

function Get-GitBlobSha1([string]$Path) {
    $content = [System.IO.File]::ReadAllBytes($Path)
    $header = [System.Text.Encoding]::ASCII.GetBytes("blob $($content.Length)`0")
    $combined = New-Object byte[] ($header.Length + $content.Length)
    [System.Buffer]::BlockCopy($header, 0, $combined, 0, $header.Length)
    [System.Buffer]::BlockCopy($content, 0, $combined, $header.Length, $content.Length)

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $hash = $sha1.ComputeHash($combined)
        return ([System.BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha1.Dispose()
    }
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
foreach ($font in $fonts) {
    $directory = Split-Path -Parent $font.Output
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Invoke-WebRequest -Uri $font.Uri -OutFile $font.Output -UseBasicParsing

    $actualLength = (Get-Item -LiteralPath $font.Output).Length
    if ($actualLength -ne $font.ExpectedLength) {
        throw "Downloaded font has an unexpected size: $($font.Output)"
    }

    $actualBlobSha = Get-GitBlobSha1 $font.Output
    if ($actualBlobSha -ne $font.ExpectedBlobSha) {
        throw "Downloaded font failed its pinned Git blob SHA-1 check: $($font.Output)"
    }

    $fontBytes = [System.IO.File]::ReadAllBytes($font.Output)
    if ($fontBytes.Length -lt 4 -or
        $fontBytes[0] -ne 0 -or
        $fontBytes[1] -ne 1 -or
        $fontBytes[2] -ne 0 -or
        $fontBytes[3] -ne 0) {
        throw "Downloaded asset is not a TrueType font: $($font.Output)"
    }

    Write-Host "Verified Google Fonts asset: $($font.Output) ($actualLength bytes)"
}

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
