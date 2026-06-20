Write-Host "=== SweetPlayer 启动诊断 ===" -ForegroundColor Cyan

# 1. 检查编译状态
Write-Host "`n1. 检查编译状态..." -ForegroundColor Yellow
dotnet build -c Debug 2>&1 | Select-String -Pattern "error|错误|warning|警告" | Select-Object -Last 10

# 2. 检查依赖注入配置
Write-Host "`n2. 检查关键服务注册..." -ForegroundColor Yellow
$appFile = "src/SweetPlayer/App.xaml.cs"
Write-Host "IScrapingQueueService 注册:" -ForegroundColor Gray
Select-String -Path $appFile -Pattern "IScrapingQueueService"

Write-Host "`nHomeViewModel 注册:" -ForegroundColor Gray
Select-String -Path $appFile -Pattern "AddTransient<HomeViewModel>"

# 3. 检查数据库路径
Write-Host "`n3. 检查数据库位置..." -ForegroundColor Yellow
$dbPath = "$env:LOCALAPPDATA\SweetPlayer\sweetplayer.db"
if (Test-Path $dbPath) {
    Write-Host "✓ 数据库存在: $dbPath" -ForegroundColor Green
    $size = (Get-Item $dbPath).Length
    Write-Host "  大小: $size bytes" -ForegroundColor Gray
} else {
    Write-Host "✗ 数据库不存在: $dbPath" -ForegroundColor Red
}

# 4. 检查可执行文件
Write-Host "`n4. 检查可执行文件..." -ForegroundColor Yellow
$exePath = "src/SweetPlayer/bin/x64/Debug/net8.0-windows10.0.19041.0/SweetPlayer.exe"
if (Test-Path $exePath) {
    Write-Host "✓ 可执行文件存在" -ForegroundColor Green
} else {
    Write-Host "✗ 可执行文件不存在" -ForegroundColor Red
}

Write-Host "`n=== 诊断完成 ===" -ForegroundColor Cyan
Write-Host "`n请尝试运行应用并提供具体的错误信息。" -ForegroundColor White
Write-Host "如果看到异常，请复制完整的错误堆栈信息。" -ForegroundColor White
