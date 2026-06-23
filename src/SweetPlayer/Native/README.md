# Native Dependencies

此目录用于存放本地平台相关的动态库文件。

## LibMPV 部署

将 `libmpv-2.dll` 放置在对应架构的子目录中：

```
Native/
├── x64/
│   └── libmpv-2.dll      # 64位 Windows
├── x86/
│   └── libmpv-2.dll      # 32位 Windows
└── arm64/
    └── libmpv-2.dll      # ARM64 Windows
```

## 自动部署

运行以下 PowerShell 脚本自动下载和部署 libmpv：

```powershell
# 下载 x64 版本（默认）
.\scripts\setup-libmpv.ps1

# 或指定其他架构
.\scripts\setup-libmpv.ps1 -Architecture x86
.\scripts\setup-libmpv.ps1 -Architecture arm64
```

## 手动部署

1. 从 [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake/releases) 下载对应架构的版本
2. 解压并找到 `libmpv-2.dll`
3. 复制到此目录下对应的架构文件夹

## 更多信息

详细配置说明请参见 [docs/LIBMPV_SETUP.md](../../docs/LIBMPV_SETUP.md)

---

**注意**：DLL 文件已添加到 `.gitignore`，不会提交到版本控制系统。每个开发者需要独立下载和配置。


libmpv 编译选项：-Dprefix=/__w/mpv-winbuild-cmake/mpv-winbuild-cmake/build_x86_64_v3/x86_64_v3-w64-mingw32 -Dlibdir=/__w/mpv-winbuild-cmake/mpv-winbuild-cmake/build_x86_64_v3/x86_64_v3-w64-mingw32/lib -Ddefault_library=shared -Dprefer_static=true -Ddebug=true -Db_ndebug=true -Doptimization=3 -Db_lto=true -Db_lto_mode=thin -Dlibmpv=true -Dpdf-build=enabled -Dlua=enabled -Djavascript=enabled -Dsdl2-gamepad=enabled -Dlibarchive=enabled -Dlibbluray=enabled -Ddvdnav=enabled -Duchardet=enabled -Drubberband=enabled -Dlcms2=enabled -Dopenal=enabled -Dspirv-cross=enabled -Dvulkan=enabled -Dvapoursynth=enabled -Dsubrandr=enabled -Dsixel=enabled -Dgl=enabled -Degl-angle=enabled -Dc_args=-Wno-error=int-conversion --cross-file=/__w/mpv-winbuild-cmake/mpv-winbuild-cmake/build_x86_64_v3/meson_cross.txt