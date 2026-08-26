# START HERE: HideAR Real AR IP1

Main scene:

`Assets/START_HERE_HideAR_AR.unity`

## What To Demo For IP1

This prototype is the real AR version of HideAR:

1. scan a real horizontal surface,
2. tap to place a virtual object,
3. drag the object to choose a hiding position,
4. press `Hide / Start Seek`,
5. give the phone to the seeker,
6. seeker moves the phone viewpoint and taps when the object is found,
7. the prototype records the seek time.

## Test In Unity On Mac

Mac cannot run true mobile AR tracking, but you can check the interaction flow:

1. Open `Assets/START_HERE_HideAR_AR.unity`.
2. Press Play.
3. Click the dark preview surface to place the object.
4. Drag it.
5. Press `Hide / Start Seek`.
6. Aim the centre `+` at the object and click.

## Build To Phone

For iPhone:

1. You already have Xcode installed.
2. In Unity Hub, add `iOS Build Support` to Unity `6000.3.21f1` if iOS does not appear in Build Profiles.
3. Unity: `File > Build Profiles`.
4. Select `iOS`.
5. Press `Switch Platform`.
6. Enable `Development Build` if you want easier debugging.
7. Build, then open the generated Xcode project.
8. In Xcode, sign in with your Apple ID under `Xcode > Settings > Accounts`.
9. In Xcode, select your iPhone as the run target and press Run.

For Android:

1. Unity Hub must include Android Build Support.
2. Unity: `File > Build Profiles`.
3. Select `Android`.
4. Build and Run to an ARCore-supported phone.

## Important Limitation

This IP1 version does not do real physical occlusion. The object is hidden by shrinking and becoming subtle during seek mode. That is enough for testing placement, touch hiding, and viewpoint-based seeking.
