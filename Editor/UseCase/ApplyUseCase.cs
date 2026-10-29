using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using VRC.SDK3.Avatars.Components;
using NativeFTManager.Domain;
using NativeFTManager.Infra;
using Object = UnityEngine.Object;

namespace NativeFTManager.UseCase
{
    /// <summary>
    /// Transforming Phaseの処理（仕様書9章の手順5）
    ///
    /// ARKit BlendShape GeneratorとModular Avatarが終わった後に、
    /// DescriptorのSetをグループごとに分割し、マージ後のFXでトグルのステートにSetの重みを書くBehaviorを付け、
    /// DescriptorのFace Trackingの設定を整える
    /// </summary>
    internal static class ApplyUseCase
    {
        public static void Execute(BuildContext context)
        {
            var avatarRoot = context.AvatarRootObject;
            var components = ComponentSelection.CollectComponents(avatarRoot);
            var primary = ComponentSelection.SelectPrimaryComponent(avatarRoot, components);

            try
            {
                if (primary != null)
                {
                    Apply(context, primary);
                }
            }
            finally
            {
                // ビルド時にのみ意味を持つコンポーネントなので、処理の成否にかかわらず取り除く。
                // ビルド終盤で動作するAvatar Optimizer等から「未知のコンポーネント」として
                // 検出されないようにするため、Optimizing Phaseより前のここで削除する。
                ComponentSelection.RemoveComponents(components);
            }
        }

        private static void Apply(BuildContext context, NativeFTManagerComponent primary)
        {
            var descriptor = context.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                return;
            }

            var state = context.GetState<BuildState>();
            var toggles = state.Toggles ?? ComponentSettings.ResolveToggles(primary);
            var plan = state.Plan ?? ToggleControllerPlanner.Build(toggles);
            var fx = FxControllerLocator.Find(descriptor);

            var mismatch = FaceTrackingDescriptorAccess.FindKeyMismatch();
            if (mismatch != null)
            {
                DiagnosticReporter.Report(new Diagnostic(DiagnosticCodes.KeyMismatch, mismatch), primary);
                RemoveLayers(fx, plan);
                return;
            }

            var sets = FaceTrackingDescriptorAccess.ReadSets(descriptor);

            if (state.GeneratorSetExpected && state.GeneratorSetName != null)
            {
                var generatorSet = sets.FirstOrDefault(set => set.Name == state.GeneratorSetName);
                if (AvatarDiagnostics.IsGeneratorSetSparse(generatorSet))
                {
                    DiagnosticReporter.Report(
                        new Diagnostic(
                            DiagnosticCodes.GeneratorSetSparse,
                            generatorSet?.AssignedKeyCount ?? 0),
                        primary);
                }
            }

            if (sets.Count == 0)
            {
                DiagnosticReporter.Report(new Diagnostic(DiagnosticCodes.NoSets), primary);
                RemoveLayers(fx, plan);
                return;
            }

            var excluded = new HashSet<string>(primary.excludedSetNames ?? new List<string>());
            // 既存のState Behaviorが名前で指すSetも分割しない。分割すると元の名前が消え、その制御が効かなくなる。
            // 本製品のBehaviorはこの後で付けるので、ここで見つかるのは利用者が置いたものだけである
            var referenced = FaceTrackingDescriptorAccess.GetSetNamesReferencedByBehaviors(descriptor);
            referenced.UnionWith(FaceTrackingStateBehaviorWriter.CollectReferencedSetNames(
                FaceTrackingDescriptorAccess.GetAnimatorControllers(descriptor)));
            var result = SetSplitPlanner.Split(sets, excluded, referenced, group => plan.DefaultWeights[group]);

            foreach (var kept in result.Kept.Where(kept => kept.Reason == SetKeepReason.ReferencedByBehavior))
            {
                DiagnosticReporter.Report(new Diagnostic(DiagnosticCodes.SetReferencedByBehavior, kept.Name), primary);
            }

            var corrections = PlanCorrections(descriptor, result, toggles, primary);

            // Eye SyncのBehaviorが無いのにEyeClosedをEye Syncで止めると、まばたきが出なくなる。
            // その場合はEyeと同じ条件で切り替えるので、既定の重みもEyeにそろえる
            foreach (var split in result.Created.Where(split => corrections.GetLayerGroup(split.Group) != split.Group))
            {
                split.Data.Weight = plan.DefaultWeights[corrections.GetLayerGroup(split.Group)];
            }
            var gateSets = corrections.Gates
                .Select(gate => FaceTrackingSetData.CreateEmpty(gate.SetName, GetGateWeight(plan, gate, toggles)));

            FaceTrackingDescriptorAccess.WriteSets(descriptor, result.Sets.Concat(gateSets));
            FaceTrackingDescriptorAccess.AppendBehaviors(descriptor, corrections.Behaviors);
            AttachBehaviors(context, fx, plan, result, corrections, primary);
            FaceTrackingDescriptorAccess.ApplySettings(descriptor, primary);
        }

        /// <summary>
        /// 表情補正のParameter Behaviorsを決める（仕様書5.4）。
        /// 対象のシェイプは分割後のSetから引くので、分割の後に呼ぶ
        /// </summary>
        private static CorrectionPlan PlanCorrections(
            VRCAvatarDescriptor descriptor,
            SetSplitResult result,
            ToggleSet toggles,
            NativeFTManagerComponent primary)
        {
            int existingInstructions = FaceTrackingDescriptorAccess.ReadExistingBehaviors(descriptor)
                .Where(behavior => behavior.CustomExpression != null)
                .Select(behavior => CustomExpression.Analyze(behavior.CustomExpression))
                .Where(info => info.IsValid)
                .Sum(info => info.Instructions);

            var corrections = CorrectionPlanner.Plan(
                result.CorrectionSources, toggles, ComponentSettings.GetExcludedRules(primary), existingInstructions);

            if (corrections.MissingTargets.Count > 0)
            {
                DiagnosticReporter.Report(
                    new Diagnostic(DiagnosticCodes.CorrectionTargetMissing, string.Join(", ", corrections.MissingTargets)),
                    primary);
            }

            if (corrections.DroppedRules.Count > 0)
            {
                DiagnosticReporter.Report(
                    new Diagnostic(
                        DiagnosticCodes.CorrectionInstructionsExceeded,
                        string.Join(", ", corrections.DroppedRules),
                        existingInstructions),
                    primary);
            }

            return corrections;
        }

        private static float GetGateWeight(ToggleControllerPlan plan, GateSlot gate, ToggleSet toggles)
        {
            if (plan.GateWeights.TryGetValue(gate.Key, out var weight))
            {
                return weight;
            }

            return gate.Condition.EvaluateDefaults(toggles) ? 1f : 0f;
        }

        private static void AttachBehaviors(
            BuildContext context,
            UnityEditor.Animations.AnimatorController fx,
            ToggleControllerPlan plan,
            SetSplitResult result,
            CorrectionPlan corrections,
            NativeFTManagerComponent primary)
        {
            if (fx == null || plan.Layers.Count == 0)
            {
                return;
            }

            bool reportedUnavailable = false;
            foreach (var layer in plan.Layers)
            {
                var setNames = result.Created
                    .Where(split => layer.Groups.Contains(corrections.GetLayerGroup(split.Group)))
                    .Select(split => split.Data.Name)
                    .Concat(corrections.Gates
                        .Where(gate => layer.Gates.Any(planned => planned.Key == gate.Key))
                        .Select(gate => gate.SetName))
                    .ToArray();

                var outcome = ToggleBehaviorAttacher.Attach(
                    fx, layer, setNames, primary.lerpSpeed, asset => context.AssetSaver.SaveAsset(asset));

                if (outcome == ToggleBehaviorAttacher.Outcome.BehaviorUnavailable && !reportedUnavailable)
                {
                    FaceTrackingStateBehaviorWriter.TryResolve(out var failure);
                    DiagnosticReporter.Report(
                        new Diagnostic(DiagnosticCodes.StateBehaviorUnavailable, failure),
                        primary);
                    reportedUnavailable = true;
                }
            }
        }

        /// <summary>
        /// 切り替えるSetが無いときは、生成したレイヤーを残さない
        /// </summary>
        private static void RemoveLayers(UnityEditor.Animations.AnimatorController fx, ToggleControllerPlan plan)
        {
            if (fx == null)
            {
                return;
            }

            foreach (var layer in plan.Layers)
            {
                ToggleBehaviorAttacher.Attach(fx, layer, new string[0], 0f, null);
            }
        }
    }
}
