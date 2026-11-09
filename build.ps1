param(
    [switch]$SkipIcon
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$source = Join-Path $root 'DelayLauncher.cs'
$manifest = Join-Path $root 'app.manifest'
$icon = Join-Path $root 'app.ico'
$output = Join-Path $root 'DelayLauncher.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "找不到 .NET Framework 4.x 编译器：$csc"
}

if (-not $SkipIcon -and -not (Test-Path -LiteralPath $icon)) {
    & (Join-Path $root 'make-icon.ps1')
}

$compilerArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/codepage:65001',
    '/optimize+',
    '/debug-',
    "/out:$output",
    '/reference:System.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
)

if (Test-Path -LiteralPath $icon) {
    $compilerArgs += "/win32icon:$icon"
}

if (Test-Path -LiteralPath $manifest) {
    $compilerArgs += "/win32manifest:$manifest"
}

$compilerArgs += $source

& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码：$LASTEXITCODE"
}

$file = Get-Item -LiteralPath $output
Write-Host ("编译成功：{0}（{1:N0} 字节）" -f $file.FullName, $file.Length)
