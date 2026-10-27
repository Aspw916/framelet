param([switch]$Ui)
$ErrorActionPreference = 'Stop'
$exePath = Join-Path $PSScriptRoot 'release\Framelet\Framelet.exe'
if (!(Test-Path -LiteralPath $exePath)) { throw '请先运行 build.ps1。' }
$coreOutput = Join-Path $PSScriptRoot 'verification\core'
$process = Start-Process -FilePath $exePath -ArgumentList @('--self-test', ('"' + $coreOutput + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath (Join-Path $coreOutput 'failure.txt') -Encoding UTF8; throw '核心检查失败。' }
Get-Content -LiteralPath (Join-Path $coreOutput 'results.txt') -Encoding UTF8
if ($Ui) {
    $uiOutput = Join-Path $PSScriptRoot 'verification\ui'
    $process = Start-Process -FilePath $exePath -ArgumentList @('--ui-test', ('"' + $uiOutput + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath (Join-Path $uiOutput 'ui-failure.txt') -Encoding UTF8; throw '界面检查失败。' }
    Get-Content -LiteralPath (Join-Path $uiOutput 'ui-results.txt') -Encoding UTF8
}
