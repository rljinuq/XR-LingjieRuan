# Candidate model sources for HideAR_Week9

Date downloaded: 2026-10-04

These packages were initially downloaded as future candidates. Dog, Cat and Bunny from Kenney Cube Pets are now available alongside the dinosaur in the classroom scene. Quaternius Farm Animals and Kenney Prototype Kit remain unused candidates.

## Location in this Unity project

- Original zip files: `Assets/Art/CandidateModels/Downloads`
- Extracted files: `Assets/Art/CandidateModels/Sources`

Note: the repository root `.gitignore` ignores `*.zip`, so the original zip files are kept locally but may not be committed by default. The source pages, direct download links and hashes below are the durable references.

## Downloaded packages

### Quaternius - LowPoly Animated Farm Animal Pack

- Local zip: `Downloads/quaternius_farm_animals.zip`
- Extracted folder: `Sources/Quaternius_FarmAnimals`
- Source page: https://opengameart.org/content/lowpoly-animated-farm-animal-pack
- Direct download used: https://opengameart.org/sites/default/files/Farm%20Animals%20by%20%40Quaternius.zip
- Creator: Quaternius
- License: CC0
- Formats included: FBX, OBJ, Blend
- SHA-256: `9ff42b7e2445eaf259e4734c08172637db92e4b59a6145909ac56d946152c39b`
- Suggested HideAR use: first-choice animal replacement candidate, because it is close to the existing dinosaur use case and easy to explain.

### Kenney - Cube Pets

- Local zip: `Downloads/kenney_cube_pets.zip`
- Extracted folder: `Sources/Kenney_CubePets`
- Source page: https://opengameart.org/content/cube-pets
- Direct download used: https://opengameart.org/sites/default/files/kenney_cube-pets_1.0_0.zip
- Creator: Kenney
- License: CC0
- Formats included: FBX, OBJ, GLB and previews
- SHA-256: `d2f4d0b34b57f5a28fc639f809d0591a041e79ca294599459e673c93f0e924f5`
- Suggested HideAR use: cute pet choices such as dog, cat, bunny, cow or pig for a future small object-selection menu.
- Current HideAR use: `animal-dog.fbx`, `animal-cat.fbx` and `animal-bunny.fbx` are used to create the Dog, Cat and Bunny selectable placement prefabs in `Assets/HideAR/Prefabs`.
- Texture used: `Models/FBX format/Textures/colormap.png`. The original texture and source models are preserved. `ClassroomSetup.MakeColorTextures` generates Moss / Blue / Gray / White palette variants under `Assets/HideAR/Settings/Pet Color *.asset`, preserving grayscale eyes and muzzle details. Runtime uses per-instance property blocks; no downloaded source is overwritten.

### Kenney - Prototype Kit

- Local zip: `Downloads/kenney_prototype_kit.zip`
- Extracted folder: `Sources/Kenney_PrototypeKit`
- Source page: https://opengameart.org/content/prototype-kit
- Main creator page: https://kenney.nl/assets/prototype-kit
- Direct download used: https://opengameart.org/sites/default/files/kenney_prototype-kit.zip
- Creator: Kenney
- License: CC0
- Formats included: FBX, OBJ, GLB and previews
- SHA-256: `213b522fb12bcc9b9ac66c4f7581f7c74623293272212e40a70c39936ad3da95`
- Suggested HideAR use: non-animal selectable objects, such as crate, coin, sword, shield, simple vehicle or small figurine.

## Import notes

- Prefer FBX or GLB models for Unity prefab creation.
- Keep the first implementation simple: choose one model, make one prefab, add an obvious collider, and reuse the existing placement/found logic.
- For a later object-selection feature, use a small fixed list of 3-5 prefabs instead of a large asset browser.
- Do not add networking, runtime downloads, persistent anchors, or complex animation selection just to support model choices.

## Current extraction summary

- Approximate total candidate folder size: 42 MB.
- Detected model files after extraction: 167 FBX, 167 OBJ, 160 GLB, 7 Blend.
- By source: Quaternius Farm Animals has 7 FBX, 7 OBJ and 7 Blend files; Kenney Cube Pets has 15 FBX, 15 OBJ and 15 GLB files; Kenney Prototype Kit has 145 FBX, 145 OBJ and 145 GLB files.
- The downloaded assets are visual content only. Unity AR Foundation/OpenXR/XRI still provide passthrough, controller input, plane data and anchors.

## Git publishing subset (2026-10-17)

The download and extraction records above describe the complete local candidate library. All of those local files remain available; ignoring a file in Git does not remove it from this Unity project on this Mac.

For the current classroom repository, only the three used Cube Pets FBX files, their shared `FBX format/Textures/colormap.png`, the Cube Pets license, this source record and their required `.meta` files are published from this candidate folder. The dinosaur source and all four placement prefabs/materials remain published in their existing project folders.

Unused Quaternius/Prototype Kit assets, other pets, duplicate OBJ/GLB formats, thumbnails and archive downloads are excluded by the project's `.gitignore`. This keeps the current source repository focused without discarding possible future choices or their references. A future model choice must explicitly add its source files and matching `.meta` dependencies to the publishing allowlist before committing.
