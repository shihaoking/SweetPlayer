# LibMPV 配置指南

## 问题说明

SweetPlayer 依赖 libmpv 动态库（`libmpv-2.dll`）进行视频播放。如果找不到该库，应用会自动降级到模拟播放模式，无法播放真实视频。

## 解决方案

### 方案一：下载预编译的 libmpv（推荐）

1. **下载 libmpv 构建版本**
   - 访问 [shinchiro/mpv-winbuild-cmake releases](https://github.com/shinchiro/mpv-winbuild-cmake/releases)
   - 下载适合你系统架构的版本：
     - `mpv-x86_64-*.7z`（64位系统）
     - `mpv-i686-*.7z`（32位系统）

2. **提取 DLL 文件**
   - 解压下载的 7z 文件
   - 在解压目录中找到 `libmpv-2.dll`（通常在根目录或 `bin` 文件夹）

3. **部署到项目**
   
   选择以下任一方式：

   **方式 A：复制到 Native 目录（开发环境推荐）**
   ```
   SweetPlayer/
   ├── src/
   │   └── SweetPlayer/
   │       ├── Native/
   │       │   ├── x64/
   │       │   │   └── libmpv-2.dll
   │       │   ├── x86/
   │       │   │   └── libmpv-2.dll
   │       │   └── ARM64/
   │       │       └── libmpv-2.dll
   │       └── SweetPlayer.csproj
   ```

   构建时，`SweetPlayer.csproj` 会自动将 `libmpv-2.dll` 复制到输出目录。

   **方式 B：添加到系统 PATH**
   - 将包含 `libmpv-2.dll` 的文件夹添加到系统环境变量 PATH 中
   - 重启应用

   **方式 C：复制到应用执行目录**
   - 将 `libmpv-2.dll` 直接复制到 `bin\Debug\net8.0-windows10.0.19041.0\` 目录
   - 每次构建后需要重新复制

### 方案二：使用 NuGet 包（如果可用）

目前没有官方的 libmpv NuGet 包，但可以考虑社区维护的包：

```xml
<!-- 注意：这些是第三方包，使用前请验证 -->
<PackageReference Include="LibMpv" Version="xxx" />
```

## 验证安装

运行应用并尝试播放视频：

1. **成功**：视频正常播放，控制台输出：
   ```
   [Information] libmpv 初始化成功，硬件解码：d3d11va
   ```

2. **失败**：看到警告并降级到模拟模式：
   ```
   [Warning] 未找到 libmpv 动态库（libmpv-2.dll），降级到模拟播放
   ```

## 技术细节

- **DLL 名称**：`libmpv-2.dll`（在 [MpvInterop.cs](../src/SweetPlayer.Services/Playback/MpvInterop.cs#L15) 中定义）
- **加载机制**：通过 P/Invoke `DllImport` 自动加载
- **搜索顺序**：
  1. 应用程序执行目录
  2. 系统目录 (System32)
  3. Windows 目录
  4. 当前工作目录
  5. PATH 环境变量中的目录

## 常见问题

### Q: 为什么使用 libmpv-2.dll 而不是 libmpv-1.dll？
A: libmpv-2 是较新的 ABI 版本，支持更多功能和更好的稳定性。

### Q: 可以使用 VLC 或其他播放器引擎吗？
A: 当前架构设计为 libmpv 专用。如需更换引擎，需要重新实现 `IMpvPlayerService` 接口。

### Q: ARM64 支持如何？
A: libmpv 有 ARM64 构建版本，但可能需要单独下载并测试兼容性。

### Q: 如何在发布版本中包含 DLL？
A: 使用方案一的方式 A，并确保在发布配置中也包含 Native 文件夹。

## 相关文件

- [MpvInterop.cs](../src/SweetPlayer.Services/Playback/MpvInterop.cs) - P/Invoke 声明
- [MpvPlayerService.cs](../src/SweetPlayer.Services/Playback/MpvPlayerService.cs) - 播放器服务实现
- [SweetPlayer.csproj](../src/SweetPlayer/SweetPlayer.csproj) - 项目配置

## 参考资源

- [MPV 官方网站](https://mpv.io/)
- [libmpv 文档](https://mpv.io/manual/master/#embedding-into-other-programs-libmpv)
- [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake) - Windows 预编译版本
