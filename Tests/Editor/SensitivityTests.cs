using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class SensitivityTests
    {
        private static ToggleSet CreateUeToggles(SensitivityConfig sensitivity)
        {
            var settings = NativeFTPresets.CreateToggles(NativeFTPreset.UE);
            return ToggleSet.Resolve(
                toggle => settings.FirstOrDefault(s => s.toggle == toggle), CorrectionFeatures.All, sensitivity);
        }

        private static ToggleSet CreateMinimalToggles(SensitivityConfig sensitivity)
        {
            var settings = NativeFTPresets.CreateToggles(NativeFTPreset.Minimal);
            return ToggleSet.Resolve(
                toggle => settings.FirstOrDefault(s => s.toggle == toggle), CorrectionFeatures.None, sensitivity);
        }

        private static string Describe(ToggleConditionPlan condition)
        {
            switch (condition.Mode)
            {
                case ToggleConditionMode.Greater:
                    return condition.Parameter + ">" + condition.Threshold.ToString("0.###");
                case ToggleConditionMode.Less:
                    return condition.Parameter + "<" + condition.Threshold.ToString("0.###");
                default:
                    return (condition.Expected ? "" : "!") + condition.Parameter;
            }
        }

        private static string Describe(ToggleTransitionPlan transition)
        {
            return $"{transition.From}->{transition.To}: {string.Join(" & ", transition.Conditions.Select(Describe))}";
        }

        [Test]
        public void Stages_CenterIsUnityAndTopIsMaxMultiplier()
        {
            var config = new SensitivityConfig(true, true, 2f, true);

            Assert.That(config.Stages.Count, Is.EqualTo(SensitivityConfig.StageCount));
            Assert.That(
                config.Stages.Select(stage => stage.Multiplier),
                Is.EqualTo(new[] { 0.2f, 0.4f, 0.6f, 0.8f, 1f, 4f / 3f, 5f / 3f, 2f }).Within(1e-5f));
            Assert.That(config.Stages.Select(stage => stage.Weight), Is.All.InRange(0f, 1f));
            Assert.That(config.Stages.Last().Weight, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void DefaultValue_FallsInTheUnityStage()
        {
            var config = new SensitivityConfig(true, true, 3f, true);

            Assert.That(config.DefaultStage.Index, Is.EqualTo(4));
            Assert.That(config.DefaultStage.Multiplier, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(config.DefaultStage.Weight, Is.EqualTo(1f / 3f).Within(1e-6f));
        }

        [TestCase(0f, 0)]
        [TestCase(0.12f, 0)]
        [TestCase(0.126f, 1)]
        [TestCase(0.5f, 4)]
        [TestCase(0.75f, 6)]
        [TestCase(1f, 7)]
        public void FindStage_UsesEighths(float value, int expected)
        {
            var config = new SensitivityConfig(true, false, 2f, true);

            Assert.That(config.FindStage(value).Index, Is.EqualTo(expected));
        }

        [Test]
        public void Stages_RangesDoNotOverlapAndCoverTheRadial()
        {
            var config = new SensitivityConfig(true, false, 2f, true);

            Assert.That(config.Stages.First().LowerBound, Is.Null);
            Assert.That(config.Stages.Last().UpperBound, Is.Null);
            for (int i = 1; i < config.Stages.Count; i++)
            {
                Assert.That(config.Stages[i].LowerBound, Is.EqualTo(config.Stages[i - 1].UpperBound));
            }
        }

        [Test]
        public void Channels_MapEyeSideGroupsToEyeAndLipToLip()
        {
            Assert.That(SensitivityConfig.GetChannel(FaceTrackingGroup.Eye), Is.EqualTo(SensitivityChannel.Eye));
            Assert.That(SensitivityConfig.GetChannel(FaceTrackingGroup.EyeClosed), Is.EqualTo(SensitivityChannel.Eye));
            Assert.That(SensitivityConfig.GetChannel(FaceTrackingGroup.Pupil), Is.EqualTo(SensitivityChannel.Eye));
            Assert.That(SensitivityConfig.GetChannel(FaceTrackingGroup.Brow), Is.EqualTo(SensitivityChannel.Eye));
            Assert.That(SensitivityConfig.GetChannel(FaceTrackingGroup.Lip), Is.EqualTo(SensitivityChannel.Lip));
            Assert.That(SensitivityConfig.GetChannel(FaceTrackingGroup.Other), Is.Null);
        }

        [Test]
        public void Build_WithoutSensitivity_KeepsTheSingleOnState()
        {
            var plan = ToggleControllerPlanner.Build(CreateUeToggles(SensitivityConfig.None));

            Assert.That(plan.FloatParameters, Is.Empty);
            Assert.That(plan.Layers.SelectMany(layer => layer.OnStates).Select(state => state.Name), Is.All.EqualTo("On"));
            Assert.That(plan.Layers.Select(layer => layer.Sensitivity), Is.All.Null);
        }

        [Test]
        public void Build_LipSensitivity_DeclaresFloatAndStagesOnlyOnTheLipLayer()
        {
            var plan = ToggleControllerPlanner.Build(CreateUeToggles(new SensitivityConfig(false, true, 2f, false)));

            Assert.That(plan.FloatParameters.Select(p => p.Name), Is.EqualTo(new[] { NativeFTManagerParameterNames.SensitivityLip }));
            Assert.That(plan.FloatParameters[0].DefaultValue, Is.EqualTo(0.5f));
            Assert.That(plan.FloatParameters[0].Saved, Is.False);

            var lip = plan.FindLayer(FaceTrackingGroup.Lip);
            Assert.That(lip.Sensitivity, Is.EqualTo(SensitivityChannel.Lip));
            Assert.That(lip.OnStates.Select(state => state.Name), Is.EqualTo(Enumerable.Range(0, 8).Select(i => "On " + i)));
            Assert.That(plan.FindLayer(FaceTrackingGroup.Eye).Sensitivity, Is.Null);
            Assert.That(plan.FindLayer(FaceTrackingGroup.Other).Sensitivity, Is.Null);
        }

        [Test]
        public void Build_SensitivityLayer_LeavesStagesForOffFirstAndJumpsBetweenStages()
        {
            var plan = ToggleControllerPlanner.Build(CreateUeToggles(new SensitivityConfig(false, true, 2f, true)));
            var lip = plan.FindLayer(FaceTrackingGroup.Lip);

            var fromStage = lip.TransitionsFrom("On 4").Select(Describe).ToArray();
            Assert.That(fromStage.Take(2), Is.EqualTo(new[]
            {
                "On 4->Off: !KxNFTM/FaceTracking",
                "On 4->Off: !KxNFTM/LipTracking",
            }));
            Assert.That(fromStage.Skip(2).Count(), Is.EqualTo(7));
            Assert.That(fromStage, Has.Member("On 4->On 0: KxNFTM/Sensitivity/Lip<0.124"));
            Assert.That(fromStage, Has.Member("On 4->On 7: KxNFTM/Sensitivity/Lip>0.874"));
            Assert.That(
                lip.TransitionsFrom("On 4").Where(t => t.To != ToggleLayerPlan.OffState).Select(t => t.Duration),
                Is.All.EqualTo(SensitivityConfig.StageTransitionDuration));

            Assert.That(
                lip.TransitionsFrom(ToggleLayerPlan.InitState).Select(Describe),
                Has.Member("Init->On 4: KxNFTM/FaceTracking & KxNFTM/LipTracking & KxNFTM/Sensitivity/Lip>0.499"));
            Assert.That(
                lip.TransitionsFrom(ToggleLayerPlan.OffState).Select(Describe),
                Has.Member("Off->On 0: KxNFTM/FaceTracking & KxNFTM/LipTracking"));
        }

        [TestCase(0f, 0)]
        [TestCase(0.124f, 0)]
        [TestCase(0.499f, 3)]
        [TestCase(0.5f, 4)]
        [TestCase(1f, 7)]
        public void Build_EntryFromOff_ReachesAStageEvenOnABoundary(float value, int expected)
        {
            // Animatorと同じく、並びの先頭から成り立った遷移を選ぶ。境目ちょうどの値でもOffに残らない
            var plan = ToggleControllerPlanner.Build(CreateUeToggles(new SensitivityConfig(false, true, 2f, true)));
            var lip = plan.FindLayer(FaceTrackingGroup.Lip);

            var chosen = lip.TransitionsFrom(ToggleLayerPlan.OffState).FirstOrDefault(transition =>
                transition.Conditions.All(condition =>
                    condition.Mode == ToggleConditionMode.Bool
                        ? condition.Expected
                        : condition.Mode == ToggleConditionMode.Greater
                            ? value > condition.Threshold
                            : value < condition.Threshold));

            Assert.That(chosen, Is.Not.Null);
            Assert.That(chosen.To, Is.EqualTo("On " + expected));
        }

        [Test]
        public void Build_SensitivityDefaultWeight_IsTheUnityStageWeight()
        {
            var plan = ToggleControllerPlanner.Build(CreateUeToggles(new SensitivityConfig(true, false, 2f, true)));

            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Eye], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Brow], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Lip], Is.EqualTo(1f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Other], Is.EqualTo(1f));
        }

        [Test]
        public void Build_SensitivityWithoutMenuToggles_StillCreatesTheLayer()
        {
            // 条件のトグルがすべてメニューに無くても、Radialで重みが変わるのでレイヤーを作る
            var settings = new List<ToggleSettings>
            {
                new ToggleSettings(NativeFTToggle.FaceTracking, false, true),
                new ToggleSettings(NativeFTToggle.EyeTracking, false, true),
                new ToggleSettings(NativeFTToggle.LipTracking, false, true),
            };
            var toggles = ToggleSet.Resolve(
                toggle => settings.FirstOrDefault(s => s.toggle == toggle),
                CorrectionFeatures.None,
                new SensitivityConfig(false, true, 2f, true));

            var plan = ToggleControllerPlanner.Build(toggles);
            var lip = plan.FindLayer(FaceTrackingGroup.Lip);

            Assert.That(lip, Is.Not.Null);
            Assert.That(lip.Transitions.Where(t => t.To == ToggleLayerPlan.OffState), Is.Empty);
            Assert.That(plan.FindLayer(FaceTrackingGroup.Eye), Is.Null, "目の感度は無効なのでレイヤーは要らない");
        }

        [Test]
        public void Build_GatesNeverShareASensitivityLayer()
        {
            // 制限のトグルをメニューに出さず有効にすると、制限のゲートの条件はLipと同じFT ∧ Lipになる。
            // 感度のあるLipとは同じレイヤーにしない
            var settings = NativeFTPresets.CreateToggles(NativeFTPreset.UE);
            settings[settings.FindIndex(s => s.toggle == NativeFTToggle.Limits)] =
                new ToggleSettings(NativeFTToggle.Limits, false, true);
            var toggles = ToggleSet.Resolve(
                toggle => settings.FirstOrDefault(s => s.toggle == toggle),
                CorrectionFeatures.All,
                new SensitivityConfig(false, true, 2f, true));

            var plan = ToggleControllerPlanner.Build(toggles, CorrectionPlanner.GetPossibleGates(toggles, new HashSet<string>()));

            foreach (var layer in plan.Layers.Where(layer => layer.Sensitivity.HasValue))
            {
                Assert.That(layer.Gates, Is.Empty, layer.Name);
            }

            Assert.That(plan.Layers.Any(layer => layer.Gates.Any(gate => gate.Key.Contains("Limits"))), Is.True);
        }

        [Test]
        public void ScaleMultipliers_RaisesOnlyTheTargetGroups()
        {
            var config = new SensitivityConfig(false, true, 2f, true);
            var lip = FaceTrackingSetData.CreateEmpty("KxNFTM/Lip", 1f);
            lip.BlendShapeMultipliers[36] = 0.5f;
            lip.VisemeBlendShapeMultipliers[1] = 1f;
            var eye = FaceTrackingSetData.CreateEmpty("KxNFTM/Eye", 1f);

            int scaled = config.ScaleMultipliers(
                new[] { new SplitSet(FaceTrackingGroup.Lip, lip), new SplitSet(FaceTrackingGroup.Eye, eye) },
                group => group);

            Assert.That(scaled, Is.EqualTo(1));
            Assert.That(lip.BlendShapeMultipliers[36], Is.EqualTo(1f));
            Assert.That(lip.VisemeBlendShapeMultipliers[1], Is.EqualTo(2f));
            Assert.That(eye.BlendShapeMultipliers.Distinct(), Is.EqualTo(new[] { 1f }));
        }

        [Test]
        public void ScaleMultipliers_DoesNothingWhenTheMaxIsOne()
        {
            var config = new SensitivityConfig(true, true, 1f, true);
            var lip = FaceTrackingSetData.CreateEmpty("KxNFTM/Lip", 1f);

            Assert.That(config.ScaleMultipliers(new[] { new SplitSet(FaceTrackingGroup.Lip, lip) }, group => group), Is.Zero);
        }

        [Test]
        public void MenuPlan_AddsOneRadialPerChannel()
        {
            var menu = MenuPlan.Build(CreateMinimalToggles(new SensitivityConfig(true, true, 2f, true)));

            Assert.That(menu.Sensitivity.Select(r => r.Label), Is.EqualTo(new[] { "Eye", "Lip" }));
            Assert.That(
                menu.Sensitivity.Select(r => r.Parameter),
                Is.EqualTo(new[] { NativeFTManagerParameterNames.SensitivityEye, NativeFTManagerParameterNames.SensitivityLip }));
            Assert.That(MenuPlan.Build(CreateMinimalToggles(SensitivityConfig.None)).Sensitivity, Is.Empty);
        }
    }
}
