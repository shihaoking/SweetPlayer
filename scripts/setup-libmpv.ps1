# LibMPV 自动下载和部署脚本
# 此脚本会下载预编译的 libmpv 并部署到项目中

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Architecture = "x64",

    [Parameter(Mandatory=$false)]
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"

Write-Host "=== SweetPlayer LibMPV 部署工具 ===" -ForegroundColor Cyan
Write-Host ""

# 检测项目根目录
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$nativeDir = Join-Path $projectRoot "src\SweetPlayer\Native"

Write-Host "项目根目录: $projectRoot" -ForegroundColor Gray
Write-Host "目标架构: $Architecture" -ForegroundColor Gray
Write-Host ""

# 创建 Native 目录结构
$archDir = Join-Path $nativeDir $Architecture
if (-not (Test-Path $archDir)) {
    Write-Host "创建目录: $archDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $archDir -Force | Out-Null
}

# GitHub API URL
$apiUrl = "https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases"

Write-Host "正在获取 libmpv 发布信息..." -ForegroundColor Yellow

try {
    # 获取最新发布信息
    $releases = Invoke-RestMethod -Uri $apiUrl -Headers @{ "User-Agent" = "SweetPlayer-Setup" }

    if ($Version -eq "latest") {
        $release = $releases[0]
    } else {
        $release = $releases | Where-Object { $_.tag_name -eq $Version } | Select-Object -First 1
    }

    if (-not $release) {
        throw "未找到指定版本的发布: $Version"
    }

    Write-Host "找到版本: $($release.tag_name)" -ForegroundColor Green
    Write-Host "发布日期: $($release.published_at)" -ForegroundColor Gray
    Write-Host ""

    # 根据架构选择合适的资源
    $archPattern = switch ($Architecture) {
        "x64"   { "mpv-x86_64-.*\.7z$" }
        "x86"   { "mpv-i686-.*\.7z$" }
        "arm64" { "mpv-aarch64-.*\.7z$" }
    }

    $asset = $release.assets | Where-Object { $_.name -match $archPattern } | Select-Object -First 1

    if (-not $asset) {
        Write-Host "警告: 未找到 $Architecture 架构的预编译版本" -ForegroundColor Red
        Write-Host "可用的资源:" -ForegroundColor Yellow
        $release.assets | ForEach-Object { Write-Host "  - $($_.name)" -ForegroundColor Gray }
        Write-Host ""
        Write-Host "请手动从以下地址下载:" -ForegroundColor Yellow
        Write-Host $release.html_url -ForegroundColor Cyan
        exit 1
    }

    Write-Host "找到资源: $($asset.name)" -ForegroundColor Green
    Write-Host "大小: $([math]::Round($asset.size / 1MB, 2)) MB" -ForegroundColor Gray
    Write-Host ""

    # 下载文件
    $tempDir = Join-Path $env:TEMP "sweetplayer-libmpv"
    $downloadPath = Join-Path $tempDir $asset.name

    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }

    Write-Host "正在下载..." -ForegroundColor Yellow
    Write-Host "从: $($asset.browser_download_url)" -ForegroundColor Gray
    Write-Host "到: $downloadPath" -ForegroundColor Gray

    # 使用 WebClient 显示进度
    $webClient = New-Object System.Net.WebClient

    Register-ObjectEvent -InputObject $webClient -EventName DownloadProgressChanged -SourceIdentifier WebClient.DownloadProgressChanged -Action {
        Write-Progress -Activity "下载 libmpv" -Status "$($EventArgs.ProgressPercentage)% 完成" -PercentComplete $EventArgs.ProgressPercentage
    } | Out-Null

    $webClient.DownloadFile($asset.browser_download_url, $downloadPath)
    Unregister-Event -SourceIdentifier WebClient.DownloadProgressChanged
    Write-Progress -Activity "下载 libmpv" -Completed

    Write-Host "下载完成!" -ForegroundColor Green
    Write-Host ""

    # 检查是否安装了 7-Zip
    $7zipPaths = @(
        "C:\Program Files\7-Zip\7z.exe",
        "C:\Program Files (x86)\7-Zip\7z.exe",
        "$env:ProgramFiles\7-Zip\7z.exe"
    )

    $7zip = $7zipPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $7zip) {
        Write-Host "未找到 7-Zip，请手动解压以下文件:" -ForegroundColor Red
        Write-Host $downloadPath -ForegroundColor Cyan
        Write-Host ""
        Write-Host "解压后，将 libmpv-2.dll 复制到:" -ForegroundColor Yellow
        Write-Host $archDir -ForegroundColor Cyan
        Write-Host ""
        Write-Host "下载 7-Zip: https://www.7-zip.org/" -ForegroundColor Gray
        exit 1
    }

    # 解压文件
    $extractDir = Join-Path $tempDir "extracted"
    Write-Host "正在解压..." -ForegroundColor Yellow
    Write-Host "使用: $7zip" -ForegroundColor Gray

    & $7zip x "$downloadPath" -o"$extractDir" -y | Out-Null

    # 查找 libmpv-2.dll
    Write-Host "正在查找 libmpv-2.dll..." -ForegroundColor Yellow
    $mpvDll = Get-ChildItem -Path $extractDir -Filter "libmpv-2.dll" -Recurse | Select-Object -First 1

    if (-not $mpvDll) {
        Write-Host "错误: 在解压的文件中未找到 libmpv-2.dll" -ForegroundColor Red
        Write-Host "解压目录: $extractDir" -ForegroundColor Gray
        exit 1
    }

    Write-Host "找到: $($mpvDll.FullName)" -ForegroundColor Green
    Write-Host ""

    # 复制到目标目录
    $targetPath = Join-Path $archDir "mpv-2.dll"
    Write-Host "正在部署到: $targetPath" -ForegroundColor Yellow
    Copy-Item -Path $mpvDll.FullName -Destination $targetPath -Force

    Write-Host ""
    Write-Host "=== 部署成功! ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "mpv-2.dll 已部署到: $targetPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "下一步:" -ForegroundColor Yellow
    Write-Host "1. 确保在 SweetPlayer.csproj 中配置了 Native 文件复制（参见 docs/LIBMPV_SETUP.md）" -ForegroundColor Gray
    Write-Host "2. 重新构建项目" -ForegroundColor Gray
    Write-Host "3. 运行应用并测试视频播放功能" -ForegroundColor Gray
    Write-Host ""

    # 清理临时文件
    Write-Host "正在清理临时文件..." -ForegroundColor Gray
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

} catch {
    Write-Host ""
    Write-Host "错误: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "请手动从以下地址下载 libmpv:" -ForegroundColor Yellow
    Write-Host "https://github.com/shinchiro/mpv-winbuild-cmake/releases" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}
