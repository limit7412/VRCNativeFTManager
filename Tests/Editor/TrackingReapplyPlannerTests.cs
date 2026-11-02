using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    public class TrackingReapplyPlannerTests
    {
        private const string FT = NativeFTManagerParameterNames.FaceTracking;
        private const string Eye = NativeFTManagerParameterNames.EyeTracking;
        private const string Lip = NativeFTManagerParameterNames.LipTracking;
        private const string Visemes = NativeFTManagerParameterNames.Visemes;

        /// <summary>既定の設定（すべてのトグルをメニューに出す）に、指定したトグルの設定を上書きする</summary>
        private static ToggleSet CreateToggles(params ToggleSettings[] overrides)
        {
            var settings = ToggleSettings.CreateDefaults();
            foreach (var entry in overrides)
            {
                settings[settings.FindIndex(s => s.toggle == entry.toggle)] = entry;
            }

            return ToggleSet.Resolve(toggle => settings.FirstOrDefault(s => s.toggle == toggle), CorrectionFeatures.All);
        }

        private static ToggleSettings Hidden(NativeFTToggle toggle, bool defaultValue)
        {
            return new ToggleSettings(toggle, false, defaultValue);
        }

        private static string Describe(TrackingReapplyTransitionPlan transition)
        {
            var conditions = transition.Conditions.Select(c => (c.Expected ? "" : "!") + c.Parameter);
            string exit = transition.HasExitTime ? $" (exit {transition.ExitTime})" : "";
            return $"{transition.From}->{transition.To}: {string.Join(" & ", conditions)}{exit}";
        }

        [Test]
        public void Build_WithoutTheOption_HasNoLayer()
        {
            var plan = ToggleControllerPlanner.Build(CreateToggles());

            Assert.That(plan.TrackingReapply, Is.Null);
        }

        [Test]
        public void Build_WithTheOption_AddsTheLayer()
        {
            var plan = ToggleControllerPlanner.Build(CreateToggles(), trackingReapply: true);

            Assert.That(plan.TrackingReapply, Is.Not.Null);
            Assert.That(plan.HasAnyLayer, Is.True);
        }

        [Test]
        public void Build_Defaults_HasAStatePerCombination()
        {
            var plan = TrackingReapplyPlanner.Build(CreateToggles());

            Assert.That(
                plan.States.Select(state => (state.Name, state.Eyes, state.Mouth, state.Loops)),
                Is.EqualTo(new[]
                {
                    (TrackingReapplyPlan.ArmedState, false, false, false),
                    ("Apply Eyes+Mouth", true, true, true),
                    ("Apply Eyes", true, false, true),
                    ("Apply Mouth", false, true, true),
                    (TrackingReapplyPlan.IdleState, false, false, false),
                }));
        }

        [Test]
        public void Build_Defaults_ArmedPicksTheCombinationFromTheToggles()
        {
            var plan = TrackingReapplyPlanner.Build(CreateToggles());

            // 口はLipのSetの条件にVisemesを足したもの。Face Trackingは目と口で1つにまとめ、
            // Face Trackingのオンとオフを同時に求める組み合わせは作らない
            Assert.That(
                plan.TransitionsFrom(TrackingReapplyPlan.ArmedState).Select(Describe),
                Is.EqualTo(new[]
                {
                    $"Armed->Apply Eyes+Mouth: {FT} & {Eye} & {Lip} & {Visemes}",
                    $"Armed->Apply Eyes: {FT} & {Eye} & !{Lip}",
                    $"Armed->Apply Eyes: {FT} & {Eye} & !{Visemes}",
                    $"Armed->Apply Mouth: !{Eye} & {FT} & {Lip} & {Visemes}",
                    $"Armed->Idle: !{FT}",
                    $"Armed->Idle: !{FT} & !{Lip}",
                    $"Armed->Idle: !{FT} & !{Visemes}",
                    $"Armed->Idle: !{Eye} & !{FT}",
                    $"Armed->Idle: !{Eye} & !{Lip}",
                    $"Armed->Idle: !{Eye} & !{Visemes}",
                }));
        }

        [Test]
        public void Build_Defaults_ApplyFollowsTogglesBeforeLooping()
        {
            var plan = TrackingReapplyPlanner.Build(CreateToggles());

            // トグルが変わったらすぐArmedへ戻る。条件付きの遷移を先に並べ、最後に周期のループを置く
            Assert.That(
                plan.TransitionsFrom("Apply Eyes+Mouth").Select(Describe),
                Is.EqualTo(new[]
                {
                    $"Apply Eyes+Mouth->Armed: !{FT}",
                    $"Apply Eyes+Mouth->Armed: !{Eye}",
                    $"Apply Eyes+Mouth->Armed: !{Lip}",
                    $"Apply Eyes+Mouth->Armed: !{Visemes}",
                    "Apply Eyes+Mouth->Armed:  (exit 1)",
                }));
            Assert.That(
                plan.TransitionsFrom(TrackingReapplyPlan.IdleState).Select(Describe),
                Is.EqualTo(new[]
                {
                    $"Idle->Armed: {FT} & {Eye}",
                    $"Idle->Armed: {FT} & {Lip} & {Visemes}",
                }),
                "Idleは周期を持たず、トグルの変化だけで抜ける");
        }

        [Test]
        public void Build_AllTogglesHiddenAndOn_AlwaysAppliesBoth()
        {
            var plan = TrackingReapplyPlanner.Build(CreateToggles(
                Hidden(NativeFTToggle.FaceTracking, true),
                Hidden(NativeFTToggle.EyeTracking, true),
                Hidden(NativeFTToggle.LipTracking, true),
                Hidden(NativeFTToggle.Visemes, true)));

            Assert.That(
                plan.States.Select(state => state.Name),
                Is.EqualTo(new[] { TrackingReapplyPlan.ArmedState, "Apply Eyes+Mouth" }));

            // 条件の無い遷移は作れないので、Armedは終了時間0で抜ける
            Assert.That(
                plan.Transitions.Select(Describe),
                Is.EqualTo(new[]
                {
                    "Armed->Apply Eyes+Mouth:  (exit 0)",
                    "Apply Eyes+Mouth->Armed:  (exit 1)",
                }));
        }

        [Test]
        public void Build_HiddenVisemesOff_OnlyAppliesTheEyes()
        {
            // Visemesを既定でオフに固定すると口は常にAnimationなので、口は戻さない
            var plan = TrackingReapplyPlanner.Build(CreateToggles(Hidden(NativeFTToggle.Visemes, false)));

            Assert.That(plan.States.Any(state => state.Mouth), Is.False);
            Assert.That(
                plan.States.Select(state => state.Name),
                Is.EqualTo(new[] { TrackingReapplyPlan.ArmedState, "Apply Eyes", TrackingReapplyPlan.IdleState }));
        }

        [Test]
        public void Build_NeitherEyesNorMouthCanBeTracked_HasNoLayer()
        {
            var plan = TrackingReapplyPlanner.Build(CreateToggles(Hidden(NativeFTToggle.FaceTracking, false)));

            Assert.That(plan, Is.Null);
        }
    }
}
