# 数据库查看工具使用说明

本目录包含查看SweetPlayer数据库的实用工具。

## 工具列表

### 1. view_db.sh (Linux/Mac/Git Bash)
Bash脚本，适用于Git Bash或Linux/Mac环境。

### 2. view_db.ps1 (Windows PowerShell)
PowerShell脚本，适用于Windows原生环境。

## 使用方法

### Windows用户（推荐使用Git Bash）

```bash
# 在项目根目录执行
bash scripts/view_db.sh stats
bash scripts/view_db.sh movies
bash scripts/view_db.sh tmdb
```

### Windows用户（PowerShell）

```powershell
# 在项目根目录执行
.\scripts\view_db.ps1 stats
.\scripts\view_db.ps1 movies
.\scripts\view_db.ps1 tmdb
```

## 可用命令

| 命令 | 说明 | 示例 |
|------|------|------|
| `stats` | 显示数据库统计信息（推荐首次使用） | `bash scripts/view_db.sh stats` |
| `movies` | 显示所有电影元数据 | `bash scripts/view_db.sh movies` |
| `files` | 显示所有视频文件 | `bash scripts/view_db.sh files` |
| `sources` | 显示媒体源 | `bash scripts/view_db.sh sources` |
| `tmdb` | 仅显示使用TMDB数据源的电影 | `bash scripts/view_db.sh tmdb` |
| `douban` | 仅显示使用豆瓣数据源的电影 | `bash scripts/view_db.sh douban` |
| `all` | 显示所有表的数据 | `bash scripts/view_db.sh all` |
| `query` | 执行自定义SQL查询 | `bash scripts/view_db.sh query "SELECT * FROM MovieMetadata WHERE Year=2025"` |

## 输出示例

### stats - 统计信息
```
=== 数据库统计 ===

电影总数：
50

视频文件总数：
140

数据源分布：
数据源   数量
----  --
豆瓣    24
TMDB  26

已匹配/未匹配文件：
状态   数量
---  --
已匹配  54
未匹配  86
```

### tmdb - TMDB数据源电影
```
=== 使用TMDB数据源的电影 ===
Id  中文标题                 原始标题                        年份    TmdbId   评分   
--  ------------------  ---------------------------  ----  -------  -----
10  复仇者联盟2：奥创纪元      Avengers: Age of Ultron      2015  99861    7.273
11  复仇者联盟4：终局之战      Avengers: Endgame            2019  299534   8.238
12  复仇者联盟3：无限战争      Avengers: Infinity War       2018  299536   8.237
```

## 数据库位置

Windows: `%LOCALAPPDATA%\SweetPlayer\sweetplayer.db`  
完整路径: `C:\Users\YourUsername\AppData\Local\SweetPlayer\sweetplayer.db`

## 高级用法

### 自定义查询

查看特定年份的电影：
```bash
bash scripts/view_db.sh query "SELECT ChineseTitle, Year, CASE DataSource WHEN 0 THEN '豆瓣' WHEN 1 THEN 'TMDB' END AS Source FROM MovieMetadata WHERE Year = 2025"
```

查看HDR视频文件：
```bash
bash scripts/view_db.sh query "SELECT FileName, HasHdr10, HasDolbyVision FROM VideoFiles WHERE HasHdr10 = 1 OR HasDolbyVision = 1"
```

统计每年的电影数量：
```bash
bash scripts/view_db.sh query "SELECT Year, COUNT(*) as Count FROM MovieMetadata GROUP BY Year ORDER BY Year DESC"
```

## 使用GUI工具

如果您更喜欢图形界面，可以使用以下SQLite浏览器：

1. **DB Browser for SQLite** (推荐)
   - 下载: https://sqlitebrowser.org/
   - 免费开源，功能强大

2. **SQLiteStudio**
   - 下载: https://sqlitestudio.pl/
   - 跨平台，功能丰富

3. **VS Code扩展**
   - 安装 "SQLite" 或 "SQLite Viewer" 扩展
   - 直接在VS Code中打开.db文件

## 注意事项

- 这些工具**只读取**数据，不会修改数据库
- 确保在查看数据库时应用没有运行（避免文件锁定）
- 如果遇到权限问题，请以管理员身份运行

## 验证TMDB功能

要验证TMDB fallback功能是否正常工作，可以：

1. 查看统计信息中的数据源分布：
   ```bash
   bash scripts/view_db.sh stats
   ```

2. 查看TMDB数据源的电影列表：
   ```bash
   bash scripts/view_db.sh tmdb
   ```

3. 检查日志文件中是否有"TMDB fallback"相关信息

## 常见查询

### 查看未匹配的视频文件
```bash
bash scripts/view_db.sh query "SELECT FileName FROM VideoFiles WHERE MovieMetadataId IS NULL"
```

### 查看最近刮削的元数据
```bash
bash scripts/view_db.sh query "SELECT ChineseTitle, Year, ScrapedAt, CASE DataSource WHEN 0 THEN '豆瓣' WHEN 1 THEN 'TMDB' END AS Source FROM MovieMetadata ORDER BY ScrapedAt DESC LIMIT 10"
```

### 查看评分最高的电影
```bash
bash scripts/view_db.sh query "SELECT ChineseTitle, Year, DoubanRating FROM MovieMetadata WHERE DoubanRating IS NOT NULL ORDER BY DoubanRating DESC LIMIT 10"
```
