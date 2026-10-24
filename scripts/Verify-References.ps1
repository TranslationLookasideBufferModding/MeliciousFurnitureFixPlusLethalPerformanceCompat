param([Parameter(Mandatory=$true)][string]$ProfileDirectory,[Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $ProfileDirectory 'BepInEx\core\Mono.Cecil.dll')
$game=Join-Path $GameDirectory 'Lethal Company_Data\Managed\Assembly-CSharp.dll'
$furniture=Join-Path $ProfileDirectory 'BepInEx\plugins\MelanieMelicious-MelanieMelicious_Utility_Furniture_Pack\MelanieMeliciousUtilFurniture.dll'
$lll=Join-Path $ProfileDirectory 'BepInEx\plugins\IAmBatby-LethalLevelLoader\LethalLevelLoader.dll'
function Expect($condition,[string]$message){if(-not $condition){throw "Reference check failed: $message"}}
function IL($type,[string]$method){return (($type.Methods | Where-Object Name -eq $method).Body.Instructions | ForEach-Object {$_.ToString()}) -join "`n"}
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($game)
try {
 $round=$a.MainModule.GetType('RoundManager')
 Expect (([regex]::Matches((IL $round 'SetExitIDs'),'call .*UnityEngine.Object::FindObjectsOfType<EntranceTeleport>\(\)')).Count -eq 1) 'v81 SetExitIDs entrance scan layout'
 Expect ((IL $round 'FinishGeneratingNewLevelClientRpc') -match '::SetLevelObjectVariables\(\)') 'exit-ID initialization also runs from the client RPC'
 Expect ((IL $round 'SetLevelObjectVariables') -match '::waitForMainEntranceTeleportToSpawn\(\)') 'client ID coroutine entry point'
 Expect ((IL ($a.MainModule.GetType('RoundManager/<waitForMainEntranceTeleportToSpawn>d__231')) 'MoveNext') -match '::SetExitIDs\(') 'client coroutine assigns exit IDs'
 $door=$a.MainModule.GetType('EntranceTeleport')
 foreach($name in @('entranceId','entrancePoint','isEntranceToBuilding','exitScript','gotExitPoint','checkedForFirstTime','exitPointDoesntExist')){Expect (@($door.Fields | Where-Object Name -eq $name).Count -eq 1) "EntranceTeleport.$name"}
}finally{$a.Dispose()}
foreach($item in @(@($furniture,'MelanieMeliciousFurniture.Plugin','2.0.5'),@($lll,'LethalLevelLoader.Plugin','1.7.13'))){
 $a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($item[0])
 try {
  $plugin=$a.MainModule.GetType($item[1])
  $attribute=$plugin.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'}
  Expect ($attribute.ConstructorArguments[2].Value -eq $item[2]) ($item[1]+' version '+$item[2])
  if($item[1] -match 'Furniture'){
   $fire=$a.MainModule.GetType('FireExitPatch')
   Expect ((IL $fire 'PreLevelLoad') -match 'UnityEngine.Transform::SetParent') 'original furniture reparenting helper'
   Expect (@($fire.Methods | Where-Object Name -eq 'PreShipLeft').Count -eq 1) 'original takeoff helper'
  }else{
   $loader=$a.MainModule.GetType('LethalLevelLoader.DungeonLoader')
   Expect ((IL $loader 'GetEntranceTeleports') -match '::GetRootGameObjects\(\)') 'LLL moon-scene entrance enumeration'
   Expect ((IL $loader 'PatchFireEscapes') -match '::entranceId') 'LLL assigns exterior IDs during generation'
  }
 }finally{$a.Dispose()}
}
$dawn=Join-Path $ProfileDirectory 'BepInEx\plugins\TeamXiaolan-DawnLib\DawnLib\com.github.teamxiaolan.dawnlib.dll'
if(Test-Path -LiteralPath $dawn){
 $a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($dawn)
 try {
  $handler=$a.MainModule.GetType('Dawn.DungeonRegistrationHandler')
  Expect ($null -ne $handler) 'DawnLib dungeon registration handler'
  $body=IL $handler 'AdjustFireExits'
  Expect (([regex]::Matches($body,'call .*UnityEngine.Object::FindObjectsByType<EntranceTeleport>\(')).Count -eq 1) 'DawnLib fire-exit scan layout'
  Expect ($body -match 'GlobalPropSettings::Count' -and $body -match '::entranceId') 'DawnLib count and exterior ID assignment'
  Write-Host 'DawnLib fire-exit generation layout verified.'
 }finally{$a.Dispose()}
}
Write-Host 'Installed-reference checks passed, including the v81 CLIENT exit-ID call chain.'
