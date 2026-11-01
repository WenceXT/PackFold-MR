# Validation — v0.5

## Build

The v0.5 APK was built with Unity 6000.6.7f1 for Android ARM64/IL2CPP. Its manifest reports `com.robotlab.packfoldmr`, version name `0.5`, version code `9`. The APK ZIP integrity and the presence of Meta AR Foundation and OpenXR native libraries were checked successfully.

v0.5 renames the preceding v0.4.4 fix build without changing interaction behaviour. The 35 editor checks were run during the v0.5 build; the 15 UI routing checks were run against the same interaction code before the version rename.

## Published reports

| Report | Result | Meaning |
| --- | --- | --- |
| [Editor checks](../Verification/mr_editor_checks.json) | 35 passed | Scene/XR configuration, placement, stereo material, model alignment, UI layers, structures, history and exports |
| [UI ray checks](../Verification/ui_ray_checks.json) | 15 passed | Actual XR ray → XRUIInputModule → UGUI routing with synthetic poses and trigger states |
| [Before-fix regression](../Verification/ui_ray_before_fix.json) | Expected failure | The original zero raycast mask prevents the HINGED button hit |

The ray-routing checks cover both controllers, button activation, page changes, slider pointer-down, and continued slider dragging. They do not invoke button callbacks directly. They do not emulate the entire OpenXR hardware input stack or prove real-controller usability.

## Hardware status

Earlier releases were installed and run on Quest 3. Wearer feedback confirmed corrected viewing height, binocular ray rendering, and official Touch Plus model alignment. **v0.5 has not yet been installed or accepted by a wearer.** No v0.5 real-device demonstration video or formal long-duration performance benchmark is included.

## Reproduce the UI routing checks

Run from a terminal in the repository root, with the project closed in other Unity instances:

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe'
& $unityExe -batchmode -buildTarget Android `
  -projectPath (Get-Location).Path `
  -executeMethod MRUIRayQA.Run `
  -logFile "$PWD\ui-ray-checks.log"
```

Do not add `-quit`: the harness enters Play Mode and exits after writing `Verification/ui_ray_checks.json`. The harness controls poses and trigger-reader values, switches box presets, and restores a standard box at the end. Run it in a test workspace, not while editing an unsaved design.

`MRPlayModeQA.Run` is a separate integration harness. `MRDeviceSelfTest` is opt-in via the `packfold_qa=true` launch-intent extra, or the install script's `-SelfTest` switch; it uses synthetic manipulation frames and is separate from wearer acceptance. Normal installation does not enable it.

## Pending headset acceptance

- Confirm left/right ray hover and trigger clicks on both panel pages.
- Drag dimensions and fold sliders, release, and verify undo/redo.
- Grab/release the body, use two-controller scaling, and lift the lid.
- Recenter while seated and standing; verify height and stereo comfort.
- Export a design, pull it to a PC, and inspect the SVG/JSON output.
- Record the genuine headset demonstration in [the reserved section](demo/README.md).
