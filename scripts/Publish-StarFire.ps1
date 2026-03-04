param(
    [string]$Version = "1.0.10",
    [string]$Configuration = "Release",
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "ExtintorCrm.App\ExtintorCrm.App.csproj"
$publishRoot = Join-Path $root "artifacts\publish"
$outputDir = Join-Path $publishRoot "StarFire-$Version"

Write-Host ">> Limpando saída anterior: $outputDir"
if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}

Write-Host ">> Publicando Star Fire (self-contained, win-x64, multi-file)..."
dotnet publish $project `
  -c $Configuration `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=false `
  /p:PublishTrimmed=false `
  /p:DebugType=None `
  /p:DebugSymbols=false `
  /p:Version=$Version `
  -o $outputDir

if ($LASTEXITCODE -ne 0) {
    throw "Falha no dotnet publish (exit code: $LASTEXITCODE)."
}

Write-Host ">> Publish concluído em: $outputDir"
$exePath = Join-Path $outputDir "ExtintorCrm.App.exe"
if (-not (Test-Path $exePath)) {
    throw "Executável não encontrado após publish: $exePath"
}

if ($BuildInstaller) {
    $iscc = Get-Command iscc -ErrorAction SilentlyContinue
    $isccPath = $null

    if ($iscc) {
        $isccPath = $iscc.Source
    } else {
        $candidatePaths = @(
            "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            "C:\Program Files\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        )

        foreach ($candidate in $candidatePaths) {
            if (Test-Path $candidate) {
                $isccPath = $candidate
                break
            }
        }
    }

    if (-not $isccPath) {
        throw "Inno Setup (iscc.exe) não encontrado. Instale o Inno Setup 6."
    }

    $installerScript = Join-Path $root "installer\StarFire.iss"
    if (-not (Test-Path $installerScript)) {
        throw "Script do instalador não encontrado: $installerScript"
    }

    Write-Host ">> Gerando instalador Inno Setup..."
    & $isccPath "/DMyAppVersion=$Version" "/DPublishDir=$outputDir" $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Falha na geração do instalador (iscc exit code: $LASTEXITCODE)."
    }
    Write-Host ">> Instalador gerado em artifacts\installer"
}

