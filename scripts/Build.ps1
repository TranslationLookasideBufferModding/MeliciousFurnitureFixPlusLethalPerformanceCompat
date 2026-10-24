param([switch]$Test)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$settings=Get-Content -LiteralPath (Join-Path $repo 'local.settings.json') -Raw | ConvertFrom-Json
$core=Join-Path $settings.ProfileDirectory 'BepInEx\core'
$managed=Join-Path $settings.GameDirectory 'Lethal Company_Data\Managed'
$furniture=Join-Path $settings.ProfileDirectory 'BepInEx\plugins\MelanieMelicious-MelanieMelicious_Utility_Furniture_Pack\MelanieMeliciousUtilFurniture.dll'
$lll=Join-Path $settings.ProfileDirectory 'BepInEx\plugins\IAmBatby-LethalLevelLoader\LethalLevelLoader.dll'
$dawn=Join-Path $settings.ProfileDirectory 'BepInEx\plugins\TeamXiaolan-DawnLib\DawnLib\com.github.teamxiaolan.dawnlib.dll'
& (Join-Path $PSScriptRoot 'Verify-References.ps1') -ProfileDirectory $settings.ProfileDirectory -GameDirectory $settings.GameDirectory
$bin=Join-Path $repo 'artifacts\bin'
$reports=Join-Path $repo 'artifacts\tests'
New-Item -ItemType Directory -Path $bin,$reports -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@((Join-Path $core 'BepInEx.dll'),(Join-Path $core '0Harmony.dll'))
foreach($name in @('Assembly-CSharp.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.AudioModule.dll','UnityEngine.AnimationModule.dll','UnityEngine.UnityWebRequestModule.dll','Unity.Netcode.Runtime.dll','netstandard.dll')){$refs+=Join-Path $managed $name}
$dll=Join-Path $bin 'MliciosFurnitureFixPlus.dll'
$compile=@('/nologo','/target:library','/optimize+','/langversion:5',"/out:$dll")
$compile+=@($refs | ForEach-Object {if(-not(Test-Path -LiteralPath $_)){throw "Missing reference $_"};"/reference:$_"})
$compile+=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -File -Filter '*.cs' | Sort-Object Name | ForEach-Object {$_.FullName})
& $compiler @compile
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
Write-Host "Built $dll"
if($Test){
 $support=Join-Path $reports 'CacheSimulation.dll'
 & $compiler /nologo /target:library /optimize+ /langversion:5 "/reference:$(Join-Path $core '0Harmony.dll')" "/out:$support" (Join-Path $repo 'tests\CacheSimulation.cs')
 if($LASTEXITCODE -ne 0){throw 'Test helper build failed.'}
 $unityLog=Join-Path $reports 'unity-runtime.log'
 $project=Join-Path $repo 'tests\UnityHarness'
 $arguments=@('-batchmode','-nographics','-quit','-noUpm','-projectPath',('"'+$project+'"'),'-executeMethod','FireExitTests.Run','-pluginPath',('"'+$dll+'"'),'-supportPath',('"'+$support+'"'),'-corePath',('"'+$core+'"'),'-managedPath',('"'+$managed+'"'),'-furniturePath',('"'+$furniture+'"'),'-lllPath',('"'+$lll+'"'),'-pluginsPath',('"'+(Join-Path $settings.ProfileDirectory 'BepInEx\plugins')+'"'),'-logFile',('"'+$unityLog+'"'))
 if(Test-Path -LiteralPath $dawn){$arguments+=@('-dawnPath',('"'+$dawn+'"'))}
 $process=Start-Process -FilePath $settings.UnityEditor -ArgumentList $arguments -PassThru -WindowStyle Hidden
 while(-not $process.WaitForExit(1000)){if((Get-Date)-$process.StartTime -gt [TimeSpan]::FromMinutes(4)){$process.Kill();throw 'Unity tests timed out.'}}
 $log=Get-Content -LiteralPath $unityLog -Raw
 if($process.ExitCode -ne 0 -or -not $log.Contains('FIRE_EXIT_TESTS_OK')){
  $log -split "`r?`n" | Where-Object {$_ -match 'Exception|error CS|FAIL' -and $_ -notmatch '^\[Licensing'} | ForEach-Object {Write-Host $_}
  throw "Unity tests failed. See $unityLog"
 }
 $log -split "`r?`n" | Where-Object {$_ -match '^PASS|^FIRE_EXIT_TESTS_OK'} | Tee-Object -FilePath (Join-Path $reports 'TESTRESULTS.txt') | ForEach-Object {Write-Host $_}
 $inputs=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -File -Filter '*.cs')+@(Get-ChildItem -LiteralPath (Join-Path $repo 'scripts') -File -Filter '*.ps1')+@(Get-ChildItem -LiteralPath (Join-Path $repo 'tests') -File -Filter '*.cs')+@(Get-ChildItem -LiteralPath (Join-Path $repo 'tests\UnityHarness\Assets\Editor') -File -Filter '*.cs')
 $sourceHashes=($inputs | Sort-Object FullName | ForEach-Object {(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}) -join ':'
 [ordered]@{DllSHA256=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash;SourceHashes=$sourceHashes;InstalledReferences='Passed';UnityRuntime='Passed';LobbyWithLethalPerformance='Pending';Utc=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reports 'verification.json') -Encoding UTF8
}
