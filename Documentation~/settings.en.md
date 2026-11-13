# Settings

日本語: [settings.md](settings.md)

This page describes the items in the inspector of Kx NativeFT Manager, in the order they appear. The language switcher at the top of the inspector selects Japanese or English.

## Status and warnings

The top of the inspector shows the number of Blendshapes Sets on the Descriptor and the number of assigned keys, the state of Use All Tracking Keys, and whether Kx VRC ARKit BlendShape Generator is present and its Set output is enabled.
Below that, errors, warnings and notices about the settings or the avatar appear with numbers starting with `KxNFTM-`. Each message states the cause and what to do, and the same messages appear in the NDMF console at build time.

## Auto-set Shape Keys

| Item | Default | Meaning |
| --- | --- | --- |
| Auto-set Shape Keys | On | Assigns the shape keys to a Blendshapes Set automatically at build time. When the avatar has a Kx VRC ARKit BlendShape Generator component, its Set output is enabled during the build only (the Generator's settings in the scene are not changed). Otherwise, only when the Descriptor has no Set, a Set is built from the ARKit- or UE-named shapes of the face mesh with the Generator's mapping table. Grayed out with a warning (KxNFTM-W-20) when the Generator is not installed in the project |
| Convert ARKit to UE | On | When the avatar has no Generator component, adds Unified Expressions shape keys converted from the ARKit shape keys of the face mesh during the build only, before the Set is built. Keys that UE splits into left/right or upper/lower, such as browInnerUp, cheekPuff, mouthPress, mouthFunnel and mouthLeft/Right, then move separately. The original mesh is not changed. When the avatar has a Generator component, the output naming of the Generator is switched from ARKit to Both during the build only, so that the Generator makes the UE shape keys and the Set (the Generator settings in the scene are not changed, and an output naming other than ARKit is used as it is). Grayed out when Auto-set Shape Keys is off, and when the Generator is not installed or does not have the conversion |

In the booth version, when the Generator is not imported yet, a Download & Import VRC ARKit BlendShape Generator button appears at the top of this section. It downloads the latest stable release of the Generator from GitHub and imports it. After the import, the settings in this section become available.

For avatars without ARKit shape keys, press the Create Shape Keys from MMD Shape Keys (Attach Plugin) button. It creates a "Kx ARKit BlendShape Generator" object under the avatar root and adds a Generator component to it, and at build time the Generator creates ARKit shape keys from the MMD shape keys. Auto-set Shape Keys then assigns those shape keys to the Set. The button is hidden while the avatar has a Generator component, and grayed out when the Generator is not installed in the project. Adjust the Generator's own settings in the inspector of the added component. The created object can be moved anywhere inside the avatar. With an older Generator that only works on the avatar root, the component is added to the root itself instead of a new object.

When the avatar has no Generator component and the Descriptor already has Sets, those Sets are used as they are. Prepare them with Auto Fill or by hand. Avatars that already have ARKit shapes do not need Auto Fill, because a Set is built during the build when the Descriptor has none.

## Default Settings

A component created from the right-click menu or added with Add Component starts with the following settings.

| Target | Default |
| --- | --- |
| Toggles in the menu | All eight (only Eye Sync defaults to off) |
| Corrections | Emulation, Limits and Eye Sync are all generated |

ARKit shapes have no pupils, so the Pupil toggle has no Set to switch and is removed from the menu at build time (KxNFTM-I-05). If you do not want to keep it in the hierarchy, delete the Pupil Menu Item. To reduce synced bits, delete the Menu Items of toggles you do not need and turn off the corrections.

## Expressions Menu

There are two ways the Expressions Menu is made. A component created from the right-click menu uses "Menu Items in the hierarchy".

| Way | When | Where the toggle layout is decided |
| --- | --- | --- |
| Menu Items in the hierarchy | Created from the right-click menu, or after pressing "Expand the menu into the hierarchy" | The MA Menu Items under the component |
| Generated at build time | Added with Add Component | The values stored in the component (the default settings right after Add Component). They cannot be changed in the inspector; to change them, convert to Menu Items with the expand button |

With Menu Items in the hierarchy, the following hierarchy is created. As with Jerry's, you can reorder the items, change their labels and icons, and move them to another submenu or elsewhere on the avatar.

```
Face Tracking            MA Menu Installer + Menu Item (submenu)
├ Face Tracking          Toggle  KxNFTM/FaceTracking
├ Eye Tracking           Toggle  KxNFTM/EyeTracking
├ Lip Tracking           Toggle  KxNFTM/LipTracking
├ Pupil                  Toggle  KxNFTM/Pupil
├ Visemes                Toggle  KxNFTM/Visemes
├ Sensitivity            Submenu (only after adding a sensitivity Radial)
│ ├ Eye                  Radial  KxNFTM/Sensitivity/Eye
│ └ Lip                  Radial  KxNFTM/Sensitivity/Lip
└ Settings               Submenu
  ├ Emulation            Toggle  KxNFTM/Emulation
  ├ Limits               Toggle  KxNFTM/Limits
  └ Eye Sync             Toggle  KxNFTM/EyeSync
```

The object names are always the English names above, whatever the editor language. The label field of each Menu Item stays empty, so the object name is shown in the Expressions Menu. To rename an item, change its object name or fill in the label of the Menu Item.

Each item gets a bundled default icon (an eye, lips, a gear and so on). Replace the Icon of a Menu Item to use your own. Pressing "Fill empty icons and restore the default English names" under "Toggles" in the inspector fills in the default icon only for items whose icon is empty, and keeps icons you have set. The second half of the button name restores the default English names that an older version gave in Japanese in a Japanese editor; names you have set yourself are not changed. Menus generated at build time get the same default icons.

At build time, the MA Menu Items on the avatar whose parameter name (after the renames of MA Parameters) matches a public `KxNFTM/` name are read.

- A toggle with a Menu Item appears in the menu and gets a synced Bool parameter (1 bit each). Its default follows the Default setting of the Menu Item, and saving follows its Saved setting. Syncing is always on regardless of the setting, because remote clients decide the Set weights with their own animators
- A toggle whose Menu Item is deleted stays out of the menu and has no parameter. It keeps the default value stored in the component (the default settings right after creating from the right-click menu; after migrating with the expand button, the default from before migration)
- The value of a toggle Menu Item must be 1 (or use the automatic value). Menu Items with another value, and Menu Items that use the automatic value while the same parameter has several items, are not read, with a warning (KxNFTM-W-19)
- When no Menu Item points to a public name, a warning (KxNFTM-W-18) appears
- Menu Items whose parameter name differs from the public names are treated as your own and are not read

A component whose menu is generated at build time shows an "Expand the menu into the hierarchy" button under "Toggles" in the inspector. Pressing it creates Menu Items that match the current toggle and sensitivity settings, and from then on the menu is edited with the Menu Items (Undo restores it).

## Toggles

The toggles in the Expressions Menu are decided by the Menu Items in the hierarchy. The inspector has no table of toggles. To bring back a toggle whose Menu Item was deleted, press "Restore deleted toggle Menu Items" under "Toggles" in the inspector. It recreates only the Menu Items of deleted toggles, with the value the toggle had while its Menu Item was missing as the default, and leaves existing Menu Items unchanged (Undo restores them). Toggles of corrections that are not generated are not recreated. You can also duplicate a Menu Item and set its parameter name to the public name.

| Toggle | What it switches in the menu |
| --- | --- |
| Face Tracking | Face tracking as a whole. Turning it off sets the weight of every Set (including the neck, NeckFlex) to 0 and also releases FaceEmo suppression |
| Eye Tracking | The Sets of the eyes, eyelids, pupils and brows |
| Lip Tracking | The mouth Set (including cheeks and tongue) |
| Pupil | The pupil Set. It has no effect while Eye Tracking is off |
| Visemes | Visemes (mouth movement from voice). Turning it off stops visemes only while face tracking moves the mouth (Face Tracking and Lip Tracking on). The mouth tracked by the camera keeps moving. Visemes are stopped by limiting their shapes (the viseme mapping of the Sets and the Lip Sync shapes) to 0, so shapes that a tracking key also uses are left out. On avatars with no such shape, the toggle is removed from the menu |
| Emulation | The emulation correction. Appears under "Settings" in the Expressions Menu |
| Limits | The limits correction. Appears under "Settings" |
| Eye Sync | The correction that keeps both eyelids together. Appears under "Settings" |

While the matching correction is not generated (see "Expression Corrections" below), a Menu Item left for Emulation, Limits or Eye Sync is removed from the menu at build time (KxNFTM-I-05). The inspector also shows a notice under "Toggles".

"Restore Eye and Mouth Tracking During FT" below the toggles (on for a newly added component) keeps the tracked gaze and mouth moving even when Tracking Controls in the avatar's FX or FaceEmo expressions set the eyes or mouth to Animation. While face tracking is in use (the eyes with Face Tracking and Eye Tracking on, the mouth with Face Tracking and Lip Tracking on), they are set back to Tracking every 0.5 seconds. Parts that face tracking does not use are left to the avatar.

## Expression Corrections

The corrections in the FX of Jerry's 7.0.5 are generated as Parameter Behaviors (Custom expressions) on the Descriptor. When a correction is off, neither the correction nor its menu toggle is generated.

| Item | Default | Meaning |
| --- | --- | --- |
| Emulation | On | Adds movements such as raising cheeks and dimples when smiling, moving brows with the mouth, and narrowing pupils when the eyes widen. Written as a lower bound that never goes below the tracked value |
| Limits | On | Puts upper bounds on combinations that break when moved together, such as not raising the upper lip too far while the lips pucker |
| Eye Sync | On (the menu default is off) | Writes the average of both eyelids to both eyes |
| Rules | All checked | A foldout listing each correction rule. Unchecked rules are not generated. Rules whose target shape is not in any Set show "No target shape" and are not generated at build time either |

When the avatar-wide Custom expression instructions would exceed the limit of 2048, the rules with the lowest priority are dropped with a warning (KxNFTM-W-12).

### Custom corrections

A face mesh can have blend shapes that cannot be assigned to a tracking key one to one: a corrective shape that fixes the clipping when the tongue is out with the mouth open, or shapes that are raised in steps as the tongue comes out.
A Blendshapes Set only ties one blend shape to one key, so a Set cannot drive these shapes.
When you write a blend shape name and a Custom expression in the custom corrections list, a Parameter Behavior that drives the blend shape with the expression is generated at build time.

| Item | Meaning |
| --- | --- |
| Checkbox | Unchecked entries are not generated |
| Blend shape | Name of the blend shape to drive. Write the exact, case-sensitive name of a blend shape on a Face Tracking Mesh |
| Mode | "Write value" writes the result of the expression to the blend shape as it is. "Limit maximum" and "Limit minimum" bound the final value a Set produced with the result of the expression. Use "Write value" for a blend shape that no Set drives |
| Expression | Custom expression of a Parameter Behavior. Inputs are Unified Expressions key names (`JawOpen`, `TongueOut`, and so on), `Voice`, and visemes (`Viseme_aa`, and so on). The operators are `+` `-` `*` `/` and parentheses, and the functions are `abs` `min` `max` `clamp`. A result of 0 to 1 corresponds to 0 to 100 on the blend shape |

Expressions can be built by combining the following forms.

| Purpose | Example | Behavior |
| --- | --- | --- |
| Raise a shape only while two keys are active together | `JawOpen * clamp(TongueOut * 5 - 1, 0, 1)` | Rises while the tongue moves from 0.2 to 0.4, then follows how far the mouth is open |
| Raise a shape from the middle of one key | `clamp(TongueOut * 2 - 1, 0, 1)` | Starts when the tongue is halfway out and reaches 1 when it is fully out |
| Make a shape rise quickly | `clamp(TongueOut * 5 - 1.5, 0, 1)` | Goes from 0 to 1 while the tongue moves from 0.3 to 0.5 |

Under each entry, the inspector shows the instruction count of the expression, or the reason the entry is not generated.
Entries with an empty blend shape name or an invalid expression are not generated, with a warning (KxNFTM-W-23). Entries whose blend shape is not on a Face Tracking Mesh at build time are not generated either, with a warning (KxNFTM-W-22).

A custom correction stops while the Face Tracking toggle is off. When the expression reads eye or mouth keys, it also stops while the toggle of that side (Eye Tracking, Pupil, Lip Tracking) is off. It does not follow the Emulation, Limits, or Eye Sync toggles.

When the Custom expression instructions would exceed the limit, the custom corrections are kept and the built-in rules are dropped first.

## Sensitivity

Adds "Sensitivity" under "Face Tracking" in the Expressions Menu, with Radials that change how strongly the eyes and the mouth move.
With Menu Items in the hierarchy, "Add Radial" and "Remove Radial" buttons replace the eye and lip sensitivity settings below, and whether a Radial Menu Item exists decides the channel. Saving the value follows the Saved setting of the Radial Menu Item.

| Item | Default | Meaning |
| --- | --- | --- |
| Eye Sensitivity | Off | Adds a Radial for the Sets of the eyes, eyelids, pupils and brows, with the synced parameter `KxNFTM/Sensitivity/Eye` (Float, 8 bits) |
| Lip Sensitivity | Off | Adds a Radial for the Sets of the mouth, cheeks, tongue and visemes, with the synced parameter `KxNFTM/Sensitivity/Lip` (Float, 8 bits) |
| Max Multiplier | 2 | The strength with the Radial turned all the way right (1 to 4). Shown only when eye or lip sensitivity is on |
| Saved | On | Keep the Radial value across worlds. Shown only when eye or lip sensitivity is on |

The center of the Radial (the default) is 1x, and the strength changes in 8 steps: 0.2x to 0.8x on the left, and 1x up to the max multiplier on the right.
The neck (NeckFlex) and the corrections are not affected. Turning a group off with its toggle stops it regardless of the sensitivity.
Why the strength changes only in steps is explained in [What happens at build time](build.en.md#how-sensitivity-works).

## Avatar Descriptor (at build time)

These values are written to the Descriptor of the build copy. The avatar in the scene is not modified. Face Tracking itself is enabled on the Descriptor regardless of these settings.

| Item | Default | Meaning |
| --- | --- | --- |
| Enable Use All Tracking Keys | On | Enables Use All Tracking Keys on the Descriptor. Without it, only the 10 basic eye keys are tracked (KxNFTM-W-05). When off, the Descriptor's value is left unchanged |
| Viseme Dampen | DoNotChange | The Viseme Dampen mode of the Descriptor. DoNotChange keeps the Descriptor's value. None, DampenTracking and DampenVisemes write the Descriptor's mode of the same name |
| Amount | 0.5 | The amount written together with Viseme Dampen (0 to 1). Shown only for DampenTracking and DampenVisemes |

When Eye Look on the Descriptor is set up not to use eye face tracking (Eye Gaze Mode is Bone Rotation without eye bones, Eyelids Type is Blendshapes, and so on), it is set up for face tracking during the build as long as the Sets have eye keys (KxNFTM-I-07). The button under the inspector warning (KxNFTM-W-21) writes the same values to the Descriptor in the scene. The values written are listed in [What happens at build time](build.en.md).

## FaceEmo Suppression

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

## Estimate (before build)

Shows how much the build will generate, calculated from the current Descriptor. There is nothing to set here.

| Display | Meaning |
| --- | --- |
| Sets after splitting | The number of Sets after grouping, and the number of empty Sets (gates) used to switch the corrections |
| Parameter Behaviors | The number this package generates, and the number already present |
| Custom expression instructions | The avatar-wide count against the limit of 2048, and this package's share |
| Face Tracking Operations | Operations counted from the Sets and Parameter Behaviors (including existing ones). It follows how SDK 3.10.6 counts them, but existing Parameter Behaviors other than Custom have not been checked, so this is a rough guide |
| Synced bits | The avatar-wide synced bits against the limit, and this package's share. A warning (KxNFTM-W-13) appears when the limit is likely to be exceeded |
| FX layers | The number of FX layers this package generates |

When the Generator's Set is used, it exists only during the build and is not included in the estimate.

## Details

Items in the foldout that usually need no change.

| Item | Default | Meaning |
| --- | --- | --- |
| Lerp Speed | 8 | How fast the Set weight follows a toggle (0 to 20). 0 switches instantly |
| Sets not to split | Empty | Blendshapes Sets named here are kept as they are instead of being split into groups. They are not switched by the toggles and are not targets of the corrections |
