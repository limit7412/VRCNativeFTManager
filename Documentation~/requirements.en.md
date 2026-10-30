# Requirements and companion packages

日本語: [requirements.md](requirements.md)

## Requirements

- Unity 2022.3
- VRChat SDK Avatars 3.10.6-beta.2 or later (a pre-release; 3.10.6-beta.3 or later if you use Avatar Optimizer)
- NDMF 1.6.0 or later
- Modular Avatar 1.12.0 or later

## Installing the pre-release SDK

The SDK with native Face Tracking is still distributed as a pre-release. VCC and ALCOM hide pre-releases by default, so turn them on first.

1. In VCC, enable "Show Pre-Release Packages" under Settings > Packages. In ALCOM, enable showing pre-release packages in its settings
2. In the project's package list, change "VRChat SDK - Avatars" to 3.10.6-beta.2 or later (3.10.6-beta.3 or later if you use Avatar Optimizer)
3. Add this package. It depends on that SDK, so it cannot be added while the SDK is older

## Packages integrated only when installed

The following packages are not dependencies. Integration is enabled only when they are installed.

- Kx VRC ARKit BlendShape Generator
- Avatar Optimizer 1.9.21-beta.1 or later (when used together)

### Kx VRC ARKit BlendShape Generator

When "Generator Integration" in the inspector is Auto and the avatar has the Generator, the Generator's Set output is enabled during the build only.
The ARKit version (`Kx NativeFT Manager (ARKit)` in the context menu) is set to Auto from the start.
The Generator's Set is created only during the build, so it is not included in the inspector's estimate.

### Avatar Optimizer

Avatar Optimizer added native Face Tracking support in 1.9.21-beta.1 and keeps the shapes used by Blendshapes Sets and Parameter Behaviors from being optimized away.
This package relies on that support and has no Avatar Optimizer workaround of its own.
Avatar Optimizer's support is active only with VRChat SDK 3.10.6-beta.3 or later, so update the SDK as well when you use both.
Older combinations produce warnings at build time (KxNFTM-W-14, KxNFTM-W-15).

If you have to stay on an older Avatar Optimizer, add the face mesh to "Exclusions" under "Debug Options" of Trace and Optimize. Its shapes are then neither removed nor merged.
The mesh is no longer optimized at all, so use this only until you can update Avatar Optimizer.
