# HideAR IP1: Real AR Prototype

Open scene:

`Assets/START_HERE_HideAR_AR.unity`

## What This Prototype Tests

This is the real AR direction for HideAR. It tests whether users can understand and enjoy:

1. placing a virtual object on a real detected surface,
2. moving the object with touch to choose a hiding location,
3. physically moving the phone and changing viewpoint to find it.

## Mac Editor Preview

The Mac preview is only for checking interaction logic. It cannot run true AR tracking.

1. Open `START_HERE_HideAR_AR`.
2. Press Play.
3. Click the dark preview surface to place the object.
4. Drag it to test hiding.
5. Press `Hide / Start Seek`.
6. Aim the centre reticle at the object and click, or use `Found Manually`.

## Phone AR Test

1. Build to a real phone.
2. Point the phone at a textured table or floor.
3. Move slowly until Unity detects a horizontal surface.
4. Tap the detected surface to place the object.
5. Drag the object to hide it.
6. Press `Hide / Start Seek`.
7. Give the phone to the seeker.
8. The seeker moves around and aims the centre reticle at the object.
9. Tap when found.

## IP1 Scope

Included:

- real phone camera AR scene,
- horizontal surface placement,
- touch drag hiding,
- hider/seeker flow,
- seeking timer,
- reticle-based found interaction,
- CSV result logging.

Not included for IP1:

- real occlusion behind physical objects,
- multiplayer sync,
- full room scanning,
- final art polish.
