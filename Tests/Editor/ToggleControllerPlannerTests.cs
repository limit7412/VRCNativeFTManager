using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class ToggleControllerPlannerTests
    {
        /// <summary>
        /// M1と同じ3つのトグルだけをメニューに出し、表情補正を生成しない設定
        /// </summary>
        private static ToggleSet CreateM1Toggles(params ToggleSettings[] overrides)
        {
            var settings = new List<ToggleSettings>
            {
                new ToggleSettings(NativeFTToggle.FaceTracking),
                new ToggleSettings(NativeFTToggle.EyeTracking),
                new ToggleSettings(NativeFTToggle.LipTracking),
            };
            return Resolve(settings, CorrectionFeatures.None, overrides);
        }

        /// <summary>
        /// UEプリセットの設定（すべてのトグルと表情補正）
        /// </summary>
        private static ToggleSet CreateUeToggles(params ToggleSettings[] overrides)
        {
            return Resolve(NativeFTPresets.CreateToggles(NativeFTPreset.UE), CorrectionFeatures.All, overrides);
        }

        private static ToggleSet Resolve(
            List<ToggleSettings> settings,
            CorrectionFeatures features,
            ToggleSettings[] overrides)
        {
            foreach (var entry in overrides)
            {
                int index = settings.FindIndex(s => s.toggle == entry.toggle);
                if (index >= 0)
                {
                    settings[index] = entry;
                }
                else
                {
                    settings.Add(entry);
                }
            }

            return ToggleSet.Resolve(toggle => settings.FirstOrDefault(s => s.toggle == toggle), features);
        }

        private static string Describe(ToggleTransitionPlan transition)
        {
            var conditions = transition.Conditions.Select(c => (c.Expected ? "" : "!") + c.Parameter);
            return $"{transition.From}->{transition.To}: {string.Join(" & ", conditions)}";
        }

        [Test]
        public void Build_DeclaresOneBoolPerMenuToggle()
        {
            var plan = ToggleControllerPlanner.Build(CreateM1Toggles());

            Assert.That(
                plan.Parameters.Select(p => p.Name),
                Is.EqualTo(new[]
                {
                    NativeFTManagerParameterNames.FaceTracking,
                    NativeFTManagerParameterNames.EyeTracking,
                    NativeFTManagerParameterNames.LipTracking,
                }));
        }

        [Test]
        public void Build_M1Settings_KeepTheM1Layers()
        {
            // 後から足したトグルの設定が無いコンポーネントは、M1と同じレイヤーになる
            var plan = ToggleControllerPlanner.Build(CreateM1Toggles());

            Assert.That(
                plan.Layers.Select(layer => layer.Name),
                Is.EqualTo(new[]
                {
                    "KxNFTM Eye/EyeClosed/Pupil/Brow",
                    "KxNFTM Lip",
                    "KxNFTM Other",
                }));
            Assert.That(plan.VisemeLayer, Is.Null, "Visemesの設定が無ければTracking Controlを置かない");
        }

        [Test]
        public void Build_UePreset_SplitsPupilAndEyeClosedFromEye()
        {
            var plan = ToggleControllerPlanner.Build(CreateUeToggles());

            Assert.That(
                plan.Layers.Select(layer => layer.Name),
                Is.EqualTo(new[]
                {
                    "KxNFTM Eye/Brow",
                    "KxNFTM EyeClosed",
                    "KxNFTM Pupil",
                    "KxNFTM Lip",
                    "KxNFTM Other",
                }));
        }

        [Test]
        public void Build_EyeClosed_TurnsOffWhileEyeSyncIsOn()
        {
            var plan = ToggleControllerPlanner.Build(CreateUeToggles());
            var layer = plan.FindLayer(FaceTrackingGroup.EyeClosed);

            Assert.That(
                layer.TransitionsFrom(ToggleLayerPlan.OffState).Select(Describe),
                Is.EqualTo(new[]
                {
                    "Off->On: KxNFTM/FaceTracking & KxNFTM/EyeTracking & !KxNFTM/EyeSync",
                }));
            Assert.That(
                layer.TransitionsFrom(ToggleLayerPlan.OnState).Select(Describe),
                Has.Member("On->Off: KxNFTM/EyeSync"));
        }

        [Test]
        public void Build_EyeLayer_UsesConjunctionForOnAndOneTransitionPerNegationForOff()
        {
            var plan = ToggleControllerPlanner.Build(CreateM1Toggles());
            var layer = plan.FindLayer(FaceTrackingGroup.Eye);

            Assert.That(
                layer.Transitions.Select(Describe),
                Is.EqualTo(new[]
                {
                    "Init->On: KxNFTM/FaceTracking & KxNFTM/EyeTracking",
                    "Init->Off: !KxNFTM/FaceTracking",
                    "Init->Off: !KxNFTM/EyeTracking",
                    "Off->On: KxNFTM/FaceTracking & KxNFTM/EyeTracking",
                    "On->Off: !KxNFTM/FaceTracking",
                    "On->Off: !KxNFTM/EyeTracking",
                }));
        }

        [Test]
        public void Build_HiddenToggle_IsFoldedIntoItsDefault()
        {
            var plan = ToggleControllerPlanner.Build(CreateM1Toggles(
                new ToggleSettings(NativeFTToggle.EyeTracking) { showInMenu = false, defaultValue = true }));
            var layer = plan.FindLayer(FaceTrackingGroup.Eye);

            Assert.That(plan.Parameters.Select(p => p.Name), Has.No.Member(NativeFTManagerParameterNames.EyeTracking));
            Assert.That(
                layer.Transitions.SelectMany(t => t.Conditions).Select(c => c.Parameter).Distinct(),
                Is.EqualTo(new[] { NativeFTManagerParameterNames.FaceTracking }));
        }

        [Test]
        public void Build_HiddenToggleDefaultingToOff_DisablesTheGroupWithoutALayer()
        {
            var plan = ToggleControllerPlanner.Build(CreateM1Toggles(
                new ToggleSettings(NativeFTToggle.LipTracking) { showInMenu = false, defaultValue = false }));

            Assert.That(plan.FindLayer(FaceTrackingGroup.Lip), Is.Null);
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Lip], Is.EqualTo(0f));
        }

        [Test]
        public void Build_NoMenuToggles_CreatesNoLayers()
        {
            var hidden = new List<ToggleSettings>();
            foreach (NativeFTToggle toggle in System.Enum.GetValues(typeof(NativeFTToggle)))
            {
                hidden.Add(new ToggleSettings(toggle) { showInMenu = false });
            }

            var plan = ToggleControllerPlanner.Build(CreateM1Toggles(hidden.ToArray()));

            Assert.That(plan.Layers, Is.Empty);
            Assert.That(plan.Parameters, Is.Empty);
            Assert.That(plan.DefaultWeights.Values, Is.All.EqualTo(1f));
        }

        [Test]
        public void Build_DefaultWeights_FollowToggleDefaults()
        {
            var plan = ToggleControllerPlanner.Build(CreateM1Toggles(
                new ToggleSettings(NativeFTToggle.EyeTracking) { defaultValue = false }));

            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Eye], Is.EqualTo(0f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Brow], Is.EqualTo(0f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Lip], Is.EqualTo(1f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Other], Is.EqualTo(1f));
        }

        [Test]
        public void Resolve_DisabledFeature_HidesItsToggleAndTurnsItOff()
        {
            var toggles = Resolve(
                NativeFTPresets.CreateToggles(NativeFTPreset.UE),
                new CorrectionFeatures(true, false, false),
                new ToggleSettings[0]);

            Assert.That(toggles[NativeFTToggle.Emulation].IsDynamic, Is.True);
            Assert.That(toggles[NativeFTToggle.Limits].IsDynamic, Is.False);
            Assert.That(toggles[NativeFTToggle.Limits].DefaultValue, Is.False);
            Assert.That(toggles[NativeFTToggle.EyeSync].IsDynamic, Is.False);
        }

        [Test]
        public void Build_Gates_GetTheirOwnLayersAndWeights()
        {
            var toggles = CreateUeToggles();
            var gates = CorrectionPlanner.GetPossibleGates(toggles, new string[0]);
            var plan = ToggleControllerPlanner.Build(toggles, gates);

            var eyeSync = gates.Single(gate => gate.Key == "Gate/EyeSync/Eye");
            Assert.That(plan.FindGateLayer(eyeSync.Key), Is.Not.Null);
            Assert.That(plan.GateWeights[eyeSync.Key], Is.EqualTo(0f), "Eye Syncの既定は無効");
            Assert.That(plan.GateWeights["Gate/Limits/Lip"], Is.EqualTo(1f), "制限の既定は有効");
        }

        [Test]
        public void Build_VisemeLayer_SwitchesWithTheToggle()
        {
            var plan = ToggleControllerPlanner.Build(CreateUeToggles());

            Assert.That(plan.VisemeLayer, Is.Not.Null);
            Assert.That(plan.VisemeLayer.AlwaysOff, Is.False);
            Assert.That(
                plan.VisemeLayer.Layer.TransitionsFrom(ToggleLayerPlan.OffState).Select(Describe),
                Is.EqualTo(new[] { "Off->On: KxNFTM/Visemes" }));
        }

        [Test]
        public void Build_HiddenVisemesDefaultingToOff_KeepsVisemesOff()
        {
            var plan = ToggleControllerPlanner.Build(CreateUeToggles(
                new ToggleSettings(NativeFTToggle.Visemes, false, false)));

            Assert.That(plan.VisemeLayer.AlwaysOff, Is.True);
        }

        [Test]
        public void MenuPlan_ListsMenuTogglesInOrder()
        {
            var menu = MenuPlan.Build(CreateM1Toggles(
                new ToggleSettings(NativeFTToggle.EyeTracking) { showInMenu = false }));

            Assert.That(
                menu.Toggles.Select(t => (t.Label, t.Parameter)),
                Is.EqualTo(new[]
                {
                    ("Face Tracking", NativeFTManagerParameterNames.FaceTracking),
                    ("Lip Tracking", NativeFTManagerParameterNames.LipTracking),
                }));
        }

        [Test]
        public void MenuPlan_PutsCorrectionsIntoSettings()
        {
            var menu = MenuPlan.Build(CreateUeToggles());

            Assert.That(
                menu.Toggles.Select(t => t.Label),
                Is.EqualTo(new[] { "Face Tracking", "Eye Tracking", "Lip Tracking", "Pupil", "Visemes" }));
            Assert.That(
                menu.Settings.Select(t => t.Label),
                Is.EqualTo(new[] { "Emulation", "Limits", "Eye Sync" }));
        }
    }
}
