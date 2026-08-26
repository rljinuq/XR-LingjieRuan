# HideAR IP1 Unity Prototype

This prototype supports the first horizontal test for HideAR:

1. Scan a surface.
2. Tap to place a virtual hidden object.
3. Drag the object to choose a hiding position.
4. Press Start Seek and give the phone to the seeker.
5. Press Found when the seeker finds the object.

## Unity Setup

- Unity version in this project: 6000.3.21f1.
- Required packages added to `Packages/manifest.json`:
  - `com.unity.xr.arfoundation`
  - `com.unity.xr.arcore`
  - `com.unity.xr.arkit`
  - `com.unity.xr.management`

After Unity finishes importing packages:

1. Open the project at `Prototype1`.
2. In the top menu, choose `HideAR > Build IP1 AR Scene`.
3. Open `Assets/HideAR/HideAR_IP1.unity`.
4. Press Play in Unity on Mac to test the flow with the mouse on the dark preview floor.
5. Check `Project Settings > Player > Other Settings > Active Input Handling` is set to `Both`.
6. For Android, enable ARCore in `Project Settings > XR Plug-in Management > Android`.
7. For iPhone, enable ARKit in `Project Settings > XR Plug-in Management > iOS`.
8. Build to a real phone for AR testing.

## Mac Preview Controls

This preview is not real AR, but it lets you test the IP1 interaction flow before building to a phone.

1. Press Play.
2. Click the dark floor to place the hidden object.
3. Hold and drag on the floor to move the hidden object.
4. Click `Start Seek`.
5. Pretend you are the seeker, move the Scene/Game view camera if needed, then click `Found`.
6. Click `Reset` for the next participant.

## Testing Data

When the seeker presses `Found`, the prototype writes a CSV file to:

`Application.persistentDataPath/hidear_ip1_results.csv`

The Unity Console also prints the path and the latest row.

## Current Prototype Scope

Implemented for IP1:

- Surface-based placement through AR raycast.
- Touch/mouse dragging for hiding.
- Hider/seeker flow.
- Seek timer.
- Result logging for testing.

Not implemented yet:

- Multiplayer/networked phone handoff.
- Advanced object occlusion.
- Multiple selectable 3D objects.
- Polished art assets.
