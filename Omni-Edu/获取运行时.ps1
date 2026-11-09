# Omni-Edu 推理运行时一键下载
# 说明：GitHub 单个文件不能超过 100MB，所以仓库里没有放 runtime（约 1.2GB）。
#      第一次使用前运行本脚本，会自动从 llama.cpp 官方发布页下载并解压，约 650MB。
# 用法：在 Omni-Edu 文件夹空白处右键 →「使用 PowerShell 运行」，或执行：
#   powershell -ExecutionPolicy Bypass -File .\获取运行时.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$runtimeDir = Join-Path $root 'runtime\llama.cpp'
$tempDir = Join-Path $env:TEMP 'omniedu-runtime-download'

New-Item -ItemType Directory -Force -Path $runtimeDir, $tempDir | Out-Null

$baseUrl = 'https://github.com/ggml-org/llama.cpp/releases/download/b12211'
$packages = @(
    'llama-b12211-bin-win-cuda-12.4-x64.zip',
    'cudart-llama-bin-win-cuda-12.4-x64.zip'
)

foreach ($name in $packages) {
    $zipPath = Join-Path $tempDir $name
    if (-not (Test-Path $zipPath)) {
        Write-Host "正在下载 $name（约 650MB，请耐心等待）……" -ForegroundColor Cyan
        & curl.exe -L --retry 3 --retry-delay 5 -o "$zipPath" "$baseUrl/$name"
        if ($LASTEXITCODE -ne 0) {
            throw "下载失败：$name，请检查网络后重新运行本脚本。"
        }
    }
    Write-Host "正在解压 $name ……" -ForegroundColor Cyan
    Expand-Archive -Path $zipPath -DestinationPath $runtimeDir -Force
}

$server = Join-Path $runtimeDir 'llama-server.exe'
if (-not (Test-Path $server)) {
    throw "解压完成后没有找到 llama-server.exe，请确认压缩包是否下载完整。"
}

Write-Host ""
Write-Host "运行时已就绪：$runtimeDir" -ForegroundColor Green
Write-Host "接下来双击 OmniEdu.exe 即可；第一次运行会自动下载模型（约 12.4GB）。" -ForegroundColor Green
Write-Host "如果不再需要安装包，可以手动删除临时目录：$tempDir" -ForegroundColor DarkGray
