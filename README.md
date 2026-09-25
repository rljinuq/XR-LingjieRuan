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
