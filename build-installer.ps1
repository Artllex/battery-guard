param([string]$Compiler)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if (!$Compiler) {
    $candidate = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($candidate) { $Compiler = $candidate.Source }
    else { $Compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
}
if (!(Test-Path -LiteralPath $Compiler)) { throw 'Podaj ścieżkę do ISCC.exe: .\build-installer.ps1 -Compiler <ścieżka>' }
& $Compiler (Join-Path $PSScriptRoot 'installer\BatteryGuard.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
