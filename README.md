# QuickLook-Next

> QuickLook 的 **UI 美化与功能完善版**（基于 [QL-Win/QuickLook](https://github.com/QL-Win/QuickLook)
> 4.5.0 的 .NET 10 迁移版）。

QuickLook-Next 不再只是“精简版”：保留完整的文件预览能力，并把预览背景、窗口圆角、
主题、托盘菜单、插件管理、语言、自动更新等体验全面重构与增强——**功能只多不少**。

独立分支 `lite`，与官方完整版命名隔离（管道 / 互斥体使用 `QuickLookNext.App.*`），
可同时安装互不干扰。

## 相比原版的主要改进

### UI 美化

- **Acrylic 一打开即生效、跟随壁纸**：原版在 Win11 上使用 DWM Acrylic，而预览窗口
  从不抢焦点，导致文本 / 代码等内容打开时是纯色、点击后才出现毛玻璃。QuickLook-Next
  改用 WCA 方案，打开即是毛玻璃，且桌面壁纸变化时背景同步变化
- **Win11 原生圆角**：毛玻璃与内容一起 8px 圆角，没有方形毛玻璃边角
- **统一 Acrylic 观感**：预览窗口、托盘菜单、插件管理面板同为无边框非分层
  WCA 毛玻璃 + DWM 圆角，没有方形毛玻璃边角，也没有多余的外圈投影
- **亮色 / 暗色 / 跟随系统**：托盘菜单一键切换并持久化，预览立即生效
- **顶部状态栏默认隐藏**：鼠标移到窗口顶部标题栏区域才显示，移开后自动隐藏，
  不遮挡内容
- **托盘菜单分组**：主题模式、背景模式、语言、选项收进二级子菜单，顶层保持简短

### 功能完善

- **插件管理面板（原版没有）**：列出用户安装与内置插件，用户插件可直接卸载；
  插件契约保持 `QuickLook.Common` / `QuickLook.Plugin.*`，老插件无需重新编译即可
  安装加载
- **内置语言切换**：托盘菜单「语言」子菜单支持跟随系统 + 30 种语言，选择后持久化
- **自动更新**：检查更新时直接下载 Release 安装包并原地更新重启，不再只是打开网页
- **文本预览可上下滚动**：修复分层窗口收不到滚轮消息的问题，txt / log / json /
  代码等预览可正常滚动
- **预览打开提速**：第二实例不再初始化 WPF，直接通过命名管道转发给常驻实例；
  图片解码管线在启动后台预热，首次预览不再支付一次性初始化成本

### 工程与架构

- **.NET 10 迁移**：目标框架 net10.0-windows，构建只需 .NET SDK
- **纯 C# 空格键链路**：焦点判断 + Explorer / 桌面选区读取改为 P/Invoke + Shell COM，
  不再依赖原生 C++ 工具链
- **命名隔离**：管道 / 互斥体使用 `QuickLookNext.App.*`，与官方完整版互不干扰
- **自动化测试**：png / txt / md / zip / ttf / mp4 预览 + Shell 选区链路（test.ps1，
  提交前必须全绿）

## 安装与使用

1. 从 [Releases](https://github.com/Adstrax/QuickLook-Next/releases) 下载最新版，解压后运行
   `QuickLook-Next.exe`
2. 选中文件按 **空格** 预览，**Esc** 关闭；预览窗口支持置顶、跨预览拖拽内容
3. 托盘图标右键可切换主题 / 背景 / 语言、管理插件、检查更新等

## 构建

需要 .NET 10 SDK：

```powershell
dotnet build QuickLookNext.slnx -c Release
```

提交前运行冒烟测试：`.\test.ps1`（要求全部通过）。

生成用户友好的发布包（exe 位于解压根目录，插件在 `QuickLook.Plugin` 子目录，
自带便携模式）：

```powershell
.\Scripts\pack-release.ps1 -MakeZip
```

产物：`Build\QuickLook-Next-<版本号>.zip`，可直接上传到 GitHub Release 供用户下载。

## 支持的文件格式

文件夹预览由主程序内置的 InfoPanel 提供，内置 14 个常用插件，覆盖日常所有格式：

| 插件 | 用途 |
|---|---|
| ImageViewer | 图片（png/jpg/gif/webp/bmp/psd/raw/heic/svg/ico 等 100+ 格式） |
| VideoViewer | 视频与音频（MediaInfo 嗅探，支持 mp4/mkv/avi/mov/webm/mp3/flac 等） |
| TextViewer | 文本与代码（txt/log/ini/json/xml/rtf/csv 及数百种代码语言） |
| MarkdownViewer | Markdown（md/mdx/mermaid/ipynb/adoc/rst 等） |
| OfficeViewer | Office（doc/docx、xls/xlsx、ppt/pptx、odt/ods/odp、vsd/vsdx） |
| HtmlViewer | HTML/MHT/URL（WebView2 渲染，也是 MD 插件的依赖） |
| PdfViewer | PDF |
| ArchiveViewer | 压缩包与安装包（zip/rar/7z/tar/gz/bz2/xz/cbz/cbr/jar/apk/msi 等） |
| CsvViewer | CSV/TSV/PSV 表格化视图 |
| FontViewer | 字体（ttf/otf/woff/woff2/ttc/eot） |
| MediaInfoViewer | 右键菜单查看媒体信息 |
| CLSIDViewer | 系统 shell 特殊对象（我的电脑、回收站等） |
| AppViewer | 应用安装包详情（apk/ipa/msi/dmg/deb/rpm 等） |
| PluginInstaller | .qlplugin 插件安装 |

## 更新历史

详细更新记录见 [CHANGELOG.md](CHANGELOG.md) 与 GitHub [Releases](https://github.com/Adstrax/QuickLook-Next/releases)。
