# Limitations of the experimental release

日本語: [limitations.md](limitations.md)

This package is an experimental 0.x release. Native Face Tracking became stable in VRChat SDK 3.10.6, but this package stays experimental until version 1.0, which will follow checks of the items below on the VRChat client.

## What works in the current version

Blendshapes Set grouping, toggles for face tracking, eyes, mouth, pupils and visemes, expression corrections (emulation, limits, Eye Sync), sensitivity, and FaceEmo suppression.

## Still being checked on the VRChat client

- Whether the State Behavior used by the toggles and the empty Sets (gates) used to switch the corrections work as intended. If the State Behavior type cannot be found, the build reports a warning (KxNFTM-E-05) and only the toggles and the sensitivity stop working
- What face shapes do when no tracking data arrives (see the section below)
- Whether native Face Tracking needs "Tracking Control reapply" and "warning on shapes shared by FaceEmo and tracking" from FE-JsT Bridge. They will be considered after this is checked

## Parameter names may change

Parameter names (those starting with `KxNFTM/`) may change before 1.0. If they do, saved toggle values go back to their defaults.

## When no tracking data arrives

Whether face shapes go back to 0 or stay at the last value when the tracker stops or VRCFaceTracking exits is decided by VRChat. This has not been checked on the VRChat client yet.
Turn "Face Tracking" off in the Expressions Menu while you are not using a tracker. The Set weights become 0 and FaceEmo suppression is released.
If you often use the avatar without a tracker, you can also set the toggle's default value to off.
