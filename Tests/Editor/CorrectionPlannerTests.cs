using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class CorrectionPlannerTests
    {
        private static ToggleSet UeToggles(CorrectionFeatures features)
        {
            var settings = NativeFTPresets.CreateToggles(NativeFTPreset.UE);
            return ToggleSet.Resolve(toggle => settings.FirstOrDefault(s => s.toggle == toggle), features);
        }

        private static ToggleSet HiddenToggles(CorrectionFeatures features)
        {
            var settings = NativeFTPresets.CreateToggles(NativeFTPreset.UE)
                .Select(s => new ToggleSettings(s.toggle, false, true))
                .ToList();
            return ToggleSet.Resolve(toggle => settings.FirstOrDefault(s => s.toggle == toggle), features);
        }

        /// <summary>
        /// UEキーと同名のシェイプをすべて持つSet
        /// </summary>
        private static FaceTrackingSetData FullSet()
        {
            var set = FaceTrackingSetData.CreateEmpty("Generator", 1f);
            var names = FaceTrackingGroupTable.GetKeyNames();
            for (int key = 0; key < names.Count; key++)
            {
                set.BlendShapeNames[key] = names[key];
            }

            return set;
        }

        private static FaceTrackingSetData SetWith(params (string Key, string Shape)[] shapes)
        {
            var set = FaceTrackingSetData.CreateEmpty("Generator", 1f);
            var names = FaceTrackingGroupTable.GetKeyNames().ToList();
            foreach (var (key, shape) in shapes)
            {
                set.BlendShapeNames[names.IndexOf(key)] = shape;
            }

            return set;
        }

        [Test]
        public void Plan_FullSet_CreatesEveryRuleInOrder()
        {
            var plan = CorrectionPlanner.Plan(new[] { FullSet() }, UeToggles(CorrectionFeatures.All), null, 0);

            Assert.That(
                plan.Behaviors.Select(b => b.RuleId).Distinct(),
                Is.EqualTo(CorrectionRuleTable.All.Select(rule => rule.Id)));
            Assert.That(plan.MissingTargets, Is.Empty);
            Assert.That(plan.DroppedRules, Is.Empty);
            Assert.That(
                plan.Instructions,
                Is.EqualTo(plan.Behaviors.Sum(b => CustomExpression.Analyze(b.Expression).Instructions)));
        }

        [Test]
        public void Plan_DisabledFeatureAndExcludedRule_AreSkipped()
        {
            var plan = CorrectionPlanner.Plan(
                new[] { FullSet() },
                UeToggles(new CorrectionFeatures(true, false, false)),
                new[] { "BrowAngry" },
                0);

            var ids = plan.Behaviors.Select(b => b.RuleId).Distinct().ToArray();
            Assert.That(ids, Has.No.Member("EyeSync"));
            Assert.That(ids, Has.No.Member("JawOpenHelper"));
            Assert.That(ids, Has.No.Member("BrowAngry"));
            Assert.That(ids, Has.Member("BrowSad"));
            Assert.That(plan.Behaviors.Select(b => b.Mode).Distinct(), Is.EqualTo(new[] { CorrectionMode.LimitMin }));
        }

        [Test]
        public void Plan_MissingShapes_AreReportedAsI01()
        {
            var set = SetWith(("JawOpen", "jaw"), ("TongueOut", "tongue"));
            var plan = CorrectionPlanner.Plan(new[] { set }, UeToggles(CorrectionFeatures.All), null, 0);

            var behavior = plan.Behaviors.Single(b => b.RuleId == "JawOpenHelper");
            Assert.That(behavior.TargetBlendShape, Is.EqualTo("jaw"));
            Assert.That(plan.Behaviors.Select(b => b.RuleId).Distinct(), Is.EquivalentTo(new[] { "JawOpenHelper", "TongueOutLimitFunnel" }));
            Assert.That(plan.MissingTargets, Has.Member("EyeSync"));
            Assert.That(plan.MissingTargets, Has.Member("BrowSad"));
        }

        [Test]
        public void Plan_SharedShapeWithSameExpression_IsWrittenOnce()
        {
            // 両目で1つのまばたきのシェイプなら、Eye Syncの式は左右で同じなので1つで足りる
            var set = SetWith(("EyeClosedRight", "blink"), ("EyeClosedLeft", "blink"));
            var plan = CorrectionPlanner.Plan(new[] { set }, UeToggles(CorrectionFeatures.All), null, 0);

            Assert.That(plan.Behaviors.Count(b => b.RuleId == "EyeSync"), Is.EqualTo(1));
        }

        [Test]
        public void Plan_SharedShapeWithDifferentExpressions_KeepsBoth()
        {
            var set = SetWith(("BrowOuterUpRight", "brow"), ("BrowOuterUpLeft", "brow"));
            var plan = CorrectionPlanner.Plan(new[] { set }, UeToggles(CorrectionFeatures.All), null, 0);

            Assert.That(plan.Behaviors.Count(b => b.RuleId == "BrowWide"), Is.EqualTo(2));
        }

        [Test]
        public void Plan_OverBudget_DropsLowerPriorityRules()
        {
            var full = CorrectionPlanner.Plan(new[] { FullSet() }, UeToggles(CorrectionFeatures.All), null, 0);
            int existing = CustomExpression.AvatarInstructionLimit - full.Instructions + 1;

            var plan = CorrectionPlanner.Plan(new[] { FullSet() }, UeToggles(CorrectionFeatures.All), null, existing);

            Assert.That(plan.DroppedRules, Is.Not.Empty);
            Assert.That(plan.Instructions + existing, Is.LessThanOrEqualTo(CustomExpression.AvatarInstructionLimit));
            Assert.That(plan.Behaviors.First().RuleId, Is.EqualTo("EyeSync"), "優先度の高い規則は残る");
        }

        [Test]
        public void Plan_MenuToggles_GateEachRuleByTheSidesItReads()
        {
            var plan = CorrectionPlanner.Plan(new[] { FullSet() }, UeToggles(CorrectionFeatures.All), null, 0);

            string GateOf(string id) => plan.Behaviors.First(b => b.RuleId == id).GateSetName;

            Assert.That(GateOf("EyeSync"), Is.EqualTo("KxNFTM/Gate/EyeSync/Eye"));
            Assert.That(GateOf("JawOpenHelper"), Is.EqualTo("KxNFTM/Gate/Limits/Lip"));
            Assert.That(GateOf("BrowAngry"), Is.EqualTo("KxNFTM/Gate/Emulation/EyeLip"));
            Assert.That(GateOf("EyeWidePupil"), Is.EqualTo("KxNFTM/Gate/Emulation/EyePupil"));
            Assert.That(GateOf("EyeSadPupil"), Is.EqualTo("KxNFTM/Gate/Emulation/EyePupilLip"));
            Assert.That(
                plan.Gates.Select(g => g.SetName),
                Is.EquivalentTo(plan.Behaviors.Select(b => b.GateSetName).Distinct()));
        }

        [Test]
        public void Plan_NoMenuToggles_NeedsNoGates()
        {
            var plan = CorrectionPlanner.Plan(new[] { FullSet() }, HiddenToggles(CorrectionFeatures.All), null, 0);

            Assert.That(plan.Behaviors, Is.Not.Empty);
            Assert.That(plan.Behaviors.Select(b => b.GateSetName), Is.All.Null);
            Assert.That(plan.Gates, Is.Empty);
        }

        [Test]
        public void GetPossibleGates_CoversEveryGateThePlanUses()
        {
            var toggles = UeToggles(CorrectionFeatures.All);
            var possible = CorrectionPlanner.GetPossibleGates(toggles, new string[0]).Select(g => g.Key);
            var used = CorrectionPlanner.Plan(new[] { FullSet() }, toggles, null, 0).Gates.Select(g => g.Key);

            Assert.That(used, Is.SubsetOf(possible));
        }

        [Test]
        public void Estimate_CountsSetsBehaviorsAndBits()
        {
            var toggles = UeToggles(CorrectionFeatures.All);
            var sets = new[] { FullSet() };
            var corrections = CorrectionPlanner.Plan(sets, toggles, null, 0);
            var gates = CorrectionPlanner.GetPossibleGates(toggles, new string[0]);
            var controller = ToggleControllerPlanner.Build(toggles, gates);
            var existing = new[]
            {
                new BuildEstimate.ExistingBehavior("JawOpen", 2, null),
                new BuildEstimate.ExistingBehavior("JawOpen", 0, "JawOpen * (2 + 3)"),
            };

            var estimate = BuildEstimate.Calculate(sets, 0, corrections, existing, toggles, controller, _ => 1, 10);

            Assert.That(estimate.BehaviorCount, Is.EqualTo(corrections.Behaviors.Count));
            Assert.That(estimate.GateSetCount, Is.EqualTo(corrections.Gates.Count));
            Assert.That(estimate.ExistingInstructions, Is.EqualTo(3));
            Assert.That(estimate.SyncedBits, Is.EqualTo(8));
            Assert.That(estimate.TotalSyncedBits, Is.EqualTo(18));
            int mappings = FaceTrackingGroupTable.KeyCount;
            int customOperations = corrections.Behaviors.Sum(b => CustomExpression.Analyze(b.Expression).Operations);
            Assert.That(estimate.Operations, Is.EqualTo(mappings + customOperations + 2 + 2));
        }
    }
}
