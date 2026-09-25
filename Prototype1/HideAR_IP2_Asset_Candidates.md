# HideAR IP2 Asset Candidates

This file records visual and audio options for later selection. Nothing listed here has been imported into `Assets` yet.

## Recommended Dinosaur Models

### 1. Quaternius Animated Dinosaur Pack

- Source: https://quaternius.com/packs/animateddinosaurs.html
- License: CC0
- Formats: FBX, OBJ, Blend
- Contents: 6 textured and animated dinosaurs
- Best use: primary model pack for HideAR; choose between several species without changing the official AR placement logic

### 2. Quaternius Dino

- Source: https://poly.pizza/m/wuerCFCWNR
- License: CC0 / Public Domain
- Formats: FBX, GLTF
- Animated: yes
- Best use: a rounder, more character-like single dinosaur candidate

### 3. Quaternius T-Rex

- Source: https://poly.pizza/m/UYtneO5FpF
- License: CC0 / Public Domain
- Formats: FBX, GLTF
- Best use: recognizable low-poly T-Rex alternative

### 4. Current Low Poly Dino

- Source: https://opengameart.org/content/low-poly-dino
- License: CC0
- Current project file: `Assets/_HideAR_IP2_Prototype/Resources/LowPolyDino/dino.fbx`
- Best use: retain as the tested fallback model

## Opening And Transition Assets

### Cute Dinosaur Sprite

- Source: https://opengameart.org/content/free-dino-sprites
- License: CC0
- Contents: PNG sequences with 5 animation states
- Best use: opening mascot, handoff screen, or success animation

### Kenney UI Pack - Adventure

- Source: https://kenney.nl/assets/ui-pack-adventure
- License: CC0
- Contents: 130 buttons, panels, and interface elements
- Best use: replace plain programmatic panels with a friendlier game-style interface

### Kenney Interface Sounds

- Source: https://kenney.nl/assets/interface-sounds
- License: CC0
- Contents: 100 interface sounds
- Best use: Start Game, Confirm Hide, countdown ticks, and found feedback

### Kenney Particle Pack

- Source: https://kenney.nl/assets/particle-pack
- License: CC0
- Contents: 80 particle and VFX sprites
- Best use: short success burst when the dinosaur is found

## Assets Already Available In The Project

- `Assets/Textures/success_icon.png`
- `Assets/Samples/XR Interaction Toolkit/3.4.0-pre.2/Hands Interaction Demo/DemoAssets/Audio/ButtonClick.wav`
- `Assets/Samples/XR Interaction Toolkit/3.4.0-pre.2/Starter Assets/DemoAssets/Audio/Button Pop.wav`

These can be used for an initial UI polish pass without downloading anything else.

## Recommended First Combination

1. Keep the current dinosaur while interaction testing continues.
2. Add a custom opening illustration or the CC0 cute dinosaur sprite.
3. Add one subtle button sound, three countdown ticks, and one success sound.
4. Add a brief particle burst only on the found screen.
5. Import the Quaternius Animated Dinosaur Pack later and expose the chosen model as a replaceable prefab reference.

Keeping the model replacement behind one prefab reference allows visual changes without changing Anchor, Plane Detection, Raycast, or XR Origin behaviour.
