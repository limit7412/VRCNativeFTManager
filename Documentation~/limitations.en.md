# Limitations and notes

日本語: [limitations.md](limitations.md)

The features of this package (Blendshapes Set grouping, toggles for face tracking, eyes, mouth, pupils and visemes, expression corrections (emulation, limits, Eye Sync), sensitivity, and FaceEmo suppression) have been checked on the VRChat client.
The behaviors below are worth knowing when you use it.

## When animations and face tracking drive the same shape

While Face Tracking is on, the shapes in a Set are overwritten by the tracking values. Values that FX animations (such as hand gesture expressions) write to the same shapes are not visible.
Turning "Face Tracking" (or "Eye Tracking" / "Lip Tracking") off in the Expressions Menu sets that Set's weight to 0, and the animation values show again.
Shapes that are not in any Set (such as blush) keep showing the animation values during face tracking.

## Corrections that are not generated

A correction rule is generated only when the shape it writes to is in a Set (the build reports KxNFTM-I-01 otherwise). For example, on an avatar without an inner brow raise shape, the correction that makes sad brows from a frown has no effect.

## When no tracking data arrives

Whether face shapes go back to 0 or stay at the last value when the tracker stops or VRCFaceTracking exits is decided by VRChat. This has not been checked on the VRChat client yet.
Turn "Face Tracking" off in the Expressions Menu while you are not using a tracker. The Set weights become 0 and FaceEmo suppression is released.
If you often use the avatar without a tracker, you can also set the toggle's default value to off.
