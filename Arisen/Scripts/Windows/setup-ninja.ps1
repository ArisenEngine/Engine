$ErrorActionPreference = 'Stop'

$ninjaVersion = "1.12.1"
$ninjaZipUrl = "https://github.com/ninja-build/ninja/releases/download/v$ninjaVersion/ninja-win.zip"
$toolsDir = Join-Path $PSScriptRoot "tools"
$ninjaDir = Join-Path $toolsDir "ninja"
$ninjaExe = Join-Path $ninjaDir "ninja.exe"

if (Test-Path $ninjaExe) {
    Write-Host "Found ninja.exe at $ninjaExe"
    exit 0
}

Write-Host "ninja.exe not found, downloading from $ninjaZipUrl..."

# Prepare directories
New-Item -ItemType Directory -Path $ninjaDir -Force | Out-Null
$tempZip = Join-Path $env:TEMP "ninja-win.zip"

# Download ninja
Invoke-WebRequest -Uri $ninjaZipUrl -OutFile $tempZip

# Extract using .NET built-in Zip API
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($tempZip, $ninjaDir)

# Clean up
Remove-Item $tempZip

if (Test-Path $ninjaExe) {
    Write-Host "Successfully installed ninja.exe to $ninjaDir"
    exit 0
} else {
    Write-Error "Failed to install ninja.exe"
    exit 1
}
