$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$dist = Join-Path $repoRoot 'dist'
$app = Join-Path $dist 'TimCookGuard.exe'
$setup = Join-Path $dist 'TimCookGuard-Setup.exe'
$installerSource = Join-Path $repoRoot 'installer\Installer.cs'
$icon = Join-Path $repoRoot 'assets\tim-cook-guard-logo.ico'

if (-not (Test-Path -LiteralPath $app)) {
    & (Join-Path $repoRoot 'build.ps1')
}

& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$setup" "/win32icon:$icon" /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll "/resource:$app,TimCookGuardInstaller.payload.exe" $installerSource
if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed with exit code $LASTEXITCODE"
}

$test = Start-Process -FilePath $setup -ArgumentList '--self-test' -Wait -PassThru
if ($test.ExitCode -ne 0) {
    throw "Installer self-test failed with exit code $($test.ExitCode)"
}

Get-FileHash -Algorithm SHA256 -LiteralPath $setup
