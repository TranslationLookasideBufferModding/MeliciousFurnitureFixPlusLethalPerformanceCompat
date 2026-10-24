$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$manifestPath=Join-Path $root 'package\manifest.json'
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$dll=Join-Path $root 'artifacts\bin\MliciosFurnitureFixPlus.dll'
$proof=Get-Content -LiteralPath (Join-Path $root 'artifacts\tests\verification.json') -Raw | ConvertFrom-Json
if($manifest.name -notmatch '^[A-Za-z0-9_]{1,128}$' -or $manifest.version_number -notmatch '^\d+\.\d+\.\d+$' -or $manifest.description.Length -gt 250){throw 'Ungueltiges Manifest.'}
if(([Reflection.AssemblyName]::GetAssemblyName($dll)).Version.ToString(3) -ne $manifest.version_number){throw 'DLL- und Manifest-Version unterscheiden sich.'}
if($proof.DllSHA256 -ne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -or $proof.UnityRuntime -ne 'Passed' -or $proof.InstalledReferences -ne 'Passed'){throw 'Test.cmd ausfuehren: kein passender Testnachweis.'}
$inputs=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs')+@(Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -File -Filter '*.ps1')+@(Get-ChildItem -LiteralPath (Join-Path $root 'tests') -File -Filter '*.cs')+@(Get-ChildItem -LiteralPath (Join-Path $root 'tests\UnityHarness\Assets\Editor') -File -Filter '*.cs')
$sourceHashes=($inputs | Sort-Object FullName | ForEach-Object {(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}) -join ':'
if($sourceHashes -ne $proof.SourceHashes){throw 'Quellcode/Skripte wurden seit dem Test geaendert. Erst Test.cmd ausfuehren.'}
$icon=Join-Path $root 'package\icon.png'
Add-Type -AssemblyName System.Drawing
$picture=[Drawing.Image]::FromFile($icon)
try {if($picture.Width -ne 256 -or $picture.Height -ne 256){throw 'icon.png muss 256x256 Pixel haben.'}}finally{$picture.Dispose()}
$release=Join-Path $root 'artifacts\releases'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$destination=Join-Path $release ($manifest.name+'-'+$manifest.version_number+'.zip')
$temporary=$destination+'.tmp'
if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary}
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zip=[IO.Compression.ZipFile]::Open($temporary,[IO.Compression.ZipArchiveMode]::Create)
try{
 $files=@{'README.md'=(Join-Path $root 'README.md');'CHANGELOG.md'=(Join-Path $root 'CHANGELOG.md');'LICENSE.txt'=(Join-Path $root 'LICENSE.txt');'manifest.json'=$manifestPath;'icon.png'=$icon;'plugins/MliciosFurnitureFixPlus/MliciosFurnitureFixPlus.dll'=$dll}
 foreach($name in $files.Keys){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$files[$name],$name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null}
}finally{$zip.Dispose()}
Move-Item -LiteralPath $temporary -Destination $destination -Force
Write-Host "Fertiges Paket: $destination"
Write-Host 'Vor dem Veroeffentlichen Host + Client mit aktiviertem LethalPerformance testen.'
