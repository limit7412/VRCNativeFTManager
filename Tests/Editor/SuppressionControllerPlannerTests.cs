using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;
using Planner = NativeFTManager.Domain.SuppressionControllerPlanner;

namespace NativeFTManager.Tests
{
    public class SuppressionControllerPlannerTests
    {
        private static SuppressionControllerPlan Build(
            FaceEmoControlMethod method = FaceEmoControlMethod.ExpressionControl,
            FaceEmoSuppressionTrigger trigger = FaceEmoSuppressionTrigger.EyeOrLip,
            int emote = 0,
            bool controlFaceEmo = true,
            ExtraParameterTarget[] extra = null,
            ToggleSet toggles = null)
        {
            return Planner.Build(
                new SuppressionSettings(method, trigger, emote, controlFaceEmo, extra),
                toggles ?? SuppressionTriggerTests.CreateToggles(),
                FaceEmoParameterNames.Raw);
        }

        private static string Describe(SuppressionTransitionPlan transition)
        {
            var conditions = transition.Conditions.Select(c => (c.Expected ? "" : "!") + c.Parameter);
            var exit = transition.HasExitTime ? $" @{transition.ExitTime}" : "";
            return $"{transition.From}->{transition.To}: {string.Join(" & ", conditions)}{exit}";
        }

        private const string FT = NativeFTManagerParameterNames.FaceTracking;
        private const string Eye = NativeFTManagerParameterNames.EyeTracking;
        private const string Lip = NativeFTManagerParameterNames.LipTracking;

        [Test]
        public void ExpressionControl_EngagesAndReleasesWithTrigger()
        {
            var layer = Build().FindLayer(Planner.ExpressionControlLayerName);

            Assert.That(layer, Is.Not.Null);
            Assert.That(layer.DefaultStateName, Is.EqualTo(Planner.InitialStateName));
            Assert.That(layer.Transitions.Select(Describe), Is.EqualTo(new[]
            {
                $"Initial->Engaged: {FT} & {Eye}",
                $"Initial->Engaged: {FT} & {Lip}",
                $"Engaged->Released: !{FT}",
                $"Engaged->Released: !{Eye} & !{Lip}",
                $"Released->Engaged: {FT} & {Eye}",
                $"Released->Engaged: {FT} & {Lip}",
            }));
        }

        [Test]
        public void ExpressionControl_InitialState_WritesNothing()
        {
            // 読み込んだだけで、FaceEmoに保存された表情ロックとまばたき停止を上書きしない
            var layer = Build().FindLayer(Planner.ExpressionControlLayerName);
            Assert.That(layer.FindState(Planner.InitialStateName).Driver, Is.Null);
        }

        [Test]
        public void ExpressionControl_LocksBlinkAndSwitchesEmote()
        {
            var layer = Build(emote: 3).FindLayer(Planner.ExpressionControlLayerName);

            var engaged = layer.FindState(Planner.EngagedStateName).Driver;
            Assert.That(engaged.LocalOnly, Is.True);
            Assert.That(
                engaged.Entries.Select(entry => $"{entry.Parameter}={entry.Value}"),
                Is.EqualTo(new[]
                {
                    FaceEmoParameterNames.RawEmoteLockEnable + "=1",
                    FaceEmoParameterNames.RawForceBlinkDisable + "=1",
                    FaceEmoParameterNames.RawEmote + "=3",
                }));

            // 表情番号は戻さない
            var released = layer.FindState(Planner.ReleasedStateName).Driver;
            Assert.That(released.LocalOnly, Is.True);
            Assert.That(
                released.Entries.Select(entry => $"{entry.Parameter}={entry.Value}"),
                Is.EqualTo(new[]
                {
                    FaceEmoParameterNames.RawEmoteLockEnable + "=0",
                    FaceEmoParameterNames.RawForceBlinkDisable + "=0",
                }));
        }

        [Test]
        public void ExpressionControl_UsesResolvedNames()
        {
            var faceEmo = new FaceEmoParameterNames("FaceEmo_Lock", "FaceEmo_Blink", "FaceEmo_Emote", true);
            var plan = Planner.Build(
                new SuppressionSettings(FaceEmoControlMethod.ExpressionControl, FaceEmoSuppressionTrigger.EyeOrLip, 0),
                SuppressionTriggerTests.CreateToggles(),
                faceEmo);

            var names = plan.Parameters.Select(parameter => parameter.Name).ToArray();
            Assert.That(names, Does.Contain("FaceEmo_Lock"));
            Assert.That(names, Does.Contain("FaceEmo_Emote"));
            Assert.That(names, Does.Not.Contain(FaceEmoParameterNames.RawEmote));
        }

        [Test]
        public void NegativeEmote_IsClampedToZero()
        {
            var layer = Build(emote: -2).FindLayer(Planner.ExpressionControlLayerName);
            var emote = layer.FindState(Planner.EngagedStateName).Driver.Entries
                .Single(entry => entry.Parameter == FaceEmoParameterNames.RawEmote);
            Assert.That(emote.Value, Is.EqualTo(0f));
        }

        [Test]
        public void Bypass_WritesForceBypassAndRedrives()
        {
            var plan = Build(FaceEmoControlMethod.Bypass, FaceEmoSuppressionTrigger.LipOnly);
            Assert.That(plan.FindLayer(Planner.ExpressionControlLayerName), Is.Null);

            var layer = plan.FindLayer(Planner.BypassLayerName);
            Assert.That(layer.Transitions.Select(Describe), Is.EqualTo(new[]
            {
                $"Idle->Bypass: {FT} & {Lip}",
                $"Bypass->Idle: !{FT}",
                $"Bypass->Idle: !{Lip}",
                "Idle->Redrive:  @1",
                "Bypass->Redrive:  @1",
                $"Redrive->Bypass: {FT} & {Lip}",
                $"Redrive->Idle: !{FT}",
                $"Redrive->Idle: !{Lip}",
            }));

            var bypass = layer.FindState(Planner.BypassStateName).Driver;
            Assert.That(bypass.LocalOnly, Is.False);
            Assert.That(bypass.Entries.Single().Parameter, Is.EqualTo(FaceEmoParameterNames.ForceBypassEnable));
            Assert.That(bypass.Entries.Single().Value, Is.EqualTo(1f));
            Assert.That(
                plan.Parameters.Select(parameter => parameter.Name),
                Does.Contain(FaceEmoParameterNames.ForceBypassEnable));
        }

        [Test]
        public void DeclaresOnlyTriggerToggles()
        {
            var plan = Build(trigger: FaceEmoSuppressionTrigger.LipOnly);
            var toggles = plan.Parameters
                .Where(parameter => parameter.Name.StartsWith(NativeFTManagerParameterNames.Prefix))
                .Select(parameter => parameter.Name)
                .ToArray();

            Assert.That(toggles, Is.EquivalentTo(new[] { FT, Lip }));
        }

        [Test]
        public void WithoutFaceEmo_OnlyExtraParameters()
        {
            var extra = new[] { new ExtraParameterTarget("Mask", SuppressionParameterType.Bool, 0f, 1f) };
            var plan = Build(controlFaceEmo: false, extra: extra);

            Assert.That(plan.Layers.Select(layer => layer.Name), Is.EqualTo(new[] { Planner.ExtraParametersLayerName }));
        }

        [Test]
        public void WithoutFaceEmoOrExtra_IsEmpty()
        {
            Assert.That(Build(controlFaceEmo: false).IsEmpty, Is.True);
        }

        [Test]
        public void NeverEngaged_IsEmpty()
        {
            var toggles = SuppressionTriggerTests.CreateToggles(new ToggleSettings(NativeFTToggle.FaceTracking, false, false));
            var extra = new[] { new ExtraParameterTarget("Mask", SuppressionParameterType.Bool, 0f, 1f) };

            Assert.That(Build(extra: extra, toggles: toggles).IsEmpty, Is.True);
        }

        [Test]
        public void AlwaysEngaged_UsesExitTimeZero_AndNeverReleases()
        {
            var toggles = SuppressionTriggerTests.CreateToggles(
                new ToggleSettings(NativeFTToggle.FaceTracking, false, true),
                new ToggleSettings(NativeFTToggle.EyeTracking, false, true),
                new ToggleSettings(NativeFTToggle.LipTracking, false, true));
            var layer = Build(toggles: toggles).FindLayer(Planner.ExpressionControlLayerName);

            Assert.That(layer.Transitions.Select(Describe), Is.EqualTo(new[] { "Initial->Engaged:  @0", "Released->Engaged:  @0" }));
        }

        [Test]
        public void SyncedExtra_StartsInInitial_AndStashesBeforeWriting()
        {
            var extra = new[]
            {
                new ExtraParameterTarget("Mask", SuppressionParameterType.Bool, 0f, null, restore: true),
                new ExtraParameterTarget("Outfit", SuppressionParameterType.Int, 2f, 1f),
            };
            var layer = Build(extra: extra).FindLayer(Planner.ExtraParametersLayerName);

            Assert.That(layer.DefaultStateName, Is.EqualTo(Planner.InitialStateName));
            Assert.That(layer.FindState(Planner.InitialStateName).Driver, Is.Null);

            var engaged = layer.FindState(Planner.EngagedStateName).Driver;
            Assert.That(engaged.LocalOnly, Is.True);
            Assert.That(engaged.Entries[0].IsCopy, Is.True);
            Assert.That(engaged.Entries[0].Source, Is.EqualTo("Mask"));
            Assert.That(engaged.Entries[0].Parameter, Is.EqualTo(ExtraParameterTarget.StashPrefix + "Mask"));

            var released = layer.FindState(Planner.ReleasedStateName).Driver;
            Assert.That(released.Entries.Select(entry => entry.IsCopy ? $"{entry.Source}>{entry.Parameter}" : $"{entry.Parameter}={entry.Value}"),
                Is.EqualTo(new[] { ExtraParameterTarget.StashPrefix + "Mask>Mask", "Outfit=1" }));
        }

        [Test]
        public void UnsyncedExtra_ReassertsWhileEngaged()
        {
            var extra = new[] { new ExtraParameterTarget("Local", SuppressionParameterType.Float, 0.5f, 0f, synced: false) };
            var plan = Build(extra: extra);

            Assert.That(plan.FindLayer(Planner.ExtraParametersLayerName), Is.Null);
            var layer = plan.FindLayer(Planner.UnsyncedExtraParametersLayerName);
            Assert.That(layer.FindState(Planner.EngagedStateName).Driver.LocalOnly, Is.False);

            var fromEngaged = layer.TransitionsFrom(Planner.EngagedStateName).Select(Describe).ToArray();
            Assert.That(fromEngaged.Last(), Is.EqualTo("Engaged->Redrive:  @1"), "解除が書き直しより先");
            Assert.That(
                layer.TransitionsFrom(Planner.RedriveStateName).Select(Describe),
                Is.EqualTo(new[]
                {
                    $"Redrive->Reassert: {FT} & {Eye}",
                    $"Redrive->Reassert: {FT} & {Lip}",
                    $"Redrive->Released: !{FT}",
                    $"Redrive->Released: !{Eye} & !{Lip}",
                }));
        }

        [Test]
        public void ExtraParameters_AreDeclaredOnce()
        {
            var extra = new[]
            {
                new ExtraParameterTarget(FT, SuppressionParameterType.Bool, 0f, null, restore: true),
            };
            var plan = Build(extra: extra);

            Assert.That(plan.Parameters.Count(parameter => parameter.Name == FT), Is.EqualTo(1));
            Assert.That(plan.Parameters.Count(parameter => parameter.Name == ExtraParameterTarget.StashPrefix + FT), Is.EqualTo(1));
        }
    }
}
