using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using NativeFTManager.Domain;
using NativeFTManager.Infra;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// インスペクタに出す状態と警告（仕様書6.4の1と2）
    ///
    /// ビルドと同じ判定をシーンのアバターに対して行う。
    /// GeneratorのSetはビルド中にしか存在しないので、Setの有無はGeneratorの設定から見込む
    /// </summary>
    internal static class InspectUseCase
    {
        internal sealed class Report
        {
            public bool HasAvatar;
            public int SetCount;
            public int AssignedKeyCount;
            public bool UseAllTrackingKeys;
            public GeneratorPresence Generator;
            public bool GeneratorSetWriteEnabled;
            public readonly List<Diagnostic> Diagnostics = new List<Diagnostic>();

            /// <summary>ビルド結果の見積もり（F-13）。アバターが無ければnull</summary>
            public BuildEstimate Estimate;

            /// <summary>表情補正の計画。見積もりと規則の一覧の表示に使う</summary>
            public CorrectionPlan Corrections = CorrectionPlan.Empty;

            /// <summary>GeneratorのSetがビルド中にしか無く、見積もりに入っていないか</summary>
            public bool EstimateExcludesGenerator;
        }

        public static Report Inspect(NativeFTManagerComponent component)
        {
            var report = new Report();
            var avatarRoot = FindAvatarRoot(component);
            if (avatarRoot == null)
            {
                report.Diagnostics.Add(new Diagnostic(DiagnosticCodes.OutsideAvatar));
                return report;
            }

            report.HasAvatar = true;

            var toggles = ComponentSettings.ResolveToggles(component);
            var facts = AvatarScanner.Scan(avatarRoot, component, toggles.Dynamic);
            report.Diagnostics.AddRange(AvatarDiagnostics.Evaluate(facts));

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var sets = FaceTrackingDescriptorAccess.ReadSets(descriptor);
            report.SetCount = sets.Count;
            foreach (var set in sets)
            {
                report.AssignedKeyCount += set.AssignedKeyCount;
            }

            report.UseAllTrackingKeys = facts.DescriptorUseAllTrackingKeys;
            report.Generator = facts.Generator;
            report.GeneratorSetWriteEnabled = facts.GeneratorSetWriteEnabled;

            bool generatorWillWrite = facts.Generator == GeneratorPresence.Supported
                                      && (component.generatorIntegration == GeneratorIntegration.Auto
                                          || facts.GeneratorSetWriteEnabled);
            if (sets.Count == 0 && !generatorWillWrite)
            {
                report.Diagnostics.Insert(0, new Diagnostic(DiagnosticCodes.NoSets));
            }

            Estimate(report, descriptor, sets, toggles, component, facts);
            report.EstimateExcludesGenerator = generatorWillWrite
                                               && GeneratorIntegrationRegistry.Current != null
                                               && sets.All(set => set.Name != GeneratorIntegrationRegistry.Current.SetName);

            return report;
        }

        /// <summary>
        /// ビルドと同じ規則で分割と表情補正をなぞり、見積もりを出す（仕様書6.4の7）
        /// </summary>
        private static void Estimate(
            Report report,
            VRCAvatarDescriptor descriptor,
            IReadOnlyList<FaceTrackingSetData> sets,
            ToggleSet toggles,
            NativeFTManagerComponent component,
            AvatarFacts facts)
        {
            var excludedRules = ComponentSettings.GetExcludedRules(component);
            var plan = ToggleControllerPlanner.Build(toggles, CorrectionPlanner.GetPossibleGates(toggles, excludedRules));

            var referenced = FaceTrackingDescriptorAccess.GetSetNamesReferencedByBehaviors(descriptor);
            referenced.UnionWith(FaceTrackingStateBehaviorWriter.CollectReferencedSetNames(
                FaceTrackingDescriptorAccess.GetAnimatorControllers(descriptor)));
            var split = SetSplitPlanner.Split(
                sets,
                new HashSet<string>(component.excludedSetNames ?? new List<string>()),
                referenced,
                group => plan.DefaultWeights[group]);

            var existing = FaceTrackingDescriptorAccess.ReadExistingBehaviors(descriptor);
            int existingInstructions = existing
                .Where(behavior => behavior.CustomExpression != null)
                .Select(behavior => CustomExpression.Analyze(behavior.CustomExpression))
                .Where(info => info.IsValid)
                .Sum(info => info.Instructions);

            report.Corrections = CorrectionPlanner.Plan(split.Sets, toggles, excludedRules, existingInstructions);
            report.Estimate = BuildEstimate.Calculate(
                split.Sets,
                split.Created.Count,
                report.Corrections,
                existing,
                toggles,
                plan,
                FaceTrackingDescriptorAccess.GetMeshCounter(descriptor),
                facts.OtherSyncedBits);

            if (report.Corrections.DroppedRules.Count > 0)
            {
                report.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.CorrectionInstructionsExceeded,
                    string.Join(", ", report.Corrections.DroppedRules),
                    existingInstructions));
            }
        }

        /// <summary>
        /// 親を辿って最も外側のVRCAvatarDescriptorを持つオブジェクト（DuplicateComponentGuardと同じ規則）
        /// </summary>
        private static GameObject FindAvatarRoot(NativeFTManagerComponent component)
        {
            GameObject found = null;
            for (var cursor = component.transform; cursor != null; cursor = cursor.parent)
            {
                if (cursor.GetComponent<VRCAvatarDescriptor>() != null)
                {
                    found = cursor.gameObject;
                }
            }

            return found;
        }
    }
}
