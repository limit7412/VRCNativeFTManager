using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class EyeLookSetupTests
    {
        private static FaceTrackingSetData SetWith(params int[] keys)
        {
            var set = FaceTrackingSetData.CreateEmpty("Set", 1f);
            foreach (var key in keys)
            {
                set.BlendShapeNames[key] = "shape" + key;
            }

            return set;
        }

        private static readonly FaceTrackingSetData EyeSet = SetWith(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);

        private static EyeLookSettings Settings(
            bool enabled = true,
            EyeGazeMode gaze = EyeGazeMode.BoneRotation,
            EyelidType eyelid = EyelidType.Blendshapes,
            bool bones = false)
        {
            return new EyeLookSettings { Enabled = enabled, GazeMode = gaze, EyelidType = eyelid, HasEyeBones = bones };
        }

        [Test]
        public void Plan_BoneRotationWithoutBonesAndBlendshapeEyelids_SwitchesBothToFaceTracking()
        {
            // 2026-11-01の実機報告（ARKitのみ、Generator未アタッチ、目のボーン未割り当て）と同じ形
            var correction = EyeLookSetup.Plan(Settings(), new[] { EyeSet });

            Assert.That(correction, Is.Not.Null);
            Assert.That(correction.After.GazeMode, Is.EqualTo(EyeGazeMode.FaceTracking));
            Assert.That(correction.After.EyelidType, Is.EqualTo(EyelidType.FaceTracking));
            Assert.That(correction.After.Enabled, Is.True);
            Assert.That(
                EyeLookSetup.Describe(correction),
                Is.EqualTo("Eye Gaze Mode: BoneRotation → FaceTracking, Eyelids Type: Blendshapes → FaceTracking"));
        }

        [Test]
        public void Plan_EyeBonesAssigned_KeepsGazeMode()
        {
            var correction = EyeLookSetup.Plan(Settings(bones: true), new[] { EyeSet });

            Assert.That(correction.After.GazeMode, Is.EqualTo(EyeGazeMode.BoneRotation));
            Assert.That(correction.After.EyelidType, Is.EqualTo(EyelidType.FaceTracking));
        }

        [Test]
        public void Plan_BoneEyelids_AreKept()
        {
            var correction = EyeLookSetup.Plan(Settings(eyelid: EyelidType.Bones, bones: true), new[] { EyeSet });

            Assert.That(correction, Is.Null);
        }

        [Test]
        public void Plan_NoneEyelids_SwitchToFaceTracking()
        {
            var correction = EyeLookSetup.Plan(Settings(eyelid: EyelidType.None, bones: true), new[] { EyeSet });

            Assert.That(correction.After.EyelidType, Is.EqualTo(EyelidType.FaceTracking));
        }

        [Test]
        public void Plan_AlreadyFaceTracking_ReturnsNull()
        {
            var settings = Settings(gaze: EyeGazeMode.Both, eyelid: EyelidType.FaceTracking, bones: true);

            Assert.That(EyeLookSetup.Plan(settings, new[] { EyeSet }), Is.Null);
            Assert.That(
                EyeLookSetup.Plan(Settings(gaze: EyeGazeMode.FaceTracking, eyelid: EyelidType.FaceTracking), new[] { EyeSet }),
                Is.Null);
        }

        [Test]
        public void Plan_NoEyeKeys_ReturnsNull()
        {
            Assert.That(EyeLookSetup.Plan(Settings(enabled: false), new[] { SetWith(36, 37) }), Is.Null);
            Assert.That(EyeLookSetup.Plan(Settings(), new FaceTrackingSetData[0]), Is.Null);
        }

        [Test]
        public void Plan_OnlyEyeClosedKeys_ChangesEyelidsOnly()
        {
            var correction = EyeLookSetup.Plan(Settings(), new[] { SetWith(8, 9) });

            Assert.That(correction.After.GazeMode, Is.EqualTo(EyeGazeMode.BoneRotation));
            Assert.That(correction.After.EyelidType, Is.EqualTo(EyelidType.FaceTracking));
        }

        [Test]
        public void Plan_DisabledEyeLook_IsEnabled()
        {
            var correction = EyeLookSetup.Plan(
                Settings(enabled: false, gaze: EyeGazeMode.FaceTracking, eyelid: EyelidType.FaceTracking),
                new[] { EyeSet });

            Assert.That(correction.EnablesEyeLook, Is.True);
            Assert.That(correction.ChangesGazeMode, Is.False);
            Assert.That(correction.ChangesEyelidType, Is.False);
            Assert.That(EyeLookSetup.Describe(correction), Is.EqualTo("Eye Look: Off → On"));
        }

        [Test]
        public void EnumValues_MatchSdk()
        {
            // Descriptorとは値で写すので、SDKの列挙体と名前・値がそろっているかをフィールドの型から確かめる
            var settingsType = typeof(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor)
                .GetField("customEyeLookSettings").FieldType;
            AssertSameEnum(typeof(EyeGazeMode), settingsType.GetField("eyeGazeMode").FieldType);
            AssertSameEnum(typeof(EyelidType), settingsType.GetField("eyelidType").FieldType);
        }

        private static void AssertSameEnum(System.Type ours, System.Type sdk)
        {
            foreach (var name in System.Enum.GetNames(sdk))
            {
                Assert.That(System.Enum.GetNames(ours), Contains.Item(name), $"{ours.Name}に{name}が無い");
                Assert.That(
                    System.Convert.ToInt32(System.Enum.Parse(ours, name)),
                    Is.EqualTo(System.Convert.ToInt32(System.Enum.Parse(sdk, name))),
                    $"{ours.Name}.{name}の値");
            }

            Assert.That(System.Enum.GetNames(ours).Length, Is.EqualTo(System.Enum.GetNames(sdk).Length));
        }
    }
}
