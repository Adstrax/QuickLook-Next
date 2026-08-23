$version = git describe --always --tags --exclude latest

Set-Location ../Build

Write-Output "This file makes QuickLookNext portable." >> .\Package\portable.lock

Remove-Item .\QuickLookNext-$version.7z -ErrorAction SilentlyContinue
Remove-Item -Recurse .\Package\QuickLookNext.WoW64HookHelper.exe -ErrorAction SilentlyContinue
7z.exe a .\QuickLookNext-$version.7z .\Package\* -t7z -mx=9 -ms=on -m0=lzma2 -mf=BCJ2 -r -y

Remove-Item .\Package\portable.lock
