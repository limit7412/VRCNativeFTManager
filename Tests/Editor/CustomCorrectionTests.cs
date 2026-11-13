using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// カスタム補正の規則への変換と、表情補正の計画への組み込み
    /// </summary>
    public class CustomCorrectionTests
    {
        private const string Corrective = "JawOpen * clamp(TongueOut * 5 - 1, 0, 1)";

        private static ToggleSet UeToggles(CorrectionFeatures features)
        {
            var settings = ToggleSettings.CreateDefaults();
            return ToggleSet.Resolve(toggle => settings.FirstOrDefault(s => s.toggle == toggle), features);
        }

        private static ToggleSet HiddenToggles(CorrectionFeatures features)
        {
            var settings = ToggleSettings.CreateDefaults()
                .Select(s => new ToggleSettings(s.toggle, false, true))
                .ToList();
            return ToggleSet.Resolve(toggle => settings.FirstOrDefault(s => s.toggle == toggle), features);
        }

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

        private static CustomCorrectionSource Source(
            string shape,
            string expression,
            CorrectionMode mode = CorrectionMode.ApplyToBlendshape,
            bool enabled = true)
        {
            return new CustomCorrectionSource(enabled, shape, mode, expression);
        }

        private static IReadOnlyList<CorrectionRule> Rules(params CustomCorrectionSource[] sources)
        {
            var built = CustomCorrections.Build(sources);
            Assert.That(built.Problems, Is.Empty);
            return built.Rules;
        }

        [Test]
        public void Build_ValidSource_BecomesARuleTargetingTheShapeByName()
        {
            var built = CustomCorrections.Build(new[] { Source(" TongueJawCorrection ", Corrective, CorrectionMode.LimitMin) });

            Assert.That(built.Problems, Is.Empty);
            var rule = built.Rules.Single();
            Assert.That(rule.Id, Is.EqualTo("Custom#1:TongueJawCorrection"));
            Assert.That(rule.Feature, Is.EqualTo(CorrectionFeature.Custom));
            Assert.That(rule.Mode, Is.EqualTo(CorrectionMode.LimitMin));
            var target = rule.Targets.Single();
            Assert.That(target.Key, Is.EqualTo(CorrectionTarget.NoKey));
            Assert.That(target.Shape, Is.EqualTo("TongueJawCorrection"), "前後の空白は除く");
            Assert.That(target.Expression, Is.EqualTo(Corrective));
        }

        [Test]
        public void Build_DisabledAndNullSources_AreLeftOutSilently()
        {
            var built = CustomCorrections.Build(new[]
            {
                Source("A", "JawOpen", enabled: false),
                null,
                Source("B", "JawOpen"),
            });

            Assert.That(built.Problems, Is.Empty);
            Assert.That(built.Rules.Select(rule => rule.Id), Is.EqualTo(new[] { "Custom#3:B" }), "番号は一覧での位置を保つ");
        }

        [Test]
        public void Build_EmptyShapeAndInvalidExpressions_AreReportedAsProblems()
        {
            var built = CustomCorrections.Build(new[]
            {
                Source("  ", "JawOpen"),
                Source("A", ""),
                Source("B", "JawOpen *"),
                Source("C", "NoSuchKey * 2"),
                Source("D", "TongueOut"),
            });

            Assert.That(built.Rules.Select(rule => rule.Id), Is.EqualTo(new[] { "Custom#5:D" }));
            Assert.That(
                built.Problems.Select(problem => (problem.Index, problem.Kind)),
                Is.EqualTo(new[]
                {
                    (0, CustomCorrectionProblemKind.EmptyShape),
                    (1, CustomCorrectionProblemKind.InvalidExpression),
                    (2, CustomCorrectionProblemKind.InvalidExpression),
                    (3, CustomCorrectionProblemKind.InvalidExpression),
                }));
            Assert.That(built.Problems[0].Describe(), Does.StartWith("Custom#1 "));
            Assert.That(built.Problems[3].Describe(), Does.Contain("NoSuchKey"));
        }

        [Test]
        public void Build_NullList_IsEmpty()
        {
            var built = CustomCorrections.Build(null);

            Assert.That(built.Rules, Is.Empty);
            Assert.That(built.Problems, Is.Empty);
        }

        [Test]
        public void Plan_CustomRule_WritesTheExpressionToTheNamedShape()
        {
            var rules = Rules(Source("TongueJawCorrection", Corrective));

            var plan = CorrectionPlanner.Plan(
                new[] { FullSet() }, UeToggles(CorrectionFeatures.None), null, 0, customRules: rules);

            var behavior = plan.Behaviors.Single();
            Assert.That(behavior.RuleId, Is.EqualTo("Custom#1:TongueJawCorrection"));
            Assert.That(behavior.Mode, Is.EqualTo(CorrectionMode.ApplyToBlendshape));
            Assert.That(behavior.TargetBlendShape, Is.EqualTo("TongueJawCorrection"));
            Assert.That(behavior.Expression, Is.EqualTo(Corrective), "Setの倍率は掛けない");
            Assert.That(plan.Instructions, Is.EqualTo(CustomExpression.Analyze(Corrective).Instructions));
            Assert.That(plan.MissingShapes, Is.Empty);
            Assert.That(plan.MissingTargets, Is.Empty);
        }

        [Test]
        public void Plan_CustomRule_IsGatedByFaceTrackingAndTheSidesItReads()
        {
            var rules = Rules(
                Source("LipOnly", "JawOpen"),
                Source("EyeOnly", "EyeWideLeft"),
                Source("Both", "JawOpen * EyeWideLeft"),
                Source("NoKeys", "Voice"));

            var plan = CorrectionPlanner.Plan(
                new[] { FullSet() }, UeToggles(CorrectionFeatures.None), null, 0, customRules: rules);

            string GateOf(string shape) => plan.Behaviors.Single(b => b.TargetBlendShape == shape).GateSetName;

            Assert.That(GateOf("LipOnly"), Is.EqualTo("KxNFTM/Gate/Custom/Lip"));
            Assert.That(GateOf("EyeOnly"), Is.EqualTo("KxNFTM/Gate/Custom/Eye"));
            Assert.That(GateOf("Both"), Is.EqualTo("KxNFTM/Gate/Custom/EyeLip"));
            Assert.That(GateOf("NoKeys"), Is.EqualTo("KxNFTM/Gate/Custom"));
            Assert.That(
                plan.Gates.Select(g => g.SetName),
                Is.EquivalentTo(plan.Behaviors.Select(b => b.GateSetName).Distinct()));
        }

        [Test]
        public void Plan_CustomRule_DoesNotDependOnTheCorrectionFeatures()
        {
            var rules = Rules(Source("TongueJawCorrection", Corrective));

            var none = CorrectionPlanner.Plan(
                new[] { FullSet() }, UeToggles(CorrectionFeatures.None), null, 0, customRules: rules);
            var all = CorrectionPlanner.Plan(
                new[] { FullSet() }, UeToggles(CorrectionFeatures.All), null, 0, customRules: rules);

            Assert.That(none.Behaviors.Select(b => b.RuleId), Is.EqualTo(new[] { "Custom#1:TongueJawCorrection" }));
            Assert.That(all.Behaviors.First().RuleId, Is.EqualTo("Custom#1:TongueJawCorrection"), "規則表より前に並ぶ");
            Assert.That(
                all.Behaviors.Select(b => b.RuleId).Distinct().Skip(1),
                Is.EqualTo(CorrectionRuleTable.All.Select(rule => rule.Id)));
        }

        [Test]
        public void Plan_NoMenuToggles_CustomRuleNeedsNoGate()
        {
            var rules = Rules(Source("TongueJawCorrection", Corrective));

            var plan = CorrectionPlanner.Plan(
                new[] { FullSet() }, HiddenToggles(CorrectionFeatures.None), null, 0, customRules: rules);

            Assert.That(plan.Behaviors.Single().GateSetName, Is.Null);
            Assert.That(plan.Gates, Is.Empty);
        }

        [Test]
        public void Plan_UnknownShape_IsReportedAndNotGenerated()
        {
            var rules = Rules(Source("TongueJawCorrection", Corrective), Source("Missing", "JawOpen"));

            var plan = CorrectionPlanner.Plan(
                new[] { FullSet() },
                UeToggles(CorrectionFeatures.None),
                null,
                0,
                customRules: rules,
                knownShapes: new HashSet<string> { "TongueJawCorrection" });

            Assert.That(plan.Behaviors.Select(b => b.TargetBlendShape), Is.EqualTo(new[] { "TongueJawCorrection" }));
            Assert.That(plan.MissingShapes, Is.EqualTo(new[] { "Custom#2:Missing" }));
            Assert.That(plan.MissingTargets, Is.Empty, "組み込みの規則のI-01には混ぜない");
        }

        [Test]
        public void Plan_OverBudget_DropsBuiltInRulesBeforeCustomRules()
        {
            var rules = Rules(Source("TongueJawCorrection", Corrective));
            var toggles = UeToggles(CorrectionFeatures.All);
            var full = CorrectionPlanner.Plan(new[] { FullSet() }, toggles, null, 0, customRules: rules);
            int existing = CustomExpression.AvatarInstructionLimit - full.Instructions + 1;

            var plan = CorrectionPlanner.Plan(new[] { FullSet() }, toggles, null, existing, customRules: rules);

            Assert.That(plan.DroppedRules, Is.Not.Empty);
            Assert.That(plan.DroppedRules, Has.None.StartsWith(CustomCorrections.IdPrefix));
            Assert.That(plan.Behaviors.First().RuleId, Is.EqualTo("Custom#1:TongueJawCorrection"));
        }

        [Test]
        public void Plan_CustomRuleOverBudget_IsDropped()
        {
            var rules = Rules(Source("TongueJawCorrection", Corrective));

            var plan = CorrectionPlanner.Plan(
                new[] { FullSet() },
                UeToggles(CorrectionFeatures.None),
                null,
                CustomExpression.AvatarInstructionLimit,
                customRules: rules);

            Assert.That(plan.Behaviors, Is.Empty);
            Assert.That(plan.DroppedRules, Is.EqualTo(new[] { "Custom#1:TongueJawCorrection" }));
        }

        [Test]
        public void GetPossibleGates_CoversTheGatesOfCustomRules()
        {
            var rules = Rules(Source("A", "JawOpen"), Source("B", "EyeWideLeft"), Source("C", "Voice"));
            var toggles = UeToggles(CorrectionFeatures.All);

            var possible = CorrectionPlanner.GetPossibleGates(toggles, new string[0], rules).Select(g => g.Key).ToList();
            var used = CorrectionPlanner.Plan(new[] { FullSet() }, toggles, null, 0, customRules: rules)
                .Gates.Select(g => g.Key);

            Assert.That(used, Is.SubsetOf(possible));
            Assert.That(possible, Has.Member("Gate/Custom/Lip"));
            Assert.That(possible, Has.Member("Gate/Custom/Eye"));
            Assert.That(possible, Has.Member("Gate/Custom"));
        }
    }
}
