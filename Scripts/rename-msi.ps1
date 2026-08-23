$version = git describe --always --tags --exclude latest

Remove-Item ..\Build\QuickLookNext-$version.msi -ErrorAction SilentlyContinue
Rename-Item ..\Build\QuickLookNext.msi QuickLookNext-$version.msi