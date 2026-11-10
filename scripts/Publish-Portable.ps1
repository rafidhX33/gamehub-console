param([string]$Output = (Join-Path $PSScriptRoot '..\artifacts\GameHub-win-x64'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$Output = [IO.Path]::GetFullPath($Output)
dotnet publish (Join-Path $repo 'src\GameHub.App') -c Release -r win-x64 --self-contained true -o $Output
if ($LASTEXITCODE -ne 0) { throw 'App publish failed' }
dotnet publish (Join-Path $repo 'src\GameHub.Session') -c Release -r win-x64 --self-contained true -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Session guardian publish failed' }
Copy-Item (Join-Path $repo 'README.md') (Join-Path $Output 'README.md') -Force
New-Item -ItemType Directory -Path (Join-Path $Output 'scripts') -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'Restore-WindowsDesktop.ps1') (Join-Path $Output 'scripts\Restore-WindowsDesktop.ps1') -Force
$files = @(Get-ChildItem $Output -File -Recurse | Where-Object { $_.Name -ne 'package.manifest.json' } | ForEach-Object { [IO.Path]::GetRelativePath($Output, $_.FullName) })
ConvertTo-Json -InputObject $files | Set-Content (Join-Path $Output 'package.manifest.json') -Encoding utf8
Write-Host "Portable package ready: $Output"
