param([string]$Serial='', [switch]$SelfTest)
$ErrorActionPreference='Stop'
$adb=Join-Path $PSScriptRoot 'tools\adb.exe'
$apk=Join-Path $PSScriptRoot 'PackFold_MR_Quest3_v0.5.apk'
if(!(Test-Path -LiteralPath $adb) -or !(Test-Path -LiteralPath $apk)){throw 'APK or bundled ADB is missing. Extract the full ZIP first.'}
$deviceArgs=@()
if($Serial){$deviceArgs=@('-s',$Serial)}else{
    $connected=@(& $adb devices | Where-Object {$_ -match '^\S+\s+device$'})
    if($connected.Count -ne 1){throw 'Connect exactly one authorized Quest over USB, or run with -Serial DEVICE_ID.'}
    $deviceArgs=@('-s',(($connected[0] -split '\s+')[0]))
}
$hashFile=Join-Path $PSScriptRoot 'apk.sha256'
if(Test-Path -LiteralPath $hashFile){$expected=(Get-Content -LiteralPath $hashFile -Raw).Trim().Split(' ')[0];if((Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash -ne $expected){throw 'APK checksum mismatch.'}}
& $adb @deviceArgs install -r $apk
if($LASTEXITCODE -ne 0){throw 'ADB install failed. Check the headset USB debugging authorization.'}
$activity=@(& $adb @deviceArgs shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER com.robotlab.packfoldmr | Where-Object {$_ -match '^com\.robotlab\.packfoldmr/'})[-1]
if(!$activity){throw 'Installed app launcher activity not found.'}
if($SelfTest){& $adb @deviceArgs shell am force-stop com.robotlab.packfoldmr;& $adb @deviceArgs shell am start -n $activity --ez packfold_qa true}else{& $adb @deviceArgs shell am start -n $activity}
if($LASTEXITCODE -ne 0){throw 'App launch failed.'}
Write-Host 'Installed and launched PackFold MR. Wear Quest 3 and use the Touch controllers.'
