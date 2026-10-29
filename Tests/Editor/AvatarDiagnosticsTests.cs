using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class AvatarDiagnosticsTests
    {
        private static AvatarFacts CleanFacts()
        {
            return new AvatarFacts
            {
                Preset = NativeFTPreset.UE,
                Integration = GeneratorIntegration.Off,
                Generator = GeneratorPresence.None,
                SetUseAllTrackingKeys = true,
                DescriptorUseAllTrackingKeys = false,
                EyeLookAngles = new[] { 12f, 12f, 18f, 18f },
            };
        }

        private static string[] Codes(AvatarFacts facts)
        {
            return AvatarDiagnostics.Evaluate(facts).Select(d => d.Code).ToArray();
        }

        [Test]
        public void Evaluate_CleanAvatar_HasNoDiagnostics()
        {
            Assert.That(Codes(CleanFacts()), Is.Empty);
        }

        [Test]
        public void Evaluate_FaceEmoWithSuppressionOff_InformsI03()
        {
            var facts = CleanFacts();
            facts.HasFaceEmo = true;

            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.SuppressionDisabled }));

            facts.FaceEmoSuppressionEnabled = true;
            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void Evaluate_SuppressionWithoutFaceEmo_InformsI04()
        {
            var facts = CleanFacts();
            facts.FaceEmoSuppressionEnabled = true;

            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.FaceEmoMissing }));
        }

        [Test]
        public void Evaluate_SuppressionNeverEngaged_WarnsW16()
        {
            var facts = CleanFacts();
            facts.HasFaceEmo = true;
            facts.FaceEmoSuppressionEnabled = true;
            facts.FaceEmoSuppressionNeverEngaged = true;

            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.SuppressionNeverEngaged }));
        }

        [Test]
        public void Evaluate_ArkitPresetWithoutGenerator_WarnsW01()
        {
            var facts = CleanFacts();
            facts.Preset = NativeFTPreset.ARKit;

            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.GeneratorMissing }));
        }

        [Test]
        public void Evaluate_GeneratorWithBothSetWritesOff_WarnsW02()
        {
            var facts = CleanFacts();
            facts.Generator = GeneratorPresence.Supported;

            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.GeneratorSetWriteOff }));

            facts.Integration = GeneratorIntegration.Auto;
            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void Evaluate_UnsupportedGenerator_WarnsW03()
        {
            var facts = CleanFacts();
            facts.Generator = GeneratorPresence.Unsupported;

            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.GeneratorUnsupported }));
        }

        [Test]
        public void Evaluate_UseAllTrackingKeysLeftOff_WarnsW05()
        {
            var facts = CleanFacts();
            facts.SetUseAllTrackingKeys = false;

            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.UseAllTrackingKeysOff }));

            facts.DescriptorUseAllTrackingKeys = true;
            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void Evaluate_NarrowEyeLook_WarnsW07WithTheNarrowestAngle()
        {
            var facts = CleanFacts();
            facts.EyeLookAngles = new[] { 3f, 3.2f, 12f, 12f };

            var diagnostic = AvatarDiagnostics.Evaluate(facts).Single();
            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.EyeLookTooNarrow));
            Assert.That(diagnostic.Args, Is.EqualTo(new object[] { "3" }));
        }

        [Test]
        public void Evaluate_DisabledEyeLook_IsNotChecked()
        {
            var facts = CleanFacts();
            facts.EyeLookAngles = null;

            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void Evaluate_JerrysJsTBridgeAndConflicts_AreReported()
        {
            var facts = CleanFacts();
            facts.JerrysEvidence = new[] { "MA_UE_VRCFT" };
            facts.HasJsTBridge = true;
            facts.ConflictingParameters = new[] { NativeFTManagerParameterNames.FaceTracking };

            Assert.That(
                Codes(facts),
                Is.EqualTo(new[]
                {
                    DiagnosticCodes.JerrysDetected,
                    DiagnosticCodes.JsTBridgePresent,
                    DiagnosticCodes.ParameterConflict,
                }));
        }

        [Test]
        public void Evaluate_SyncedBitsOverTheLimit_WarnsW13()
        {
            var facts = CleanFacts();
            facts.PublishedSyncedBits = 8;
            facts.OtherSyncedBits = 248;

            Assert.That(Codes(facts), Is.Empty, "ちょうど256bitなら収まる");

            facts.OtherSyncedBits = 249;
            var diagnostic = AvatarDiagnostics.Evaluate(facts).Single();
            Assert.That(diagnostic.Code, Is.EqualTo(DiagnosticCodes.SyncedBitsExceeded));
            Assert.That(diagnostic.Args, Is.EqualTo(new object[] { 257, 256, 8 }));
        }

        [Test]
        public void Evaluate_NoPublishedBits_DoesNotBlameOtherParameters()
        {
            // 本製品が同期パラメーターを作らないなら、超過は本製品の責任ではない
            var facts = CleanFacts();
            facts.OtherSyncedBits = 300;

            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void IsGeneratorSetSparse_RequiresTenKeys()
        {
            var set = FaceTrackingSetData.CreateEmpty("Generator", 1f);
            for (int key = 0; key < 9; key++)
            {
                set.BlendShapeNames[key] = "shape" + key;
            }

            Assert.That(AvatarDiagnostics.IsGeneratorSetSparse(null), Is.True);
            Assert.That(AvatarDiagnostics.IsGeneratorSetSparse(set), Is.True);

            set.BlendShapeNames[9] = "shape9";
            Assert.That(AvatarDiagnostics.IsGeneratorSetSparse(set), Is.False);
        }

        [Test]
        public void Evaluate_AaoWithoutNativeFaceTrackingSupport_WarnsW14OrW15()
        {
            var facts = CleanFacts();

            facts.Aao = AaoPresence.Outdated;
            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.AaoOutdated }));

            facts.Aao = AaoPresence.SdkTooOld;
            Assert.That(Codes(facts), Is.EqualTo(new[] { DiagnosticCodes.AaoSdkTooOld }));

            facts.Aao = AaoPresence.Supported;
            Assert.That(Codes(facts), Is.Empty);
        }
    }

    public class JerrysDetectionTests
    {
        [Test]
        public void Detect_FindsJerrysControllerParameters()
        {
            var evidence = JerrysDetection.Detect(
                new[] { new[] { "FacialExpressionsDisabled", "EyeTrackingActive", "Other" } },
                new string[0],
                new string[0]);

            Assert.That(evidence, Is.EqualTo(new[] { "FacialExpressionsDisabled" }));
        }

        [Test]
        public void Detect_FindsVrcftParametersAndPrefabObjects()
        {
            var evidence = JerrysDetection.Detect(
                new string[0][],
                new[] { "Unrelated", "FT/v2/EyeLeftX" },
                new[] { "Body", "MA_UE_VRCFT", "MA_EyeRotation" });

            Assert.That(evidence, Is.EqualTo(new[] { "FT/v2/EyeLeftX", "MA_UE_VRCFT", "MA_EyeRotation" }));
        }

        [Test]
        public void Detect_IgnoresPartialMatches()
        {
            var evidence = JerrysDetection.Detect(
                new[] { new[] { "EyeTrackingActive" } },
                new[] { "KxNFTM/EyeTracking" },
                new[] { "EyeRotation" });

            Assert.That(evidence, Is.Empty);
        }
    }
}
