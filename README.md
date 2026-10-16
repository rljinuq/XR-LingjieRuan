# HideAR: Quest Classroom Version

The current playable project is **[HideAR_Week9](HideAR_Week9/)**, an independent Unity project for **Meta Quest 3 / Quest 3S** with right-controller input. It replaces the mobile prototype as the active implementation in this repository.

## Open the Current Project

1. Use Unity **6000.3.21f1** with Android Build Support, SDK, NDK and OpenJDK.
2. In Unity Hub, add the **HideAR_Week9 folder**, not this repository root or Prototype1.
3. Open `Assets/HideAR/Scenes/HideAR_Week9.unity` inside that project.
4. Use **HideAR > Classroom > Build APK** for the classroom app (`com.lingjieruan.hidear.classroom`). Local APK output is `Builds/Week9/HideAR_Classroom.apk`.

The game supports selecting a small object/color, placing and reviewing one anchored object, explicit Start search confirmation, a countdown, finding and search timing. Hold the right-controller A button to reposition only the task card. Environment-depth occlusion is not implemented.

## Guides and Records

- [Chinese setup and play guide](HideAR_Week9/START_HERE_ZH.md)
- [Code structure and defense guide](HideAR_Week9/CODE_GUIDE_ZH.md)
- [Verification record and remaining hardware checks](HideAR_Week9/VERIFICATION.md)
- [Migration scope and preservation checks](MIGRATION_HEADSET.md)

`Week3_Activity1` and existing test plans, results forms, explanatory documents, assets and licenses are retained. Only non-test C# source files in the old mobile `Prototype1` are removed by this migration. Existing test source files are also retained. **Prototype1 is now a legacy record/resource directory, not the runnable project to open.** Full old source remains in Git history; the local original projects were not modified.

The Unity test results and APK recorded in the verification guide are historical checks, not a new hardware test performed for this repository migration. Generated Unity caches and APKs are not committed.

## Original Mobile README

The original text below is preserved verbatim as historical context, not current setup instructions.

<details>
<summary>Original mobile-project README (preserved)</summary>

# XR-LingjieRuan

Course project repository for Lingjie Ruan.

This repository contains design work, evaluation material, and Unity prototypes for an XR/AR interaction project.

## Prototype 2: HideAR IP2

The current working AR prototype is in:

`Prototype1/`

Prototype 2 is built in Unity 6.3 with AR Foundation. The app opens directly into an IP2 testing scene where the user scans a real surface, taps the detected plane, and places a dinosaur visual that stays anchored in the real world.

The prototype is intentionally focused on a small, testable AR interaction:

1. Open the iPhone app.
2. Move the phone to scan a table or floor.
3. Tap a detected real-world surface.
4. Place the dinosaur at that real-world point.
5. Use Reset to clear the placed object and test again.

The AR anchor, raycast, plane detection, and XR Origin behaviour are based on Unity's AR Foundation Samples anchor scene. The project-specific work is the IP2 scene, presentation UI, and dinosaur visual content.

</details>
