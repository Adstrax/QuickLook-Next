$version = git describe --always --tags --exclude latest

Start-Sleep -s 1

Write-Output "This file makes QuickLookNext portable." >> ..\Build\Package\portable.lock

Remove-Item ..\Build\QuickLookNext-$version.zip -ErrorAction SilentlyContinue
Remove-Item -Recurse ..\Build\Package\QuickLookNext.WoW64HookHelper.exe -ErrorAction SilentlyContinue
Compress-Archive ..\Build\Package\* ..\Build\QuickLookNext-$version.zip

Remove-Item ..\Build\Package\portable.lock