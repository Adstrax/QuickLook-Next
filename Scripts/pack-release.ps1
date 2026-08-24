# 生成用户友好的发布包：Build\Release -> Build\Package -> Build\QuickLook-Next-<version>.zip
#
# 解压后 QuickLook-Next.exe 直接位于根目录（不再藏在 Build\Release 深处），
# 插件保留在 QuickLook.Plugin 子目录，并带 portable.lock（数据目录跟随程序目录）。
#
# 用法：
#   .\Scripts\pack-release.ps1             # 只整理到 Build\Package
#   .\Scripts\pack-release.ps1 -MakeZip    # 整理并生成 zip

param([switch]$MakeZip)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = Join-Path $root 'Build\Release'
$package = Join-Path $root 'Build\Package'

if (-not (Test-Path $release)) {
    throw "未找到构建产物：$release（请先执行 .\build.ps1）"
}

$version = & git -C $root describe --always --tags --exclude latest 2>$null
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = '0.0.0'
}

# 重建 Package 目录
if (Test-Path $package) {
    Remove-Item -LiteralPath $package -Recurse -Force
}
New-Item -ItemType Directory -Path $package | Out-Null

# exe 与全部运行文件放到根目录；插件保留 QuickLook.Plugin 子目录；去掉 pdb
Get-ChildItem -LiteralPath $release |
    Where-Object { $_.Name -ne 'QuickLook.Plugin' -and $_.Extension -ne '.pdb' } |
    Copy-Item -Destination $package -Recurse -Force
Copy-Item -LiteralPath (Join-Path $release 'QuickLook.Plugin') `
    -Destination (Join-Path $package 'QuickLook.Plugin') -Recurse -Force

# 发布包不需要调试符号
Get-ChildItem -LiteralPath $package -Recurse -Filter *.pdb |
    Remove-Item -Force

# 便携标记：设置数据目录跟随程序目录
Set-Content -LiteralPath (Join-Path $package 'portable.lock') `
    -Value 'This file makes QuickLook-Next portable.' -Encoding ASCII

if (-not $MakeZip) {
    Write-Host "已整理到：$package"
    Write-Host "（加 -Zip 参数可生成压缩包）"
    exit 0
}

$zip = Join-Path $root "Build\QuickLook-Next-$version.zip"
Remove-Item -LiteralPath $zip -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression

# 手动写 zip 并统一使用正斜杠分隔符，避免 Windows 下反斜杠路径导致
# 部分解压工具（macOS / Linux 等）把条目当成单文件名
$base = $package.TrimEnd('\')
$fileStream = [System.IO.File]::Open($zip, 'Create')
$archive = New-Object System.IO.Compression.ZipArchive($fileStream, 'Create')
try {
    Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($base.Length + 1).Replace('\', '/')
        $entry = $archive.CreateEntry($relative, 'Optimal')
        $inputStream = [System.IO.File]::OpenRead($_.FullName)
        try {
            $entryStream = $entry.Open()
            try {
                $inputStream.CopyTo($entryStream)
            } finally {
                $entryStream.Dispose()
            }
        } finally {
            $inputStream.Dispose()
        }
    }
} finally {
    $archive.Dispose()
    $fileStream.Dispose()
}
Remove-Item -LiteralPath (Join-Path $package 'portable.lock')

Write-Host "已生成发布包：$zip"
