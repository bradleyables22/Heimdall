[CmdletBinding()]
param(
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'

$argsForNode = @('build.mjs')
if ($Verify) {
    $argsForNode += '--verify'
}

Push-Location $PSScriptRoot
try {
    $esbuildMarker = if ($IsWindows -or $env:OS -eq 'Windows_NT') {
        Join-Path $PSScriptRoot 'node_modules\.bin\esbuild.cmd'
    } else {
        Join-Path $PSScriptRoot 'node_modules/.bin/esbuild'
    }

    if (-not (Test-Path -LiteralPath $esbuildMarker)) {
        $npm = if ($IsWindows -or $env:OS -eq 'Windows_NT') { 'npm.cmd' } else { 'npm' }
        & $npm ci
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    & node @argsForNode
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
