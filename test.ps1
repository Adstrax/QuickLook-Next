# QuickLookNext 冒烟测试：每次提交前必须运行并通过。
# 覆盖：全量构建 -> 启动 -> 插件加载无失败 -> PNG/文本/SQLite 预览 -> 窗口断言 -> 日志零新增错误。
#
# 用法: .\test.ps1

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root 'Build\Release\QuickLook-Next.exe'
$log = Join-Path $env:APPDATA 'pooi.moe\QuickLookNext\QuickLookNext.Exception.log'
# v1.2.36: keep the smoke-test files inside the repository
# (E:\Codex\QK-Lite\<version>\ql-smoke) instead of the C: temp folder; the
# app's diagnostics (timing/startup/tray-menu) follow via QL_SMOKE_DIR.
$smoke = Join-Path $root 'ql-smoke'
$env:QL_SMOKE_DIR = $smoke
$failed = $false

function Assert([bool]$cond, [string]$msg) {
    if ($cond) { Write-Host "PASS: $msg" -ForegroundColor Green }
    else { Write-Host "FAIL: $msg" -ForegroundColor Red; $script:failed = $true }
}

function Get-LogLength {
    if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
}

function Get-QuickLookNextWindows([int]$targetPid) {
    if (-not ('WinEnum3' -as [type])) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
public class WinEnum3 {
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
  [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  public static List<string> Titles(uint targetPid) {
    var list = new List<string>();
    EnumWindows((h, l) => { uint pid; GetWindowThreadProcessId(h, out pid); if (pid == targetPid) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); if (sb.Length > 0) list.Add(sb.ToString()); } return true; }, IntPtr.Zero);
    return list;
  }
}
"@
    }
    [WinEnum3]::Titles($targetPid)
}

function Get-QuickLookNextWindowRect([int]$targetPid, [string]$titleMatch) {
    if (-not ('WinEnumRect' -as [type])) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinEnumRect {
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
  [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  static string result = "";
  public static string Rect(uint targetPid, string titleMatch) {
    result = "";
    EnumWindows((h, l) => { uint pid; GetWindowThreadProcessId(h, out pid); if (pid == targetPid && IsWindowVisible(h)) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); if (sb.Length > 0 && sb.ToString().Contains(titleMatch)) { RECT r; GetWindowRect(h, out r); result = r.Left + "|" + r.Top + "|" + (r.Right - r.Left) + "|" + (r.Bottom - r.Top); return false; } } return true; }, IntPtr.Zero);
    return result;
  }
}
"@
    }
    [WinEnumRect]::Rect($targetPid, $titleMatch)
}

# ---------- 1. 清理旧实例 ----------
Write-Host "== 1/6 清理旧实例 ==" -ForegroundColor Cyan
Get-Process -Name QuickLookNext -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3

# ---------- 2. 构建 ----------
Write-Host "== 2/6 全量构建 ==" -ForegroundColor Cyan
Get-ChildItem (Join-Path $root 'Build\Release') -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force
# v1.2.34: build the whole solution in one parallel invocation instead of
# compiling the 16 projects one by one - roughly halves the test time.
& dotnet build (Join-Path $root 'QuickLookNext.slnx') -c Release -v minimal --nologo *> $null
Assert ($LASTEXITCODE -eq 0) '构建 QuickLookNext.slnx'
if ($LASTEXITCODE -ne 0) { exit 1 }

# ---------- 3. 准备测试文件 ----------
Write-Host "== 3/6 准备测试文件 ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $smoke | Out-Null
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::SteelBlue)
$font = New-Object System.Drawing.Font 'Arial', 24
$g.DrawString('QL Test', $font, [System.Drawing.Brushes]::White, 40, 100)
$g.Dispose()
$bmp.Save((Join-Path $smoke 'test.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Set-Content -Path (Join-Path $smoke 'test.txt') -Value "QuickLookNext smoke test`r`nLine 2" -Encoding UTF8
Set-Content -Path (Join-Path $smoke 'test.md') -Value "# Markdown`n`n这是 **测试**。" -Encoding UTF8
Set-Content -Path (Join-Path $smoke 'test.json') -Value '{"name":"ql-smoke","version":"1.0.0","scripts":{}}' -Encoding UTF8
Compress-Archive -Path (Join-Path $smoke 'test.txt') -DestinationPath (Join-Path $smoke 'test.zip') -Force
Copy-Item -LiteralPath "$env:WINDIR\Fonts\arial.ttf" -Destination (Join-Path $smoke 'test.ttf') -Force
if (-not (Test-Path (Join-Path $smoke 'test.mp4'))) {
    curl.exe -sL -o (Join-Path $smoke 'test.mp4') "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4"
}
if (-not (Test-Path (Join-Path $smoke 'test.pdf'))) {
    curl.exe -sL -o (Join-Path $smoke 'test.pdf') "https://raw.githubusercontent.com/mozilla/pdf.js/master/web/compressed.tracemonkey-pldi-09.pdf"
}
# v3.7.0: rare-format coverage (on-demand plugin loading) + mermaid/math
# markdown paths. These used to be untested, which let a lazy-loading
# regression slip through.
[System.IO.File]::WriteAllBytes(
    (Join-Path $smoke 'test.bin'),
    [byte[]](0..9))
Copy-Item -LiteralPath (Join-Path $root 'Build\Release\QuickLook-Next.exe') `
    -Destination (Join-Path $smoke 'test-pe.exe') -Force
Set-Content -Path (Join-Path $smoke 'test-mermaid.md') `
    -Value "## 图`n`n``````mermaid`ngraph TD;`n  A-->B;`n``````" -Encoding UTF8
Set-Content -Path (Join-Path $smoke 'test-math.md') `
    -Value "## 公式`n`n质能方程 $E=mc^2$ 或 $$\int_0^1 x dx$$" -Encoding UTF8

# ---------- 4. 启动 + 插件加载 ----------
Write-Host "== 4/6 启动并验证插件加载 ==" -ForegroundColor Cyan
$before = Get-LogLength
$p = Start-Process -FilePath $exe -ArgumentList '/autorun /test-tray-menu' -PassThru
$trayMenuSeen = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    $alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
    if ($null -eq $alive) { break }
    $titles = Get-QuickLookNextWindows $p.Id
    if (($titles -join ' ') -match 'QuickLook-Next Tray Menu') { $trayMenuSeen = $true; break }
}
# Wait for the menu to auto-close and the plugins to finish loading.
Start-Sleep -Seconds 15
$alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
Assert ($null -ne $alive) '启动后进程存活'
Assert $trayMenuSeen '托盘菜单窗口出现并自动关闭'
$diagFile = Join-Path $smoke 'tray-menu-dwm.txt'
$dwmDiag = if (Test-Path $diagFile) { Get-Content $diagFile -Raw } else { '' }
if ($env:QL_SMOKE_CI -eq '1') {
    # CI runners may not have DWM compositing; skip the Acrylic assertion.
    Write-Host 'SKIP: 托盘菜单 Acrylic（CI 环境）' -ForegroundColor Yellow
}
else {
    Assert ($dwmDiag -match 'accent-applied=True') '托盘菜单 Acrylic 已应用（WCA 调用成功）'
}
Assert ($dwmDiag -match 'more-menu-opened=true') 'More 菜单复用同一 Acrylic 菜单路径'
Assert ((Get-LogLength) -eq $before) '插件加载无失败（日志零新增）'

# ---------- 5. 预览测试 ----------
Write-Host "== 5/6 预览测试 ==" -ForegroundColor Cyan
$previews = @(
    @{ File = 'test.png'; Title = 'test.png' },
    @{ File = 'test.txt'; Title = 'test.txt' },
    @{ File = 'test.md'; Title = 'test.md' },
    @{ File = 'test.json'; Title = 'test.json' },
    @{ File = 'test.zip'; Title = 'test.zip' },
    @{ File = 'test.ttf'; Title = 'test.ttf' }
)
if (Test-Path (Join-Path $smoke 'test.mp4')) {
    $previews += @{ File = 'test.mp4'; Title = 'test.mp4' }
}
if (Test-Path (Join-Path $smoke 'test.pdf')) {
    $previews += @{ File = 'test.pdf'; Title = 'test.pdf' }
}
# v3.7.0: rare-format previews exercise the on-demand lazy plugin loading.
# PEViewer deliberately shows no window title (full-bleed PE info panel), so
# only the process-alive and no-error assertions apply to it.
$previews += @{ File = 'test-pe.exe'; Title = 'test-pe.exe'; CheckTitle = $false }
$previews += @{ File = 'test.bin'; Title = 'test.bin' }
$previews += @{ File = 'test-mermaid.md'; Title = 'test-mermaid.md' }
$previews += @{ File = 'test-math.md'; Title = 'test-math.md' }
# v3.8.0: folder preview (InfoPanel) coverage - InfoPanel shows no title.
$previews += @{ Folder = $smoke; Title = 'ql-smoke'; CheckTitle = $false }
foreach ($pv in $previews) {
    $before = Get-LogLength
    $target = if ($pv.Folder) { $pv.Folder } else { Join-Path $smoke $pv.File }
    $label = if ($pv.Folder) { $pv.Title } else { $pv.File }
    & $exe $target
    Start-Sleep -Seconds 12
    $alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
    Assert ($null -ne $alive) "预览 $label 后进程存活"
    if ($pv.CheckTitle -ne $false) {
        $titles = Get-QuickLookNextWindows $p.Id
        Assert (($titles -join ' ') -match [regex]::Escape($pv.Title)) "预览窗口出现: $($pv.Title)"
    }
    Assert ((Get-LogLength) -eq $before) "预览 $label 无错误（日志零新增）"

    # v1.2.36: regression guard - the preview window must be centered on the
    # screen that contains it (the off-screen warm-up once broke this).
    if ($pv.File -eq 'test.png') {
        $rectStr = Get-QuickLookNextWindowRect $p.Id $pv.Title
        if ($rectStr) {
            $rp = $rectStr.Split('|')
            $winCenter = [System.Drawing.Point]::new(
                [int]$rp[0] + [int]$rp[2] / 2,
                [int]$rp[1] + [int]$rp[3] / 2)
            $screen = [System.Windows.Forms.Screen]::AllScreens |
                Where-Object { $_.WorkingArea.Contains($winCenter) } |
                Select-Object -First 1
            if ($null -eq $screen) { $screen = [System.Windows.Forms.Screen]::PrimaryScreen }
            $wa = $screen.WorkingArea
            $cx = $wa.Left + $wa.Width / 2
            $cy = $wa.Top + $wa.Height / 2
            Assert ([math]::Abs($winCenter.X - $cx) -lt $wa.Width * 0.15) "预览窗口水平居中: $($pv.Title)"
            Assert ([math]::Abs($winCenter.Y - $cy) -lt $wa.Height * 0.15) "预览窗口垂直居中: $($pv.Title)"
        }
        else {
            Assert $false "预览窗口矩形可读取: $($pv.Title)"
        }
    }
}

# ---------- 6. 清理 ----------
# ---------- 6. Shell 集成验证（空格键链路：Explorer 选区读取） ----------
Write-Host "== 6/7 Shell 集成验证 ==" -ForegroundColor Cyan
$selWin = Get-Process explorer -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -like '*ql-smoke*' } | Select-Object -First 1
if ($selWin) { $selWin.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 2 }
Start-Process explorer.exe "/select,`"$smoke\test.png`""
Start-Sleep -Seconds 6
$shellProbe = @"
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
public class ShellProbe2 {
    private static readonly Guid IID_IDataObject = new Guid("0000010E-0000-0000-C000-000000000046");
    private static readonly Guid IID_IShellBrowser = new Guid("000214E2-0000-0000-C000-000000000046");
    private static readonly Guid CLSID_ShellWindows = new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IServiceProvider { [PreserveSig] int QueryService(ref Guid g, ref Guid r, out IntPtr p); }
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellBrowser {
        [PreserveSig] int GetWindow(out IntPtr phwnd);
        [PreserveSig] int ContextSensitiveHelp(int f);
        [PreserveSig] int InsertMenusSB(IntPtr a, IntPtr b);
        [PreserveSig] int SetMenuSB(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int RemoveMenusSB(IntPtr a);
        [PreserveSig] int SetStatusTextSB(IntPtr a);
        [PreserveSig] int EnableModelessSB(int f);
        [PreserveSig] int TranslateAcceleratorSB(IntPtr a, ushort b);
        [PreserveSig] int BrowseObject(IntPtr a, uint b);
        [PreserveSig] int GetViewStateStream(uint a, out IntPtr b);
        [PreserveSig] int GetControlWindow(uint a, out IntPtr b);
        [PreserveSig] int SendControlMsg(uint a, uint b, IntPtr c, IntPtr d, out IntPtr e);
        [PreserveSig] int QueryActiveShellView(out IntPtr ppshv);
        [PreserveSig] int OnViewWindowActive(IntPtr a);
        [PreserveSig] int SetToolbarItems(IntPtr a, uint b, uint c);
    }
    [ComImport, Guid("000214E3-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellView {
        [PreserveSig] int GetWindow(out IntPtr phwnd);
        [PreserveSig] int ContextSensitiveHelp(int f);
        [PreserveSig] int TranslateAccelerator(IntPtr a);
        [PreserveSig] int EnableModeless(int f);
        [PreserveSig] int UIActivate(uint s);
        [PreserveSig] int Refresh();
        [PreserveSig] int CreateViewWindow(IntPtr a, IntPtr b, uint c, IntPtr d, out IntPtr e);
        [PreserveSig] int DestroyViewWindow();
        [PreserveSig] int GetCurrentInfo(out IntPtr a);
        [PreserveSig] int AddPropertySheetPages(uint a, IntPtr b, IntPtr c);
        [PreserveSig] int SaveViewState();
        [PreserveSig] int SelectItem(IntPtr a, uint b);
        [PreserveSig] int GetItemObject(uint u, ref Guid r, out IntPtr p);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int DragQueryFile(IntPtr h, uint i, StringBuilder b, int c);
    [DllImport("ole32.dll")]
    static extern void ReleaseStgMedium(ref STGMEDIUM m);
    public static string Probe() {
        Type swType = Type.GetTypeFromCLSID(CLSID_ShellWindows);
        object sw = Activator.CreateInstance(swType);
        int count = (int)swType.InvokeMember("Count", BindingFlags.GetProperty, null, sw, null);
        for (int i = 0; i < count; i++) {
            object disp = swType.InvokeMember("Item", BindingFlags.InvokeMethod, null, sw, new object[] { i });
            var sp = (IServiceProvider)disp;
            Guid iid = IID_IShellBrowser;
            IntPtr sbPtr;
            if (sp.QueryService(ref iid, ref iid, out sbPtr) != 0) continue;
            var browser = (IShellBrowser)Marshal.GetObjectForIUnknown(sbPtr);
            IntPtr psvPtr;
            if (browser.QueryActiveShellView(out psvPtr) != 0 || psvPtr == IntPtr.Zero) continue;
            var view = (IShellView)Marshal.GetObjectForIUnknown(psvPtr);
            Guid diid = IID_IDataObject;
            IntPtr daoPtr;
            if (view.GetItemObject(0x1, ref diid, out daoPtr) != 0 || daoPtr == IntPtr.Zero) continue;
            var dao = (IDataObject)Marshal.GetObjectForIUnknown(daoPtr);
            var fe = new FORMATETC { cfFormat = 15, ptd = IntPtr.Zero, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
            STGMEDIUM sm;
            try { dao.GetData(ref fe, out sm); }
            catch { return string.Empty; }
            try {
                var sb = new StringBuilder(32767);
                return DragQueryFile(sm.unionmember, 0, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
            }
            finally { ReleaseStgMedium(ref sm); }
        }
        return string.Empty;
    }
}
"@
Add-Type -TypeDefinition $shellProbe
$probeResult = [ShellProbe2]::Probe()
Assert (-not [string]::IsNullOrWhiteSpace($probeResult)) "Explorer 选区读取链路（COM 探针）返回: $probeResult"

# ---------- 7. 清理 ----------
Write-Host "== 7/7 清理 ==" -ForegroundColor Cyan
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue

if ($failed) {
    Write-Host "`n=== 测试失败 ===" -ForegroundColor Red
    exit 1
}
Write-Host "`n=== 全部测试通过 ===" -ForegroundColor Green
exit 0
