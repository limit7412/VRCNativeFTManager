using System.Collections.Generic;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// トグルのFXレイヤーの生成計画（仕様書5.3）
    ///
    /// 条件の組み立てはすべてToggleControllerPlannerが行い、Infra側はこの計画を機械的に実体化する。
    /// Setの名前はGeneratingの時点では決まらない（Generatorが後からSetを書く）ので、計画はグループで持ち、
    /// State BehaviorはTransformingで分割した後に、実際のSetに合わせて付ける
    /// </summary>
    internal sealed class ToggleControllerPlan
    {
        public ToggleControllerPlan(
            IReadOnlyList<ToggleParameterPlan> parameters,
            IReadOnlyList<ToggleLayerPlan> layers,
            IReadOnlyDictionary<FaceTrackingGroup, float> defaultWeights)
        {
            Parameters = parameters;
            Layers = layers;
            DefaultWeights = defaultWeights;
        }

        /// <summary>コントローラに宣言するBoolパラメーター</summary>
        public IReadOnlyList<ToggleParameterPlan> Parameters { get; }

        public IReadOnlyList<ToggleLayerPlan> Layers { get; }

        /// <summary>
        /// グループごとのSetの既定の重み（setWeight）。トグルの既定値で条件を評価した値
        /// </summary>
        public IReadOnlyDictionary<FaceTrackingGroup, float> DefaultWeights { get; }

        /// <summary>グループを受け持つレイヤー。トグルで変わらないグループはnull</summary>
        public ToggleLayerPlan FindLayer(FaceTrackingGroup group)
        {
            return Layers.FirstOrDefault(layer => layer.Groups.Contains(group));
        }
    }

    internal sealed class ToggleParameterPlan
    {
        public ToggleParameterPlan(string name, bool defaultValue)
        {
            Name = name;
            DefaultValue = defaultValue;
        }

        public string Name { get; }

        public bool DefaultValue { get; }
    }

    /// <summary>
    /// レイヤー1つ。条件が同じグループは1つのレイヤーにまとめ、ステートにはグループのSetの数だけBehaviorを付ける
    /// </summary>
    internal sealed class ToggleLayerPlan
    {
        public const string InitState = "Init";
        public const string OnState = "On";
        public const string OffState = "Off";

        public ToggleLayerPlan(
            string name,
            IReadOnlyList<FaceTrackingGroup> groups,
            IReadOnlyList<ToggleTransitionPlan> transitions)
        {
            Name = name;
            Groups = groups;
            Transitions = transitions;
        }

        public string Name { get; }

        public IReadOnlyList<FaceTrackingGroup> Groups { get; }

        /// <summary>
        /// レイヤー内の全遷移。同じ遷移元を持つものは記載順がそのまま優先順位になる
        /// </summary>
        public IReadOnlyList<ToggleTransitionPlan> Transitions { get; }

        public IReadOnlyList<ToggleTransitionPlan> TransitionsFrom(string state)
        {
            return Transitions.Where(transition => transition.From == state).ToArray();
        }
    }

    /// <summary>
    /// 遷移1本。条件は論理積で、Has Exit TimeなしDuration 0で作る
    /// </summary>
    internal sealed class ToggleTransitionPlan
    {
        public ToggleTransitionPlan(string from, string to, IReadOnlyList<ToggleConditionPlan> conditions)
        {
            From = from;
            To = to;
            Conditions = conditions;
        }

        public string From { get; }

        public string To { get; }

        public IReadOnlyList<ToggleConditionPlan> Conditions { get; }
    }

    /// <summary>Boolパラメーターがexpectedのとき（If / IfNot）</summary>
    internal sealed class ToggleConditionPlan
    {
        public ToggleConditionPlan(string parameter, bool expected)
        {
            Parameter = parameter;
            Expected = expected;
        }

        public string Parameter { get; }

        public bool Expected { get; }
    }

    /// <summary>
    /// トグルの設定からFXレイヤーの計画を組み立てる
    /// </summary>
    internal static class ToggleControllerPlanner
    {
        public static ToggleControllerPlan Build(ToggleSet toggles)
        {
            var parameters = toggles.Dynamic
                .Select(toggle => new ToggleParameterPlan(toggle.ParameterName, toggle.DefaultValue))
                .ToArray();

            var defaultWeights = new Dictionary<FaceTrackingGroup, float>();
            var layerGroups = new List<(GroupCondition condition, List<FaceTrackingGroup> groups)>();

            foreach (var group in FaceTrackingGroupTable.Order)
            {
                var condition = GroupCondition.For(group);
                defaultWeights[group] = condition.EvaluateDefaults(toggles) ? 1f : 0f;

                // メニューに出すトグルが条件に1つも無ければ、重みは既定値のまま変わらない。
                // 条件の無い遷移は作れないので、レイヤーを作らずsetWeightだけで決める
                var simplified = condition.Simplify(toggles);
                if (simplified == null || simplified.Literals.Count == 0)
                {
                    continue;
                }

                var existing = layerGroups.FirstOrDefault(entry => entry.condition.SameAs(simplified));
                if (existing.groups != null)
                {
                    existing.groups.Add(group);
                }
                else
                {
                    layerGroups.Add((simplified, new List<FaceTrackingGroup> { group }));
                }
            }

            var layers = layerGroups
                .Select(entry => BuildLayer(entry.condition, entry.groups, toggles))
                .ToArray();

            return new ToggleControllerPlan(parameters, layers, defaultWeights);
        }

        public static string GetLayerName(IEnumerable<FaceTrackingGroup> groups)
        {
            return NativeFTManagerParameterNames.LayerPrefix + string.Join("/", groups);
        }

        /// <summary>
        /// Init / On / Off の3ステート（仕様書5.3）。
        /// Initは読み込み直後に必ずOnかOffへ抜け、一度Behaviorを走らせるために置く。
        /// 論理積はOnへの1本の遷移の条件の並び、Offへの論理和（¬A ∨ ¬B）は条件ごとに遷移を分ける
        /// </summary>
        private static ToggleLayerPlan BuildLayer(
            GroupCondition condition,
            IReadOnlyList<FaceTrackingGroup> groups,
            ToggleSet toggles)
        {
            var onConditions = condition.Literals
                .Select(literal => ToConditionPlan(literal, toggles))
                .ToArray();
            var offConditions = condition.Literals
                .Select(literal => ToConditionPlan(literal.Negate(), toggles))
                .ToArray();

            var transitions = new List<ToggleTransitionPlan>
            {
                new ToggleTransitionPlan(ToggleLayerPlan.InitState, ToggleLayerPlan.OnState, onConditions),
            };

            foreach (var off in offConditions)
            {
                transitions.Add(new ToggleTransitionPlan(
                    ToggleLayerPlan.InitState, ToggleLayerPlan.OffState, new[] { off }));
            }

            transitions.Add(new ToggleTransitionPlan(ToggleLayerPlan.OffState, ToggleLayerPlan.OnState, onConditions));

            foreach (var off in offConditions)
            {
                transitions.Add(new ToggleTransitionPlan(
                    ToggleLayerPlan.OnState, ToggleLayerPlan.OffState, new[] { off }));
            }

            return new ToggleLayerPlan(GetLayerName(groups), groups.ToArray(), transitions);
        }

        private static ToggleConditionPlan ToConditionPlan(ToggleLiteral literal, ToggleSet toggles)
        {
            return new ToggleConditionPlan(toggles[literal.Toggle].ParameterName, literal.Expected);
        }
    }
}
