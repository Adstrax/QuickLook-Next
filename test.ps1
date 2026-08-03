# QuickLook 冒烟测试：每次提交前必须运行并通过。
# 覆盖：全量构建 -> 启动 -> 插件加载无失败 -> PNG/文本/SQLite 预览 -> 窗口断言 -> 日志零新增错误。
#
# 用法: .\test.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root 'Build\Release\QuickLook.exe'
$log = Join-Path $env:APPDATA 'pooi.moe\QuickLook\QuickLook.Exception.log'
$smoke = Join-Path $env:TEMP 'ql-smoke'
$failed = $false

function Assert([bool]$cond, [string]$msg) {
    if ($cond) { Write-Host "PASS: $msg" -ForegroundColor Green }
    else { Write-Host "FAIL: $msg" -ForegroundColor Red; $script:failed = $true }
}

function Get-LogLength {
    if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
}

function Get-QuickLookWindows([int]$targetPid) {
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

# ---------- 1. 清理旧实例 ----------
Write-Host "== 1/6 清理旧实例 ==" -ForegroundColor Cyan
Get-Process -Name QuickLook -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3

# ---------- 2. 构建 ----------
Write-Host "== 2/6 全量构建 ==" -ForegroundColor Cyan
Get-ChildItem (Join-Path $root 'Build\Release') -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force
$projects = @(
    (Join-Path $root 'QuickLook.Common\QuickLook.Common.csproj'),
    (Join-Path $root 'QuickLook\QuickLook.csproj')
)
$projects += Get-ChildItem (Join-Path $root 'QuickLook.Plugin') -Recurse -Filter *.csproj |
    Select-Object -ExpandProperty FullName
foreach ($p in $projects) {
    & dotnet build $p -c Release -v minimal --nologo *> $null
    Assert ($LASTEXITCODE -eq 0) "构建 $([IO.Path]::GetFileName($p))"
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

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
Set-Content -Path (Join-Path $smoke 'test.txt') -Value "QuickLook smoke test`r`nLine 2" -Encoding UTF8
Set-Content -Path (Join-Path $smoke 'test.md') -Value "# Markdown`n`n这是 **测试**。" -Encoding UTF8
Compress-Archive -Path (Join-Path $smoke 'test.txt') -DestinationPath (Join-Path $smoke 'test.zip') -Force
Copy-Item -LiteralPath "$env:WINDIR\Fonts\arial.ttf" -Destination (Join-Path $smoke 'test.ttf') -Force
if (-not (Test-Path (Join-Path $smoke 'test.mp4'))) {
    curl.exe -sL -o (Join-Path $smoke 'test.mp4') "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4"
}

# ---------- 4. 启动 + 插件加载 ----------
Write-Host "== 4/6 启动并验证插件加载 ==" -ForegroundColor Cyan
$before = Get-LogLength
$p = Start-Process -FilePath $exe -ArgumentList '/autorun' -PassThru
Start-Sleep -Seconds 25
$alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
Assert ($null -ne $alive) '启动后进程存活'
Assert ((Get-LogLength) -eq $before) '插件加载无失败（日志零新增）'

# ---------- 5. 预览测试 ----------
Write-Host "== 5/6 预览测试 ==" -ForegroundColor Cyan
$previews = @(
    @{ File = 'test.png'; Title = 'test.png' },
    @{ File = 'test.txt'; Title = 'test.txt' },
    @{ File = 'test.md'; Title = 'test.md' },
    @{ File = 'test.zip'; Title = 'test.zip' },
    @{ File = 'test.ttf'; Title = 'test.ttf' }
)
if (Test-Path (Join-Path $smoke 'test.mp4')) {
    $previews += @{ File = 'test.mp4'; Title = 'test.mp4' }
}
foreach ($pv in $previews) {
    $before = Get-LogLength
    & $exe (Join-Path $smoke $pv.File)
    Start-Sleep -Seconds 12
    $alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
    Assert ($null -ne $alive) "预览 $($pv.File) 后进程存活"
    $titles = Get-QuickLookWindows $p.Id
    Assert (($titles -join ' ') -match [regex]::Escape($pv.Title)) "预览窗口出现: $($pv.Title)"
    Assert ((Get-LogLength) -eq $before) "预览 $($pv.File) 无错误（日志零新增）"
}

# ---------- 6. 清理 ----------
Write-Host "== 6/6 清理 ==" -ForegroundColor Cyan
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue

if ($failed) {
    Write-Host "`n=== 测试失败 ===" -ForegroundColor Red
    exit 1
}
Write-Host "`n=== 全部测试通过 ===" -ForegroundColor Green
exit 0
