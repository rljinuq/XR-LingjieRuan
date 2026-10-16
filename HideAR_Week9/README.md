# HideAR Classroom

An independent, controller-only Quest 3 / 3S version of HideAR IP2, organised around the course's Week 09 mixed-reality activity.

This is a separate Unity project. `../Prototype1` and its APK are the older backup version; this project does not depend on either one. No AR Foundation Samples scenes or sample scripts are included.

## Open

- Unity: **6000.3.21f1**, with Android Build Support, SDK, NDK and OpenJDK.
- Scene: `Assets/HideAR/Scenes/HideAR_Week9.unity`.
- App name: **HideAR Classroom**.
- Android identifier: `com.lingjieruan.hidear.classroom` (different from the backup app).
- Controls: **right controller trigger** for buttons, placement and finding; **Change object** cycles Dinosaur / Dog / Cat / Bunny before placement; color swatches select Original / Moss / Blue / Gray / White; hold **A** to reposition only the menu, release to pin it.

## Play

Allow room access and use the independent **Set up room** button to complete Quest Space Setup, including the table/floor. This button stays available at welcome and during placement even if room data already exists, so a different room can be set up. Choose an object and color using the model preview. Start hiding and aim at a surface to see an actual-size preview before placing. Review, change color or move the object, then confirm. On the readiness screen, the seeker explicitly clicks **Start search**; only then does the three-second countdown begin. No headset removal is required, so one person can test the whole flow. During seeking, planes and selection previews are hidden. Point at the placed object and press the trigger. The success screen shows search time. Reset reloads this game's scene.

Launch the installed app from the headset's **Unknown Sources** library, or connect USB and double-click `Tools/Launch_Quest.command` in Finder. The launcher opens only the installed classroom app; it does not install a new build.

The menu appears in front of the player on first tracking. Afterwards it stays in world space, including stage changes and tracking recovery. Tap A to bring it back, or hold A while turning/moving to choose its location. It stays upright near eye height; looking almost straight down/up preserves its last horizontal heading. Release A before clicking. Repositioning the menu does not move the hidden object or its anchor.

## Read the code in this order

For a complete file map, reading priorities and defense questions, see [文件结构与答辩速查 / Code guide](CODE_GUIDE_ZH.md). Core scripts, editor/test tools and Mac launchers now have concise Chinese/English comments. These explain existing behavior; they add no new gameplay systems.

| File in `Assets/HideAR/Scripts` | Responsibility |
| --- | --- |
| `RoundState.cs` | Game stages, explicit start confirmation, trigger release, countdown and search timer |
| `GameFlow.cs` | Connect ordinary button methods to game rules and tracking state |
| `RoomAccess.cs` | Week 09 permission flow and the system's room setup entry point |
| `PlaceOnPlane.cs` | Controller ray, one anchor, selected model, surface preview, per-instance colors and finding |
| `GameUI.cs` | Change labels/buttons and reposition the world-space menu only on request |

The Inspector contains ordinary references and persistent Button events. There is no ScriptableObject event bus, hand gesture detection, networking, database, GPS, persistent anchor storage or custom tracking algorithm.

The `Editor` folder creates the initial scene/settings and builds the APK. It is not part of the game's runtime logic. The saved scene and prefab layout can be changed directly in Unity; setup does not overwrite an existing scene. `Tests` contains automated checks, not gameplay code.

## Build

The only enabled build scene is the classroom scene. Switch to Android in File > Build Profiles and use **HideAR > Classroom > Build APK**. The output is `../Builds/Week9/HideAR_Classroom.apk`. No Xcode is involved. The application identifier allows this APK and the older Quest APK to coexist.

## Verification

- Unity EditMode tests: 27 passed, 0 failed (2026-10-05), including independent room setup, explicit start without headset removal, model/color preview checks and stable manual menu positioning.
- Android development APK built successfully in Unity; ARM64, package identifier and APK v2 signature verified. Output: `../Builds/Week9/HideAR_Classroom.apk` (about 72 MB).
- Nine game screens, two menu-position views and 20 model/color combinations passed editor text-height, viewport-boundary and nonblank-image checks. The pinned menu and Start search screen were also visually inspected.
- Test XML and UI previews are saved separately in `../Builds/Week9/Review`.
- On 2026-10-05 the menu-position APK was installed on Quest 3S with ADB returning Success. The launch check reported the classroom Unity activity, and pidof confirmed a running process. This confirms installation and process startup, not successful gameplay. Room setup, the revised flow and menu comfort still need hardware validation. See VERIFICATION.md for current checks.

## Boundaries

- Space Setup is required. These planes are a query of saved room geometry, not continuous iPhone-style scanning.
- Only upward horizontal surfaces are accepted. The right controller is the only game input in this version.
- One running headset is passed between players. There is no cross-headset sharing or persistence after closing the app.
- Passthrough stays on during handoff for safety; the chosen object is hidden until seeking begins.
- Readiness is confirmed with the Start search button. No removal/rewearing cycle or person detection is required. Tracking loss or application pause suspends countdown/search timing.
- Depth occlusion is not implemented: real furniture may not hide the virtual object.
- Automated tests cannot establish real-device tracking stability, input alignment or permission behaviour. Headset checks remain necessary.

## Sources and attribution

- Course reference: [Week 09 Activity 1](https://github.com/DECO2300-course/XR-activities-2026/blob/main/Week%2009/Activity%201%20-%20Augmenting%20Reality.md), `PlaceOnPlane.cs` and `ScenePermission.cs`. The controller-ray/plane-hit and permission-enable patterns are adapted from these lessons. This is not an unchanged course sample.
- Unity AR Foundation and Unity OpenXR: Meta provide plane data, passthrough, tracking and `ARAnchorManager.TryAddAnchorAsync`. Quest does not support plane-attached anchors; this project calls the official pose-based API. No official package code is modified.
- Room setup uses the package's `MetaOpenXRSessionSubsystem.TryRequestSceneCapture`; returning from the system flow restarts the plane and raycast managers. See [Unity's scene capture life cycle](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.4/manual/features/session.html).
- XRI provides controller-ray UI interaction. Input System provides tracked poses and actions. UGUI provides ordinary buttons and text.
- Dinosaur: [Low Poly Dino by OpenGameArt contributor](https://opengameart.org/content/low-poly-dino), CC0. Original supplied license is in `Assets/Art/Dinosaur/LICENSE.txt`.
- Dog, cat and bunny choices: [Kenney Cube Pets](https://opengameart.org/content/cube-pets), CC0. Source details are recorded in `Assets/Art/CandidateModels/MODEL_SOURCES.md`.
- Project-specific C# and scene assembly were developed with Codex assistance. Course and Unity functionality should not be presented as algorithms authored by the student.

Package versions are pinned to the installed Unity-compatible stack; they are not automatically upgraded to every version mentioned by the course. `Packages/packages-lock.json` should be committed along with the manifest.

## Source Repository Footprint

The repository includes the current four model choices and their required source meshes, textures, materials, licenses and Unity metadata. Unused candidate packs, extra model formats and previews remain local and are ignored rather than deleted. Their original sources and download hashes remain recorded in `Assets/Art/CandidateModels/MODEL_SOURCES.md`. The publishing allowlist must be updated if a future choice requires an additional candidate asset.
