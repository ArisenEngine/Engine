param(
    [string]$LogFile = "$PSScriptRoot\setup-llvm.log"
)

# 配置
$llvmVersion = "20.1.5"
$llvmBaseName = "clang+llvm-$llvmVersion-x86_64-pc-windows-msvc"
$llvmUrl = "https://github.com/llvm/llvm-project/releases/download/llvmorg-$llvmVersion/$llvmBaseName.tar.xz"

$toolsDir = Join-Path $PSScriptRoot "tools"
$llvmDir = Join-Path $toolsDir "llvm"
$archiveName = "$llvmBaseName.tar.xz"
$archivePath = Join-Path $toolsDir $archiveName

$sevenZipPath = Join-Path $toolsDir "7zip\7z.exe"

# 写日志函数
function Log {
    param([string]$msg)
    Write-Host $msg
    if ($LogFile) {
        Add-Content -Path $LogFile -Value $msg
    }
}

Log "==== Setup LLVM started at $(Get-Date) ===="

# 检查 7z.exe
if (-not (Test-Path $sevenZipPath)) {
    Log "ERROR: 7z.exe not found at $sevenZipPath. Please run setup-7z.ps1 first."
    exit 1
}
Log "7z.exe found at $sevenZipPath"

# 确保 tools 和 llvm 目录存在
if (-not (Test-Path $toolsDir)) {
    New-Item -Path $toolsDir -ItemType Directory | Out-Null
}
if (-not (Test-Path $llvmDir)) {
    New-Item -Path $llvmDir -ItemType Directory | Out-Null
}

# 下载 LLVM 安装包（如果不存在）
if (-not (Test-Path $archivePath)) {
    Log "Downloading LLVM $llvmVersion from $llvmUrl ..."
    try {
        Invoke-WebRequest -Uri $llvmUrl -OutFile $archivePath -UseBasicParsing
        Log "Download completed."
    } catch {
        Log "ERROR: Failed to download LLVM from $llvmUrl"
        exit 1
    }
} else {
    Log "LLVM archive already downloaded, skipping download."
}

# 解压 LLVM（先解压 tar.xz 为 tar，再解压 tar）
$tarPath = $archivePath -replace ".xz$", ""

if (-not (Test-Path $tarPath)) {
    Log "Extracting .xz archive to .tar using 7z..."
    $process = Start-Process -FilePath $sevenZipPath -ArgumentList "x", "`"$archivePath`"", "-o`"$toolsDir`"", "-y" -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Log "ERROR: Failed to extract .xz archive."
        exit 1
    }
} else {
    Log ".tar archive already exists, skipping .xz extraction."
}

# 解压 tar 到 llvm 目录
if (-not (Test-Path (Join-Path $llvmDir $llvmBaseName))) {
    Log "Extracting .tar archive to LLVM directory..."
    $process = Start-Process -FilePath $sevenZipPath -ArgumentList "x", "`"$tarPath`"", "-o`"$llvmDir`"", "-y" -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Log "ERROR: Failed to extract .tar archive."
        exit 1
    }
} else {
    Log "LLVM directory already exists, skipping .tar extraction."
}

Log "LLVM setup completed successfully."
Log "LLVM is ready at: $(Join-Path $llvmDir $llvmBaseName)"
