# Migrating from Jerry's

日本語: [jerrys.md](jerrys.md)

## Jerry's cannot be on the same avatar

Jerry's Face Tracking Templates cannot be on the same avatar as this package. If it is still there, the inspector and the build report an error (KxNFTM-E-04).
This package does not remove Jerry's for you, so remove its prefab before uploading. To keep both a Jerry's version and a native version, upload them as separate avatars.

## Differences

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
