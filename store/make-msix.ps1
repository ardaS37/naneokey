param(
    [string]$PackageName = "Sapsoft.NaneOkeyOyunu",
    [string]$Publisher = "CN=YOUR-PARTNER-CENTER-PUBLISHER",
    [string]$PublisherDisplayName = "Sapsoft",
    [string]$Version = "4.0.0.0",
    [string]$ExePath = "build\NaneOkey.exe",
    [switch]$SkipSigning
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$layout = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "package"))
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

if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') {
    throw "Store sürümü dört parçalı olmalı ve son parça 0 olmalı: örnek 4.0.0.0."
}
$packageVersion = [version]$Version
if ($packageVersion.Major -lt 1 -or $packageVersion.Major -gt 65535 -or $packageVersion.Minor -gt 65535 -or $packageVersion.Build -gt 65535) {
    throw "Store sürüm bileşenleri geçerli aralıkta değil: $Version"
}
$exeVersion = (Get-Item -LiteralPath $exeFull).VersionInfo.FileVersion
if ($exeVersion -ne $Version) {
    throw "EXE sürümü ($exeVersion) ile Store paket sürümü ($Version) eşleşmiyor. Önce doğru EXE'yi derle."
}

# This directory is generated; never clean a path outside the Store workspace.
$storeRoot = (Resolve-Path -LiteralPath $PSScriptRoot).ProviderPath.TrimEnd('\')
if (-not $layout.StartsWith($storeRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Paket dizini Store çalışma alanının dışında: $layout"
}
if (Test-Path -LiteralPath $layout) {
    $layoutItem = Get-Item -LiteralPath $layout -Force
    if ($layoutItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Paket dizini bir bağlantı/junction olamaz: $layout"
    }
    Remove-Item -LiteralPath $layout -Recurse -Force
}
New-Item -Path $layout -ItemType Directory | Out-Null
New-Item -Path (Join-Path $layout "Assets") -ItemType Directory | Out-Null
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
& $makeAppx pack /d $layout /p $outPackage /o
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outPackage)) {
    throw "MSIX paketleme başarısız: $outPackage"
}

Write-Warning "The generated package is unsigned. Sign it only in your own local or CI environment."
Write-Host "MSIX hazır: $outPackage"
Write-Host "Set the Partner Center Publisher value locally before packaging."
