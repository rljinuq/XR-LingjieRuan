# Verification record

Updated: 2026-10-19 (readiness-only following). Unity: 6000.3.21f1. Latest EditMode run: 36 passed. The readiness-follow APK is built, signature-verified and installed on Quest 3S with matching APK hash; Unity activity and process startup are confirmed. Hardware following comfort and full gameplay remain unverified.

Earlier classroom APKs were installed on Quest 3S and the user reported basic functionality working. After the preview/color build, the user reported the room setup entry was unavailable and asked to remove the headset removal requirement. That room/start correction was installed on Quest 3S on 2026-10-05 with ADB returning Success. Its checked launch stopped at Quest's controller-required system dialog. The user subsequently reported fixed/odd task-card positions and selected manual positioning by holding A. This newer menu update was initially built without a connected device; after the user requested installation, Quest 3S was connected and authorized, and adb install -r returned Success on 2026-10-05. The new am start -W result reported Status: ok and the classroom UnityPlayerGameActivity; pidof then returned PID 7165. This confirms installation and process startup, not successful gameplay or visual correctness. Actual new-room capture, the revised flow and menu comfort still require hardware checks.

## Readiness-only following (2026-10-19)

- Implemented only the selected Ready to search follow-up. GameFlow reuses the existing RequestRecenter/LateUpdate positioning while Stage.Handoff is active and tracking/setup allow it. Start search changes to Countdown and stops automatic following. Other stages retain their existing one-shot recall/manual-A behavior. The menu remains upright, 1.5 m ahead and 0.1 m below eye height; near-vertical heading protection is unchanged.
- Continuous readiness following does not repeatedly call Round.Disarm, so a fresh trigger release can still unlock Start search. Initial recalls and manual A adjustment retain their accidental-input protection. Waiting, walking, headset handoff or tracking recovery never starts the countdown automatically.
- GameUI uses a compact 780x420 ready card, with Start search and Reset round inside it and matching collider dimensions. Leaving readiness restores the existing 900x560 layout. No new canvas, runtime script, follow engine, wall placement, start-point detection, occlusion or removal/rewearing requirement was added. Models, colors, anchor placement and room access were not changed by this update.
- EditMode: 36 passed, zero failures. New tests cover moving/turning during readiness, remaining ready while waiting, usable Start search during following, following stopping at Countdown/Seeking, tracking-loss deferral/recovery and compact layout restoration. The existing stage/world-pose test now expects continuous follow only in Handoff and still checks the placed object is unmoved. These are simulated editor checks, not physical tracking or comfort results.
- 62 editor views passed text-height, viewport and nonblank checks, including two additional camera poses during readiness; Start search remained interactable. Visually inspected the compact ready card and restored countdown layout. New output is in ReadyFollow, preserving older preview/test files.
- Incremental Android build succeeded, Unity exit code 0. The build logged 2.9 GiB free, a 2 GiB guard and prior Android outputs present. No cache cleanup or project reconstruction was performed. Available space after this build was approximately 1.9 GiB.
- Latest APK: 75,357,717 bytes, SHA-256 c9a6b3205cbb0776ee1b71997e0bdcf123439d1aad3caece461047708d53b951. AAPT confirmed HideAR Classroom, com.lingjieruan.hidear.classroom, UnityPlayerGameActivity and ARM64; apksigner verified APK v2. The prior menu-return APK was preserved as History/HideAR_Classroom-2026-10-19-menu-return-fix.apk with its matching 35899b9a... hash.
- Quest 3S installation via adb install -r returned Success. The installed base APK hash exactly matches c9a6b3205cbb0776ee1b71997e0bdcf123439d1aad3caece461047708d53b951. Launch returned Status: ok, LaunchState: COLD and the classroom UnityPlayerGameActivity (TotalTime 379 ms, WaitTime 419 ms); pidof returned 11391. Installation and process startup are verified, not the full physical handoff/follow interaction.
- Prototype1, Week3, model sources, historical test records and old APKs were not modified or deleted. Horizontal-only placement remains unchanged. Test the actual ready card while moving away from the hiding spot to an agreed starting position, then confirm that Start search is clickable and stops following.

## Menu recall and dinosaur preview follow-up (2026-10-19)

- User reported that the fixed-world menu stayed behind after room setup and hiding, making the next interaction inconvenient, and requested that the dinosaur preview be restored. This supersedes the earlier always-pinned stage policy and dinosaur-free menu requirement; earlier records below describe those historical versions.
- GameFlow now queues a one-shot menu recall when entering Welcome, Placing, Review, Handoff, Found or Error. RoomAccess increments RefreshCount after the resume/refresh flow finishes; GameFlow observes that completion even if tracking was unavailable at that moment. Recall waits for valid tracking and for system setup to end, then is applied in GameUI.LateUpdate. Thereafter the card stays pinned. Countdown and Seeking transitions alone do not summon the menu; A-based manual positioning remains available.
- Both automatic and manual menu requests disarm accidental placement/finding. Neither request moves the placed object or anchor. There is still no headset-removal requirement, no automatic Start search and no environment-depth occlusion.
- Restored the selected dinosaur's menu preview using the same preview path as Dog, Cat and Bunny, including the existing twelve colors. Exactly one selection preview is shown; the removed decorative mascot was not reintroduced. Object prefab dimensions and actual-size surface previews are unchanged. No new runtime script, scene reconstruction or new gameplay system was added.
- EditMode: 33 passed, zero failures. Tests cover interactive-stage recall once tracking is valid, unchanged placed-object world pose, no continuous head following, room-return deferral during setup/tracking loss, refresh completion signaling, held-A positioning, all four menu previews and existing round/color rules. Synthetic editor tracking and anchors are not hardware stability evidence.
- Rendered 60 editor views: nine screens, three menu-position/room-return views and 48 model/color combinations. Text-height, whole-card viewport, nonblank output and single-model selection checks passed. Dinosaur welcome and room-return previews were visually inspected. New images use MenuReturnFix; older previews and test records are retained.
- Corrected the build tool's fixed free-space check: require 2 GiB when both prior Android native output and Gradle APK exist, otherwise 6 GiB. These are conservative guard values, not expected new disk usage or guaranteed cache reuse. No cache was deleted for this follow-up. The successful build logged 3.7 GiB available and prior Android outputs present; approximately 3.0 GiB remained afterwards. Native build logs show changed items being updated with existing work reused, rather than the earlier clean rebuild.
- Unity exited with code 0 and logged Build Finished, Result: Success and Classroom APK built successfully. Android tools verified HideAR Classroom, com.lingjieruan.hidear.classroom, UnityPlayerGameActivity, ARM64 and APK Signature Scheme v2. Latest APK size: 75,360,309 bytes. SHA-256: 35899b9aed438827bfc6f4a7716267a7811e8f060a1ce647d824e79e6f133e56.
- Preserved the earlier color-only APK in History/HideAR_Classroom-2026-10-19-expanded-colors.apk with SHA-256 59b2e322789767cc80d5b2ce324685f34f9ece8726aff86be78aae9215a1788e. The October 5 backup and Prototype1 remain untouched.
- ADB initially returned an empty device list. After the Quest 3S was reconnected and authorized, adb install -r returned Success. The installed base APK SHA-256 matched 35899b9aed438827bfc6f4a7716267a7811e8f060a1ce647d824e79e6f133e56. Launch then returned Status: ok, LaunchState: COLD and the classroom UnityPlayerGameActivity (TotalTime 616 ms); pidof returned 20503. This confirms installation and process startup, not the physical room-return interaction or complete gameplay. No uninstall/data reset or original-app update was performed.

## Rebuild and Quest 3S installation (2026-10-19, earlier color-only version)

- User explicitly approved deleting only `HideAR_Week9/Library/Bee`, approximately 6.7 GiB of logical cache data. Unity was not running. The exact path was checked against its resolved path before deletion. Available disk space increased from approximately 3.8 GiB to 8.5 GiB; rebuilding regenerated the cache and left approximately 1.5 GiB. No other cleanup was performed.
- Preserved the previous classroom APK as `../Builds/Week9/History/HideAR_Classroom-2026-10-05-menu-position.apk`. Its SHA-256 remains `15dc9959f3f6231a0c33e0eb5e88c056dc445d970d62367b7790aa71c8736150`.
- Built the existing Week9 scene with `ClassroomSetup.BuildApk`, targeting Android. Unity exited with code 0 and logged both `Build Finished, Result: Success.` and `Classroom APK built successfully.` Cache removal required complete IL2CPP/native compilation, not just an APK transfer. No runtime scripts, scene, model prefabs or build settings were manually changed in this rebuild/install step.
- Android tools verified the `HideAR Classroom` label, `com.lingjieruan.hidear.classroom` identifier, UnityPlayerGameActivity entry point, ARM64 architecture and APK Signature Scheme v2. New APK size: 53,177,692 bytes (about 50.7 MiB). SHA-256: `59b2e322789767cc80d5b2ce324685f34f9ece8726aff86be78aae9215a1788e`.
- Authorized Quest 3S was detected, and `adb install -r` returned `Performing Streamed Install` followed by `Success`. Reading the device's installed base APK SHA-256 returned the same new hash, confirming this was not the old classroom APK. No uninstall or app-data reset was performed.
- Launch command returned Status: ok but resolved to `com.oculus.vrshell/.systemdialog.launchcheck.LaunchCheckControllerRequiredDialogActivity`, not the classroom Unity activity. A subsequent pidof check found no classroom process. Installation is verified; successful gameplay startup is not. Wake the controllers and follow the headset prompt, then open HideAR Classroom if needed.
- The 30 passing EditMode tests and 59 editor previews remain the 2026-10-17 checks, not a new test run or headset gameplay result. Prototype1 and Week3 have no new Git changes from this work; historical tests, models, source files and records were retained. No original-project APK was built or installed.

## Color and menu update (2026-10-17)

- Expanded Original / Moss / Blue / Gray / White with Red / Yellow / Orange / Purple / Pink / Teal / Black. The original five indices are unchanged. Eleven solid-color swatches and Original use the existing SelectColor method and MaterialPropertyBlock logic; no new runtime script or system was added.
- Removed the saved decorative Menu dinosaur object and its unused field/creation code. The selectable dinosaur no longer appears as a menu model preview. Dog, Cat and Bunny retain their menu previews; all four objects, including Dinosaur, retain actual-size surface previews and placement. Existing model prefabs, dimensions, anchors, room setup and round rules are unchanged.
- Ran Apply Selection UI against the existing scene, not initial scene generation. Saved twelve persistent color-button bindings and seven additional pet palette assets using the existing grayscale-face-preserving generator. Color controls remain separate from Start hiding, Change object and Set up room.
- Final EditMode run: 30 passed, zero failures. Added checks cover every saved swatch argument and selection outline, non-overlapping controls, dinosaur-free menu with placement retained, all palette references, per-instance overrides and unchanged shared materials. The first run passed 28/30; two new test assumptions were corrected (RuntimeOnly event invocation in EditMode and exact float color equality), then the complete suite was rerun. Both result files are retained.
- Rendered 59 editor views: nine stages/screens, two menu-position views and 48 model/color combinations. Text-height, viewport-boundary, nonblank-image and expected menu-preview visibility checks passed. Visually inspected the dinosaur-free authorized welcome menu and pink dog menu preview. These are simulated editor views, not headset captures.
- New preview images use the separate ExpandedColors folder. Earlier tests, previews and explanatory records were not deleted or overwritten. Prototype1 and Week3 have no new Git changes from this work.
- Disk space was approximately 3.8 GiB, below the existing build tool's 6 GiB guard (its user message recommends freeing at least 8 GB). No new APK build or installation was attempted. The existing APK SHA-256 remains 15dc9959f3f6231a0c33e0eb5e88c056dc445d970d62367b7790aa71c8736150. Rebuild and install after freeing sufficient space before testing these changes on Quest 3S / Quest 3.

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
- Latest readiness-follow APK SHA-256: `c9a6b3205cbb0776ee1b71997e0bdcf123439d1aad3caece461047708d53b951` (built and installed 2026-10-19; device base APK hash matches).
- Latest APK size: 75,357,717 bytes (about 71.9 MiB).
- Previous menu-return-fix APK SHA-256: `35899b9aed438827bfc6f4a7716267a7811e8f060a1ce647d824e79e6f133e56` (75,360,309 bytes; preserved in `../Builds/Week9/History/HideAR_Classroom-2026-10-19-menu-return-fix.apk`).
- Earlier installed expanded-color APK SHA-256: `59b2e322789767cc80d5b2ce324685f34f9ece8726aff86be78aae9215a1788e` (53,177,692 bytes; preserved in `../Builds/Week9/History/HideAR_Classroom-2026-10-19-expanded-colors.apk`).
- Previous menu-position APK SHA-256: `15dc9959f3f6231a0c33e0eb5e88c056dc445d970d62367b7790aa71c8736150` (74,979,873 bytes); preserved in `../Builds/Week9/History/HideAR_Classroom-2026-10-05-menu-position.apk`.
- Previously installed room/start APK SHA-256: `382b38ab52e638fb7a8b533eefd14802c47321bf2797b1cde7390a840ba57e2e`
- Old APK SHA-256: `8e4518b49eab2243ea917b6efb41854fdcae5bf1e12460e541eacd5090ae1454`
- Latest test XML: `../Builds/Week9/Review/EditMode-ready-follow.xml` (36 passed, 2026-10-19).
- Previous menu-recall test XML: `../Builds/Week9/Review/EditMode-menu-return-fix.xml` (33 passed, 2026-10-19).
- Previous color-update test XML: `../Builds/Week9/Review/EditMode-expanded-colors-v2.xml` (30 passed, 2026-10-17).
- Initial color-update test run: `../Builds/Week9/Review/EditMode-expanded-colors.xml` (28 passed, 2 failed; test assumptions corrected before the successful v2 run).
- Previous menu-position tests: `../Builds/Week9/Review/EditMode-menu-position-fix.xml` (27 passed).
- Previous room/start tests: `../Builds/Week9/Review/EditMode-room-start-fix.xml` (22 passed).
- Historical initial tests: `../Builds/Week9/Review/EditMode-tests.xml` (16 passed on 2026-10-03).
- Editor images: `../Builds/Week9/Review/01-welcome.png` through `06-found.png`, `01b-room-authorized.png`, `04b-seeker-ready.png`, `04c-countdown.png`, `07-menu-seated.png`, `08-menu-pinned.png` and `choice-*.png`.
- Latest test log: `../Builds/Week9/Review/Unity-ready-follow-tests.log`.
- Latest preview images: `../Builds/Week9/Review/ReadyFollow/*.png` (62 views).
- Latest preview log: `../Builds/Week9/Review/Unity-ready-follow-preview.log`.
- Previous menu-recall checks: `../Builds/Week9/Review/Unity-menu-return-fix-tests.log`, `Unity-menu-return-fix-preview.log` and `MenuReturnFix/*.png` (60 views).
- Previous color-update checks: `../Builds/Week9/Review/Unity-expanded-colors-tests-v2.log`, `Unity-expanded-colors-preview.log` and `ExpandedColors/*.png` (59 views).
- Targeted scene-update log: `../Builds/Week9/Review/Unity-expanded-colors-setup.log`.
- Previous menu-position logs: `../Builds/Week9/Review/Unity-menu-position-fix-tests.log` and `Unity-menu-position-fix-preview.log`.
- Latest build log: `../Builds/Week9/Review/Unity-ready-follow-build.log` (successful incremental Android build).
- Latest build/install record: `../Builds/Week9/Review/Quest-ready-follow-2026-10-19.md`.
- Previous menu-recall build/install: `../Builds/Week9/Review/Unity-menu-return-fix-build.log` and `Quest-menu-return-fix-2026-10-19.md`.
- Earlier color-only build and install: `../Builds/Week9/Review/Unity-expanded-colors-build-2026-10-19.log` and `Quest-expanded-colors-install-2026-10-19.md`.
- Previous build log: `../Builds/Week9/Review/Unity-menu-position-fix-build.log` (successful Android build, 2026-10-05).

## Still requires Quest 3S / Quest 3 testing

1. Passthrough, right-controller aim and clickable UI appear.
2. Set up room remains available with existing room data. In a different physical room, complete the system Space Setup and verify the app returns usable horizontal surfaces for that room, rather than old surfaces. Also test denied permission, cancelled capture and manual Space Setup from Quest Settings.
3. Change object updates the selected name and shows exactly one matching menu preview, including Dinosaur. All twelve color choices work, the chosen name/highlight updates, and colors match the menu preview, actual-size surface preview and placed object.
4. Surface preview shows actual model size before confirmation and never blocks raycasts. A UI press does not also place an object.
5. One object remains near its real-world spot when the player moves and looks away. Review color changes, reposition and confirmation do not duplicate it.
6. Confirm hiding hides the object and selection information and waits for Start search. One person can continue without removing the headset.
7. A fresh release plus explicit Start search click starts the three-second countdown. Merely waiting, putting on the headset or tracking recovery never starts it automatically.
8. Losing tracking or pausing the app suspends countdown/search timing; recovery continues the existing stage instead of forcing another readiness confirmation.
9. During seeking no planes, marker or selection previews reveal the hiding spot. Triggering the small object's collider produces success and stops the timer.
10. Passing the headset preserves the anchor after tracking resumes; reset/replay clear the previous round.
11. Unknown Sources launches the installed app without USB. Finder launch/install scripts work when one authorized Quest is connected.
12. Walk/turn during room setup and placement. Once setup returns or placement reaches Review, the menu should appear in front once tracking is valid and then stay pinned. In Ready to search only, the compact card follows the wearer; walk away from the hiding spot to an agreed starting position before handoff. Waiting must not start the countdown. Start search must remain clickable and stop following once it starts Countdown. Other stages keep existing one-shot recall/manual-A behavior. Include looking down/up, seated/standing positions, a pause and headset handoff. Neither anchor nor hidden object should move with the card; real tracking shifts and comfort still require hardware checks.

The headset runtime, permissions, input alignment and physical tracking cannot be validated by the editor tests or screenshots above.
