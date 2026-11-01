# Deployment

## Option A: install the prebuilt release

**Requirements:** Meta Quest 3, both Touch Plus controllers, Developer Mode, a USB data cable, and a Windows PC. Unity is not required for the prebuilt application.

Download `PackFold_MR_Quest3_v0.5.zip` from the [v0.5 release](https://github.com/WenceXT/PackFold-MR/releases/tag/v0.5), extract it, connect the headset, and accept the USB debugging prompt. Run `Install_Quest.cmd`. The script verifies `apk.sha256`, requires one authorized device unless `-Serial` is specified, installs with `adb install -r`, and launches PackFold MR.

```powershell
# Run inside the extracted installation kit.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Quest.ps1

# If more than one Android device is attached:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Quest.ps1 -Serial YOUR_DEVICE_SERIAL
```

Existing app data is retained when updating an installation signed with the same key. A signature mismatch requires a matching signed build, or uninstalling the old application; uninstalling removes its local app data, so retrieve exports first.

### Manual ADB installation

The release contains `tools/adb.exe`. With an existing ADB installation, use `adb` instead of `tools/adb.exe`.

```powershell
.\tools\adb.exe devices
.\tools\adb.exe install -r .\PackFold_MR_Quest3_v0.5.apk
.\tools\adb.exe shell am start -n com.robotlab.packfoldmr/com.unity3d.player.UnityPlayerGameActivity
```

After installation, PackFold MR also appears in Unknown Sources in the headset's app library. Complete any device boundary or tracking prompts and wake the controllers.

### First interaction

Wait for the workspace to appear at viewing height. Aim a controller ray at **HINGED**, wait for its highlight, then press and release the **index trigger**. The box structure should change. Aim at **WIDTH**, hold the index trigger, move the ray left/right, and release. Use the **side grip** for the box, not for panel buttons. **Left X** recenters the workspace.

### Retrieve exported designs

Press **EXPORT** in the headset, reconnect USB, and run `Pull_Exports.cmd` in the release kit. It creates a timestamped `Quest_Exports_*` folder on the PC.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Pull-Exports.ps1
```

Device export directory:

```text
/sdcard/Android/data/com.robotlab.packfoldmr/files/PackFoldExports/
```

A lift-off structure exports `design.json`, `base_dieline.svg`, `lid_dieline.svg`, `dieline.svg`, and `mr_session.json`. Other structures export a single dieline and design/session metadata. The last design is also saved automatically in the application's persistent data directory.

## Option B: build from source

### Environment

| Component | Version used for v0.5 |
| --- | --- |
| Unity Editor | 6000.6.7f1 |
| Android architecture / backend | ARM64 / IL2CPP |
| JDK | OpenJDK 17 |
| Android NDK | r27c |
| Android SDK | Platform API 34; Build Tools 36 |
| Minimum / target Android API | 32 / 34 |
| Graphics | Vulkan, linear colour, 4× MSAA, single-pass instanced |

Install Unity Android Build Support, then set Android tools under **Edit → Preferences → External Tools**. The builder can alternatively use `PACKFOLD_ANDROID_TOOLS`, pointing to a folder with this layout:

```text
AndroidTools/
  SDK/
  JDK/
  NDK/android-ndk-r27c/
```

Clone the repository and open its root folder in Unity Hub. Let Package Manager restore dependencies using `Packages/manifest.json` and `packages-lock.json`. Open `Assets/Scenes/PackFold_MR.unity`, then choose **Tools → PackFold MR → Build APK**.

The builder recreates the scene and writes `Build/PackFold_MR_Quest3_v0.5.apk`. Changes made only to the generated scene may be overwritten; keep persistent configuration changes in `Assets/Editor/PackFoldMRBuilder.cs`.

### Windows command-line build

```powershell
# Set these paths for your installation.
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe'
$projectPath = (Get-Location).Path
$env:PACKFOLD_ANDROID_TOOLS = 'D:\AndroidTools'

& $unityExe -batchmode -quit -buildTarget Android `
  -projectPath $projectPath `
  -executeMethod PackFoldMRBuilder.BuildAndroid `
  -logFile "$projectPath\build.log"
```

Omit `PACKFOLD_ANDROID_TOOLS` when using valid paths already configured in Unity. Android SDK licences must be accepted for your toolchain.

### Source-repository helper scripts

The scripts under `tools/` are the same helpers used by the release kit, but this repository does not vendor ADB binaries or an APK. To use them, put your ADB platform-tools files in `tools/tools/` and the built APK plus its `apk.sha256` beside `tools/Install-Quest.ps1`. The prebuilt release kit already supplies this layout.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `unauthorized` in `adb devices` | Wear the headset and allow USB debugging; reconnect the cable if needed |
| No device listed | Use a data-capable USB cable, enable Developer Mode, and check the PC's device driver |
| No workspace visible | Wake the headset/controllers and complete Quest tracking or boundary prompts |
| Workspace is out of reach | Press Left X to recenter it from the current head pose |
| Panel seems unresponsive | Use the index trigger; check for a button highlight; install v0.5, which fixes UI ray filtering |
| Export pull fails | Press EXPORT first and ensure the same application is installed and USB-authorized |
| Missing Android tools | Check Unity's External Tools settings or the `PACKFOLD_ANDROID_TOOLS` layout |

For diagnostics, inspect `adb logcat` for `PACKFOLD_MR_READY`, `PACKFOLD_MR_TRACKING`, `PACKFOLD_MR_UI`, and `PACKFOLD_MR_EXPORT`. The application also writes a device validation report in its persistent data directory.
