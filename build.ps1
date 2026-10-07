param([switch]$Tests, [string]$OutputDirectory = 'build')
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskBuild = [IO.Path]::GetFullPath((Join-Path $taskRoot $OutputDirectory))
if (-not $taskBuild.StartsWith($taskRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Çıktı klasörü proje dizininin içinde olmalı.'
}
New-Item -ItemType Directory -Force -Path $taskBuild | Out-Null
$taskDotnet = (Get-Command dotnet.exe).Source
$taskSdk = & $taskDotnet --list-sdks | Select-Object -Last 1
if ($taskSdk -notmatch '^([^ ]+) \[(.+)\]') { throw '.NET SDK bulunamadı.' }
$taskCompiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn/bincore/csc.dll'
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319'
$taskReferences = @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Web.Extensions', 'System.Runtime.Serialization', 'System.Xml', 'System.Xml.Linq', 'System.Data', 'Microsoft.CSharp') | ForEach-Object { '/reference:"' + (Join-Path $taskFramework ($_ + '.dll')) + '"' }
$taskPhoton = Join-Path $taskRoot 'lib/Photon/Photon-DotNet.dll'
$taskSources = Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src/NaneOkey') -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } | Sort-Object FullName
$taskArguments = @('/nologo', '/target:winexe', '/platform:x86', '/optimize+', '/deterministic+', '/langversion:7.3', '/nostdlib+', ('/out:"' + (Join-Path $taskBuild 'NaneOkey.exe') + '"'), ('/win32icon:"' + (Join-Path $taskRoot 'src/NaneOkey/Assets/app.ico') + '"'))
$taskArguments += $taskReferences
$taskArguments += '/reference:"' + $taskPhoton + '"'
$taskArguments += '/resource:"' + $taskPhoton + '",Photon-DotNet.dll'
foreach ($taskAsset in @('emotes.png', 'cay.png')) { $taskArguments += '/resource:"' + (Join-Path $taskRoot ('src/NaneOkey/Assets/' + $taskAsset)) + '",NaneOkey.Assets.' + $taskAsset }
foreach ($taskSound in @('move.wav', 'basla.wav', 'win.wav', 'lose.wav', 'fullwin.wav')) { $taskArguments += '/resource:"' + (Join-Path $taskRoot $taskSound) + '",NaneOkey.Assets.' + $taskSound }
$taskArguments += $taskSources | ForEach-Object { '"' + $_.FullName + '"' }
$taskResponse = Join-Path $taskBuild 'compile.rsp'
[IO.File]::WriteAllLines($taskResponse, [string[]]$taskArguments, [Text.UTF8Encoding]::new($false))
& $taskDotnet $taskCompiler ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
Write-Output ('Derlendi: ' + (Join-Path $taskBuild 'NaneOkey.exe'))
if ($Tests) {
    $taskTestSources = Get-ChildItem -LiteralPath (Join-Path $taskRoot 'tests') -Filter '*Tests.cs' | Sort-Object Name
    foreach ($taskTest in $taskTestSources) {
        $taskTestExe = Join-Path $taskBuild ($taskTest.BaseName + '.exe')
        $taskTestArgs = @('/nologo', '/target:exe', '/platform:x86', '/langversion:7.3', '/nostdlib+', ('/out:"' + $taskTestExe + '"')) + $taskReferences + @(('/reference:"' + (Join-Path $taskBuild 'NaneOkey.exe') + '"'), ('"' + $taskTest.FullName + '"'))
        $taskTestResponse = Join-Path $taskBuild ($taskTest.BaseName + '.rsp')
        [IO.File]::WriteAllLines($taskTestResponse, [string[]]$taskTestArgs, [Text.UTF8Encoding]::new($false))
        & $taskDotnet $taskCompiler ('@' + $taskTestResponse)
        if ($LASTEXITCODE -ne 0) { throw ('Test derlemesi başarısız: ' + $taskTest.Name) }
        & $taskTestExe
        if ($LASTEXITCODE -ne 0) { throw ('Test başarısız: ' + $taskTest.Name) }
    }
}
