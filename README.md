# PackFold MR

**A mixed reality paper-packaging design workbench for Meta Quest 3, built with Unity and C#.**

Design a box at real-world scale, inspect it in passthrough, adjust its dimensions, fold it into an assembled package, and export its dielines. PackFold MR combines parametric packaging geometry with controller interaction and a spatial editing panel.

**Current release:** [v0.5](https://github.com/WenceXT/PackFold-MR/releases/tag/v0.5) · **Platform:** standalone Quest 3 · **Editor:** Unity 6000.6.7f1

## Real-device demonstration

**[Watch / download the Quest 3 demo — v0.5](https://github.com/WenceXT/PackFold-MR/releases/download/v0.5/demo.mp4)**

Recorded on Meta Quest 3. Uploaded on November 2, 2026. The MP4 is hosted with the v0.5 release; open the link to play or download it.

## What it does

| Capability | Implementation |
| --- | --- |
| Packaging structures | Lift-off lid box, hinged lid box, and open tray |
| Parametric dimensions | Width 60–300 mm, depth 50–240 mm, height 25–150 mm |
| Spatial manipulation | One-controller move/rotate; two-controller scale/rotate |
| Folding and opening | Global assembly fold/unfold, plus independently controlled lift-off lid opening |
| Lid fit | Adjustable skirt depth and side clearance, with a nominal 1.2 mm board model |
| Design panel | World-space controls for structures, dimensions, folding, fit, undo/redo, and export |
| Visual details | Cardboard textures, printed sample branding, optional foam insert and sensor props |
| Persistence | Automatic design saving and a history of up to 80 revisions |
| Output | SVG dielines, design JSON, and MR session metadata |
| Offline assistance | Explicitly labelled rule-based templates for sample product envelopes |

Preview scaling changes how large the package looks in the workspace; exported dimensions remain in millimetres. The Quest application runs independently after installation and does not require a connected PC for normal use.

This is an independent portfolio prototype aligned with the MR interaction direction of [PolyU–NVIDIA's Project 3: AI-Assisted MR Packaging Design Developer](https://www.polyu.edu.hk/nvidiarc/grants-and-opportunities/2026-stem-winter-internship/). It is not the laboratory's existing system or an official PolyU/NVIDIA product.

## Install on Quest 3

1. Enable Developer Mode on the Quest, connect it to a Windows PC over USB, and allow USB debugging inside the headset.
2. Download **`PackFold_MR_Quest3_v0.5.zip`** from [Releases](https://github.com/WenceXT/PackFold-MR/releases/tag/v0.5).
3. Extract the entire ZIP and run **`Install_Quest.cmd`**. Keep its APK, scripts, and `tools` directory together. The installer verifies the APK checksum, installs the application, and launches it.
4. Put on the headset, wake both Touch Plus controllers, and complete any Quest boundary prompts. The application is named **PackFold MR**; it can also be found under the headset's Unknown Sources applications.

Windows PowerShell alternative:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Quest.ps1
```

The release also includes a standalone APK for users who already have ADB. For manual installation, troubleshooting, and export retrieval, see [Deployment](docs/DEPLOYMENT.md).

## Controls

| Input | Action |
| --- | --- |
| Either controller ray + index trigger | Point at a panel button, press and release to click |
| Hold index trigger on a slider | Move the ray along the slider, then release |
| Grip a box with one controller | Move and rotate the package |
| Grip the body with both controllers | Scale and rotate the package; preview scale is limited to 0.5–3× |
| Right grip on a lift-off lid | Raise/lower the lid along its constrained opening path |
| Right A | Open/close the cover |
| Right B | Fold/unfold the package assembly |
| Left X | Recenter the workspace in front of the current head pose |
| Left Y | Undo the previous design revision |

**The index trigger operates the panel; the side grip operates the package.** Both rays can use the panel. Point at a button until it highlights before pressing the trigger.

Default placement is approximately 0.80 m ahead for the panel and 0.65 m ahead for the package, relative to a valid, stable head pose. The workspace stays in place as the user looks around.

## Open and build the Unity project

```bash
git clone https://github.com/WenceXT/PackFold-MR.git
```

1. Install **Unity 6000.6.7f1** with Android Build Support. Configure the SDK, NDK, and JDK as described in [Deployment](docs/DEPLOYMENT.md).
2. Add the repository root to Unity Hub as a project and allow Unity Package Manager to restore the pinned dependencies.
3. Open **`Assets/Scenes/PackFold_MR.unity`**.
4. Select **Tools → PackFold MR → Build APK**. The builder configures the Quest scene, runs editor checks, and writes **`Build/PackFold_MR_Quest3_v0.5.apk`**.

Build target: Android ARM64, IL2CPP, Vulkan, linear colour, 4× MSAA, and single-pass instanced stereo. Application ID: `com.robotlab.packfoldmr`; version name: `0.5`; version code: `9`.

## Validation status

- **35 editor checks passed** for scene configuration, placement, stereo ray material, official controller alignment, UI layers, structures, history, and exports.
- **15 XR UI routing checks passed** for left/right ray hits, trigger state, button activation, page switching, and slider dragging. These checks pass through the XR ray, `XRUIInputModule`, and UGUI events using synthetic poses and trigger states.
- The earlier zero-raycast-mask configuration fails the same button-hit regression check; v0.5 includes the fix.
- The Android v0.5 build and APK integrity checks passed. **v0.5 headset installation and wearer acceptance remain pending.** Earlier builds were run on Quest 3, with user confirmation of panel height, stereo rays, and Touch Plus model alignment.

See [Validation](docs/VALIDATION.md) for evidence and repeatable commands. Automated input checks are not a substitute for a real-controller usability session.

## Source layout

```text
Assets/Scripts/               Parametric geometry, MR interaction, UI, session and diagnostics
Assets/Editor/                Quest scene builder and editor/Play Mode checks
Assets/Shaders/               Stereo-aware XR pointer shader
Assets/ThirdParty/            Original Meta Touch Plus assets and notices
Packages/                    Unity dependency manifest and lock file
ProjectSettings/             Unity project configuration
Python_Advisor/              Optional desktop Python recommendation API
tools/                       Windows install and export scripts; supply ADB yourself
Verification/                Published editor and UI regression reports
docs/                        Deployment, validation and demonstration placeholder
```

The source repository excludes Unity caches, generated builds, machine-specific settings, and local credentials. The portable release kit includes ADB so that a Unity installation is not required to install the prebuilt app.

## Scope and next steps

Folding currently uses a shared assembly progression. Directly grabbing and folding each individual paper panel is not implemented. Input uses Touch Plus controllers; bare-hand tracking, room-mesh occlusion, and real-robot control are outside the current implementation.

The headset's structure assistant uses deterministic templates, not a generative model. An optional desktop Python adapter supports a user-configured provider API, but is not connected to the standalone Quest runtime. See [Python advisor](Python_Advisor/README.md).

Dielines and clearance values are design previews. Material strength, crease behaviour, glue joints, and manufacturing tolerances have not been validated through physical prototyping.

## Assets and attribution

Cardboard maps are from ambientCG under CC0. Touch Plus models, textures, and animation controllers originate from Meta XR Core SDK 207.0.1 and retain their original licence and notices. Unity and its packages retain their respective terms. See [Third-party notices](THIRD_PARTY_NOTICES.md); third-party assets are not relicensed by this repository.
