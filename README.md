# QuickLook Lite（精简版）v1.2.31

基于 QuickLook 4.5.0 的 .NET 10 迁移版（net10.0 分支）精简而来，只保留常用格式，
默认开启 Win11 Mica 背景效果。独立分支 `lite`、独立文件夹，与完整版互不干扰
（管道/互斥体使用 `QuickLook.Lite.*` 命名）。

## 保留的插件（14 个）

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

文件夹预览由主程序内置的 InfoPanel 默认面板提供（无需插件）。

## 相对 net10 完整版的改动

- 删除 11 个插件：Binary/Cert/Chm/Db/Dump/ELF/Helix/Mail/PE/Prefetch/Thumbnail
- 更新解决方案 `QuickLook.slnx`（移除已删插件与 WiX 安装工程）
- **删除原生 C++ 依赖（QuickLook.Native）**：空格键链路（焦点判断 + Explorer/桌面
  选区读取）改为纯 C# 实现（P/Invoke + Shell COM），不再需要 VS C++/ATL 工具链，
  构建只需 .NET SDK。第三方文件管理器（DOpus/Everything 等）集成不在精简版范围
- Win11 Mica 默认开启：`WindowBackdrop` 默认值 `Auto` → `Mica`
  （Win10 自动回退 Acrylic/Blur，不影响低版本系统）
- 命名隔离：管道 `QuickLook.Lite.App.Pipe.*`、互斥体 `QuickLook.Lite.App.Mutex`，
  避免与已安装的完整版冲突
- 修复第二实例挂起：管道连接加 2 秒超时
- 测试覆盖：png/txt/md/zip/ttf/mp4 预览 + Shell 选区读取链路（test.ps1，提交前必须全绿）

## v1.1.0 更新内容

- **图片预览顶栏自动隐藏**：鼠标静止约 1 秒后自动淡出，鼠标移入时重新显示
- **顶栏 Mica 效果**：去掉纯黑遮罩，顶栏透出 Win11 Mica 背景（深色模式同样生效），
  内容不再滑入顶栏区域

## v1.1.1 修复内容

- **修复自动隐藏失效**：`AutoHideCaptionContainer` 原先在 `TitlebarOverlap=false`
  时直接返回（这是视频预览模式的隐藏前置条件），导致图片顶栏显示后永不隐藏。
  已移除该限制，顶栏独立于内容（不重叠）时同样自动隐藏
- **修复 Mica 观感**：顶栏的模糊层与噪点层一并关闭（`BlurVisibility=false`，
  噪点改为跟随模糊开关），顶栏现在完全透明，纯透出窗口的 Mica 背景，不再有
  深色噪点颗粒

## v1.1.2 修复内容

- **修复隐藏后残留黑框**：深色模式 Mica 的基础色就是 #202020，顶栏独立条带
  （`Overlap=false`）隐藏后会留下一块深色区域。现在图片内容延伸到顶栏条带
  （`Overlap=true`，与视频预览一致），工具栏隐藏后该区域显示的是图片本身，
  不再有黑框；窗口的 Mica 背景仍在窗口边缘生效
- 图片自身的操作按钮（复制/元数据/背景）下移 40px，避免与顶栏按钮重叠

## v1.1.3 交互逻辑调整

- **工具栏默认隐藏**：预览打开后的前 600ms 忽略鼠标移动事件（窗口打开在光标附近时
  WPF 会合成一次 MouseMove，导致工具栏一闪而过），确保打开时完全隐藏
- **鼠标移到预览区才显示**：之后任意鼠标移动显示工具栏，静止约 1 秒后自动淡出

## v1.1.4 交互逻辑修正

- **只响应真实鼠标移动**：600ms 抑制窗口无法覆盖「切换预览」的场景（内容重绘同样会
  合成 MouseMove）。改为在渲染时记录光标基线坐标，收到 MouseMove 时用
  `GetCursorPos` 对比屏幕坐标——坐标未变即合成事件，直接忽略；只有坐标真正变化
  （用户移动鼠标）才显示工具栏。窗口打开、切换文件、窗口移动都不再触发闪现

## v1.2.0 全场景主题统一

- **所有预览场景统一现代工具栏**：`ContextObject` 默认值改为「自动隐藏 + 内容重叠
  + 透明工具栏」，MD/文本/Office/PDF/压缩包/HTML 等不再显示常驻状态栏、不再有
  深色条带；视频保留其深色玻璃栏观感
- **WebView2 背景跟随主题**：去掉页面加载后强制白色背景的逻辑，改为跟随系统主题
  （深色 #202020 / 浅色白），MD/HTML 内容在深色模式下不再出现白边/黑边
- **亮/暗模式在所有场景生效**：窗口 Mica 与 Web 内容均跟随系统主题
  （MD 模板已通过 `prefers-color-scheme` 切换 github-dark CSS）

## v1.2.1 更新内容

- **MD/Web 内容支持 Mica**：WebView2 背景改为透明，并向页面注入透明背景脚本，
  MD/HTML/AsciiDoc/ipynb/rst 的页面背景透出窗口 Mica，不再是纯黑
- **顶栏新增亮/暗切换按钮**（太阳/月亮图标）：手动切换主题，窗口、Mica 与 Web
  内容同步变化（Web 通过 PreferredColorScheme + 重载预览跟随），选择会被记住
- **快捷方式**：桌面「QuickLook Lite.lnk」与 `D:\Codex\QuickLook-Lite.lnk`
  直达最新版 exe，无需层层翻目录

## v1.2.2 修复内容

- **修复工具栏黑条 + 不自动隐藏（关键）**：`ContextObject.Reset()` 在每次预览前
  把标题栏标志硬编码重置为旧值（AutoHide=false、Colour=true），导致 MD/TXT 等
  非图片预览又变回「常驻黑色工具栏」。现在 Reset() 重置为现代默认
  （自动隐藏 + 内容重叠 + 透明工具栏），全场景生效
- **修复 MD 背景未透出 Mica**：透明注入脚本改为在 DOMContentLoaded 后注入
  `!important` 样式（之前脚本在 body 未创建时运行，直接失败），页面背景真正透明
- **WebView2 初始化失败会写日志**，不再无声黑屏
- **自动隐藏更彻底**：移除「光标悬停工具栏则不隐藏」的守卫，静止 1 秒后必然淡出

## v1.2.3 更新内容

- **细滚动条**：MD/HTML 等 Web 内容滚动条改为 6px 细条（半透明圆角滑块，轨道透明）；
  文本预览的 WPF 滚动条改为 8px 细条（半透明圆角滑块），观感更清爽

## v1.2.5 更新内容

- **滚动条再变小**：Web 内容滚动条 6px → **4px**；文本滚动条 8px → **6px**，滑块更淡
- **修复文本滚动条样式未生效**：v1.2.3 用「加载后遍历视觉树」应用样式，时机不可靠
  （AvalonEdit 惰性创建滚动条），改为**隐式样式**放在面板资源里，滚动条无论何时
  创建都会被套上细样式

## v1.2.6 更新内容

- **修复桌面文件无法预览**：`IShellWindows.FindWindowSW` 在 .NET Core 下用
  `dynamic` 调用会失败/崩溃，改为正确的 dual COM 接口 + 手动构造 VARIANT 传递，
  并增加桌面列表视图回退路径

## v1.2.7 更新内容

- **修复亮/暗切换无效**：`ContextObject.Reset()` 在每次预览前把 Theme 重置为
  「跟随系统」，刚切换的主题被立即撤销。现在 Reset() 不再重置 Theme，手动选择的
  主题在切换/重载后持续生效
- **滚动条全局统一**：把应用统一的 `ScrollBarStyleDictionary.xaml` 改为细样式
  （6px、半透明圆角滑块、透明轨道），TXT/PDF 等所有 WPF 滚动条一处生效；
  删除文本预览的独立滚动条样式，集中维护

## 构建与测试

```powershell
.\build.ps1   # 构建主程序 + 公共库 + 14 个插件
.\test.ps1    # 提交前冒烟测试（干净构建 + 启动 + 6 类预览 + 日志零错误）
```

产物输出到 `Build\Release\`，需要 .NET 10 SDK。

## 已知事项

- Office 预览走系统预览处理器（Office 自带界面，离线可用）；其内部工具栏/
  状态栏由 Office 渲染，无法由 QuickLook 隐藏
- 视频后端仍为 WPFMediaKit（DirectShow/LAV）；如遇特定格式问题，下一步可替换为
  LibVLCSharp
- 空格键预览依赖全局键盘钩子；以管理员身份运行的窗口（UIPI 限制）无法触发，
  这是 Windows 平台所有同类工具的固有限制
