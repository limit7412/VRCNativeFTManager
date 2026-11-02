# Kx VRC NativeFT Manager

An NDMF plugin that brings Jerry's Templates style toggles and expression corrections, operated from the Expressions Menu, to avatars that use VRChat's native Face Tracking.
Add it to your avatar from the Hierarchy context menu and it works. It depends on [Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator) and integrates with it when the avatar has one.
It also suppresses FaceEmo while face tracking is active. It does not integrate with [Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge), and the two should not be used on the same avatar.

日本語: [README.md](README.md)

## Requirements

- Unity 2022.3
- VRChat SDK Avatars 3.10.6 or later
- NDMF 1.6.0 or later
- Modular Avatar 1.12.0 or later
- Kx VRC ARKit BlendShape Generator 0.2.3 or later (VCC/ALCOM installs it together with this package. If the booth version is imported without it, this package still works and only the Generator integration stops)

How this package relates to the Generator, and the conditions for using Avatar Optimizer (1.9.21 or later) together, are in [Requirements and companion packages](Documentation~/requirements.en.md).

## Installation

1. Add the [VPM repository](https://limit7412.github.io/vcc-vpm/) to VCC/ALCOM
2. Add "Kx VRC NativeFT Manager" to your project

Manual installation from the zip, the booth version (unitypackage), and how updates are announced and applied are described in [Installation and updates](Documentation~/install.md) (Japanese).

## Usage

1. Right-click the avatar in the Hierarchy and choose `Kx NativeFT Manager` under "Kx VRC NativeFT Manager". An object with the component is created directly under the avatar, with the Expressions Menu "Face Tracking" (an MA Menu Installer and MA Menu Items) as its children
2. If needed, change the settings in the inspector and the Menu Items in the hierarchy. Each item is described in [Settings](Documentation~/settings.en.md)
3. Upload. Use "Face Tracking" in the Expressions Menu to switch Face Tracking, Eye Tracking, Lip Tracking, Pupil and Visemes. The corrections (Emulation, Limits, Eye Sync) are in "Settings" below it

A newly added component shows all the toggles in the menu and generates all the corrections (Emulation, Limits and Eye Sync).
The Blendshapes Sets on the Descriptor are written during the build by Kx VRC ARKit BlendShape Generator when the avatar has one. Otherwise the Sets on the Descriptor are used as they are, and when it has none, a Set is built during the build from the ARKit- or UE-named shapes of the face mesh. Avatars that already have ARKit shapes do not need Auto Fill.
The component can also be added to an existing object (including the avatar root) with "Add Component"; its menu is then generated at build time and cannot be edited in the hierarchy (see [Expressions Menu](Documentation~/settings.en.md#expressions-menu)).

Jerry's Face Tracking Templates cannot be on the same avatar. If it is still there, the inspector and the build report an error (KxNFTM-E-04), so remove its prefab before uploading.
The differences from Jerry's are in [Migrating from Jerry's](Documentation~/jerrys.en.md), and what the build adds to the avatar is in [What happens at build time](Documentation~/build.en.md).

## Documentation

- [Requirements and companion packages](Documentation~/requirements.en.md)
- [Installation and updates](Documentation~/install.md) (Japanese)
- [Settings](Documentation~/settings.en.md)
- [What happens at build time](Documentation~/build.en.md)
- [Migrating from Jerry's](Documentation~/jerrys.en.md)
- [Limitations and notes](Documentation~/limitations.en.md)

Information for developers (development environment and tests, CI, distribution packages) starts at [Development and running tests](docs/development.md) (Japanese).
The specification and the overall plan are in [issue #1](https://github.com/limit7412/VRCNativeFTManager/issues/1) (Japanese).

## License

[MIT License](LICENSE)

The correction coefficients are ported from Jerry's Face Tracking Templates (MIT License, Copyright (c) 2023 Adjerry91). The notice is in [THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md).
