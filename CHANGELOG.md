# QuickLook-Next Changelog

> QuickLookNext Changelog starting from version `4.0.0`.

## QuickLook-Next 3.7.0

### 更美观

- 插件管理面板：每行插件加上带底色的拼图图标，行呈现为卡片式布局

### 工程

- 冒烟测试补强：新增罕见格式覆盖（test-pe.exe 走 PEViewer、test.bin 走
  BinaryViewer）与 mermaid / 数学公式 Markdown 覆盖，按需加载与 WebView2
  懒加载路径今后有回归会立刻被测试拦下（此前这类问题曾漏过一轮）

## QuickLook-Next 3.6.0

### 更快 / 更轻（Markdown 预览）

- Markdown 预览不再无条件加载 mermaid（2.9MB）与 MathJax（2.1MB）：
  只有文档里检测到 mermaid 代码块或数学公式时才按需注入对应脚本。
  普通 Markdown 预览跳过约 5MB 的 JavaScript 解析，WebView2 页面加载与
  内存占用都更低；带图表 / 公式的文档渲染功能保持不变

## QuickLook-Next 3.5.0

### 更轻（内存 / 体积）

- 罕见格式插件按需加载：启动只载入常用插件（文本/图片/Markdown/视频/PDF/
  压缩包/字体/HTML/CSV/Office），3D、数据库、PE、邮件、CHM 等 15 个罕见
  插件在首次预览对应格式时才加载，常驻进程不再预载这些程序集和原生库
- 发布包清理：移除 IntelliSense 的 *.xml 文档（约 4MB）、插件目录下的
  *.deps.json、macOS 原生库（*.dylib），并移除 VideoViewer 根目录冗余的
  MediaInfo.dll 副本（zip 61MB -> 60.4MB）

### 修复

- 修复打包脚本可能误删根目录 QuickLook-Next.deps.json（apphost 必需文件）的
  问题，只清理插件目录下的 deps.json

## QuickLook-Next 3.4.0

### 更轻（内存）

- MediaInfo 原生库改为按需加载：VideoViewer 不再在启动时把 MediaInfo.dll
  （约 8MB）载入常驻进程，只有实际预览媒体文件时才加载
- 移除启动时的 ImageMagick 原生库预热（约 24MB）：png / jpg / gif 等常见
  格式仍走 WPF/WIC 快速解码，Magick.Native 在首次预览非常见格式时才按需加载

## QuickLook-Next 3.3.0

### 更快

- 插件初始化改为按需懒加载：预览只等待插件程序集发现，不再等待全部插件的
  Init 完成；命中的插件立即初始化，无关插件的初始化在后台继续，启动后立刻
  空格预览不再被其他插件拖慢
- 程序集解析增加 lib 索引缓存：发布包布局下解析缺失程序集时不再每次全目录
  扫描，冷启动更快

### 更轻

- 发布包共享依赖去重：WebView2、UtfUnknown、SharpZipLib、PureSharpCompress
  等跨插件重复的共享 DLL 统一收进 lib\ 一份（字节级校验，带独立原生库的
  MediaInfo / SQLite / freetype 等保持原位），一次移除 44 个重复文件

### 更美观

- 托盘菜单各项添加 Fluent 图标（主题、语言、背景、选项、检查更新、获取插件、
  插件管理、数据目录、重启、退出）

## QuickLook-Next 3.2.1

- 修复 3.2.0 发布包中设置/托盘菜单显示原始键（如 `icon_CheckUpdate`、
  `icon-Restart`）的问题：`QuickLook.Common.dll` 移入 `lib\` 后，翻译文件
  定位改为基于程序根目录（`AppContext.BaseDirectory`），不再依赖
  `QuickLook.Common.dll` 所在目录
- 同步修复便携模式的便携标记（`portable.lock`）检测：同样改为基于程序根目录，
  保证发布包解压后数据目录跟随程序目录

## QuickLook-Next 3.2.0

### 性能

- 插件加载提速：25 个插件程序集的发现与实例化从串行改为并行，启动时的
  插件就绪时间从约 2.5s 降到约 0.4s（-84%）
- TextViewer 语法高亮提速：248 个 XSHD 语法文件改为并行解析 + 分层编译，
  高亮初始化从约 1.1s 降到约 0.3s
- 空格键热路径改用单调时钟（`Environment.TickCount64`），并清理了 PE 解析中
  一次性的跳字节缓冲分配

### 发布包

- 发布包目录整理：根目录不再与十几个 dll / config 混杂，第三方运行库统一收进
  `lib\` 子目录，用户只需双击根目录的 `QuickLook-Next.exe`
- 移除 .NET Framework 时代的 `QuickLook-Next.dll.config` 与调试符号

### 工程

- 构建警告从 31 个清理到 1 个：移除失效的 ruleset 引用，改用 .NET 10 的
  `X509CertificateLoader` / 强类型公钥 API，修复 PE 读取的 CA2022 等

## QuickLook-Next 3.1.0

- 恢复全部被精简掉的预览插件：BinaryViewer（bin/hex）、CertViewer（证书）、
  ChmViewer（CHM）、DbViewer（数据库）、DumpViewer（dmp）、ELFViewer（ELF）、
  HelixViewer（3D 模型）、MailViewer（eml/msg）、PEViewer（PE）、
  PrefetchViewer（pf）、ThumbnailViewer（设计文件缩略图），内置插件由 14 个
  恢复为 25 个，预览覆盖与完整版一致

## QuickLook-Next 3.0.5

- 移除 ImageViewer 内嵌 Excalidraw 模板中的演示 Firebase API Key
  （Excalidraw 官方公开 demo 密钥，静态渲染用不到），消除 GitHub Secret
  Scanning 告警

## QuickLook-Next 3.0.4

- 新增自动更新：「检查更新」发现新版本后不再只是打开 GitHub 页面，而是直接下载
  Release 的 zip 安装包、替换程序文件并自动重启；目录不可写或没有安装包时回退为
  打开下载页面。后台静默检查仍只提示，点击通知再触发自动更新

## QuickLook-Next 3.0.3

- 修复子菜单快速点击被吞的问题：子菜单刚弹出时立刻点击某项（如「语言 -> 跟随
  系统」）会被父菜单的鼠标钩子误判为“点击外部”而抢先关闭，导致点击无反应；
  现在父菜单把已打开子菜单的区域视为内部，快速点击也能正常生效

## QuickLook-Next 3.0.2

- 修复更新检查指向原项目的问题：「检查更新」现在查询本仓库
  （Adstrax/QuickLook-Next）的 Releases，提示新版本与下载链接均指向本项目的
  Release，不再误报原版 QuickLook 的版本

## QuickLook-Next 3.0.1

- 修复预览窗口可能被其他窗口挡住：1.2.36 的离屏预热让窗口在首次预览前就已
  “可见”，而 BringToFront 只在 `!IsVisible` 时执行，导致预热后预览打开不置前；
  现在每次打开 / 切换预览都会把窗口提到最前（仍不抢焦点，顶部置顶开关行为不变）

## QuickLook-Next 3.0.0

- 恢复老插件兼容：插件契约（`QuickLook.Common` 接口与程序集、`QuickLook.Plugin.*`
  前缀、元数据文件名、注册表关联）全部改回旧名，老插件无需重新编译即可安装与加载
- 应用本体保持 QuickLook-Next 命名（`QuickLook-Next.exe`、命名空间 `QuickLookNext.*`、
  管道 / 互斥体 `QuickLookNext.App.*`、设置域 `QuickLookNext`）

## QuickLook-Next 2.0.0

- 全面改名定型：可执行文件改为 `QuickLook-Next.exe`，程序集与 C# 命名空间改为
  `QuickLookNext.*`，命名管道 / 互斥体改为 `QuickLookNext.App.*`，插件前缀改为
  `QuickLookNext.Plugin.*`，与上游 QuickLook 彻底隔离
- 设置域名同步改为 `QuickLookNext`：原主题 / 语言 / 背景等设置与用户插件目录不再
  沿用，需要重新设置；第三方 `QuickLook.Plugin.*` 插件需按 `QuickLookNext.Plugin.*`
  适配

## QuickLook-Next 1.5.0

- 新增语言切换：托盘菜单「语言」子菜单，支持跟随系统 + 全部支持语言（约 25 种，
  按母语名称显示），选择持久化到 Language 设置，菜单/窗口下次打开生效
- 长菜单（语言列表）改用 ScrollViewer 限高滚动，避免超出屏幕

## QuickLook-Next 1.3.12

- 插件管理面板背景改为与托盘菜单一致：无边框非分层窗口 + WCA Acrylic 毛玻璃 +
  DWM 8px 圆角（毛玻璃一起圆角），跟随亮/暗主题；面板头部可拖动，右上角加关闭按钮

## QuickLook-Next 1.3.11

- 新增插件管理面板（托盘菜单「管理插件...」）：枚举用户安装与内置插件，显示
  名称/版本/说明/来源；用户插件可直接卸载（立即从匹配列表移除，文件被占用时
  标记为待删除、下次启动清理），内置插件仅展示
- 面板支持刷新与打开用户插件文件夹；新增 /test-plugin-manager 测试钩子

## QuickLook-Next 1.3.10

- 修复托盘菜单外圈第二层背景：1.3.9 保留原生窗口框后 DWM 会画一层很大的原生
  投影；改为 `WindowStyle=None` 的无边框非分层窗口，DWM 圆角（含毛玻璃）不变，
  原生大投影消失，菜单恢复单层观感

## QuickLook-Next 1.3.9

- 托盘菜单（含二级子菜单）改为非分层 WCA Acrylic + WindowChrome：DWM 圆角直接
  作用到整窗（毛玻璃一起圆角），不再有方形毛玻璃边角；原生窗口框同时恢复
  Win11 投影，去掉 WPF 自绘阴影的裁切问题
- 修复「Find new & Plugins...」无法打开网站：.NET Core 下 URL 必须走
  `UseShellExecute=true` 才会调用默认浏览器；「检查更新」的 Store/Releases
  链接同步修复

## QuickLook-Next 1.3.8

- 修复托盘菜单外圈直角边框：托盘菜单与二级子菜单都是分层窗口，WCA Acrylic 的
  模糊区域是整窗矩形，圆角面板外的四个角会残留方形毛玻璃边框；改用预览窗口同款
  `SetWindowRgn` 8px 圆角裁剪，让窗口本身（含毛玻璃）与圆角面板完全一致

## QuickLook-Next 1.3.7

- 托盘右键菜单精简：主题模式（跟随系统/亮色/暗色）、背景模式（7 种）、选项
  （开机自启/失去焦点时关闭/顶栏默认隐藏）收进二级子菜单，分组行显示当前选择；
  顶层保留版本、检查更新、获取插件、打开数据文件夹、重启、退出，菜单长度约减半。
  子菜单沿用同款 Acrylic 自绘菜单与勾选态，点击外部/Esc 联动关闭

## QuickLook-Next 1.3.6

- 托盘菜单新增「主题模式」：跟随系统 / 亮色 / 暗色三选一，当前预览立即切换并
  持久化（LastTheme），下次打开沿用；顶栏亮/暗切换按钮逻辑复用同一入口
- 顶部状态栏默认隐藏：之前只要鼠标在预览窗口内移动，顶栏就会弹出遮挡内容；
  现在默认只有把鼠标移到窗口顶部标题栏区域才显示，移开后约 1 秒自动隐藏。
  托盘菜单新增「顶部状态栏默认隐藏」开关（默认开启），取消勾选立即恢复旧行为，
  无需重启；顶栏显示区域判断改用固定高度，不再受隐藏态布局影响

## QuickLook-Next 1.3.5

基于 1.3.2 的「一打开即 Acrylic + 跟随壁纸」效果重构，修复分层窗口的两个遗留
缺陷，放弃 1.3.3/1.3.4 的激活抢焦点与 Mica 方案。

- 文本预览恢复上下滚动：分层窗口收不到系统转发给光标下窗口的滚轮消息
  （`WM_MOUSEWHEEL` 只发给焦点窗口，转发链路跳过分层窗口）。Acrylic 改在普通
  窗口上直接走 WCA（`SetWindowCompositionAttribute`）渲染，滚轮恢复原生路由，
  txt/log/json/代码等文本类预览可正常滚动
- 恢复 Win11 原生圆角：WCA 毛玻璃模糊区域固定为整窗矩形（`SetWindowRgn` 只能
  裁剪内容、裁不掉模糊区域，1.3.2 因此仍有方形毛玻璃边角）。普通窗口下 DWM
  `WindowCornerPreference` 重新生效，毛玻璃与内容一起圆角，最大化/全屏自动直角
- WCA 毛玻璃不依赖激活状态，一打开即显示、跟随壁纸变化，且不再抢占焦点
- 保留分层 + 滚轮钩子的兜底路径（`ShouldUseLayeredAcrylic` 开关），便于回归

## QuickLook-Next 1.3.2

- 修复分层窗口边缘黑线：1px 的窗口边框（深色 BorderBrush）与 WindowChrome
  的 1px 玻璃框在分层窗口上没有 DWM 玻璃填充，会渲染成黑色描边；分层模式下
  现在移除窗口边框并把 WindowChrome 玻璃厚度归零
- 预览窗口恢复 Win11 圆角：分层窗口不受 DWM 圆角偏好控制，改用
  `SetWindowRgn` 把窗口本身（含 WCA 毛玻璃）裁成 8px 圆角，随窗口尺寸/状态
  同步；最大化与全屏时自动恢复直角

## QuickLook-Next 1.3.1

- 预览窗口一打开就显示 Acrylic 背景：Win11 的 DWM `SystembackdropType.Acrylic`
  只在窗口激活时渲染毛玻璃，而预览窗口从不激活（`ShowActivated=false`），所以
  之前文本类预览打开是纯色、点击后才出现毛玻璃。现在当背景设置为 Acrylic 系
  （Acrylic/Acrylic10/Acrylic11）时，预览窗口改为分层窗口并走托盘菜单同款的
  WCA（SetWindowCompositionAttribute）方案——毛玻璃不依赖激活状态，一打开即
  显示；Mica/Tabbed 等其他背景仍使用普通窗口（硬件加速渲染不受影响）
- 分层窗口的取舍：窗口失去 DWM 原生投影与 Win11 圆角（分层窗口的限制），文字、
  视频（D3DImage）与 WebView2 内容渲染经实测正常；如需恢复原生观感，可将
  WindowBackdrop 设为 Mica/Tabbed，或回退到 1.3.0 文件夹

## QuickLook-Next 1.3.0

大版本更新：预览调用链路与构建体积全面优化，并恢复预览窗口的 DWM Acrylic
背景（与 v1.2.38 一致，不再使用未激活即失效的 WCA 实验方案）。

- 预览调用大提速：第二实例不再初始化 WPF（此前每次空格预览都要付约 400ms 的
  PresentationFramework/XAML 加载成本），改为入口处先检查互斥体、直接通过命名
  管道把请求转发给常驻实例，转发失败才走完整启动。实测第二实例进程开销从约
  404ms 降到约 75ms
- 恢复预览窗口 Acrylic 背景：改用 DWM `SystembackdropType.Acrylic`（Win11
  原生方案），文本类预览点击/激活后即显示毛玻璃，不再出现「背景直接透出桌面、
  无任何模糊」的问题；托盘菜单仍使用已验证的 WCA 方案，不受影响
- 显示器色彩配置改为按显示器缓存（30s TTL）：该 WCS 查询原本每次预览都在 UI
  线程执行，但默认配置下只有 ImageMagick 色彩管理（UseColorProfile）才会用到
  结果，缓存后预览不再重复支付这段开销
- VideoViewer 匹配时跳过对明确由其他插件处理的扩展名（txt/md/json/zip/pdf/字体/
  图片/Office 等）的 MediaInfo 原生嗅探，非媒体文件预览不再在 UI 线程白白打开
  一次原生库；.ts/.rm/.asf 等非常规媒体扩展仍走嗅探，不受影响
- 构建体积 -8MB：VideoViewer 显式从 `runtimes\win-x64\native` 加载 MediaInfo，
  flatten-native 在插件根目录复制的 `MediaInfo.dll` 是冗余副本，构建后自动删除

基准（bench.ps1，稳态即第二轮）：png 88ms / txt 78ms / md 70ms / json 124ms /
zip 59ms / pdf 118ms（优化前首轮 399–591ms；固定调用开销约 -330ms）

## QuickLook-Next 1.2.38

- 首次图片预览提速：启动后后台预热图片解码管线（WPF/WIC + ImageMagick
  原生库），首次预览不再支付一次性解码器初始化（灰色背景闪现明显缩短）；
  实测首次 png 预览从约 1260ms 降到约 476ms

## QuickLook-Next 1.2.37

- 设置界面（托盘菜单 / More 菜单）适配 Win11 圆角与毛玻璃：
  - 改用 `SetWindowCompositionAttribute`（TranslucentTB 同款 API），对
    无边框弹出窗口稳定生效；不再用 DWM `SystembackdropType`（该方案在
    无边框窗口上会静默失效、渲染成一片死色）
  - 窗口改为分层窗口，圆角、1px 边框、投影由 WPF 绘制（圆角 8px、
    深色 55% 半透明叠加 / 亮色浅色叠加、投影 24px 模糊）
  - 亮色 / 暗色主题适配：叠加色与文字颜色随主题切换（暗色深蓝灰、亮色
    浅色），两种模式都有毛玻璃效果
- 冒烟测试断言改为验证 Acrylic API 调用成功（WCA 回读不可靠）
- 修复设置界面"两层"观感：WCA 毛玻璃会模糊整个窗口矩形，之前内容四周
  的透明边距会形成外层直角毛玻璃框；圆角面板改为铺满整个窗口（与 E-Tab
  一致），投影调小避免边缘裁切（12px 模糊、4px 深度）

## QuickLook-Next 1.2.36

- 预览窗口首次展示提速：窗口在启动空闲时离屏预热一次（不激活、不显示在
  屏幕上），HWND 创建、布局、DWM Mica/Acrylic 背景初始化全部提前到后台
  空闲期完成；首次按空格不再等待约 200ms（2.9MB JSON 首次预览
  912ms → 649ms）
- 修复预热导致的定位回归：预热尺寸不再被误存为自定义窗口尺寸；首次真实
  预览强制按"新窗口"居中定位，窗口位置与尺寸和 1.2.35 完全一致
  （1536x960 工作区下实测正居中）
- 修复第二次按空格有时无反应：预热窗口使预览窗口"始终可见"，而空格切换
  原先用窗口可见性判断"是否正在预览"，导致预览关闭后同一文件无法重新
  打开（换文件才恢复）。改为用内部预览状态判断，并在窗口关闭时重置状态；
  实测 打开→关闭→重开→切换 序列全部正常
- 修复视频预览有时无法正常播放：打开视频时先显示系统缩略图（v1.2.15
  特性），若视频就绪较快而缩略图提取较慢，迟到的缩略图会盖住正在播放的
  视频且不再隐藏（视频其实在播，看起来却像卡住）。现在媒体就绪（或失败）
  后，迟到的缩略图不再显示，播放表面/错误提示优先

## QuickLook-Next 1.2.35

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

## QuickLook-Next 1.2.34

- 启动再提速：移除已废弃的 TrayIconWindow 预热窗口（原生托盘右键菜单早在
  v1.2.8 已停用，该窗口只剩开销）；启动（UI + 插件就绪）从约 0.35 秒进一步
  降到约 0.08 秒（热缓存下稳定 ~75 ms）
- `test.ps1` / `build.ps1` 改为一次并行构建整个解决方案（原来 16 个项目
  逐个编译），冒烟测试总耗时显著下降
- 移除 NuGet 包复制进输出的 win-x86 / win-arm64 运行时副本
  （WebView2Loader 等，新增共享清理目标），发布体积再减约 1 MB（170.5 MB）

## QuickLook-Next 1.2.33

- 启动提速：MessageBox 的 Harmony 补丁（.NET 10 下耗时约 1.2 秒）从启动
  同步执行改为延迟 3 秒后台执行；启动（UI + 插件就绪）从约 1.64 秒降到约
  0.35 秒（快约 78%）。补丁未完成时消息框使用默认样式，功能不受影响
- 新增隐藏 `/test-startup` 诊断钩子，记录各启动阶段耗时；
  `bench.ps1` 现在同时输出启动耗时与预览延迟

## QuickLook-Next 1.2.32

- PDF 预览瘦身：PDFium 由带 JavaScript 引擎的 V8 版换成普通版
  （`bblanchon.PDFium.Win32` 153.0），`pdfium.dll` 从 28.9 MB 降至 6.9 MB；
  预览渲染不受影响（预览不需要 PDF 的 JS 交互能力）
- 冒烟测试新增 PDF 预览覆盖（14 页示例文档），PDFViewer 变更后全绿
- 修复 SQLite 高危漏洞警告：`Microsoft.Data.Sqlite` 升到 10.0.11，
  消除 NU1903（GHSA-2m69-gcr7-jv3q）
- 新增 `bench.ps1` 预览延迟基准（基于内置 `/test-timing` 钩子）；
  首次基线（含进程启动与管道开销）：png 0.73s / txt 1.02s / md 0.40s /
  zip 0.43s / ttf 0.69s / pdf 0.45s

## QuickLook-Next 1.2.31

- Release 体积瘦身（仅保留 64 位）：移除视频插件的 LAVFilters-x86（约 24 MB）
  与 MediaInfo win-x86（约 7 MB）、图片插件的 exiv2-ql-32、字体插件的
  freetype win-x86 等全部 32 位运行时副本，发布目录从约 227 MB 降至约 194 MB
  （-15%）。本版本仅面向 64 位 Windows（Win11）
- 图片 exiv2 元数据读取精简为纯 64 位路径，删除 x86 分支代码
- 修复插件加载失败警告框在无窗口启动（开机自启/托盘模式）时因 Owner 未显示
  而崩溃的问题：现在只在存在可见窗口时弹窗，否则仅写日志

## QuickLook-Next 1.2.30

- Image previews no longer show the top-right action icons (copy / metadata /
  background) or the image-info tag that appeared on hover; the image area is
  now completely clean

## QuickLook-Next 1.2.16

- Fix the broken startup shortcut: `Assembly.Location` resolves to QuickLook-Next.dll
  under the .NET apphost, so the auto-start shortcut (and the shell
  context-menu command / restart) pointed at the DLL - Windows then tried to
  "open" the DLL after every restart. The executable path is now resolved
  explicitly, so auto-start launches QuickLook-Next.exe properly

## QuickLook-Next 1.2.15

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

## QuickLook-Next 1.2.14

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

## QuickLook-Next 1.2.13

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

## QuickLook-Next 1.2.10

- Switch tray menu, "More" menu and the preview window default backdrop from Mica to Acrylic - the same frosted-glass effect as the startup notification popup
- Smoke test now asserts the menu's DWM backdrop is Acrylic (`systembackdrop=3`)

## QuickLook-Next 1.2.9

- Unify the tray menu and the preview window's "More" menu into one Mica-backed menu with a Win11-style translucent panel, rounded corners and icons
- Add automated smoke checks: DWM readback proves Mica is applied to the tray menu, and the "More" menu opens through the same unified path

## QuickLook-Next 1.2.8

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
- Add auto-terminate QuickLook-Next.exe before install/upgrade/uninstall
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
  > Nevertheless, QuickLookNext has chosen to continue with an up-to-date update strategy.
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
- Fix DOpus crash when QuickLookNext runs with different privilege level [#1781](https://github.com/QL-Win/QuickLook/issues/1781)
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
- Improve QuickLookNext initialization speed
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
- Add "Restart QuickLookNext" option to tray menu [#1448](https://github.com/QL-Win/QuickLook/issues/1448)
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
