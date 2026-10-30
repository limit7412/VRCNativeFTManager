# Kx VRC NativeFT Manager

An NDMF plugin that brings Jerry's Templates style toggles and expression corrections, operated from the Expressions Menu, to avatars that use VRChat's native Face Tracking.
Add it to your avatar from the Hierarchy context menu and it works. It integrates with [Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator).
It also suppresses FaceEmo while face tracking is active. It does not integrate with [Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge), and the two should not be used on the same avatar.

日本語: [README.md](README.md)

> [!WARNING]
> **This is an experimental release (0.x).** Native Face Tracking is a feature of the VRChat SDK beta and may change until the SDK is released. This package stays experimental until version 1.0, which will follow the SDK release.
> Parameter names (those starting with `KxNFTM/`) may change before 1.0. If they do, saved toggle values go back to their defaults.
> What is still being checked on the VRChat beta client is listed in [Limitations of the experimental release](Documentation~/limitations.en.md).

## Requirements

- Unity 2022.3
- VRChat SDK Avatars 3.10.6-beta.2 or later (a pre-release; 3.10.6-beta.3 or later if you use Avatar Optimizer)
- NDMF 1.6.0 or later
- Modular Avatar 1.12.0 or later

How to install the pre-release SDK, and the conditions for using Kx VRC ARKit BlendShape Generator and Avatar Optimizer together, are in [Requirements and companion packages](Documentation~/requirements.en.md).

## Installation

1. Add the [VPM repository](https://limit7412.github.io/vcc-vpm/) to VCC/ALCOM
2. Add "Kx VRC NativeFT Manager" to your project

Manual installation from the zip, the booth version (unitypackage), and how updates are announced and applied are described in [Installation and updates](Documentation~/install.md) (Japanese).

## Usage

1. Right-click the avatar in the Hierarchy and choose one of the following under "Kx VRC NativeFT Manager". An object with the component is created directly under the avatar. Adding the component to an existing object (including the avatar root) with "Add Component" works the same way
   - Generic (`Kx NativeFT Manager`): for avatars whose Blendshapes Sets on the Descriptor are prepared with Auto Fill or by hand
   - ARKit (`Kx NativeFT Manager (ARKit)`): for avatars that use Kx VRC ARKit BlendShape Generator. The Generator's Set output is enabled during the build only
2. Choose a preset in the inspector, and change individual settings as described in "Settings" below if needed
3. Upload. Use "Face Tracking" in the Expressions Menu to switch Face Tracking, Eye Tracking, Lip Tracking, Pupil and Visemes. The corrections (Emulation, Limits, Eye Sync) are in "Settings" below it

Jerry's Face Tracking Templates cannot be on the same avatar. If it is still there, the inspector and the build report an error (KxNFTM-E-04), so remove its prefab before uploading.
The differences from Jerry's are in [Migrating from Jerry's](Documentation~/jerrys.en.md), and what the build adds to the avatar is in [What happens at build time](Documentation~/build.en.md).

## Settings

The settings are listed in the order they appear in the inspector. The language switcher at the top of the inspector selects Japanese or English.

### Status and warnings

The top of the inspector shows the number of Blendshapes Sets on the Descriptor and the number of assigned keys, the state of Use All Tracking Keys, and whether Kx VRC ARKit BlendShape Generator is present and its Set output is enabled.
Below that, errors, warnings and notices about the settings or the avatar appear with numbers starting with `KxNFTM-`. Each message states the cause and what to do, and the same messages appear in the NDMF console at build time.

### Preset and Generator Integration

| Item | Default | Meaning |
| --- | --- | --- |
| Preset | UE for the generic version, ARKit for the ARKit version | A combination of the toggle layout and which corrections to generate. Choosing a preset rewrites the toggles and the correction settings. Changing a toggle or a correction away from the preset switches it to Custom |
| Generator Integration | Off for the generic version, Auto for the ARKit version | With Auto, when the avatar has Kx VRC ARKit BlendShape Generator, its Set output is enabled during the build only. The Generator's settings in the scene are not changed. Off does nothing |

The presets contain the following.

| Preset | Toggles in the menu | Corrections | Intended avatars |
| --- | --- | --- | --- |
| UE | All eight (Eye Sync defaults to off) | Emulation, Limits and Eye Sync are generated | Avatars with UE (Unified Expressions) shapes. Equivalent to the UE version of Jerry's |
| ARKit | The seven other than Pupil (Eye Sync defaults to off) | Same as UE | Avatars that use Kx VRC ARKit BlendShape Generator. ARKit shapes have no pupils |
| Minimal | Face Tracking, Eye Tracking and Lip Tracking | None | When you want the fewest synced bits and menu entries |
| Custom | Whatever you set | Whatever you set | Selected automatically when any of the above is changed |

### Toggles

The table decides which toggles appear in the Expressions Menu.

| Toggle | What it switches in the menu |
| --- | --- |
| Face Tracking | Face tracking as a whole. Turning it off sets the weight of every Set (including the neck, NeckFlex) to 0 and also releases FaceEmo suppression |
| Eye Tracking | The Sets of the eyes, eyelids, pupils and brows |
| Lip Tracking | The mouth Set (including cheeks and tongue) |
| Pupil | The pupil Set. It has no effect while Eye Tracking is off |
| Visemes | Visemes (mouth movement from voice). Turning it off sets Mouth & Jaw to Animation with Tracking Control in FX, which stops visemes |
| Emulation | The emulation correction. Appears under "Settings" in the Expressions Menu |
| Limits | The limits correction. Appears under "Settings" |
| Eye Sync | The correction that keeps both eyelids together. Appears under "Settings" |

The columns mean the following.

| Column | Meaning |
| --- | --- |
| Menu | Show the toggle in the Expressions Menu and create a synced Bool parameter for it (such as `KxNFTM/FaceTracking`, 1 bit each). A toggle not in the menu has no parameter and always stays at its default |
| Default | The value of the toggle when the avatar is worn. For a toggle not in the menu, this is its fixed value |
| Saved | Keep the value across worlds. Not available for a toggle that is not in the menu |

The Emulation, Limits and Eye Sync rows are grayed out while the matching correction is not generated (see "Expression Corrections" below).
A component saved with an older version may lack some toggles. The inspector then shows a notice and an "Add the toggles to the table" button. A toggle missing from the table stays out of the menu and behaves as in earlier versions.

### Expression Corrections

The corrections in the FX of Jerry's 7.0.5 are generated as Parameter Behaviors (Custom expressions) on the Descriptor. When a correction is off, neither the correction nor its menu toggle is generated.

| Item | Default for UE and ARKit | Meaning |
| --- | --- | --- |
| Emulation | On | Adds movements such as raising cheeks and dimples when smiling, moving brows with the mouth, and narrowing pupils when the eyes widen. Written as a lower bound that never goes below the tracked value |
| Limits | On | Puts upper bounds on combinations that break when moved together, such as not raising the upper lip too far while the lips pucker |
| Eye Sync | On (the menu default is off) | Writes the average of both eyelids to both eyes |
| Rules | All checked | A foldout listing each correction rule. Unchecked rules are not generated, and unchecking one switches the preset to Custom. Rules whose target shape is not in any Set show "No target shape" and are not generated at build time either |

When the avatar-wide Custom expression instructions would exceed the limit of 512, the rules with the lowest priority are dropped with a warning (KxNFTM-W-12).

### Sensitivity

Adds "Sensitivity" under "Face Tracking" in the Expressions Menu, with Radials that change how strongly the eyes and the mouth move. Sensitivity is not part of the presets, so changing it does not switch the preset to Custom.

| Item | Default | Meaning |
| --- | --- | --- |
| Eye Sensitivity | Off | Adds a Radial for the Sets of the eyes, eyelids, pupils and brows, with the synced parameter `KxNFTM/Sensitivity/Eye` (Float, 8 bits) |
| Lip Sensitivity | Off | Adds a Radial for the Sets of the mouth, cheeks, tongue and visemes, with the synced parameter `KxNFTM/Sensitivity/Lip` (Float, 8 bits) |
| Max Multiplier | 2 | The strength with the Radial turned all the way right (1 to 4). Shown only when eye or lip sensitivity is on |
| Saved | On | Keep the Radial value across worlds. Shown only when eye or lip sensitivity is on |

The center of the Radial (the default) is 1x, and the strength changes in 8 steps: 0.2x to 0.8x on the left, and 1x up to the max multiplier on the right.
The neck (NeckFlex) and the corrections are not affected. Turning a group off with its toggle stops it regardless of the sensitivity.
Why the strength changes only in steps is explained in [What happens at build time](Documentation~/build.en.md#how-sensitivity-works).

### Avatar Descriptor (at build time)

These values are written to the Descriptor of the build copy. The avatar in the scene is not modified. Face Tracking itself is enabled on the Descriptor regardless of these settings.

| Item | Default | Meaning |
| --- | --- | --- |
| Enable Use All Tracking Keys | On | Enables Use All Tracking Keys on the Descriptor. Without it, only the 10 basic eye keys are tracked (KxNFTM-W-05). When off, the Descriptor's value is left unchanged |
| Viseme Dampen | DoNotChange | The Viseme Dampen mode of the Descriptor. DoNotChange keeps the Descriptor's value. None, DampenTracking and DampenVisemes write the Descriptor's mode of the same name |
| Amount | 0.5 | The amount written together with Viseme Dampen (0 to 1). Shown only for DampenTracking and DampenVisemes |

### FaceEmo Suppression

On avatars that use FaceEmo, this keeps FaceEmo's expressions and blinking from writing face shapes while face tracking is active.
When FaceEmo is present but suppression is off, the inspector shows a notice (KxNFTM-I-03). The items other than "Suppress FaceEmo" appear only when it is on.

| Item | Default | Meaning |
| --- | --- | --- |
| Suppress FaceEmo | Off | Generates the suppression layers. When off, the extra parameters and the FX layers to remove are not used either |
| Emote Number | 0 | The FaceEmo expression number to switch to while tracking. It is the same number the FaceEmo expression select menu writes, counted 0, 1, 2… from the first expression pattern |
| Trigger | Eye or lip tracking is on | "Eye or lip tracking is on" means Face Tracking is on and either Eye Tracking or Lip Tracking is on. "Lip tracking is on" means only when Face Tracking and Lip Tracking are on, so FaceEmo's blinking remains with eye tracking alone |
| Extra Parameters | Empty | Parameters written only while face tracking is active (such as expression toggles made with MA). See the table below for each entry |
| FX Layers To Remove | Empty | Names of FX layers removed at build time. List the avatar's own layers that conflict with tracking, such as blinking or Tracking Control switching. The avatar's assets are not modified. Open the built FX in the Animator window to find the names |

The suppression keeps FaceEmo running and writes expression lock, blink stop and an expression switch.
Because FaceEmo keeps writing, the avatar's own expression layers stay covered as usual and the menu can still switch expressions while tracking.
FaceEmo's default face writes the same shapes as tracking, so register the tracking shapes in FaceEmo's Excluded Blend Shapes.
The suppression depends only on the menu toggles and does not check whether a tracker is sending data. Turn Face Tracking off in the Expressions Menu when you are not using a tracker.

Each entry of the extra parameters has the following items.

| Item | Default | Meaning |
| --- | --- | --- |
| Source | MA Menu Item | Whether the target is given as an MA Menu Item or as a parameter name |
| Menu Item | None | For MA Menu Item. A toggle or button MA Menu Item. Its parameter name and value are read at build time |
| State While Tracking | Off | For MA Menu Item. Whether the menu item is On or Off while tracking |
| Parameter Name | Empty | For Parameter Name. The name used in FX after Modular Avatar renames it |
| Type | Bool | For Parameter Name. Bool, Int or Float |
| Value While Tracking | 0 | For Parameter Name. The value written while tracking |
| On Release | Restore Previous Value | When face tracking is turned off: restore the previous value, write the release value, or do nothing |
| Release Value | 1 | The value written when On Release is Write Release Value. For an MA Menu Item, the item is switched to the opposite of its state while tracking |
| Sync | Auto | Synced parameters are written only by the wearer's client, once at each switch. Parameters that are not synced are written on every client and rewritten about every second while tracking. Auto decides from Expression Parameters, MA Parameters and MA Menu Item |

### Estimate (before build)

Shows how much the build will generate, calculated from the current Descriptor. There is nothing to set here.

| Display | Meaning |
| --- | --- |
| Sets after splitting | The number of Sets after grouping, and the number of empty Sets (gates) used to switch the corrections |
| Parameter Behaviors | The number this package generates, and the number already present |
| Custom expression instructions | The avatar-wide count against the limit of 512, and this package's share |
| Face Tracking Operations | Operations counted from the Sets and Parameter Behaviors (including existing ones). VRChat does not publish every detail of how they are counted, so this is a rough guide |
| Synced bits | The avatar-wide synced bits against the limit, and this package's share. A warning (KxNFTM-W-13) appears when the limit is likely to be exceeded |
| FX layers | The number of FX layers this package generates |

With the ARKit version, the Generator's Set exists only during the build and is not included in the estimate.

### Details

Items in the foldout that usually need no change.

| Item | Default | Meaning |
| --- | --- | --- |
| Lerp Speed | 8 | How fast the Set weight follows a toggle (0 to 20). 0 switches instantly |
| Sets not to split | Empty | Blendshapes Sets named here are kept as they are instead of being split into groups. They are not switched by the toggles and are not targets of the corrections |

## Documentation

- [Requirements and companion packages](Documentation~/requirements.en.md)
- [Installation and updates](Documentation~/install.md) (Japanese)
- [What happens at build time](Documentation~/build.en.md)
- [Migrating from Jerry's](Documentation~/jerrys.en.md)
- [Limitations of the experimental release](Documentation~/limitations.en.md)

Information for developers (development environment and tests, CI, distribution packages) starts at [Development and running tests](docs/development.md) (Japanese).
The specification and the overall plan are in [issue #1](https://github.com/limit7412/VRCNativeFTManager/issues/1) (Japanese).

## License

[MIT License](LICENSE)

The correction coefficients are ported from Jerry's Face Tracking Templates (MIT License, Copyright (c) 2023 Adjerry91). The notice is in [THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md).
