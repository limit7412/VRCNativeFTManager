using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// ビルド結果の見積もり（仕様書6.4の7、F-13）
    ///
    /// ビルド前のDescriptorから、分割とParameter Behaviorsの生成を同じ規則でなぞって数える。
    /// Face Tracking Operationsは、SDK 3.10.6の<c>AvatarPerformance.CalculatePerformanceStats</c>と突き合わせた数え方に合わせる（issue #39、#42）。
    /// キーのシェイプ対応は、そのシェイプを持つFace Tracking MeshのRendererの数だけ数える。同じMeshを使うRendererも別に数える。
    /// Setのビゼーム対応は数えない。Parameter Behaviorは演算の数を1回だけ数え、Rendererの数で掛けない。
    /// 重み0のSetも数えられるので、分割したSetとゲートSetもそのまま数える。
    /// Custom以外のParameter Behaviorの数え方はSDKと突き合わせていないので、入力キーの数を1回だけ数える目安とする
    /// </summary>
    internal sealed class BuildEstimate
    {
        /// <summary>Expression Parametersの同期bitの上限</summary>
        public const int SyncedBitLimit = 256;

        public int SplitSetCount;
        public int GateSetCount;
        public int BehaviorCount;
        public int ExistingBehaviorCount;
        public int Instructions;
        public int ExistingInstructions;
        public int Operations;
        public int SyncedBits;
        public int OtherSyncedBits;
        public int FxLayerCount;

        public int TotalInstructions => Instructions + ExistingInstructions;

        public int TotalSyncedBits => SyncedBits + OtherSyncedBits;

        public bool ExceedsSyncedBits => TotalSyncedBits > SyncedBitLimit;

        /// <summary>Descriptorに元からあるParameter Behavior 1つ分の見積もり材料</summary>
        internal readonly struct ExistingBehavior
        {
            public ExistingBehavior(string targetBlendShape, int inputCount, string customExpression)
            {
                TargetBlendShape = targetBlendShape;
                InputCount = inputCount;
                CustomExpression = customExpression;
            }

            public string TargetBlendShape { get; }

            /// <summary>Custom以外のときの入力キーの数</summary>
            public int InputCount { get; }

            /// <summary>CustomのときのCustom式。Custom以外ならnull</summary>
            public string CustomExpression { get; }
        }

        /// <param name="sets">分割後にDescriptorへ書くSet（ゲートを除く）</param>
        /// <param name="splitSetCount">そのうち分割で作ったSetの数</param>
        /// <param name="corrections">表情補正の計画</param>
        /// <param name="existingBehaviors">Descriptorに元からあるParameter Behaviors</param>
        /// <param name="toggles">トグル</param>
        /// <param name="plan">FXのレイヤーの計画</param>
        /// <param name="renderersWithShape">シェイプ名を持つFace Tracking MeshのRendererの数</param>
        /// <param name="otherSyncedBits">本製品以外の同期パラメーターのbit数</param>
        public static BuildEstimate Calculate(
            IReadOnlyList<FaceTrackingSetData> sets,
            int splitSetCount,
            CorrectionPlan corrections,
            IReadOnlyList<ExistingBehavior> existingBehaviors,
            ToggleSet toggles,
            ToggleControllerPlan plan,
            Func<string, int> renderersWithShape,
            int otherSyncedBits)
        {
            int Renderers(string shape) => string.IsNullOrEmpty(shape) ? 0 : Math.Max(0, renderersWithShape(shape));

            var estimate = new BuildEstimate
            {
                SplitSetCount = splitSetCount,
                GateSetCount = corrections.Gates.Count,
                BehaviorCount = corrections.Behaviors.Count,
                ExistingBehaviorCount = existingBehaviors.Count,
                Instructions = corrections.Instructions,
                SyncedBits = toggles.Dynamic.Count + toggles.Sensitivity.Channels.Count * 8,
                OtherSyncedBits = otherSyncedBits,
                FxLayerCount = CountUsedLayers(plan, sets, corrections),
            };

            foreach (var set in sets)
            {
                for (int key = 0; key < FaceTrackingGroupTable.KeyCount; key++)
                {
                    estimate.Operations += Renderers(set.GetBlendShapeName(key));
                }
            }

            foreach (var behavior in corrections.Behaviors)
            {
                estimate.Operations += CustomExpression.Analyze(behavior.Expression).Operations;
            }

            foreach (var behavior in existingBehaviors)
            {
                if (behavior.CustomExpression != null)
                {
                    var info = CustomExpression.Analyze(behavior.CustomExpression);
                    if (info.IsValid)
                    {
                        estimate.ExistingInstructions += info.Instructions;
                        estimate.Operations += info.Operations;
                    }
                }
                else
                {
                    estimate.Operations += behavior.InputCount;
                }
            }

            return estimate;
        }

        /// <summary>
        /// 実際に残るレイヤーの数。グループのSetもゲートも無いレイヤーはApplyで取り除かれる
        /// </summary>
        private static int CountUsedLayers(
            ToggleControllerPlan plan,
            IReadOnlyList<FaceTrackingSetData> sets,
            CorrectionPlan corrections)
        {
            var groups = new HashSet<FaceTrackingGroup>();
            foreach (var set in sets)
            {
                for (int key = 0; key < FaceTrackingGroupTable.KeyCount; key++)
                {
                    if (set.GetBlendShapeName(key) != null)
                    {
                        groups.Add(corrections.GetLayerGroup(FaceTrackingGroupTable.GetGroup(key)));
                    }
                }

                for (int viseme = 0; viseme < FaceTrackingGroupTable.VisemeCount; viseme++)
                {
                    if (set.GetVisemeBlendShapeName(viseme) != null)
                    {
                        groups.Add(FaceTrackingGroupTable.VisemeGroup);
                    }
                }
            }

            var gates = new HashSet<string>(corrections.Gates.Select(gate => gate.Key));
            int count = plan.Layers.Count(layer => layer.Groups.Any(groups.Contains) || layer.Gates.Any(gate => gates.Contains(gate.Key)));
            return count + (plan.VisemeLayer != null ? 1 : 0);
        }
    }
}
