# QuickLook Changelog

> QuickLook Changelog starting from version `4.0.0`.

## QuickLook Lite 1.2.35

- 预览延迟优化（大文件 / JSON 场景提速明显）：
  - PDFViewer 只对无扩展名文件做魔数检测，其他文件按扩展名匹配，每次预览
    不再为无关格式白开一次文件（慢盘/网络盘受益明显）
  - `.json` 的 Lottie 检测从"整文件读取 + 解析"改为只读前 256KB，大 JSON
    （如 package-lock.json）预览不再卡顿（4.3MB JSON 从约 1.8 秒降到约 0.9 秒）
  - 文本编码检测改为对文件头 256KB 采样（chardet 类算法在大输入上极慢）
  - 超过 0.5MB 的文本跳过格式检测器扫描（该大小下高亮本已禁用，扫描结果
    用不上）
  - 语法高亮定义加载从"首次文本预览的 UI 线程"挪到后台插件加载阶段，
    首次文本预览再省约 500ms
- 冒烟测试与 `bench.ps1` 新增 test.json 覆盖

## QuickLook Lite 1.2.34

- 启动再提速：移除已废弃的 TrayIconWindow 预热窗口（原生托盘右键菜单早在
  v1.2.8 已停用，该窗口只剩开销）；启动（UI + 插件就绪）从约 0.35 秒进一步
  降到约 0.08 秒（热缓存下稳定 ~75 ms）
- `test.ps1` / `build.ps1` 改为一次并行构建整个解决方案（原来 16 个项目
  逐个编译），冒烟测试总耗时显著下降
- 移除 NuGet 包复制进输出的 win-x86 / win-arm64 运行时副本
  （WebView2Loader 等，新增共享清理目标），发布体积再减约 1 MB（170.5 MB）

## QuickLook Lite 1.2.33

- 启动提速：MessageBox 的 Harmony 补丁（.NET 10 下耗时约 1.2 秒）从启动
  同步执行改为延迟 3 秒后台执行；启动（UI + 插件就绪）从约 1.64 秒降到约
  0.35 秒（快约 78%）。补丁未完成时消息框使用默认样式，功能不受影响
- 新增隐藏 `/test-startup` 诊断钩子，记录各启动阶段耗时；
  `bench.ps1` 现在同时输出启动耗时与预览延迟

## QuickLook Lite 1.2.32

- PDF 预览瘦身：PDFium 由带 JavaScript 引擎的 V8 版换成普通版
  （`bblanchon.PDFium.Win32` 153.0），`pdfium.dll` 从 28.9 MB 降至 6.9 MB；
  预览渲染不受影响（预览不需要 PDF 的 JS 交互能力）
- 冒烟测试新增 PDF 预览覆盖（14 页示例文档），PDFViewer 变更后全绿
- 修复 SQLite 高危漏洞警告：`Microsoft.Data.Sqlite` 升到 10.0.11，
  消除 NU1903（GHSA-2m69-gcr7-jv3q）
- 新增 `bench.ps1` 预览延迟基准（基于内置 `/test-timing` 钩子）；
  首次基线（含进程启动与管道开销）：png 0.73s / txt 1.02s / md 0.40s /
  zip 0.43s / ttf 0.69s / pdf 0.45s

## QuickLook Lite 1.2.31

- Release 体积瘦身（仅保留 64 位）：移除视频插件的 LAVFilters-x86（约 24 MB）
  与 MediaInfo win-x86（约 7 MB）、图片插件的 exiv2-ql-32、字体插件的
  freetype win-x86 等全部 32 位运行时副本，发布目录从约 227 MB 降至约 194 MB
  （-15%）。本版本仅面向 64 位 Windows（Win11）
- 图片 exiv2 元数据读取精简为纯 64 位路径，删除 x86 分支代码
- 修复插件加载失败警告框在无窗口启动（开机自启/托盘模式）时因 Owner 未显示
  而崩溃的问题：现在只在存在可见窗口时弹窗，否则仅写日志

## QuickLook Lite 1.2.30

- Image previews no longer show the top-right action icons (copy / metadata /
  background) or the image-info tag that appeared on hover; the image area is
  now completely clean

## QuickLook Lite 1.2.16

- Fix the broken startup shortcut: `Assembly.Location` resolves to QuickLook.dll
  under the .NET apphost, so the auto-start shortcut (and the shell
  context-menu command / restart) pointed at the DLL - Windows then tried to
  "open" the DLL after every restart. The executable path is now resolved
  explicitly, so auto-start launches QuickLook.exe properly

## QuickLook Lite 1.2.15

- Video previews show the file's thumbnail while the media opens, so the start
  of a video no longer shows a blank/gray loading surface or the busy spinner
- The busy spinner is disabled for video previews (the thumbnail covers the
  opening moment; the panel keeps its black background as a fallback)
- The video renderer surface stays hidden while DirectShow builds the playback
  graph ("播放区建立" phase) and is only revealed once the media is open, so
  the renderer's blank gray surface can never be seen; the thumbnail covers
  the short reveal moment as well
- Video files set HasVideo immediately, so the audio cover panel (music note +
  tags) no longer flashes as a gray area before the video actually opens

## QuickLook Lite 1.2.14

- Image previews now decode their first frame before the content is swapped
  in, so switching images never flashes a blank gray panel - the previous
  image stays on screen until the new one is ready (with a 3 s fallback)
- The old preview's resources are disposed only after the new content takes
  over, instead of being torn down mid-switch
- Fix the gray loading area that flashed while switching images: the content
  container was hidden during loading (IsBusy), leaving the bare backdrop
  visible; the previous preview now stays fully rendered until the new one is
  ready, and the window only resizes once the new frame is decoded
- Replace the separate-thread busy overlay with an in-process spinner so the
  loading indicator can no longer paint over the preview
- Video previews no longer show the gray backdrop while the media opens: the
  video panel now has a black background (standard player look)
- Image switches no longer flash the spinner or the zoom-percentage badge; the
  spinner only shows for the initial load, and the zoom badge only appears on
  manual zooming
- Tray menu: new "Backdrop Mode" section to switch between Auto / None /
  Mica / Acrylic / Acrylic 10 / Acrylic 11 / Tabbed; applies to the open
  preview immediately and persists

## QuickLook Lite 1.2.13

- Performance: cache the downscaled image decode per file, so re-previewing or
  switching back to an image no longer re-decodes the whole file (spinner goes
  from ~200 ms to near-instant for large photos)
- Performance: defer EXIF metadata reading until after the image is displayed,
  so the busy spinner no longer waits for the exiv2 metadata scan
- Performance: cache MediaInfo results per file and skip the native sniff for
  unambiguous media extensions, speeding up video/audio matching on switches
- Performance: populate the audio info panel (tags/cover art) only after the
  media has opened, so embedded covers no longer delay the first frame
- Switching previews keeps the previous content on screen until the new one is
  ready, so switches no longer flash an empty gray window
- Re-fit the image to the window once layout settles after loading, preventing
  intermittent unfitted images with blank space around them
- Softer, smaller busy spinner (drop shadow, no box) that reads cleanly over
  content
- Performance: keep the startup optimizations from the 1.2.9 line (background
  plugin loading, lazy GPU blacklist, deferred preview window creation,
  throttled post-close GC)
- Add a hidden `/test-timing` startup switch that records content-ready
  timestamps for automated preview-latency benches

## QuickLook Lite 1.2.10

- Switch tray menu, "More" menu and the preview window default backdrop from Mica to Acrylic - the same frosted-glass effect as the startup notification popup
- Smoke test now asserts the menu's DWM backdrop is Acrylic (`systembackdrop=3`)

## QuickLook Lite 1.2.9

- Unify the tray menu and the preview window's "More" menu into one Mica-backed menu with a Win11-style translucent panel, rounded corners and icons
- Add automated smoke checks: DWM readback proves Mica is applied to the tray menu, and the "More" menu opens through the same unified path

## QuickLook Lite 1.2.8

- Replace the system tray context menu (native Win32 popup) with a self-drawn Mica-backed WPF menu; it follows the app's light/dark theme and never steals focus from a live preview
- Tray menu now dismisses on outside clicks and Escape, and clamps to the monitor's working area

## 4.6.0

- Add `.resources` (.NET binary resources) support to TextViewer
- Add Greenfish Icon Editor Pro document support (`.gfie`, `.gfi`) to image viewer
- Add Excalidraw support to image viewer [#1955](https://github.com/QL-Win/QuickLook/issues/1955)
- Add binary viewer plugin [#290](https://github.com/QL-Win/QuickLook/issues/290)
- Add database viewer plugin for [LiteDB](https://github.com/litedb-org/LiteDB) v5, SQLite and encrypted SQLite support
- Add prefetch viewer plugin for `.pf` file support
- Add FilePilot preview integration by @Andrey Semjonov [#1949](https://github.com/QL-Win/QuickLook/issues/1949)
- Add AsciiDoc support to markdown viewer (`.adoc`, `.asciidoc`, `.asc`, `.ad`)
- Add Jupyter Notebook support to markdown viewer (`.ipynb`)
- Add reStructuredText support to markdown viewer (`.rst`, `.restructuredtext`)
- Add configurable keyboard shortcuts to toggle TOC visibility for markdown viewer [#1934](https://github.com/QL-Win/QuickLook/issues/1934)
- Add option to disable window show transitions `<ShowWindowTransition>False</ShowWindowTransition>`
- Add `.winmd` to PE viewer supported extensions
- Add HTTP (`.http` and `.rest`) syntax highlighting
- Add `.pyi` extension to Python syntax highlighting
- Add RViz `.rviz` in YAML highlighting extensions
- Add COM wrappers and out-of-proc preview host [#1929](https://github.com/QL-Win/QuickLook/issues/1929)
- Add `.vdproj` highlighting definitions support
- Add versioned `.so` files support in ELF viewer
- Prepare building for ARM64 [#1872](https://github.com/QL-Win/QuickLook/issues/1872) but NOT READY
- Improve acrylic tint opacity and color values [#1912](https://github.com/QL-Win/QuickLook/issues/1912)
- Add magic number checks to support image files without an extension [#1868](https://github.com/QL-Win/QuickLook/pull/1868)
- Add option to close preview when losing focus [#484](https://github.com/QL-Win/QuickLook/issues/484) (Experimental)
- Add auto-terminate QuickLook.exe before install/upgrade/uninstall
- Add `.csv`, `.tsv` and `.psv` rainbow highlighters support
- Add `.jsonc` extension to JSON syntax highlighters support
- Add `.hxx` extension to C++ syntax definitions
- Add `.sc` extension to Scala syntax definitions
- Add `.csh`, `.fish`, `.nu` extensions to ShellScript syntax
- Add `.es6` and `.pac` extensions to JS syntax
- Add `.jav` extension to Java syntax
- Add Clip Studio Paint file .clip support [#1937](https://github.com/QL-Win/QuickLook/issues/1937)
- Add F5 shortcut key for Reload [#1922](https://github.com/QL-Win/QuickLook/issues/1922)
- Add `FocusWindowOnOpen` option for window focus on open [#1695](https://github.com/QL-Win/QuickLook/issues/1695)
- Improve markdown to support frontmatter (YAML Metadata) [#1920](https://github.com/QL-Win/QuickLook/issues/1920)
- Improve tray initialization of ContextMenu
- Add `.msp` installer support for app viewer
- Add `.m3u` and `.m3u8` highlighting definitions support
- Add ShellScript syntax extensions for `.bashrc`, `.bash_profile`, `.bash_login`, `.profile`, `.bash_logout`, `.zshrc`, `.zprofile`, `.zlogin`, `.zlogout`, `.dashrc`, `.kshrc`, `.mkshrc`, `.ashrc` and `.shrc`
- Add IDMan (Internet Download Manager) support instead of [QuickLook.Plugin.IDManViewer](https://github.com/emako/QuickLook.Plugin.IDManViewer)
- Support search panel in CSV viewer [#1824](https://github.com/QL-Win/QuickLook/issues/1824)
- Add ShellScriptDetector and register in FormatDetector
- Add Graphviz (`.gv` and `.dot`) support for image viewer
- Add draw.io (`.drawio` and `.dio`) support for image viewer
- Add Clip Studio Paint (`.clip`) support for image viewer
- Support localization for CSV viewer search panel
- Add extension filter helper and integrate checks
- Improve extension parsing and add balcklist for .insv [#1802](https://github.com/QL-Win/QuickLook/issues/1802)
- Add icon font preview mode to FontViewer
- Add `.jsonld` to JSON syntax extensions
- Use `.invalid` domain to speed up CHM page loading
- Support DeskBox 3rd-party program
- Improve update notification message [#1961](https://github.com/QL-Win/QuickLook/issues/1961)
- Upgrade MSVC PlatformToolset to v145
- Update dependencies and add System.IO.Compression
- Add recycle bin images and use embedded icons on Win10+
- Disambiguate `.pl` files with Prolog/Perl detectors
- Add Perl syntax highlighting support
- Add Objective-C++ syntax highlighting (Dark/Light)
- Add `.phtml` and `.ctp` to PHP syntax extensions
- Include `.ndjson` in JSONL highlighting extensions
- Add `.dsql` extension to SQL syntax
- Add NuGet (`.nupkg` and `.snupkg`) support for AppViewer
- Add setting to auto-unblock Protected View [#1832](https://github.com/QL-Win/QuickLook/issues/1832)
- Add built-in plugin support for `.chm`
- Add PlantUML preview support for image viewer
- Add `.pyz` to supported archive extensions
- Add [ISON](https://github.com/ISON-format/ison) syntax definitions support for `.ison`
- Add `.DS_Store` and `Thumbs.db` support for archive viewer
- Add options of the font and font size used in the text viewer [#1930](https://github.com/QL-Win/QuickLook/issues/1930)
- Add Inno Setup syntax definitions support for `.iss` and `.isl`
- Add built-in dump plugin for minidumps (`.dmp`, `.dump`, `.mdmp`, `.hdmp` and `.minidump`)
- Support HW/SW (hardware acceleration) decoding toggle to video viewer [#1928](https://github.com/QL-Win/QuickLook/issues/1928)
- Support localization for video viewer
- Add `.psd1` and `.psm1` to PowerShell syntax extensions
- Add Roff (`.ms`, `.man`, `.roff`, `.tmac`, `.me` and `.troff`) syntax highlighting support
- Add `.gitconfig` to INI syntax extensions
- Add Graphviz (`.dot` and `.gv`) syntax definitions
- Improve JSON syntax highlighting color
- Fix oftentimes doesn't trigger [#1903](https://github.com/QL-Win/QuickLook/issues/1903) [#1483](https://github.com/QL-Win/QuickLook/issues/1483)
- Fix CSV auto-scroll bug on large files with virtualization enabled
- Fix fullscreen behavior for window dragging and window corners for Windows 11
- Fix the busy decorator foreground color error in dark mode
- Fix rendering lag caused by excessively long lines by truncating and sanitizing them in text viewer
- Fix twice space after Alt+Tab to explorer window [#1939](https://github.com/QL-Win/QuickLook/issues/1939)
- Fix FontViewer preview width sizing

## 4.5.0

- Update LAVFilters to `0.81.0` [#1362](https://github.com/QL-Win/QuickLook/issues/1362) [#1855](https://github.com/QL-Win/QuickLook/issues/1855) [#1863](https://github.com/QL-Win/QuickLook/issues/1863)
  > A possible side effect is that users with older GPUs or without the latest VC++ Redistributable installed may experience video playback failures.
  >
  > Nevertheless, QuickLook has chosen to continue with an up-to-date update strategy.
  >
  > If you encounter any issues, you can refer to [#1362](https://github.com/QL-Win/QuickLook/issues/1362) and consider downgrading your LAVFilters version.

- Improve LRC handling by merging duplicate timestamps [#1858](https://github.com/QL-Win/QuickLook/issues/1858)
- Improve the translation of Simplified Chinese by [@stxttkx](https://github.com/stxttkx)
- Add support for the `WindowBackdrop` option (Auto/None/Mica/Acrylic/Tabbed/Acrylic10/Acrylic11)
- Add PKCS7 extensions to supported file types (`.p7s` and `.pkcs7`)
- Add support Fortran95 (`.f90`, `.f95`, `.f03`), GDScript (`.gd`), Diff (`.patch`, `.rej`), Razor (`.cshtml`, `.razor`), ActionScript (`.as`, `.mx`), Assembly (`.asm`), Ada (`.ada`, `.ads`, `.adb`), AutoHotkey (`.ahk`), Rhai (`.rhai`), C++ ( `.cu`, `.cuh`, `.hip`), Python (`.pyx`), PlantUML (`.puml`, `.plantuml`, `.pu`, `.uml`, `.iuml`, `.wsd`), Zig (`.zig`), Moji (`.moji`), GraphQL (`.graphql`, `.gql`, `.gqls`), Mermaid (`.mmd`, `.mermaid`), KQL (`.kql`), PromQL (`.promql`), JSON Lines (`.jsonl`), ANTLR, Boo, Ceylon, ChucK, Clojure, Cocoa, CoffeeScript, Cool, and others syntax highlighting, including the dark mode theme
- Add support Chromium `.pak` viewer and file extraction
- Add `.axml` extension to XML syntax highlighting
- Add `.cursorignore` extension to GitIgnore syntax
- Add support for Python `.whl` and `.egg` archives
- Add support `.psv` parsing in CsvViewer
- Add Mermaid (`.mermaid`) support and `.mmd` detection [#1893](https://github.com/QL-Win/QuickLook/issues/1893)
- Add plugin icon registration and include `QLPlugin.ico` designed by [@Shomnipotence](https://github.com/Shomnipotence)
- Add DICOM image support to ImageViewer plugin [#1866](https://github.com/QL-Win/QuickLook/issues/1866) `This is not a long-lasting built-in plugin`
- Add Romanian translation by [@Laszlo19](https://github.com/Laszlo19)
- Add F11 full screen toggle support [#253](https://github.com/QL-Win/QuickLook/issues/253)
- Fix markdown not supporting absolute resource paths
- Fix option `<UseTransparency>False</UseTransparency>` not taking effect in Windows 10 [#1542](https://github.com/QL-Win/QuickLook/issues/1542)
- Fix loop toggle resuming paused video playback [#1852](https://github.com/QL-Win/QuickLook/issues/1852)
- Fix unhandled Exception with XLSX and CSV files in OfficeViewer resize with `RPC_E_CANTCALLOUT_ININPUTSYNCCALL` [#1854](https://github.com/QL-Win/QuickLook/issues/1854)
- Fix command line relative path resolution [#1857](https://github.com/QL-Win/QuickLook/issues/1857)
- Fix taskbar icon intermittently missing after Explorer restart [#1864](https://github.com/QL-Win/QuickLook/issues/1864)
- Fix `{Desktop composition is disabled}` exceptions in `GetMonitorColorProfileFromWindow` [#4](https://github.com/QL-Win/QuickLook.Common/pull/4)
- Fix `assimp.dll` not found for win-x64 [#1741](https://github.com/QL-Win/QuickLook/issues/1741)

## 4.4.0

- Add support for `.cnf` files to INI syntax highlighting
- Add support for `.ddeb` Debian debug symbol packages
- Add a **Reload** option to the **More** context menu [#1839](https://github.com/QL-Win/QuickLook/issues/1839)
- Add support for embedded lyrics in music file [#1847](https://github.com/QL-Win/QuickLook/issues/1847)
- Add a certificate viewer plugin to support extensions `.p12`, `.pfx`, `.cer`, `.crt`, `.pem`, `.mobileprovision` and `.certSigningRequest` 
- Add support for Compound File Binary formats (`.cfb` and `.eif` is now supported)
- Add a restart button after plugin installation [#1823](https://github.com/QL-Win/QuickLook/issues/1823)
- Improve XML version attribute detection in `XMLDetector` (e.g. `<?xml version='1.0'?>` is now supported)
- Improve YAML highlighting and support `.clang-format`
- Fix a crash that could occur when shutting down or restarting Windows [#1782](https://github.com/QL-Win/QuickLook/issues/1782)
- Fix JSON detection with UTF-8 BOM present
- Fix tags not displayed due to empty cover art [#1845](https://github.com/QL-Win/QuickLook/issues/1845)

## 4.3.0

- Add Svelte syntax highlighting support
- Add ShowInTaskbar setting to display window in taskbar [#1789](https://github.com/QL-Win/QuickLook/issues/1789)
- Add option to disable automatic update check at startup [#1801](https://github.com/QL-Win/QuickLook/issues/1801)
- Update PowerShell syntax colors in dark theme
- Improve TextViewerPanel UI and usability
- Fix DOpus crash when QuickLook runs with different privilege level [#1781](https://github.com/QL-Win/QuickLook/issues/1781)
- Fix volume control exceeding limits during mouse wheel scroll [#1813](https://github.com/QL-Win/QuickLook/issues/1813)
- Fix error in RTF file originating from version 4.2.1 [#1826](https://github.com/QL-Win/QuickLook/issues/1826)

## 4.2.2

- Fix version display issue [#1776](https://github.com/QL-Win/QuickLook/issues/1776)

## 4.2.1

- Fix theme error in MediaInfoViewer plugin [#1775](https://github.com/QL-Win/QuickLook/issues/1775)
- Fix theme error in any theme-changable plugin [#1507](https://github.com/QL-Win/QuickLook/issues/1507)

## 4.2.0

- Add built-in MediaInfoViewer plugin and support it in more menu
- Add 'Copy as path' option to more menu
- Add cross-plugin 'Reopen as' menu for SVG and HTML [#1690](https://github.com/QL-Win/QuickLook/issues/1690)
- Support Point Cloud Data (.pcd) for 3D spatial (Only PCD files with the PointXYZ format are supported, while Color and Intensity formats are not.)
- Support Mermaid diagram rendering in MarkdownViewer [#1730](https://github.com/QL-Win/QuickLook/issues/1730)
- Support .pdn in ThumbnailViewer [#1708](https://github.com/QL-Win/QuickLook/issues/1708)
- Improve CLI performance [#1706](https://github.com/QL-Win/QuickLook/issues/1706) [#1731](https://github.com/QL-Win/QuickLook/issues/1731)
- Set default background to transparent for SVG panel
- Improve UI/UX of font loading
- Add diff file syntax highlighting
- Add Swedish translation [#1755](https://github.com/QL-Win/QuickLook/issues/1755)
- Add .slnx extension to XML syntax highlighting
- Add support for Telegram Sticker (.tgs) files [#1762](https://github.com/QL-Win/QuickLook/issues/1762)
- Add .snupkg and .asar support to archive viewer
- Add .krc file support to TextViewer
- Add UseNativeProvider option [#1726](https://github.com/QL-Win/QuickLook/issues/1726)
- Fix image .jxr error reading from UseColorProfile
- Fix issue where font file stays locked [#77](https://github.com/QL-Win/QuickLook/issues/77)
- Fix font file unicode name is not supported
- Fix extracting cover art will not cause the title to be lost [#1759](https://github.com/QL-Win/QuickLook/issues/1759)
- Fix HelixViewer default height being too large
- Fix long path handling issue in HtmlViewer [#1643](https://github.com/QL-Win/QuickLook/issues/1643)
- Update Batch syntax highlighting colors
- Refactor tray icon to use TrayIconHost
- Refactor to make exe-installer no forked relaunching
- Remove unimportant UnobservedTaskException [#1691](https://github.com/QL-Win/QuickLook/issues/1691)
- Remove configuration `ModernMessageBox`

## 4.1.1

- Add built-in ThumbnailViewer plugin [#1662](https://github.com/QL-Win/QuickLook/issues/1662)
- Add built-in HelixViewer for 3d models [#1662](https://github.com/QL-Win/QuickLook/issues/1662)
- Add FBX model support using AssimpNet [#1479](https://github.com/QL-Win/QuickLook/issues/1479)
- Add `SVGA` and `Lottie Files` animation preview support
- Add MathJax inline math support to Markdown [#1640](https://github.com/QL-Win/QuickLook/issues/1640)
- Add `SubRip Subtitle (.srt) files`, `Protobuf`, `NSIS`, `.gitmodules`, `.dotsettings`, `.gitignore`, `.gitattributes`, `Markdown`, `reStructuredText`, `simple QML syntax`, `.env`, `Configuration (.conf;.config;.cfg)` highlighting [#1002](https://github.com/QL-Win/QuickLook/issues/1002)
- Add dark mode highlighting for `PowerShell`, `Registry`, `C`, `C++`, `Java`, `Rust`, `SQL`, `Ruby`, `R`, `PHP`, `Pascal`, `Objective-C`, `Lisp`, `Kotlin`, `Erlang`, `Dart`, `Swift`, `VisualSolution`, `CMake`
- Add `MakefileDetector`, `CMakeListsDetector for CMakeLists.txt`, `DockerfileDetector`, `HostsDetector for hosts` for text viewer
- Improve QuickLook initialization speed
- Optimize JSONDetector with Span
- Set RichTextBox background to transparent
- Revert Add Sandbox detection from 4.1.0 which will call crash

## 4.1.0

- Add built-in AppViewer plugin for `.msi`, `.appx`, `.msix`, `.wgt`, `.wgtu`, `.apk`, `.ipa`, `.hap`, `.deb`, `.dmg`, `.appimage`, `.rpm`, `.aab`
- Add built-in ELF viewer plugin for ELF-type files
- Add reload feature by JSuttHoops but you should enable `AutoReload` option firstly
- New option ProcessRenderMode
- Use format detector feature for TextViewer, only `JSON` / `XML` available now
- Add support more highlighting for `HLSL`, `XML`, `TXT`, `Properties`, `Lyric`, `Log`, `Python`, `JavaScript`, `Vue`, `CSS`, `Go`, `YAML`, `F#`, `INI`, `TypeScript`, `VB`, `SubStation Alpha` and `Lua`
- No markdown resource extraction [#1661](https://github.com/QL-Win/QuickLook/issues/1661) [#1670](https://github.com/QL-Win/QuickLook/issues/1670)
- Support X11 and more JPEG2000 image formats
- Support JXR image but SDR only [#1680](https://github.com/QL-Win/QuickLook/issues/1680)
- Enable window dragging in video viewer panel [#425](https://github.com/QL-Win/QuickLook/issues/425)
- Add SVG support using WebView2 in ImageViewer
- Support RTL for .txt file [#1612](https://github.com/QL-Win/QuickLook/issues/1612)
- Add `Alt+Z` shortcut to toggle word wrap [#1487](https://github.com/QL-Win/QuickLook/issues/1487)
- Improve startup speed [#1521](https://github.com/QL-Win/QuickLook/issues/1521)
- Improve PDF magic detection
- Improve GroupBox UI/UX
- Attempt to fix the crash [#1648](https://github.com/QL-Win/QuickLook/issues/1648) `This is an experimental fix, the idea is to remove the tree to prevent the DUCE command`
- Update font pangram for FontViewer
- Update de translations by King3R
- Manually resolve the assembly fails [#1618](https://github.com/QL-Win/QuickLook/issues/1618)
- Merge OfficeViewer-Native plugin [#1662](https://github.com/QL-Win/QuickLook/issues/1662)
- New option CheckPreviewHandler for OfficeViewer-Native
- Add Sandbox detection
- Revert the DataGrid style of CSV [#1664](https://github.com/QL-Win/QuickLook/issues/1664)
- Remove the WoW64HookHelper from release [#1634](https://github.com/QL-Win/QuickLook/issues/1634)
- Fix share button was not visible in win11
- Fix generic theme resources [#1652](https://github.com/QL-Win/QuickLook/issues/1652)
- Fix old version volume exception [#1653](https://github.com/QL-Win/QuickLook/issues/1653)
- Fix CaptionTextButtonStyle not static anymore
- Fix unsupported ColorContexts in Windows [#1671](https://github.com/QL-Win/QuickLook/issues/1671)
- ~~Fix long path issue [#1643](https://github.com/QL-Win/QuickLook/issues/1643)~~

## 4.0.2

- Support .pcx image [#1638](https://github.com/QL-Win/QuickLook/issues/1638)
- Improve PE parsing with extended buffer size
- Fix flickering [#1628](https://github.com/QL-Win/QuickLook/issues/1628)
- Fix DpiAwareness for PerMonitor [#1626](https://github.com/QL-Win/QuickLook/issues/1626)

- Hide PEViewer Title just like InfoPanel
- Avoid audio cover null exception in xaml

## 4.0.1

- Support more Markdown file extensions [#1562](https://github.com/QL-Win/QuickLook/issues/1562) [#1601](https://github.com/QL-Win/QuickLook/issues/1601)
- Support CLI options [#1620](https://github.com/QL-Win/QuickLook/issues/1620)
- Update pt-BR translations in Translations.config
- Delay initialization of MarkdownViewer
- Make .exe installer use MSI path by default [#1596](https://github.com/QL-Win/QuickLook/issues/1596)
- Fix style issues in the Search Panel [#1592](https://github.com/QL-Win/QuickLook/issues/1592)
- Fix volume control not working [#1578](https://github.com/QL-Win/QuickLook/issues/1578)
- Fix exception when checking for updates [#1577](https://github.com/QL-Win/QuickLook/issues/1577)

## 4.0.0

- Add built-in PE viewer plugin
- Add built-in font viewer plugin
- Update translations
- Update dependent packages
- Add support for Multi Commander
- Add support for both Everything v1.4 and v1.5(a)
- Add "Open Data Folder" and dark mode support to tray menu
- Add "Restart QuickLook" option to tray menu [#1448](https://github.com/QL-Win/QuickLook/issues/1448)
- Implement modern message box UI
- Replace icons with Segoe Fluent Icons
- Detect and auto-fix Windows blocking issues [#1495](https://github.com/QL-Win/QuickLook/issues/1495)
- Adjust tray menu position
- Use MicaSetup to create EXE installer
- Fix plugin installer description length limit
- Prevent crash when WMI fails [#1379](https://github.com/QL-Win/QuickLook/issues/1379)
- Show toast when "Prevent Closing" cannot be cancelled [#1368](https://github.com/QL-Win/QuickLook/issues/1368)
- Add support for multi-layer GIMP .xcf files [#1224](https://github.com/QL-Win/QuickLook/issues/1224) for ImageViewer
- Fix .xcf file extension check [#1229](https://github.com/QL-Win/QuickLook/issues/1229) for ImageViewer
- Fix HEIC preview rendering [#1470](https://github.com/QL-Win/QuickLook/issues/1470) for ImageViewer
- Add support for .qoi, .icns, .dds, .svgz, .psb, .cur, and .ani formats for ImageViewer
- Improve animated WebP support (x64 only) [#1024](https://github.com/QL-Win/QuickLook/issues/1024) [#1324](https://github.com/QL-Win/QuickLook/issues/1324) for ImageViewer
- Improve GIF decoding performance [#993](https://github.com/QL-Win/QuickLook/issues/993) for ImageViewer
- Add copy button to image viewer [#1399](https://github.com/QL-Win/QuickLook/issues/1399) for ImageViewer
- Fix SVG rendering error [#1430](https://github.com/QL-Win/QuickLook/issues/1430) for ImageViewer
- Add double-encoding detection [#471](https://github.com/QL-Win/QuickLook/issues/471) [#600](https://github.com/QL-Win/QuickLook/issues/600) for TextViewer
- Improve dark mode rendering for TextViewer
- Catch exceptions from XSHD loader for TextViewer
- Add syntax highlighting for shell scripts [#668](https://github.com/QL-Win/QuickLook/issues/668) for TextViewer
- Add dark mode support for C# syntax highlighting for TextViewer
- Improve support for comic archive formats [#1276](https://github.com/QL-Win/QuickLook/issues/1276) for ArchiveViewer
- Redesign file list with Fluent UI for ArchiveViewer
- Change default background color to blue for CsvViewer
- Fix issue with non-UTF8 CSV encoding for CsvViewer
- Improve rendering and stability for MarkdownViewer
- Add support for password-protected PDFs [#155](https://github.com/QL-Win/QuickLook/issues/155) for PDFViewer
- Enable auto-resizing of the viewer window for PDFViewer
- Fix audio cover parsing error for multiple embedded images for VideoViewer
- Add lyric (.lrc) support for audio files [#1506](https://github.com/QL-Win/QuickLook/issues/1506) for VideoViewer
- Add support for .mid audio format [#931](https://github.com/QL-Win/QuickLook/issues/931) for VideoViewer
- Fix time label overflow in long videos for VideoViewer
