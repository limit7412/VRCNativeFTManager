# Requirements and companion packages

日本語: [requirements.md](requirements.md)

## Requirements

- Unity 2022.3
- VRChat SDK Avatars 3.10.6 or later
- NDMF 1.6.0 or later
- Modular Avatar 1.12.0 or later
- Kx VRC ARKit BlendShape Generator 0.2.3 or later

## SDK version

Native Face Tracking became stable in VRChat SDK 3.10.6. Use the stable SDK 3.10.6 or later. The 3.10.6 betas (3.10.6-beta.N) count as older than 3.10.6, so this package cannot be added with them.

Kx VRC ARKit BlendShape Generator, which this package depends on, is supported from the stable 0.2.3, so you do not need to show pre-release packages. When you add this package, VCC/ALCOM installs the Generator together with it.

## Kx VRC ARKit BlendShape Generator

The Generator is a required dependency.
The booth zip contains the unitypackages of both this package and the Generator (see [Installation and updates](install.md), Japanese).

The booth version does not go through the dependency resolution of VCC/ALCOM, so the Generator unitypackage may be left unimported.
This package looks up the Generator's types by reflection, so it still compiles and works in a project without the Generator.
In that case, it shows a warning (KxNFTM-W-20), grays out "Auto-set Shape Keys" and "Convert ARKit to UE" in the inspector, and splits only the Blendshapes Sets on the Descriptor.
The Generator row of the status shows "Not installed".
Once the Generator is imported, the integration works with the same settings.

Even when the avatar has no Generator component, the mapping table of Generator 0.2.3 or later in the project is used.
When the Descriptor has no Blendshapes Set, a Set that assigns the ARKit- or UE-named shapes of the Face Tracking Meshes (or the lip sync mesh when that list is empty) to the keys is built during the build and then split.
This saves pressing Auto Fill on avatars that already have ARKit shapes.
No Set is built when the Descriptor has any Set, when the avatar has a Generator component, or when "Auto-set Shape Keys" in the inspector is off.

When "Convert ARKit to UE" in the inspector is on (the default), Unified Expressions shape keys converted from the ARKit shape keys of the same meshes are added to the build copy before the Set is built.
The conversion is the same as the Generator's UE output naming, so keys that UE splits into left/right or upper/lower, such as browInnerUp and cheekPuff, each get their own split shape.
It needs a Generator version that publishes the conversion. With an older version, nothing is converted and the ARKit-named shapes are assigned as they are.
An old Generator that lacks the field needed for the integration is treated as not installed.

Whether to use the Generator is up to each avatar.
If the avatar has no Generator component, the integration does nothing and the Blendshapes Sets on the Descriptor are used as they are.

When "Auto-set Shape Keys" in the inspector is on and the avatar has a Generator component, the Generator's Set output is enabled during the build only.
It is on by default, including for a Manager created from the context menu.
The Generator's Set is created only during the build, so it is not included in the inspector's estimate.

## Packages integrated only when installed

The following packages are not dependencies. Integration is enabled only when they are installed.

- Avatar Optimizer 1.9.21 or later (when used together)

### Avatar Optimizer

Avatar Optimizer added native Face Tracking support in 1.9.21 and keeps the shapes used by Blendshapes Sets and Parameter Behaviors from being optimized away.
This package relies on that support and has no Avatar Optimizer workaround of its own.
Versions older than 1.9.21 (including the 1.9.21 pre-releases), and SDKs too old for Avatar Optimizer's support (before 3.10.6-beta.4), produce warnings at build time (KxNFTM-W-14, KxNFTM-W-15).

If you have to stay on an older Avatar Optimizer, add the face mesh to "Exclusions" under "Debug Options" of Trace and Optimize. Its shapes are then neither removed nor merged.
The mesh is no longer optimized at all, so use this only until you can update Avatar Optimizer.
