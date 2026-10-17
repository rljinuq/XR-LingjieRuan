# HideAR IP2 for Meta Quest

Target devices: Quest 3S for home development, Quest 3 for classroom testing.

Open `Scenes/HideAR_IP2_Quest.unity`, or use the Unity menu `HideAR > Quest > 1. Open Quest Scene`.
The Chinese installation and test walkthrough is in `Prototype1/QUEST_TESTING_GUIDE.md`.

## Architecture

The scene is a copy of the official `Assets/Scenes/Anchors/Anchors.unity`, retaining the official XR Origin prefab, controller inputs, AR Session, plane manager, raycast manager, and anchor manager. The official sample scripts are unchanged.

Placement event flow:

1. The official `RaycastEventController` performs a controller world-space raycast using `PlaneWithinPolygon`.
2. `QuestGameController` accepts a hit only during placement, after trigger release, with room permission and tracking available. Only upward-facing horizontal planes are accepted.
3. The controller sets the round to Saving before forwarding the hit through a private approved-placement event asset.
4. The unchanged `ARPlaceAnchor` receives that event and creates the anchor using the platform-supported path.
5. When the anchor manager reports the result, the controller attaches the dinosaur visual underneath the anchor and enters Review.

The original official event assets and anchor prefab are not modified. The private Quest anchor prefab contains ARAnchor without developer labels. The private plane prefab uses the official ARPlaneMeshVisualizer for the surface mesh and boundary.

## Project code

| File | Responsibility |
| --- | --- |
| `Runtime/QuestRound.cs` | Game stages, release gate, single in-flight placement, countdown and search timer |
| `Runtime/QuestGameController.cs` | Connects official placement events, controller selection, anchors and presentation |
| `Runtime/QuestRoomAccess.cs` | Scene permission, enabling the plane manager, official Meta scene-capture API |
| `Runtime/QuestPanel.cs` | World-space UGUI/TMP menu, regular Buttons, XRI tracked graphic raycaster |
| `Runtime/QuestDinosaur.cs` | Model size, surface alignment and selection collider |
| `Editor/QuestProjectSetup.cs` | Scene construction, Android/OpenXR configuration, isolated build, validation and preview rendering |
| `Tests/Editor/QuestRoundTests.cs` | Edit Mode flow regression tests |
| `Tests/Editor/QuestSceneTests.cs` | Scene wiring, UI blocking, text bounds and dinosaur hierarchy tests |

The scene contains a world-space menu collider on the UI layer. It blocks placement rays behind the panel. Transition release gating prevents a menu press from becoming a placement or a found action. The dinosaur stays under its ARAnchor; there is no per-frame camera-relative repositioning of the placed object.

The panel is repositioned at stage changes and after tracking resumes. A or X brings it back in front of the wearer. This moves only the menu, never the XR Origin or dinosaur.

## Build and platform settings

Use `HideAR > Quest > 2. Configure Android for Quest`, then `3. Switch to Android`, then `4. Build APK` or `5. Build and Run on Connected Quest`.

The custom build includes only the Quest scene. It does not modify the shared build scene list used by the iPhone version. The APK opens directly into HideAR.

- Package identifier: `com.lingjieruan.hidear.quest`.
- Android ARM64 / IL2CPP, Vulkan, minimum Android API 32.
- Android XR provider: OpenXR; ARCore is disabled for this Android target.
- Enabled features: Meta Quest Support, Session, Camera/Passthrough, Anchors, Planes, Raycasts, Composition Layers, Oculus Touch and Touch Plus interaction profiles.
- Supported device entries: Quest 3 and Quest 3S.
- A private URP pipeline disables HDR and uses 4x MSAA. Shared pipeline references are changed temporarily for the custom build and restored afterwards.
- No package upgrades, third-party SDK installation, persistent anchors, networking, database or GPS.

Generated APK: `XR-LingjieRuan/Builds/Quest/HideAR_IP2_Quest.apk`.

## Testing boundaries

The Edit Mode tests cover the game rules. Scene validation checks serialized references, AR session and XR UI input. Android script compilation checks the Android-only C# code; it is not an APK build or a headset test.

Actual passthrough, room permissions, tracking stability, physical handoff, controller ray alignment and the final Android install must be verified on Quest hardware. A successful Editor test does not prove those device behaviours.

On 2026-10-01, all 16 Edit Mode tests passed. Four menu states were rendered in Unity and inspected for visible models and text overflow. These are Editor UI previews, not headset screenshots or participant test results.

Room setup is required before Meta returns plane data. Reset round removes game content, not the headset's room setup. Handoff uses the same running headset and AR session. There is no cross-device anchor sharing or app-relaunch persistence. Depth occlusion is not enabled in this version.

## References

- Unity [AR Foundation Samples 6.3](https://github.com/Unity-Technologies/arfoundation-samples/tree/6.3), under the existing project's Unity Companion License.
- Unity [Meta OpenXR 2.3 documentation](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.3/manual/index.html).
- Existing Low Poly Dino from [OpenGameArt](https://opengameart.org/content/low-poly-dino), CC0.
