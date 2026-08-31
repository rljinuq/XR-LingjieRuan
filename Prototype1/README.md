# HideAR IP1 Prototype

This Unity project contains the first interactive AR prototype for HideAR.

The prototype is built on top of Unity's AR Foundation Samples anchor scene. The official AR anchor, plane detection, raycast, XR Origin, and ARKit behaviour have been kept intact so the app can use a stable, tested tap-to-place workflow.

## Tutor Quick Guide

This repository contains both the IP1 submission prototype and the original Unity AR Foundation sample material used as technical reference. The IP1 work is separated into folders with `_HideAR_IP1_` in the name, so the submission-facing files can be identified quickly.

The main prototype to review is the iPhone AR scene at:

`Assets/Scenes/_HideAR_IP1_MainScene/HideAR_IP1.unity`

The expected IP1 demonstration is:

1. Open the IP1 scene.
2. Build and run on an ARKit-supported iPhone.
3. Scan a real horizontal surface, such as a desk or floor.
4. Tap the detected surface to place the virtual dinosaur.
5. Move the phone and observe whether the dinosaur remains anchored in the same physical location.

## Prototype Goal

The current goal is not a full hide-and-seek game. This version only tests the core AR placement interaction:

1. Scan a real table or floor.
2. Tap a detected real-world surface.
3. Place a virtual dinosaur at that point.
4. Move or rotate the phone.
5. Confirm the dinosaur remains anchored in the same real-world location.

## Main Scene

The IP1 testing scene is:

`Assets/Scenes/_HideAR_IP1_MainScene/HideAR_IP1.unity`

This scene is set as the first scene in Build Settings so the iPhone app opens directly into the IP1 experience instead of the AR Foundation Samples menu.

## User Experience

When the app opens, the user sees:

- HideAR IP1 title and short concept text
- Scan instruction: `Move your phone to scan the surface`
- Placement instruction: `Tap the surface to place`
- Placement feedback after an anchor is created
- Reset button for repeated testing

The technical sample UI, debug labels, persistent-anchor prompts, and coordinate visuals are hidden from the tester-facing experience.

## Project-Specific Files

IP1 scene:

`Assets/Scenes/_HideAR_IP1_MainScene/HideAR_IP1.unity`

IP1 presentation controller:

`Assets/Scripts/Runtime/_HideAR_IP1_Scripts/IP1PresentationController.cs`

Dinosaur visual resource:

`Assets/_HideAR_IP1_Prototype/Resources/LowPolyDino/dino.fbx`

## Repository Structure

The folders with `_HideAR_IP1_` in their names contain the submission-facing IP1 prototype work:

- `Assets/Scenes/_HideAR_IP1_MainScene`
- `Assets/Scripts/Runtime/_HideAR_IP1_Scripts`
- `Assets/_HideAR_IP1_Prototype`

The other AR Foundation sample scenes, scripts, prefabs, and assets are intentionally kept in the repository as external technical references and future development material. They are not all part of the IP1 user-facing prototype. They remain available for IP2 and later development, such as image tracking, occlusion, object tracking, body/face tracking, shared anchors, and other XR interaction experiments.

## Testing Document

Testing plan and testing results:

`HideAR_IP1_Testing_Plan.docx`

This document contains the intended IP1 testing method, participant task, and the recorded testing results from iPhone testing. It is the main tutor-facing testing document for evaluating the prototype process and outcome.

## Originality and References

Originality statement, external assistance, and asset references are kept in a separate file:

`STATEMENT_OF_ORIGINALITY_AND_REFERENCES.md`

This keeps the project README focused on how to locate and understand the prototype, while the originality and reference statement remains available for submission documentation.

## Preserved AR Foundation Sample Logic

The underlying AR functionality comes from Unity's official AR Foundation Samples. The following core anchor scripts are intentionally preserved:

- `Assets/Scripts/Runtime/Anchors/ARPlaceAnchor.cs`
- `Assets/Scripts/Runtime/Anchors/ARAnchorDebugVisualizer.cs`
- `Assets/Scripts/Runtime/Anchors/AnchorAddRemoveLogger.cs`

The IP1 presentation script does not replace the official anchor creation logic. It only changes what the user sees and adds the dinosaur visual under anchors created by the official sample workflow.

## Build Target

The tested target platform is iOS on iPhone using Unity 6.3 and AR Foundation 6.3.

## Asset Credits

Dinosaur model:

Low Poly Dino from OpenGameArt  
https://opengameart.org/content/low-poly-dino  
License: CC0

Unity AR Foundation Samples code and assets are covered by the Unity Companion License. See `LICENSE.md`.
