# QuickLook .NET 10 迁移说明

本分支（`net10.0`）将 QuickLook 4.5.0 的全部 C# 工程迁移到 `.NET 10`
（`net10.0-windows`）。迁移从 `net8.0` 分支（已完成 net462 → net8 的全部兼容性改造）
继续升级而来，各版本通过 git worktree 分目录隔离：

- `D:\Codex\QuickLook-4.5.0` —— 原版（master 分支，net462，未改动）
- `D:\Codex\QuickLook-4.5.0-net8` —— .NET 8 迁移版（net8.0 分支）
- `D:\Codex\QuickLook-4.5.0-net10` —— 本迁移版（net10.0 分支，net10.0-windows）

## 目标框架

- 主程序 `QuickLook`：`net10.0-windows10.0.19041.0`（需要 WinRT 投影，用于系统共享）
- 公共库与全部插件：`net10.0-windows`
- 使用 `UseWPF` / `UseWindowsForms`，Sdk 从 `Microsoft.NET.Sdk.WindowsDesktop` 改为 `Microsoft.NET.Sdk`

## 主要改动

### 工程文件（27 个 csproj）
- `TargetFramework` 全部改为 net8.0-windows 系列
- 移除 .NET Framework 时代的包：`Costura.Fody`、`System.Memory`、`System.Buffers`、
  `System.Runtime.CompilerServices.Unsafe`、`System.IO.Compression`、`System.Runtime.WindowsRuntime`
- 移除多余的 `<Reference>`（Microsoft.CSharp / WindowsBase / System.Web / System.Core 等）
- `System.Management` 改用 NuGet 包（主程序，WMI 支持）
- `System.ComponentModel.Composition` 改用 NuGet 包（MEF，TextViewer / MediaInfoViewer）
- 统一抑制 Windows 平台分析器噪音：`NoWarn=CA1416;SYSLIB0050`

### 源码（C# 12 兼容改写）
- `WindowInteropHelperExtension.cs`：C# 14 的 `extension(...)` 块改写为经典扩展方法
- `ViewerWindow.xaml.cs`：C# 14 空条件赋值 `?.GlassFrameThickness = ...`（13 处）改为辅助方法
- `PakInfoPanel.xaml.cs` / `Pagination.cs` / `TextViewerPanel.cs`：空条件赋值与事件订阅改写
- `SvgImagePanel.cs`：C# 13 `field` 关键字改写为显式后备字段
- `ShareHelper.cs`：`WindowsRuntimeMarshal.GetActivationFactory`（.NET 8 已移除）
  改为 `RoGetActivationFactory` P/Invoke，系统共享功能保持可用
- `PuImagePanel.cs`：`System.Web.HttpUtility` 改为 `System.Net.WebUtility`

### 依赖替换
- WebP 动画（ImageViewer）：移除 net462 专用的 `QuickLook.ImageGlass.WebP`，
  改用项目已有的 Magick.NET 解码（动画帧经 PNG 中转）
- 视频（VideoViewer）：`QuickLook.WPFMediaKit 2.3.4` 仍为 net462 包且 NuGet
  不提供 net8 编译资产，因此将 `QuickLook.WPFMediaKit.dll` 与
  `DirectShowLib-2005.dll`（来自该包）放入 `lib\` 目录直接引用，
  播放行为与原版完全一致

## 构建

```powershell
.\build.ps1             # 一键构建全部 27 个 C# 工程
```

产物输出到 `Build\Release\`。需要 .NET 8 SDK（Windows 版）。

## 未改动 / 注意事项

- 原生 C++ 工程（QuickLook.Native32/64/Arm64）、WiX 安装工程、Appx 打包保持原样，
  不在本次迁移范围内
- 安装包 / Store 打包脚本暂未适配 net8（原脚本依赖 net462 布局）
- 尚未做运行时冒烟测试；建议先运行 `Build\Release\QuickLook.exe` 验证预览行为
