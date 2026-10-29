# Kx VRC NativeFT Manager

An NDMF plugin that brings Jerry's Templates style toggles and expression corrections, operated from the Expressions Menu, to avatars that use VRChat's native Face Tracking.
Drop the prefab on your avatar and it works. It integrates with [Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator).
It also suppresses FaceEmo while face tracking is active. It does not integrate with [Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge), and the two should not be used on the same avatar.

日本語: [README.md](README.md)

> [!WARNING]
> **This is an experimental release (0.x).** Native Face Tracking is a feature of the VRChat SDK beta and may change until the SDK is released. This package stays experimental until version 1.0, which will follow the SDK release.
> The current version provides Blendshapes Set grouping, toggles for face tracking, eyes, mouth, pupils and visemes, expression corrections (emulation, limits, Eye Sync), sensitivity, and FaceEmo suppression.
> Whether the State Behavior used by the toggles and the empty Sets (gates) used to switch the corrections work as intended is still being checked on the VRChat beta client. If the State Behavior type cannot be found, the build reports a warning (KxNFTM-E-05) and only the toggles and the sensitivity stop working.
> Parameter names (those starting with `KxNFTM/`) may change before 1.0. If they do, saved toggle values go back to their defaults.

## Requirements

- Unity 2022.3
- VRChat SDK Avatars 3.10.6-beta.2 or later (a pre-release; see "Installing the pre-release SDK" below)
- NDMF 1.6.0 or later
- Modular Avatar 1.12.0 or later

### Installing the pre-release SDK

The SDK with native Face Tracking is still distributed as a pre-release. VCC and ALCOM hide pre-releases by default, so turn them on first.

1. In VCC, enable "Show Pre-Release Packages" under Settings > Packages. In ALCOM, enable showing pre-release packages in its settings
2. In the project's package list, change "VRChat SDK - Avatars" to 3.10.6-beta.2 or later (3.10.6-beta.3 or later if you use Avatar Optimizer)
3. Add this package. It depends on that SDK, so it cannot be added while the SDK is older

The following packages are not dependencies. Integration is enabled only when they are installed.

- Kx VRC ARKit BlendShape Generator
- Avatar Optimizer 1.9.21-beta.1 or later (when used together)

Avatar Optimizer added native Face Tracking support in 1.9.21-beta.1 and keeps the shapes used by Blendshapes Sets and Parameter Behaviors from being optimized away.
This package relies on that support and has no Avatar Optimizer workaround of its own.
Avatar Optimizer's support is active only with VRChat SDK 3.10.6-beta.3 or later, so update the SDK as well when you use both.
Older combinations produce warnings at build time (W-14, W-15).

If you have to stay on an older Avatar Optimizer, add the face mesh to "Exclusions" under "Debug Options" of Trace and Optimize. Its shapes are then neither removed nor merged.
The mesh is no longer optimized at all, so use this only until you can update Avatar Optimizer.

## Installation

1. Add the [VPM repository](https://limit7412.github.io/vcc-vpm/) to VCC/ALCOM
2. Add "Kx VRC NativeFT Manager" to your project

Manual installation from the zip, the booth version (unitypackage), and how updates are announced and applied are described in [Installation and updates](Documentation~/install.md) (Japanese).

## Usage

1. Put a prefab from `Packages/Kx VRC NativeFT Manager/Runtime/Prefabs/` directly under the avatar
   - Generic (`Kx NativeFT Manager`): for avatars whose Blendshapes Sets on the Descriptor are prepared with Auto Fill or by hand
   - ARKit (`Kx NativeFT Manager (ARKit)`): for avatars that use Kx VRC ARKit BlendShape Generator. The Generator's Set output is enabled during the build only
2. Choose a preset in the inspector. UE (default) and ARKit have the same toggles and corrections as Jerry's, and Minimal has only three toggles and no corrections. Changing a toggle or a rule switches the preset to Custom
3. Upload. Use "Face Tracking" in the Expressions Menu to switch Face Tracking, Eye Tracking, Lip Tracking, Pupil and Visemes. The corrections (Emulation, Limits, Eye Sync) are in "Settings" below it

Jerry's Face Tracking Templates cannot be on the same avatar. If it is still there, the inspector and the build report an error (KxNFTM-E-04).
This package does not remove Jerry's for you, so remove its prefab before uploading. To keep both a Jerry's version and a native version, upload them as separate avatars.

### What happens at build time

- The Blendshapes Sets on the Descriptor are split by tracking key into Eye / EyeClosed / Pupil / Brow / Lip / Other (`KxNFTM/Eye` and so on). The original Sets are removed, and the total number of key-to-shape assignments stays the same
- Each toggle shown in the menu gets a synced Bool parameter and a menu entry, and FX layers switch the Set weights between 0 and 1. The parameters are `KxNFTM/FaceTracking`, `KxNFTM/EyeTracking`, `KxNFTM/LipTracking`, `KxNFTM/Pupil`, `KxNFTM/Visemes`, `KxNFTM/Emulation`, `KxNFTM/Limits` and `KxNFTM/EyeSync`, 8 bits in total for the UE preset
- For each sensitivity channel that is enabled, the Set multipliers are raised to the max multiplier and the Set weight is written per Radial step (see "Sensitivity")
- Turning Visemes off sets Mouth & Jaw to Animation with Tracking Control in FX, which stops visemes
- The corrections are added as Parameter Behaviors (Custom expressions) on the Descriptor. The coefficients come from the FX of Jerry's 7.0.5 (see [THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md))
  - Emulation: raises cheeks and dimples when smiling, moves brows from the mouth, narrows pupils when the eyes widen, and so on. Written as a lower bound (Limit Min) that never goes below the tracked value
  - Limits: upper bounds (Limit Max) for combinations that break when moved together, such as holding down the mouth while the lips pucker
  - Eye Sync: writes the average of both eyelids to both eyes
  - The corrections are switched with the weight of empty Sets that have no assignments (`KxNFTM/Gate/...`). A rule also stops when the toggle of the keys it reads or writes (eyes, pupils, mouth) is turned off
  - Rules whose target shape is not in any Set are not generated (KxNFTM-I-01). When the Custom expression instructions exceed the avatar-wide limit of 512, the rules with the lowest priority are dropped (KxNFTM-W-12)
- Face Tracking and Use All Tracking Keys are enabled on the Descriptor (you can choose to leave them unchanged in the inspector)

### Sensitivity

Enabling eye or lip sensitivity in the inspector adds "Sensitivity" under "Face Tracking" in the Expressions Menu, with Radials that change how strongly the eyes and the mouth move.
The synced parameters are `KxNFTM/Sensitivity/Eye` and `KxNFTM/Sensitivity/Lip` (Float), and each uses 8 more synced bits. Neither is created by default.

- Eye sensitivity applies to the Sets of the eyes, eyelids, pupils and brows, and lip sensitivity applies to the mouth Set (including cheeks, tongue and visemes). The neck (NeckFlex) is not affected
- The center of the Radial (the default) is 1x, and the strength changes in 8 steps: 0.2x to 0.8x on the left, and 1x up to the "Max Multiplier" in the inspector (2x by default) on the right
- The corrections (Emulation, Limits, Eye Sync) are not affected

Jerry's has seven continuous sensitivities, but Parameter Behaviors of native Face Tracking cannot read animator parameters, so this package approximates sensitivity with the Set weight.
At build time the target Set multipliers are raised to the max multiplier, and each Radial step writes a Set weight that divides it back.
The strength therefore changes in steps rather than continuously, and only the multipliers of the original Sets are scaled.

### FaceEmo suppression

On avatars that use FaceEmo, enabling "FaceEmo Suppression" in the inspector keeps FaceEmo's expressions and blinking from writing face shapes while face tracking is active.
This is the same feature as in FE-JsT Bridge, rebuilt in this package. It does not read FaceEmo's assets and connects through parameter names. It is disabled by default, and the inspector shows a notice (KxNFTM-I-03) when FaceEmo is present but suppression is disabled.

- Trigger: "eye or lip tracking is on" (Face Tracking on, and Eye Tracking or Lip Tracking on) or "lip tracking is on". It depends only on the menu toggles and does not check whether a tracker is sending data, so turn Face Tracking off when you are not using a tracker
- How FaceEmo is held: FaceEmo keeps running, and the suppression writes expression lock, blink stop and a switch to the "Emote Number" (expression control). Because FaceEmo keeps writing, the base avatar's expression layers stay covered as usual and the menu can still switch expressions while tracking. FaceEmo's default face writes the same blend shapes as tracking, so register the tracking blend shapes in FaceEmo's Excluded Blend Shapes. The "Bypass" method of FE-JsT Bridge, which stops FaceEmo entirely, is not provided
- Extra parameters: parameters written only while face tracking is active. Specify them with an MA Menu Item or a parameter name, and choose whether to restore the previous value, write a release value, or do nothing on release
- FX layers to remove: removes FX layers with the given names from the Descriptor at build time. The base avatar's assets are not modified

"Tracking Control reapply" and "warning on shapes shared by FaceEmo and tracking" from FE-JsT Bridge will be considered after checking whether native Face Tracking needs them on the VRChat beta client.

### When no tracking data arrives

Whether face shapes go back to 0 or stay at the last value when the tracker stops or VRCFaceTracking exits is decided by VRChat. This has not been checked on the beta client yet.
Turn "Face Tracking" off in the Expressions Menu while you are not using a tracker. The Set weights become 0 and FaceEmo suppression is released.
If you often use the avatar without a tracker, you can also set the toggle's default value to off.

### Estimate

The inspector's "Estimate" shows the Sets created by the split, Parameter Behaviors, Custom expression instructions, Face Tracking Operations, synced bits and FX layers.
With the ARKit version, the Generator's Set exists only during the build and is not included in the estimate.

### Differences from Jerry's

| Item | Jerry's 7.0.5 | This package |
| --- | --- | --- |
| Input | VRCFT over OSC (`FT/v2/*`) | VRChat's native Face Tracking |
| Pupil default | Off | On |
| How emulation combines | Added to the tracked value | Used as a lower bound of the tracked value |
| Limit trees not wired up (three, including Limit Mouth Closed (JawOpen)) | Have no effect | Not ported |
| Smoothing | Yes | No (left to VRChat's interpolation) |
| Sensitivity | Seven continuous values | Two channels (eyes, mouth) in 8 steps, not created by default |

The `KxNFTM/` prefix is provisional and will be finalized before 1.0.
Menu labels are in English, as in Jerry's.

## Documentation (Japanese)

- [Installation and updates](Documentation~/install.md)
- [Development and running tests](docs/development.md)
- [Running tests in CI](docs/ci.md)
- [Distribution packages](docs/packaging.md)

## Specification

The specification and the overall plan are in [issue #1](https://github.com/limit7412/VRCNativeFTManager/issues/1) (Japanese).

## License

[MIT License](LICENSE)

The correction coefficients are ported from Jerry's Face Tracking Templates (MIT License, Copyright (c) 2023 Adjerry91). The notice is in [THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md).
