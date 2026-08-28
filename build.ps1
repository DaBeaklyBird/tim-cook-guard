$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$dist = Join-Path $repoRoot 'dist'
$app = Join-Path $dist 'TimCookGuard.exe'
$source = Join-Path $repoRoot 'src\TimCookGuard.cs'
$dashboard = Join-Path $repoRoot 'src\ControlPanelDashboard.cs'
$portrait = Join-Path $repoRoot 'assets\tim-cook-guard-logo.png'
$icon = Join-Path $repoRoot 'assets\tim-cook-guard-logo.ico'
$sound = Join-Path $repoRoot 'assets\tim-cook-wow.mp3'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework x64 C# compiler was not found at $compiler"
}

New-Item -ItemType Directory -Path $dist -Force | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$app" "/win32icon:$icon" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Security.dll "/resource:$portrait,TimCookGuard.tim-cook.jpg" "/resource:$portrait,TimCookGuard.logo.png" "/resource:$sound,TimCookGuard.wow.mp3" $source $dashboard
if ($LASTEXITCODE -ne 0) {
    throw "Application build failed with exit code $LASTEXITCODE"
}

$test = Start-Process -FilePath $app -ArgumentList '--self-test' -Wait -PassThru
if ($test.ExitCode -ne 0) {
    throw "Application self-test failed with exit code $($test.ExitCode)"
}

Get-FileHash -Algorithm SHA256 -LiteralPath $app
