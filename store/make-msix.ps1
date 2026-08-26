param(
    [string]$PackageName = "YOUR_PACKAGE_NAME",
    [string]$Publisher = "CN=YOUR_PARTNER_CENTER_PUBLISHER",
    [string]$PublisherDisplayName = "YOUR_PUBLISHER_NAME",
    [string]$Version = "3.0.5.0",
    [string]$ExePath = "build\NaneOkey.exe",
    [string]$PfxPath = "",
    [string]$PfxPassword = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$layout = Join-Path $PSScriptRoot "package"
$outDir = Join-Path $PSScriptRoot "out"
$template = Join-Path $PSScriptRoot "AppxManifest.xml.template"
$makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue

if (-not $makeAppx) {
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $sdkRoot) {
        $makeAppxPath = Get-ChildItem $sdkRoot -Recurse -Filter makeappx.exe | Where-Object { $_.FullName -match "x64|x86" } | Sort-Object FullName -Descending | Select-Object -First 1
        if ($makeAppxPath) { $makeAppx = $makeAppxPath.FullName }
        $signtoolPath = Get-ChildItem $sdkRoot -Recurse -Filter signtool.exe | Where-Object { $_.FullName -match "x64|x86" } | Sort-Object FullName -Descending | Select-Object -First 1
        if ($signtoolPath) { $signtool = $signtoolPath.FullName }
    }
}

if (-not $makeAppx) {
    throw "makeappx.exe bulunamadı. Windows 10/11 SDK kurulu olmalı."
}

$exeFull = Join-Path $root $ExePath
if (-not (Test-Path $exeFull)) {
    throw "Exe bulunamadı: $exeFull. Önce build/NaneOkey.exe üret."
}

Remove-Item $layout -Recurse -Force -ErrorAction SilentlyContinue
New-Item $layout -ItemType Directory | Out-Null
New-Item (Join-Path $layout "Assets") -ItemType Directory | Out-Null
New-Item $outDir -ItemType Directory -Force | Out-Null

Copy-Item $exeFull (Join-Path $layout "NaneOkey.exe")
Copy-Item (Join-Path $PSScriptRoot "Assets\*.png") (Join-Path $layout "Assets")

$manifest = Get-Content $template -Raw
$manifest = $manifest.Replace("{{PACKAGE_NAME}}", $PackageName)
$manifest = $manifest.Replace("{{PUBLISHER}}", $Publisher)
$manifest = $manifest.Replace("{{PUBLISHER_DISPLAY_NAME}}", $PublisherDisplayName)
$manifest = $manifest.Replace("{{VERSION}}", $Version)
Set-Content -Path (Join-Path $layout "AppxManifest.xml") -Value $manifest -Encoding UTF8

$outPackage = Join-Path $outDir ("NaneOkey_{0}_x86.msix" -f $Version)
Remove-Item $outPackage -Force -ErrorAction SilentlyContinue
& $makeAppx pack /d $layout /p $outPackage /o

if ($PfxPath) {
    if (-not $signtool) { throw "signtool.exe bulunamadı; imzalama yapılamadı." }
    if ($PfxPassword) {
        & $signtool sign /fd SHA256 /a /f $PfxPath /p $PfxPassword $outPackage
    } else {
        & $signtool sign /fd SHA256 /a /f $PfxPath $outPackage
    }
}

Write-Host "MSIX hazır: $outPackage"
Write-Host "Store için Publisher parametresini Partner Center Package identity değerindeki Publisher ile aynı yap."
