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
            IReadOnlyDictionary<FaceTrackingGroup, float> defaultWeights,
            IReadOnlyDictionary<string, float> gateWeights = null,
            VisemeLayerPlan visemeLayer = null)
        {
            Parameters = parameters;
            Layers = layers;
            DefaultWeights = defaultWeights;
            GateWeights = gateWeights ?? new Dictionary<string, float>();
            VisemeLayer = visemeLayer;
        }

        /// <summary>コントローラに宣言するBoolパラメーター</summary>
        public IReadOnlyList<ToggleParameterPlan> Parameters { get; }

        /// <summary>Setの重みを切り替えるレイヤー（グループとゲート）</summary>
        public IReadOnlyList<ToggleLayerPlan> Layers { get; }

        /// <summary>
        /// グループごとのSetの既定の重み（setWeight）。トグルの既定値で条件を評価した値
        /// </summary>
        public IReadOnlyDictionary<FaceTrackingGroup, float> DefaultWeights { get; }

        /// <summary>ゲートSetの既定の重み。キーはGateSlot.Key</summary>
        public IReadOnlyDictionary<string, float> GateWeights { get; }

        /// <summary>ビゼームを切り替えるレイヤー（仕様書5.5）。切り替えないならnull</summary>
        public VisemeLayerPlan VisemeLayer { get; }

        /// <summary>グループを受け持つレイヤー。トグルで変わらないグループはnull</summary>
        public ToggleLayerPlan FindLayer(FaceTrackingGroup group)
        {
            return Layers.FirstOrDefault(layer => layer.Groups.Contains(group));
        }

        /// <summary>ゲートを受け持つレイヤー</summary>
        public ToggleLayerPlan FindGateLayer(string gateKey)
        {
            return Layers.FirstOrDefault(layer => layer.Gates.Any(gate => gate.Key == gateKey));
        }
    }

    /// <summary>
    /// ビゼームのレイヤー。On / OffステートのVRC Animator Tracking ControlでMouth &amp; Jawを
    /// Tracking（ビゼーム有効）とAnimation（ビゼーム無効）に切り替える。
    /// トグルをメニューに出さず既定で無効にしたときは、Offだけの固定のレイヤーにする
    /// </summary>
    internal sealed class VisemeLayerPlan
    {
        public const string LayerName = NativeFTManagerParameterNames.LayerPrefix + "Visemes";

        public VisemeLayerPlan(ToggleLayerPlan layer, bool alwaysOff)
        {
            Layer = layer;
            AlwaysOff = alwaysOff;
        }

        /// <summary>遷移の計画。AlwaysOffのときは遷移を持たない</summary>
        public ToggleLayerPlan Layer { get; }

        public bool AlwaysOff { get; }
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
            IReadOnlyList<ToggleTransitionPlan> transitions,
            IReadOnlyList<GateSlot> gates = null)
        {
            Name = name;
            Groups = groups;
            Transitions = transitions;
            Gates = gates ?? new GateSlot[0];
        }

        public string Name { get; }

        public IReadOnlyList<FaceTrackingGroup> Groups { get; }

        /// <summary>このレイヤーが重みを切り替えるゲートSet</summary>
        public IReadOnlyList<GateSlot> Gates { get; }

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
        /// <param name="toggles">トグル</param>
        /// <param name="gates">生成する可能性のあるゲート（CorrectionPlanner.GetPossibleGates）。使わなかったものはApplyで取り除く</param>
        public static ToggleControllerPlan Build(ToggleSet toggles, IEnumerable<GateSlot> gates = null)
        {
            var parameters = toggles.Dynamic
                .Select(toggle => new ToggleParameterPlan(toggle.ParameterName, toggle.DefaultValue))
                .ToArray();

            var defaultWeights = new Dictionary<FaceTrackingGroup, float>();
            var gateWeights = new Dictionary<string, float>();
            var layerSlots = new List<(GroupCondition condition, List<FaceTrackingGroup> groups, List<GateSlot> gates)>();

            void AddSlot(GroupCondition condition, FaceTrackingGroup? group, GateSlot gate)
            {
                // メニューに出すトグルが条件に1つも無ければ、重みは既定値のまま変わらない。
                // 条件の無い遷移は作れないので、レイヤーを作らずsetWeightだけで決める
                var simplified = condition.Simplify(toggles);
                if (simplified == null || simplified.Literals.Count == 0)
                {
                    return;
                }

                var existing = layerSlots.FirstOrDefault(entry => entry.condition.SameAs(simplified));
                if (existing.groups == null)
                {
                    existing = (simplified, new List<FaceTrackingGroup>(), new List<GateSlot>());
                    layerSlots.Add(existing);
                }

                if (group.HasValue)
                {
                    existing.groups.Add(group.Value);
                }

                if (gate != null)
                {
                    existing.gates.Add(gate);
                }
            }

            foreach (var group in FaceTrackingGroupTable.Order)
            {
                var condition = GroupCondition.For(group);
                defaultWeights[group] = condition.EvaluateDefaults(toggles) ? 1f : 0f;
                AddSlot(condition, group, null);
            }

            foreach (var gate in gates ?? new GateSlot[0])
            {
                gateWeights[gate.Key] = gate.Condition.EvaluateDefaults(toggles) ? 1f : 0f;
                AddSlot(gate.Condition, null, gate);
            }

            var layers = layerSlots
                .Select(entry => BuildLayer(
                    GetLayerName(entry.groups.Select(group => group.ToString()).Concat(entry.gates.Select(gate => gate.Key))),
                    entry.condition,
                    entry.groups,
                    entry.gates,
                    toggles))
                .ToArray();

            return new ToggleControllerPlan(parameters, layers, defaultWeights, gateWeights, BuildVisemeLayer(toggles));
        }

        private static string GetLayerName(IEnumerable<string> slots)
        {
            return NativeFTManagerParameterNames.LayerPrefix + string.Join("/", slots);
        }

        /// <summary>
        /// ビゼームのレイヤー。トグルをメニューに出すなら切り替え、出さずに既定で無効ならOffに固定する。
        /// 出さずに既定で有効なら、Tracking Controlを置く理由が無いのでレイヤーを作らない
        /// </summary>
        private static VisemeLayerPlan BuildVisemeLayer(ToggleSet toggles)
        {
            var visemes = toggles[NativeFTToggle.Visemes];
            if (!visemes.IsDynamic)
            {
                return visemes.DefaultValue
                    ? null
                    : new VisemeLayerPlan(
                        new ToggleLayerPlan(VisemeLayerPlan.LayerName, new FaceTrackingGroup[0], new ToggleTransitionPlan[0]),
                        true);
            }

            var condition = new GroupCondition(new[] { new ToggleLiteral(NativeFTToggle.Visemes, true) });
            return new VisemeLayerPlan(
                BuildLayer(VisemeLayerPlan.LayerName, condition, new FaceTrackingGroup[0], new GateSlot[0], toggles),
                false);
        }

        /// <summary>
        /// Init / On / Off の3ステート（仕様書5.3）。
        /// Initは読み込み直後に必ずOnかOffへ抜け、一度Behaviorを走らせるために置く。
        /// 論理積はOnへの1本の遷移の条件の並び、Offへの論理和（¬A ∨ ¬B）は条件ごとに遷移を分ける
        /// </summary>
        private static ToggleLayerPlan BuildLayer(
            string name,
            GroupCondition condition,
            IReadOnlyList<FaceTrackingGroup> groups,
            IReadOnlyList<GateSlot> gates,
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

            return new ToggleLayerPlan(name, groups.ToArray(), transitions, gates.ToArray());
        }

        private static ToggleConditionPlan ToConditionPlan(ToggleLiteral literal, ToggleSet toggles)
        {
            return new ToggleConditionPlan(toggles[literal.Toggle].ParameterName, literal.Expected);
        }
    }
}
