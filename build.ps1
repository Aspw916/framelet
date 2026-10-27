param([string]$FfmpegPath = '', [switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$releaseDir = Join-Path $projectRoot 'release\Framelet'
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $releaseDir 'bin') -Force | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compilerPath)) { throw '需要 Windows .NET Framework 4.8 的 C# 编译器。' }
$assetDir = Join-Path $projectRoot 'assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
$iconPath = Join-Path $assetDir 'Framelet.ico'
if (!(Test-Path -LiteralPath $iconPath)) {
    Add-Type -AssemblyName System.Drawing
    $bitmap = [Drawing.Bitmap]::new(64, 64)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::FromArgb(15, 20, 28))
    $pen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(109, 235, 193), 5)
    $graphics.DrawArc($pen, 9, 9, 46, 46, 24, 276)
    $graphics.DrawLine($pen, 47, 9, 55, 9)
    $graphics.DrawLine($pen, 55, 9, 55, 17)
    $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(109, 235, 193))
    $graphics.FillPolygon($brush, [Drawing.Point[]]@([Drawing.Point]::new(27, 21), [Drawing.Point]::new(27, 44), [Drawing.Point]::new(45, 32)))
    $icon = [Drawing.Icon]::FromHandle($bitmap.GetHicon())
    $iconFile = [IO.File]::Create($iconPath)
    try { $icon.Save($iconFile) } finally { $iconFile.Dispose(); $icon.Dispose(); $brush.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
$outputPath = Join-Path $releaseDir 'Framelet.exe'
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /debug:pdbonly "/out:$outputPath" "/win32manifest:$projectRoot\src\app.manifest" "/win32icon:$iconPath" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll $sources
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\Framelet.exe.config') -Destination (Join-Path $releaseDir 'Framelet.exe.config') -Force
if (!$FfmpegPath) { $command = Get-Command ffmpeg -ErrorAction SilentlyContinue; if ($command) { $FfmpegPath = $command.Source } }
if (!$FfmpegPath -or !(Test-Path -LiteralPath $FfmpegPath)) { throw '找不到 ffmpeg.exe，请用 -FfmpegPath 指定 FFmpeg 8.x Windows x64 静态版本。' }
$ffItem = Get-Item -LiteralPath $FfmpegPath
if ($ffItem.LinkType -eq 'SymbolicLink') { $FfmpegPath = [string]$ffItem.Target }
$ffDest = Join-Path $releaseDir 'bin\ffmpeg.exe'
if (!(Test-Path -LiteralPath $ffDest) -or (Get-Item -LiteralPath $ffDest).Length -ne (Get-Item -LiteralPath $FfmpegPath).Length) { Copy-Item -LiteralPath $FfmpegPath -Destination $ffDest -Force }
$ffBuildRoot = Split-Path (Split-Path $FfmpegPath -Parent) -Parent
$licenseDir = Join-Path $releaseDir 'licenses'
New-Item -ItemType Directory -Path $licenseDir -Force | Out-Null
foreach ($licenseName in @('LICENSE', 'LICENSE.txt', 'COPYING.GPLv3')) { $candidate = Join-Path $ffBuildRoot $licenseName; if (Test-Path -LiteralPath $candidate) { Copy-Item -LiteralPath $candidate -Destination (Join-Path $licenseDir 'FFmpeg-LICENSE.txt') -Force } }
foreach ($docName in @('README.md', 'THIRD-PARTY.md')) { if (Test-Path -LiteralPath (Join-Path $projectRoot $docName)) { Copy-Item -LiteralPath (Join-Path $projectRoot $docName) -Destination $releaseDir -Force } }
$versionText = & $FfmpegPath -version 2>&1
[IO.File]::WriteAllText((Join-Path $licenseDir 'FFmpeg-build.txt'), ($versionText -join "`r`n"), [Text.UTF8Encoding]::new($false))
if ($Package) {
    $zipPath = Join-Path $projectRoot 'release\Framelet-Windows-x64.zip'
    # User recordings and preferences never enter the portable distribution.
    $packageItems = @($outputPath, (Join-Path $releaseDir 'Framelet.exe.config'), (Join-Path $releaseDir 'bin'), $licenseDir, (Join-Path $releaseDir 'README.md'), (Join-Path $releaseDir 'THIRD-PARTY.md'))
    Compress-Archive -LiteralPath $packageItems -DestinationPath $zipPath -Force
    Write-Output "Package: $zipPath"
}
Write-Output "Ready: $outputPath"
