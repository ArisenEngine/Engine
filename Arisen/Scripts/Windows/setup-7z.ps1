$installDir = Join-Path $PSScriptRoot "tools\7zip"
$exePath = Join-Path $env:TEMP "7zsetup.exe"
$downloadUrl = "https://www.7-zip.org/a/7z2409-x64.exe"

if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir | Out-Null
}

Write-Host "Downloading 7-Zip installer from $downloadUrl ..."
Invoke-WebRequest -Uri $downloadUrl -OutFile $exePath

Write-Host "Running silent install..."
Start-Process -FilePath $exePath -ArgumentList "/S /D=$installDir" -Wait

if (Test-Path (Join-Path $installDir "7z.exe")) {
    Write-Host "7-Zip installed successfully to $installDir"
} else {
    Write-Error "7-Zip installation failed"
    exit 1
}

# 删除安装包
Remove-Item $exePath -Force
