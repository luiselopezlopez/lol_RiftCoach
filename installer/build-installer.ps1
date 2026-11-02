$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot 'artifacts\installer-publish'
$setupScript = Join-Path $PSScriptRoot 'RiftCoach.iss'
$outputDir = Join-Path $repoRoot 'artifacts\installer'

dotnet publish (Join-Path $repoRoot 'src\LoLCoach\LoLCoach.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish fallo con codigo $LASTEXITCODE."
}

$compiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $compiler) {
    $compiler = @(
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $compiler) {
    throw 'No se encontro ISCC.exe. Instala Inno Setup 6 y vuelve a ejecutar este script.'
}

& $compiler $setupScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup fallo con codigo $LASTEXITCODE."
}

Write-Host "Instalador generado: $(Join-Path $outputDir 'RiftCoach-Setup-win-x64.exe')"