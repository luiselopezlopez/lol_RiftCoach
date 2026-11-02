param(
    [ValidatePattern('^\d{1,14}$')]
    [string]$BuildNumber = (Get-Date -Format 'yyyyMMddHHmmss')
)

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

& $compiler "/O$outputDir" "/DAppVersion=$BuildNumber" $setupScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup fallo con codigo $LASTEXITCODE."
}

$installerPath = Join-Path $outputDir 'RiftCoach-Setup-win-x64.exe'
if (-not (Test-Path $installerPath)) {
    throw "Inno Setup termino, pero no se encontro el instalador esperado: $installerPath"
}

Write-Host "Instalador generado: $installerPath"