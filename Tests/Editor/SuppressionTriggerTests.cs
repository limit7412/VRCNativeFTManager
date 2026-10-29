using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class SuppressionTriggerTests
    {
        /// <summary>FaceTracking、EyeTracking、LipTrackingをメニューに出し、ほかは出さない設定</summary>
        internal static ToggleSet CreateToggles(params ToggleSettings[] overrides)
        {
            var settings = new List<ToggleSettings>
            {
                new ToggleSettings(NativeFTToggle.FaceTracking),
                new ToggleSettings(NativeFTToggle.EyeTracking),
                new ToggleSettings(NativeFTToggle.LipTracking),
            };
            foreach (var entry in overrides)
            {
                settings.RemoveAll(s => s.toggle == entry.toggle);
                settings.Add(entry);
            }

            return ToggleSet.Resolve(toggle => settings.FirstOrDefault(s => s.toggle == toggle), CorrectionFeatures.None);
        }

        private static string[] Describe(IReadOnlyList<IReadOnlyList<ToggleLiteral>> terms)
        {
            return terms
                .Select(term => string.Join(" & ", term.Select(literal => literal.ToString()).OrderBy(text => text)))
                .OrderBy(text => text)
                .ToArray();
        }

        [Test]
        public void EyeOrLip_EngagesOnFaceTrackingWithEyeOrLip()
        {
            var trigger = SuppressionTrigger.Build(FaceEmoSuppressionTrigger.EyeOrLip, CreateToggles());

            Assert.That(Describe(trigger.OnTerms), Is.EqualTo(new[]
            {
                "EyeTracking & FaceTracking",
                "FaceTracking & LipTracking",
            }));
        }

        [Test]
        public void EyeOrLip_ReleasesOnFaceTrackingOffOrBothOff()
        {
            // ¬(FT ∧ (Eye ∨ Lip)) = ¬FT ∨ (¬Eye ∧ ¬Lip)
            var trigger = SuppressionTrigger.Build(FaceEmoSuppressionTrigger.EyeOrLip, CreateToggles());

            Assert.That(Describe(trigger.OffTerms), Is.EqualTo(new[]
            {
                "!EyeTracking & !LipTracking",
                "!FaceTracking",
            }));
        }

        [Test]
        public void LipOnly_IgnoresEyeTracking()
        {
            var trigger = SuppressionTrigger.Build(FaceEmoSuppressionTrigger.LipOnly, CreateToggles());

            Assert.That(Describe(trigger.OnTerms), Is.EqualTo(new[] { "FaceTracking & LipTracking" }));
            Assert.That(Describe(trigger.OffTerms), Is.EqualTo(new[] { "!FaceTracking", "!LipTracking" }));
        }

        [Test]
        public void HiddenToggleOn_IsFoldedAway()
        {
            // Face Trackingをメニューに出さず有効のままなら、条件から消える
            var toggles = CreateToggles(new ToggleSettings(NativeFTToggle.FaceTracking, false, true));
            var trigger = SuppressionTrigger.Build(FaceEmoSuppressionTrigger.EyeOrLip, toggles);

            Assert.That(Describe(trigger.OnTerms), Is.EqualTo(new[] { "EyeTracking", "LipTracking" }));
            Assert.That(Describe(trigger.OffTerms), Is.EqualTo(new[] { "!EyeTracking & !LipTracking" }));
        }

        [Test]
        public void HiddenToggleOff_DropsTerm()
        {
            // Eye Trackingをメニューに出さず無効にしたら、目の項は成り立たない
            var toggles = CreateToggles(new ToggleSettings(NativeFTToggle.EyeTracking, false, false));
            var trigger = SuppressionTrigger.Build(FaceEmoSuppressionTrigger.EyeOrLip, toggles);

            Assert.That(Describe(trigger.OnTerms), Is.EqualTo(new[] { "FaceTracking & LipTracking" }));
            Assert.That(Describe(trigger.OffTerms), Is.EqualTo(new[] { "!FaceTracking", "!LipTracking" }));
        }

        [Test]
        public void HiddenFaceTrackingOff_NeverEngages()
        {
            var toggles = CreateToggles(new ToggleSettings(NativeFTToggle.FaceTracking, false, false));
            var trigger = SuppressionTrigger.Build(FaceEmoSuppressionTrigger.EyeOrLip, toggles);

            Assert.That(trigger.IsNeverEngaged, Is.True);
            Assert.That(trigger.IsAlwaysEngaged, Is.False);
            Assert.That(trigger.OffTerms.Count, Is.EqualTo(1));
            Assert.That(trigger.OffTerms[0], Is.Empty);
        }

        [Test]
        public void AllHiddenOn_AlwaysEngages()
        {
            var toggles = CreateToggles(
                new ToggleSettings(NativeFTToggle.FaceTracking, false, true),
                new ToggleSettings(NativeFTToggle.EyeTracking, false, true),
                new ToggleSettings(NativeFTToggle.LipTracking, false, true));
            var trigger = SuppressionTrigger.Build(FaceEmoSuppressionTrigger.EyeOrLip, toggles);

            Assert.That(trigger.IsAlwaysEngaged, Is.True);
            Assert.That(trigger.OnTerms.Count, Is.EqualTo(1));
            Assert.That(trigger.OnTerms[0], Is.Empty);
        }

        /// <summary>
        /// どの組み合わせでも、発動の条件と解除の条件はちょうど一方だけが成り立つ
        /// </summary>
        [Test]
        public void OnAndOff_AreComplementary_ForEveryCombination(
            [Values] FaceEmoSuppressionTrigger kind,
            [Values] bool faceTrackingShown,
            [Values] bool eyeShown)
        {
            var toggles = CreateToggles(
                new ToggleSettings(NativeFTToggle.FaceTracking, faceTrackingShown, true),
                new ToggleSettings(NativeFTToggle.EyeTracking, eyeShown, false));
            var trigger = SuppressionTrigger.Build(kind, toggles);

            for (int bits = 0; bits < 8; bits++)
            {
                var values = new Dictionary<NativeFTToggle, bool>
                {
                    { NativeFTToggle.FaceTracking, (bits & 1) != 0 },
                    { NativeFTToggle.EyeTracking, (bits & 2) != 0 },
                    { NativeFTToggle.LipTracking, (bits & 4) != 0 },
                };

                bool Holds(IReadOnlyList<IReadOnlyList<ToggleLiteral>> terms) =>
                    terms.Any(term => term.All(literal => values[literal.Toggle] == literal.Expected));

                Assert.That(Holds(trigger.OnTerms), Is.Not.EqualTo(Holds(trigger.OffTerms)), $"bits={bits}");
            }
        }
    }
}
