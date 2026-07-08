# Fluent Steam Lua 管理工具

基于 WPF + Fluent Design 的现代化 Steam Lua 入库管理工具，仅适配OpenSteamTool

## 预览

| 主页 | 设置页 | 入库页 |
|------|--------|--------|
| ![主页](screenshots/home.png) | ![设置页](screenshots/settings.png) | ![入库页](screenshots/script-download.png) |

## 功能特性

- 📂 自动/手动扫描 Steam Lua 文件
- 🖼️ 封面图片自动下载与缓存
- 📥 一键搜索预览并入库新游戏 支持 AppId / 游戏名 模糊搜索
- ⚡ 封面CDN 节点测速与自动切换
- 👁️ 文件变更自动监控并刷新缓存（FileSystemWatcher）
- 🎨 Fluent Design 现代化界面（Acrylic 亚克力 / NavigationView / 圆角过渡）

## 技术栈

| 层级 | 技术 |
|------|------|
| 框架 | .NET 8 WPF |
| 语言 | C# 12 |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| Fluent 控件 | iNKORE.UI.WPF.Modern 0.10.2 |
| DI 容器 | Microsoft.Extensions.DependencyInjection 8.0 |

## 系统要求

- Windows 10 1809+ / Windows 11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## 构建方法

发布为单文件可执行程序：

```bash
dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
```

发布为散文件可执行程序：

```bash
dotnet publish -c Release -r win-x64
```

## 许可

[MIT](LICENSE)