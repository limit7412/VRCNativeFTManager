using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class ToggleControllerPlannerTests
    {
        private static ToggleSet CreateToggles(params ToggleSettings[] overrides)
        {
            var settings = ToggleSettings.CreateDefaults();
            foreach (var entry in overrides)
            {
                settings[settings.FindIndex(s => s.toggle == entry.toggle)] = entry;
            }

            return ToggleSet.Resolve(toggle => settings.First(s => s.toggle == toggle));
        }

        private static string Describe(ToggleTransitionPlan transition)
        {
            var conditions = transition.Conditions.Select(c => (c.Expected ? "" : "!") + c.Parameter);
            return $"{transition.From}->{transition.To}: {string.Join(" & ", conditions)}";
        }

        [Test]
        public void Build_DeclaresOneBoolPerMenuToggle()
        {
            var plan = ToggleControllerPlanner.Build(CreateToggles());

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
        public void Build_MergesGroupsWithTheSameCondition()
        {
            var plan = ToggleControllerPlanner.Build(CreateToggles());

            Assert.That(
                plan.Layers.Select(layer => layer.Name),
                Is.EqualTo(new[]
                {
                    "KxNFTM Eye/EyeClosed/Pupil/Brow",
                    "KxNFTM Lip",
                    "KxNFTM Other",
                }));
        }

        [Test]
        public void Build_EyeLayer_UsesConjunctionForOnAndOneTransitionPerNegationForOff()
        {
            var plan = ToggleControllerPlanner.Build(CreateToggles());
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
            var plan = ToggleControllerPlanner.Build(CreateToggles(
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
            var plan = ToggleControllerPlanner.Build(CreateToggles(
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

            var plan = ToggleControllerPlanner.Build(CreateToggles(hidden.ToArray()));

            Assert.That(plan.Layers, Is.Empty);
            Assert.That(plan.Parameters, Is.Empty);
            Assert.That(plan.DefaultWeights.Values, Is.All.EqualTo(1f));
        }

        [Test]
        public void Build_DefaultWeights_FollowToggleDefaults()
        {
            var plan = ToggleControllerPlanner.Build(CreateToggles(
                new ToggleSettings(NativeFTToggle.EyeTracking) { defaultValue = false }));

            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Eye], Is.EqualTo(0f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Brow], Is.EqualTo(0f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Lip], Is.EqualTo(1f));
            Assert.That(plan.DefaultWeights[FaceTrackingGroup.Other], Is.EqualTo(1f));
        }

        [Test]
        public void MenuPlan_ListsMenuTogglesInOrder()
        {
            var menu = MenuPlan.Build(CreateToggles(
                new ToggleSettings(NativeFTToggle.EyeTracking) { showInMenu = false }));

            Assert.That(
                menu.Toggles.Select(t => (t.Label, t.Parameter)),
                Is.EqualTo(new[]
                {
                    ("Face Tracking", NativeFTManagerParameterNames.FaceTracking),
                    ("Lip Tracking", NativeFTManagerParameterNames.LipTracking),
                }));
        }
    }
}
