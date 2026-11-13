# What happens at build time

日本語: [build.md](build.md)

During the NDMF build, this package adds the following to the build copy of the avatar. The avatar in the scene and its assets are not modified.
The settings that turn each of these on are described in [Settings](settings.en.md).

- The Blendshapes Sets on the Descriptor are split by tracking key into Eye / EyeClosed / Pupil / Brow / Lip / Other (`KxNFTM/Eye` and so on). The original Sets are removed, and the total number of key-to-shape assignments stays the same. Sets listed in "Sets not to split" and Sets referenced by name from existing Parameter Behaviors or State Behaviors (KxNFTM-W-10) are kept as they are
- Each toggle shown in the menu gets a synced Bool parameter (and, when the menu is generated at build time, a menu entry; with Menu Items in the hierarchy, the default and saved settings are read from the Menu Item), and FX layers switch the Set weights between 0 and 1. The parameters are `KxNFTM/FaceTracking`, `KxNFTM/EyeTracking`, `KxNFTM/LipTracking`, `KxNFTM/Pupil`, `KxNFTM/Visemes`, `KxNFTM/Emulation`, `KxNFTM/Limits` and `KxNFTM/EyeSync`, 8 bits in total with the default settings
- A toggle with no Set to switch (such as Pupil on an avatar with ARKit shapes only) is removed from the Expressions Menu together with its synced parameter (KxNFTM-I-05). The same happens to a Menu Item left in the hierarchy for a correction that is not generated
- For each sensitivity channel that is enabled, the Set multipliers are raised to the max multiplier and the Set weight is written per Radial step (see "How sensitivity works" below)
- Turning Visemes off sets the gate Set `KxNFTM/Gate/Visemes` to 1 while face tracking and lip tracking are on, which enables LimitBlendshapeMax behaviors that limit the viseme shapes to 0. Tracking Control is not used, because setting Mouth & Jaw to Animation also stops the tracked mouth
- With "Restore Eye and Mouth Tracking During FT" on, the FX layer `KxNFTM TrackingReapply` sets the eyes back to Tracking while face and eye tracking are on, and the mouth while face and lip tracking are on, every 0.5 seconds with Tracking Control. This keeps Tracking Controls in the avatar's FX or FaceEmo from stopping the tracked gaze and mouth
- The corrections are added as Parameter Behaviors (Custom expressions) on the Descriptor. The coefficients come from the FX of Jerry's 7.0.5 (see [THIRD_PARTY_NOTICES](../Editor/THIRD_PARTY_NOTICES.md))
  - Emulation: raises cheeks and dimples when smiling, moves brows from the mouth, narrows pupils when the eyes widen, and so on. Written as a lower bound (Limit Min) that never goes below the tracked value
  - Limits: upper bounds (Limit Max) for combinations that break when moved together, such as holding down the mouth while the lips pucker
  - Eye Sync: writes the average of both eyelids to both eyes
  - The corrections are switched with the weight of empty Sets that have no assignments (`KxNFTM/Gate/...`). A rule also stops when the toggle of the keys it reads or writes (eyes, pupils, mouth) is turned off
  - Rules whose target shape is not in any Set are not generated (KxNFTM-I-01). When the Custom expression instructions exceed the avatar-wide limit of 2048, the rules with the lowest priority are dropped (KxNFTM-W-12)
  - Custom corrections: Parameter Behaviors are generated the same way from the blend shape names and Custom expressions written in the inspector list. They are placed before the built-in rules, so they are not the first to be dropped when the instructions exceed the limit. Entries with an invalid blend shape name or expression (KxNFTM-W-23), and entries whose blend shape is not on a Face Tracking Mesh (KxNFTM-W-22), are not generated
- When FaceEmo suppression is enabled, FX layers that control FaceEmo and the extra parameters are added, and the layers listed in "FX Layers To Remove" are removed from FX
- Face Tracking is enabled on the Descriptor. Use All Tracking Keys and Viseme Dampen are written as set in "Avatar Descriptor (at build time)" in the inspector
- When the Sets assign eye keys (EyeLook, EyeClosed) but Eye Look on the Descriptor is set up not to drive them with face tracking, Eye Look is set up for face tracking (KxNFTM-I-07). Eye Gaze Mode becomes Face Tracking when it is Bone Rotation and no eye bones are assigned, Eyelids Type becomes Face Tracking when it is None or Blendshapes, and Eye Look is enabled when it is off. Eye Gaze Mode with eye bones assigned and Eyelids Type set to Bones are kept. The inspector also shows a warning (KxNFTM-W-21) with a button that applies the same values to the Descriptor in the scene

## How sensitivity works

Jerry's has seven continuous sensitivities, but Parameter Behaviors of native Face Tracking cannot read animator parameters, so this package approximates sensitivity with the Set weight.
At build time the target Set multipliers are raised to the max multiplier, and each Radial step writes a Set weight that divides it back.
The strength therefore changes in steps rather than continuously, and only the multipliers of the original Sets are scaled.
Changing the sensitivity does not change how the corrections work.
