# SweetPlayer数据库查看工具 (Windows版)
# 使用方法: .\view_db.ps1 [选项]

param(
    [Parameter(Position=0)]
    [string]$Action = "help",

    [Parameter(Position=1)]
    [string]$Query = ""
)

$DbPath = "$env:LOCALAPPDATA\SweetPlayer\sweetplayer.db"

function Show-Help {
    Write-Host @"
SweetPlayer数据库查看工具
用法：
    .\view_db.ps1 [选项]

选项：
    all         - 显示所有表的数据
    movies      - 显示电影元数据
    files       - 显示视频文件
    sources     - 显示媒体源
    tmdb        - 显示使用TMDB数据源的电影
    douban      - 显示使用豆瓣数据源的电影
    stats       - 显示统计信息
    query       - 执行自定义SQL查询

示例：
    .\view_db.ps1 movies
    .\view_db.ps1 stats
    .\view_db.ps1 query "SELECT * FROM MovieMetadata WHERE Year = 2025"
"@
}

function Run-SqliteQuery {
    param([string]$Sql, [switch]$Header, [switch]$Column)

    $args = @($DbPath)
    if ($Header) { $args += "-header" }
    if ($Column) { $args += "-column" }
    $args += $Sql

    & sqlite3 @args
}

function Show-Movies {
    Write-Host "=== 电影元数据 ===" -ForegroundColor Cyan
    Run-SqliteQuery -Header -Column @"
        SELECT
            Id,
            ChineseTitle AS 中文标题,
            Year AS 年份,
            CASE DataSource WHEN 0 THEN '豆瓣' WHEN 1 THEN 'TMDB' END AS 数据源,
            DoubanId,
            TmdbId,
            ROUND(DoubanRating, 1) AS 评分
        FROM MovieMetadata
        ORDER BY Id;
"@
}

function Show-Files {
    Write-Host "=== 视频文件 ===" -ForegroundColor Cyan
    Run-SqliteQuery -Header -Column @"
        SELECT
            v.Id,
            SUBSTR(v.FileName, 1, 50) AS 文件名,
            CASE v.MediaType WHEN 0 THEN '电影' WHEN 1 THEN '电视剧' END AS 类型,
            m.ChineseTitle AS 匹配影片,
            CASE WHEN v.HasHdr10 = 1 THEN '✓' ELSE '' END AS HDR10,
            CASE WHEN v.HasDolbyVision = 1 THEN '✓' ELSE '' END AS DV,
            CASE WHEN v.HasDolbyAtmos = 1 THEN '✓' ELSE '' END AS Atmos
        FROM VideoFiles v
        LEFT JOIN MovieMetadata m ON v.MovieMetadataId = m.Id
        ORDER BY v.Id;
"@
}

function Show-Sources {
    Write-Host "=== 媒体源 ===" -ForegroundColor Cyan
    Run-SqliteQuery -Header -Column @"
        SELECT
            Id,
            Name AS 名称,
            CASE Type WHEN 0 THEN '本地' WHEN 1 THEN 'WebDAV' END AS 类型,
            Path AS 路径
        FROM MediaSources;
"@
}

function Show-Tmdb {
    Write-Host "=== 使用TMDB数据源的电影 ===" -ForegroundColor Green
    Run-SqliteQuery -Header -Column @"
        SELECT
            Id,
            ChineseTitle AS 中文标题,
            OriginalTitle AS 原始标题,
            Year AS 年份,
            TmdbId,
            ROUND(DoubanRating, 1) AS 评分
        FROM MovieMetadata
        WHERE DataSource = 1
        ORDER BY Id;
"@
}

function Show-Douban {
    Write-Host "=== 使用豆瓣数据源的电影 ===" -ForegroundColor Yellow
    Run-SqliteQuery -Header -Column @"
        SELECT
            Id,
            ChineseTitle AS 中文标题,
            Year AS 年份,
            DoubanId,
            ROUND(DoubanRating, 1) AS 评分
        FROM MovieMetadata
        WHERE DataSource = 0
        ORDER BY Id;
"@
}

function Show-Stats {
    Write-Host "=== 数据库统计 ===" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "电影总数: " -NoNewline
    Run-SqliteQuery "SELECT COUNT(*) FROM MovieMetadata;"

    Write-Host "视频文件总数: " -NoNewline
    Run-SqliteQuery "SELECT COUNT(*) FROM VideoFiles;"

    Write-Host ""
    Write-Host "数据源分布:" -ForegroundColor Yellow
    Run-SqliteQuery -Header -Column @"
        SELECT
            CASE DataSource WHEN 0 THEN '豆瓣' WHEN 1 THEN 'TMDB' END AS 数据源,
            COUNT(*) AS 数量
        FROM MovieMetadata
        GROUP BY DataSource;
"@

    Write-Host ""
    Write-Host "匹配状态:" -ForegroundColor Yellow
    Run-SqliteQuery -Header -Column @"
        SELECT
            CASE WHEN MovieMetadataId IS NULL THEN '未匹配' ELSE '已匹配' END AS 状态,
            COUNT(*) AS 数量
        FROM VideoFiles
        GROUP BY CASE WHEN MovieMetadataId IS NULL THEN '未匹配' ELSE '已匹配' END;
"@

    Write-Host ""
    Write-Host "HDR/DV统计:" -ForegroundColor Yellow
    Run-SqliteQuery -Header -Column @"
        SELECT
            SUM(CASE WHEN HasHdr10 = 1 THEN 1 ELSE 0 END) AS HDR10文件,
            SUM(CASE WHEN HasDolbyVision = 1 THEN 1 ELSE 0 END) AS DV文件,
            SUM(CASE WHEN HasDolbyAtmos = 1 THEN 1 ELSE 0 END) AS Atmos文件
        FROM VideoFiles;
"@
}

function Run-CustomQuery {
    param([string]$Sql)
    Write-Host "=== 查询结果 ===" -ForegroundColor Cyan
    Run-SqliteQuery -Header -Column $Sql
}

# 检查sqlite3是否可用
if (-not (Get-Command sqlite3 -ErrorAction SilentlyContinue)) {
    Write-Host "错误: 未找到sqlite3命令" -ForegroundColor Red
    Write-Host "请使用Git Bash运行scripts/view_db.sh，或安装SQLite3到PATH"
    exit 1
}

# 检查数据库是否存在
if (-not (Test-Path $DbPath)) {
    Write-Host "错误: 数据库文件不存在: $DbPath" -ForegroundColor Red
    exit 1
}

# 执行相应操作
switch ($Action.ToLower()) {
    "all" {
        Show-Movies
        Write-Host ""
        Show-Files
        Write-Host ""
        Show-Sources
    }
    "movies" { Show-Movies }
    "files" { Show-Files }
    "sources" { Show-Sources }
    "tmdb" { Show-Tmdb }
    "douban" { Show-Douban }
    "stats" { Show-Stats }
    "query" {
        if ([string]::IsNullOrEmpty($Query)) {
            Write-Host "错误: 请提供SQL查询语句" -ForegroundColor Red
            Write-Host '示例: .\view_db.ps1 query "SELECT * FROM MovieMetadata LIMIT 5"'
            exit 1
        }
        Run-CustomQuery $Query
    }
    default { Show-Help }
}
