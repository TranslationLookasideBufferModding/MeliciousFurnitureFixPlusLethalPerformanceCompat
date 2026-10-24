param([string]$ProfileDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(Get-Process -Name 'Lethal Company' -ErrorAction SilentlyContinue){throw 'Bitte zuerst das Spiel schliessen.'}
if(-not $ProfileDirectory){
 $settings=Join-Path $root 'local.settings.json'
 if(Test-Path -LiteralPath $settings){$ProfileDirectory=(Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json).ProfileDirectory}
 else {$ProfileDirectory=(Read-Host 'Vollstaendigen r2modman-Profilordner einfuegen (ohne Anfuehrungszeichen)').Trim().Trim('"')}
}
if(-not $ProfileDirectory){throw 'Kein Profil ausgewaehlt.'}
$plugins=[IO.Path]::GetFullPath((Join-Path $ProfileDirectory 'BepInEx\plugins'))
if(-not(Test-Path -LiteralPath $plugins -PathType Container)){throw 'Profil mit BepInEx/plugins wurde nicht gefunden.'}
$dll=Join-Path $root 'artifacts\bin\MliciosFurnitureFixPlus.dll'
$proof=Get-Content -LiteralPath (Join-Path $root 'artifacts\tests\verification.json') -Raw | ConvertFrom-Json
if($proof.DllSHA256 -ne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -or $proof.UnityRuntime -ne 'Passed'){throw 'Der Testnachweis passt nicht zur DLL.'}
$destination=[IO.Path]::GetFullPath((Join-Path $plugins 'MliciosFurnitureFixPlus-LocalTest\MliciosFurnitureFixPlus.dll'))
if(-not $destination.StartsWith($plugins+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsicherer Zielpfad.'}
$duplicates=@(Get-ChildItem -LiteralPath $plugins -Recurse -File -Filter 'MliciosFurnitureFixPlus.dll' | Where-Object {$_.FullName -ne $destination})
if($duplicates.Count -gt 0){throw ('Andere Installation gefunden. Erst die alte Version deaktivieren/entfernen: '+($duplicates.FullName -join ', '))}
if(Test-Path -LiteralPath $destination){
 $backup=Join-Path $root ('artifacts\backups\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
 New-Item -ItemType Directory -Path $backup -Force | Out-Null
 Copy-Item -LiteralPath $destination -Destination $backup
}
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $destination -Force
Write-Host "Installiert: $destination"
Write-Host 'Auf allen Mitspielern installieren; LethalPerformance aktivieren; docs/TESTPLAN.md beachten.'
