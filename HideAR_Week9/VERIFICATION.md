# Verification record

Updated: 2026-10-16 (comments/documentation only). Unity: 6000.3.21f1. The APK and latest Unity gameplay test run remain from 2026-10-05.

Earlier classroom APKs were installed on Quest 3S and the user reported basic functionality working. After the preview/color build, the user reported the room setup entry was unavailable and asked to remove the headset removal requirement. That room/start correction was installed on Quest 3S on 2026-10-05 with ADB returning Success. Its checked launch stopped at Quest's controller-required system dialog. The user subsequently reported fixed/odd task-card positions and selected manual positioning by holding A. This newer menu update was initially built without a connected device; after the user requested installation, Quest 3S was connected and authorized, and adb install -r returned Success on 2026-10-05. The new am start -W result reported Status: ok and the classroom UnityPlayerGameActivity; pidof then returned PID 7165. This confirms installation and process startup, not successful gameplay or visual correctness. Actual new-room capture, the revised flow and menu comfort still require hardware checks.

## Comments and documentation update (2026-10-16)

- Added concise Chinese/English responsibility and rationale comments to all five runtime scripts, both editor tools, the editor test file and both Mac launch/install scripts. No code statements, constants, bindings or button labels were changed.
- Added `CODE_GUIDE_ZH.md` with the file map, every project code file's role, reading priorities, SDK/project responsibility boundaries and defense questions. README and START_HERE_ZH link to it and use the same reading order.
- Compared all eight C# files against before-edit copies after removing full-line comments: all remaining content is identical. Did the equivalent comparison for both shell scripts; both also passed `zsh -n` syntax checks.
- SHA-256 hashes of all 74 files under the scene, prefab, HideAR settings, ProjectSettings and Packages paths are unchanged. The existing menu-position APK hash is unchanged. Prototype1 Git status entries are unchanged; no old project files were edited.
- No Unity setup/Apply command, build, install or fresh Unity test run was performed for this comment-only update. The 27 passing EditMode tests below are the historical 2026-10-05 run, not new 2026-10-16 results. No new hardware-validation claims are made.
- The classroom scope still excludes environment-depth occlusion and headset-removal handoff detection. Comments explain existing behavior rather than adding either feature.

## Functional verification (2026-10-05)

- Unity compiled the updated runtime and editor scripts using the existing classroom scene. The project/scene was not rebuilt, and no models or room logic were changed for menu positioning.
- EditMode: 27 tests passed, zero failures (2026-10-05 local time). Rules cover input release protection, single placement, review, repositioning, explicit Start search without any headset removal requirement, countdown/timer pauses, replay and rejected late anchor results. Scene tests check references, managers, independent room setup and model-choice controls, small model bounds, pet textures, collider-free previews, per-instance color changes and build scene. Five new menu checks cover near-vertical heading stability, first placement while looking down, seated/standing eye height, deferred latest-pose positioning and pinned placement across all stages while tracking is unavailable. The stage test also checks the placed object remains unmoved. These are editor checks, not real headset tracking recovery tests.
- Nine game screens, two menu-position views and 20 model/color combinations rendered in the editor. Text-height, whole-menu viewport-boundary and nonblank-pixel checks passed. The pinned-menu view and readiness screen were visually inspected. Start search remains available without removal/rewearing; merely waiting does not start countdown. Selection previews remain hidden during seeking.
- The menu is positioned on first valid tracking, then only on A input. Hold A to reposition with head turning/movement, release to pin it. Stage changes and tracking recovery no longer request recentering. Near-vertical views preserve the previous horizontal heading instead of normalizing an almost-zero vector. Placement/finding is disarmed while A is held; its hardware button behavior remains unverified.
- Set up room is now an independent, persistently wired button in Welcome and Placing. It is not replaced when room permission or plane data already exists. Change object only cycles model choices. On app resume, plane/raycast managers restart after one frame to query current room data; actual new-room results are not asserted by editor tests.
- Unity BuildPipeline completed the Android development APK successfully. OpenXR validation had no blocking errors; optional input-layout and latency recommendations remain.
- Android tools confirmed the app label, separate package identifier, ARM64 architecture, spatial permission, anchor permission and passthrough feature.
- APK Signature Scheme v2 verification passed.
- Prototype1 was not edited during this update. Its Git status entries remained the same as at the start of this request. No old APK was built or installed.
- Git ignores the new project's Library, Logs and local UserSettings, and the APK output. Required CompositionLayers settings under Assets are not ignored.
- Both Finder launch/install scripts passed zsh syntax checks and have executable permissions. They could not be tested against a connected headset during this update.
- Original Cube Pets models and palette texture are preserved. Four generated palette assets retain grayscale face details. Objects use ordinary URP Simple Lit materials and one shadow-free directional light.

## Artifacts

- APK: `../Builds/Week9/HideAR_Classroom.apk`
- Package: `com.lingjieruan.hidear.classroom`
- Latest menu-position APK SHA-256: `15dc9959f3f6231a0c33e0eb5e88c056dc445d970d62367b7790aa71c8736150`
- Latest APK size: 74,979,873 bytes (about 71.5 MiB).
- Previously installed room/start APK SHA-256: `382b38ab52e638fb7a8b533eefd14802c47321bf2797b1cde7390a840ba57e2e`
- Old APK SHA-256: `8e4518b49eab2243ea917b6efb41854fdcae5bf1e12460e541eacd5090ae1454`
- Latest test XML: `../Builds/Week9/Review/EditMode-menu-position-fix.xml`
- Previous room/start tests: `../Builds/Week9/Review/EditMode-room-start-fix.xml` (22 passed).
- Historical initial tests: `../Builds/Week9/Review/EditMode-tests.xml` (16 passed on 2026-10-03).
- Editor images: `../Builds/Week9/Review/01-welcome.png` through `06-found.png`, `01b-room-authorized.png`, `04b-seeker-ready.png`, `04c-countdown.png`, `07-menu-seated.png`, `08-menu-pinned.png` and `choice-*.png`.
- Latest test log: `../Builds/Week9/Review/Unity-menu-position-fix-tests.log`.
- Latest preview log: `../Builds/Week9/Review/Unity-menu-position-fix-preview.log`.
- Latest build log: `../Builds/Week9/Review/Unity-menu-position-fix-build.log` (successful Android build, 2026-10-05).

## Still requires Quest 3S / Quest 3 testing

1. Passthrough, right-controller aim and clickable UI appear.
2. Set up room remains available with existing room data. In a different physical room, complete the system Space Setup and verify the app returns usable horizontal surfaces for that room, rather than old surfaces. Also test denied permission, cancelled capture and manual Space Setup from Quest Settings.
3. Change object shows each selected model; color swatches match the menu, surface preview and placed object.
4. Surface preview shows actual model size before confirmation and never blocks raycasts. A UI press does not also place an object.
5. One object remains near its real-world spot when the player moves and looks away. Review color changes, reposition and confirmation do not duplicate it.
6. Confirm hiding hides the object and selection information and waits for Start search. One person can continue without removing the headset.
7. A fresh release plus explicit Start search click starts the three-second countdown. Merely waiting, putting on the headset or tracking recovery never starts it automatically.
8. Losing tracking or pausing the app suspends countdown/search timing; recovery continues the existing stage instead of forcing another readiness confirmation.
9. During seeking no planes, marker or selection previews reveal the hiding spot. Triggering the small object's collider produces success and stops the timer.
10. Passing the headset preserves the anchor after tracking resumes; reset/replay clear the previous round.
11. Unknown Sources launches the installed app without USB. Finder launch/install scripts work when one authorized Quest is connected.
12. Short-press A summons the menu; holding A while turning/moving repositions it, releasing pins it. Test looking down/up, seated/standing positions, stage changes, a pause/room setup return and a headset handoff. The card should stay upright and remain where chosen until A is used again. Neither the anchor nor the hidden object should move with it, and repositioning should not cause accidental placement/finding. Real tracking can still shift world-space objects; the software checks above cannot rule that out.

The headset runtime, permissions, input alignment and physical tracking cannot be validated by the editor tests or screenshots above.
