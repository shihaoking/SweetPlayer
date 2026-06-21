# 快速开始指南

本指南帮助你快速设置和运行 SweetPlayer。

## 环境要求

- **操作系统**：Windows 10 版本 1809 (build 17763) 或更高
- **.NET SDK**：.NET 8.0 或更高
- **Visual Studio 2022** 或 **VS Code**（可选，推荐）
- **PowerShell 5.1+**（用于运行部署脚本）

## 步骤 1：克隆项目

```bash
git clone https://github.com/yourusername/SweetPlayer.git
cd SweetPlayer
```

## 步骤 2：部署 LibMPV

### 自动部署（推荐）

在项目根目录运行 PowerShell 脚本：

```powershell
# Windows x64（默认）
.\scripts\setup-libmpv.ps1

# 其他架构
.\scripts\setup-libmpv.ps1 -Architecture x86
.\scripts\setup-libmpv.ps1 -Architecture arm64
```

脚本会：
1. 从 GitHub 下载最新的 libmpv 预编译版本
2. 自动解压
3. 将 `libmpv-2.dll` 部署到 `src\SweetPlayer\Native\{架构}\` 目录（文件名必须为 `libmpv-2.dll`）

### 手动部署

如果自动脚本失败，请参考 [LibMPV 配置指南](LIBMPV_SETUP.md) 进行手动配置。

## 步骤 3：还原依赖

```bash
dotnet restore
```

## 步骤 4：构建项目

```bash
# 构建 Debug 版本
dotnet build

# 或构建 Release 版本
dotnet build -c Release
```

## 步骤 5：运行应用

```bash
dotnet run --project src/SweetPlayer
```

或者在 Visual Studio 中按 `F5` 启动调试。

## 验证安装

### 检查 LibMPV

运行应用后，查看控制台输出：

✅ **成功**：
```
[Information] libmpv 初始化成功，硬件解码：d3d11va
```

❌ **失败（降级到模拟模式）**：
```
[Warning] 未找到 libmpv 动态库（libmpv-2.dll），降级到模拟播放
```

如果看到警告，请确认：
1. `libmpv-2.dll` 存在于 `src\SweetPlayer\Native\{你的架构}\` 目录
2. 项目已重新构建
3. DLL 已复制到输出目录（检查 `bin\Debug\net8.0-windows10.0.19041.0\libmpv-2.dll`）

## 步骤 6：配置 TMDB（可选）

如果想使用 TMDB 作为元数据备用源：

1. 从 [TMDB](https://www.themoviedb.org/settings/api) 获取免费 API 密钥
2. 编辑 `src\SweetPlayer\appsettings.json`：

```json
{
  "Tmdb": {
    "ApiKey": "your_api_key_here"
  }
}
```

## 功能测试

### 测试视频播放

1. 启动应用
2. 点击"添加源" → 选择包含视频文件的本地文件夹
3. 等待扫描完成
4. 点击任意视频海报开始播放

### 测试元数据抓取

应用会自动：
- 从豆瓣获取电影/电视剧信息
- 如果豆瓣没有结果，回退到 TMDB（需要配置 API Key）
- 下载并显示海报

### 测试 HDR 检测

播放 HDR 或 Dolby Vision 视频时，应用会：
- 自动检测视频格式
- 在海报上显示相应的徽章（HDR10 / Dolby Vision / Dolby Atmos）
- 自动启用 Windows HDR 模式（如果硬件支持）

## 常见问题

### Q: 构建失败，提示找不到 Windows SDK
**A**: 安装 Windows SDK 10.0.19041.0 或更高版本。可以通过 Visual Studio Installer 安装。

### Q: 视频无法播放，显示黑屏
**A**: 检查：
1. `mpv-2.dll` 是否正确部署
2. 视频文件格式是否受支持
3. 查看控制台是否有错误日志

### Q: 元数据抓取失败
**A**: 检查：
1. 网络连接是否正常
2. 豆瓣/TMDB API 是否可访问
3. 视频文件名是否包含正确的标题和年份信息（例如：`Inception (2010).mkv`）

### Q: PowerShell 脚本执行被阻止
**A**: 以管理员身份运行 PowerShell，执行：
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## 下一步

- 📖 阅读 [LibMPV 配置指南](LIBMPV_SETUP.md) 了解更多部署选项
- 📖 阅读 [TMDB 使用说明](TMDB_USAGE.md) 配置元数据服务
- 🐛 遇到问题？查看 [GitHub Issues](https://github.com/yourusername/SweetPlayer/issues)
- 💡 有建议？欢迎提交 Pull Request！

## 开发模式

### 启用热重载

```bash
dotnet watch --project src/SweetPlayer
```

### 运行测试

```bash
dotnet test
```

### 查看日志

应用日志会输出到：
- **控制台**：实时日志
- **调试输出**：Visual Studio 输出窗口

---

🎉 现在你可以开始使用 SweetPlayer 了！
