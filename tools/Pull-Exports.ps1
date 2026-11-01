param([string]$Serial='')
$ErrorActionPreference='Stop'
$adb=Join-Path $PSScriptRoot 'tools\adb.exe'
$deviceArgs=@();if($Serial){$deviceArgs=@('-s',$Serial)}
$destination=Join-Path $PSScriptRoot ('Quest_Exports_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
& $adb @deviceArgs pull '/sdcard/Android/data/com.robotlab.packfoldmr/files/PackFoldExports' $destination
if($LASTEXITCODE -ne 0){throw 'No exports pulled. Press EXPORT in the headset first, and check USB authorization.'}
Write-Host ('Exported files copied to '+$destination)
