#!/bin/bash
# SweetPlayer数据库查看工具

DB_PATH="$LOCALAPPDATA/SweetPlayer/sweetplayer.db"

function show_help() {
    cat << 'EOF'
SweetPlayer数据库查看工具
用法：
    ./view_db.sh [选项]

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
    ./view_db.sh movies
    ./view_db.sh query "SELECT * FROM MovieMetadata WHERE Year = 2025"
EOF
}

function show_movies() {
    echo "=== 电影元数据 ==="
    sqlite3 -header -column "$DB_PATH" "
        SELECT
            Id,
            ChineseTitle,
            Year,
            CASE DataSource WHEN 0 THEN '豆瓣' WHEN 1 THEN 'TMDB' END AS 数据源,
            DoubanId,
            TmdbId,
            DoubanRating AS 评分
        FROM MovieMetadata
        ORDER BY Id;
    "
}

function show_files() {
    echo "=== 视频文件 ==="
    sqlite3 -header -column "$DB_PATH" "
        SELECT
            v.Id,
            v.FileName AS 文件名,
            CASE v.MediaType WHEN 0 THEN '电影' WHEN 1 THEN '电视剧' END AS 类型,
            m.ChineseTitle AS 匹配影片,
            CASE WHEN v.HasHdr10 = 1 THEN 'HDR10' ELSE '' END AS HDR,
            CASE WHEN v.HasDolbyVision = 1 THEN 'DV' ELSE '' END AS DV
        FROM VideoFiles v
        LEFT JOIN MovieMetadata m ON v.MovieMetadataId = m.Id
        ORDER BY v.Id;
    "
}

function show_sources() {
    echo "=== 媒体源 ==="
    sqlite3 -header -column "$DB_PATH" "
        SELECT
            Id,
            Name AS 名称,
            CASE Type WHEN 0 THEN '本地' WHEN 1 THEN 'WebDAV' END AS 类型,
            Path AS 路径
        FROM MediaSources;
    "
}

function show_tmdb() {
    echo "=== 使用TMDB数据源的电影 ==="
    sqlite3 -header -column "$DB_PATH" "
        SELECT
            Id,
            ChineseTitle AS 中文标题,
            OriginalTitle AS 原始标题,
            Year AS 年份,
            TmdbId,
            DoubanRating AS 评分
        FROM MovieMetadata
        WHERE DataSource = 1
        ORDER BY Id;
    "
}

function show_douban() {
    echo "=== 使用豆瓣数据源的电影 ==="
    sqlite3 -header -column "$DB_PATH" "
        SELECT
            Id,
            ChineseTitle AS 中文标题,
            Year AS 年份,
            DoubanId,
            DoubanRating AS 评分
        FROM MovieMetadata
        WHERE DataSource = 0
        ORDER BY Id;
    "
}

function show_stats() {
    echo "=== 数据库统计 ==="
    echo ""
    echo "电影总数："
    sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM MovieMetadata;"
    echo ""
    echo "视频文件总数："
    sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM VideoFiles;"
    echo ""
    echo "数据源分布："
    sqlite3 -header -column "$DB_PATH" "
        SELECT
            CASE DataSource WHEN 0 THEN '豆瓣' WHEN 1 THEN 'TMDB' END AS 数据源,
            COUNT(*) AS 数量
        FROM MovieMetadata
        GROUP BY DataSource;
    "
    echo ""
    echo "已匹配/未匹配文件："
    sqlite3 -header -column "$DB_PATH" "
        SELECT
            CASE WHEN MovieMetadataId IS NULL THEN '未匹配' ELSE '已匹配' END AS 状态,
            COUNT(*) AS 数量
        FROM VideoFiles
        GROUP BY CASE WHEN MovieMetadataId IS NULL THEN '未匹配' ELSE '已匹配' END;
    "
}

function run_query() {
    echo "=== 查询结果 ==="
    sqlite3 -header -column "$DB_PATH" "$1"
}

# 主逻辑
case "$1" in
    all)
        show_movies
        echo ""
        show_files
        echo ""
        show_sources
        ;;
    movies)
        show_movies
        ;;
    files)
        show_files
        ;;
    sources)
        show_sources
        ;;
    tmdb)
        show_tmdb
        ;;
    douban)
        show_douban
        ;;
    stats)
        show_stats
        ;;
    query)
        if [ -z "$2" ]; then
            echo "错误：请提供SQL查询语句"
            echo "示例：$0 query \"SELECT * FROM MovieMetadata LIMIT 5\""
            exit 1
        fi
        run_query "$2"
        ;;
    ""|help|-h|--help)
        show_help
        ;;
    *)
        echo "未知选项：$1"
        echo ""
        show_help
        exit 1
        ;;
esac
